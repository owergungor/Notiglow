using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace NotiGlow.Services.Ipc
{
    public enum WorkerHealthState
    {
        Disabled,
        Stopped,
        Starting,
        Healthy,
        Crashed,
        FallbackUnavailable
    }

    /// <summary>
    /// Supervises the single-instance NotiGlow.Background.exe Rust worker process:
    /// startup, health checks, crash detection, rate-limited restart, and clean IPC shutdown.
    /// </summary>
    public sealed class WorkerSupervisor : IDisposable
    {
        private readonly string? _explicitWorkerPath;
        private readonly string _pipeName;
        private readonly string _mutexName;
        private readonly string? _configDir;
        private readonly string? _uiExecutablePath;
        private readonly bool _ipcOnly;
        private readonly bool _simulateNotifDenied;
        private readonly SemaphoreSlim _lifecycleLock = new(1, 1);
        private readonly Queue<DateTime> _restartTimestamps = new();
        private const int MaxRestartsPerMinute = 3;

        private Process? _workerProcess;
        private int? _attachedPid;
        private bool _intentionalStop;
        private bool _disposed;

        public IWorkerIpcClient IpcClient { get; }
        public WorkerHealthState State { get; private set; } = WorkerHealthState.Stopped;
        public int? WorkerPid => _workerProcess is { HasExited: false } p ? p.Id : _attachedPid;
        public int RestartCount { get; private set; }
        public bool AttachedToExisting { get; private set; }
        public string? ConnectedWorkerVersion { get; private set; }
        public WorkerStatusData? LastStatus { get; private set; }
        public bool AutoRestartOnCrash { get; set; } = false;
        public string? ExpectedWorkerVersion { get; set; }

        public event EventHandler<int>? WorkerCrashed;
        public event EventHandler<int>? WorkerRestarted;

        public WorkerSupervisor(
            string? workerExecutablePath = null,
            string? pipeName = null,
            string? mutexName = null,
            string? configDir = null,
            bool ipcOnly = true,
            IWorkerIpcClient? ipcClient = null,
            string? uiExecutablePath = null,
            bool simulateNotifDenied = false)
        {
            _explicitWorkerPath = workerExecutablePath;
            _pipeName = string.IsNullOrWhiteSpace(pipeName)
                ? IpcProtocolConstants.GetDefaultPipeName()
                : pipeName.Trim();
            _mutexName = string.IsNullOrWhiteSpace(mutexName)
                ? "Local\\NotiGlow_Background_SingleInstance_v1"
                : mutexName.Trim();
            _configDir = configDir;
            _ipcOnly = ipcOnly;
            _uiExecutablePath = uiExecutablePath;
            _simulateNotifDenied = simulateNotifDenied;
            IpcClient = ipcClient ?? new WorkerIpcClient(_pipeName);
        }

        public string? ResolveWorkerExecutablePath()
        {
            if (!string.IsNullOrWhiteSpace(_explicitWorkerPath))
            {
                return File.Exists(_explicitWorkerPath) ? _explicitWorkerPath : null;
            }

            if (PackageService.IsPackaged)
            {
                try
                {
                    string pkgDir = Windows.ApplicationModel.Package.Current.InstalledLocation.Path;
                    string pkgWorker = Path.Combine(pkgDir, "NotiGlow_Background.exe");
                    if (File.Exists(pkgWorker)) return pkgWorker;
                }
                catch { }
            }

            string baseDir = AppContext.BaseDirectory;
            string[] directCandidates =
            {
                Path.Combine(baseDir, "NotiGlow.Background.exe"),
                Path.Combine(baseDir, "NotiGlow_Background.exe")
            };
            foreach (var candidate in directCandidates)
            {
                if (File.Exists(candidate)) return candidate;
            }

            string? dir = baseDir;
            for (int i = 0; i < 8 && !string.IsNullOrEmpty(dir); i++)
            {
                string candidate = Path.Combine(dir, "native", "NotiGlow.Background", "target", "release", "NotiGlow_Background.exe");
                if (File.Exists(candidate)) return candidate;
                dir = Directory.GetParent(dir)?.FullName;
            }

            return null;
        }

        public async Task<bool> EnsureStartedAsync(int readyTimeoutMs = 4000, CancellationToken cancellationToken = default)
        {
            ObjectDisposedException.ThrowIf(_disposed, this);

            await _lifecycleLock.WaitAsync(cancellationToken).ConfigureAwait(false);
            try
            {
                // 1. If an existing worker is already responding on our pipe, verify version & install path before attaching (single-instance!)
                string? resolvedExePath = ResolveWorkerExecutablePath();
                var existingPing = await IpcClient.PingAsync(400, cancellationToken).ConfigureAwait(false);
                if (existingPing != null && existingPing.Protocol == IpcProtocolConstants.ProtocolVersion)
                {
                    bool versionMismatch = !string.IsNullOrWhiteSpace(ExpectedWorkerVersion) &&
                        !string.Equals(existingPing.WorkerVersion, ExpectedWorkerVersion, StringComparison.OrdinalIgnoreCase);

                    bool pathMismatch = false;
                    if (!versionMismatch && !string.IsNullOrWhiteSpace(resolvedExePath))
                    {
                        try
                        {
                            using var runningProc = Process.GetProcessById(existingPing.Pid);
                            string? runningPath = runningProc.MainModule?.FileName;
                            if (!string.IsNullOrWhiteSpace(runningPath))
                            {
                                pathMismatch = !string.Equals(
                                    Path.GetFullPath(runningPath),
                                    Path.GetFullPath(resolvedExePath),
                                    StringComparison.OrdinalIgnoreCase);
                            }
                        }
                        catch { }
                    }

                    if (!versionMismatch && !pathMismatch)
                    {
                        AttachToExistingPid(existingPing.Pid);
                        ConnectedWorkerVersion = existingPing.WorkerVersion;
                        AttachedToExisting = true;
                        LastStatus = await IpcClient.GetStatusAsync(1000, cancellationToken).ConfigureAwait(false);
                        State = WorkerHealthState.Healthy;
                        return true;
                    }

                    LoggerService.LogInfo($"Worker upgrade/path recycle (runningVer={existingPing.WorkerVersion}, expectedVer={ExpectedWorkerVersion}, pathMismatch={pathMismatch}). Recycling worker.");
                    await IpcClient.ShutdownAsync("VersionUpgrade", 2000, cancellationToken).ConfigureAwait(false);
                    await Task.Delay(200, cancellationToken).ConfigureAwait(false);
                }

                AttachedToExisting = false;

                // 2. Resolve binary; if missing, enter controlled fallback without crashing UI
                string? exePath = resolvedExePath;
                if (string.IsNullOrEmpty(exePath))
                {
                    State = WorkerHealthState.FallbackUnavailable;
                    LoggerService.LogWarning("NotiGlow.Background.exe not found. Falling back to legacy single-process mode.");
                    return false;
                }

                _intentionalStop = false;
                State = WorkerHealthState.Starting;

                var args = new List<string>();
                if (_ipcOnly) args.Add("--ipc-only");
                if (_simulateNotifDenied) args.Add("--simulate-notif-denied");
                args.Add("--pipe-name");
                args.Add($"\"{_pipeName}\"");
                args.Add("--mutex-name");
                args.Add($"\"{_mutexName}\"");
                if (!string.IsNullOrWhiteSpace(_configDir))
                {
                    args.Add("--config-dir");
                    args.Add($"\"{_configDir}\"");
                }
                if (!string.IsNullOrWhiteSpace(_uiExecutablePath))
                {
                    args.Add("--ui-exe");
                    args.Add($"\"{_uiExecutablePath}\"");
                }

                var psi = new ProcessStartInfo
                {
                    FileName = exePath,
                    Arguments = string.Join(" ", args),
                    UseShellExecute = false,
                    CreateNoWindow = true,
                    WorkingDirectory = Path.GetDirectoryName(exePath) ?? AppContext.BaseDirectory
                };

                var proc = new Process { StartInfo = psi, EnableRaisingEvents = true };
                proc.Exited += OnWorkerProcessExited;

                if (!proc.Start())
                {
                    proc.Dispose();
                    State = WorkerHealthState.FallbackUnavailable;
                    return false;
                }

                _workerProcess = proc;

                // 3. Poll PING until worker is ready
                var sw = Stopwatch.StartNew();
                while (sw.ElapsedMilliseconds < readyTimeoutMs && !cancellationToken.IsCancellationRequested)
                {
                    if (proc.HasExited)
                    {
                        // Exit code 2 means duplicate worker guard triggered; check if primary is healthy
                        if (proc.ExitCode == 2)
                        {
                            var ping = await IpcClient.PingAsync(500, cancellationToken).ConfigureAwait(false);
                            if (ping != null)
                            {
                                State = WorkerHealthState.Healthy;
                                return true;
                            }
                        }
                        State = WorkerHealthState.Crashed;
                        return false;
                    }

                    var pingResp = await IpcClient.PingAsync(350, cancellationToken).ConfigureAwait(false);
                    if (pingResp != null && pingResp.Protocol == IpcProtocolConstants.ProtocolVersion)
                    {
                        _attachedPid = pingResp.Pid;
                        ConnectedWorkerVersion = pingResp.WorkerVersion;
                        LastStatus = await IpcClient.GetStatusAsync(1000, cancellationToken).ConfigureAwait(false);
                        State = WorkerHealthState.Healthy;
                        return true;
                    }

                    await Task.Delay(60, cancellationToken).ConfigureAwait(false);
                }

                State = WorkerHealthState.FallbackUnavailable;
                return false;
            }
            catch (Exception ex)
            {
                LoggerService.LogWarning($"WorkerSupervisor start failed (fallback active): {ex.Message}");
                State = WorkerHealthState.FallbackUnavailable;
                return false;
            }
            finally
            {
                _lifecycleLock.Release();
            }
        }

        private void AttachToExistingPid(int pid)
        {
            _attachedPid = pid;
            try
            {
                if (_workerProcess != null && !_workerProcess.HasExited && _workerProcess.Id == pid)
                {
                    return;
                }
                if (_workerProcess != null)
                {
                    _workerProcess.Exited -= OnWorkerProcessExited;
                    _workerProcess.Dispose();
                    _workerProcess = null;
                }
                var existing = Process.GetProcessById(pid);
                existing.EnableRaisingEvents = true;
                existing.Exited += OnWorkerProcessExited;
                _workerProcess = existing;
            }
            catch { }
        }

        /// <summary>
        /// Detaches this supervisor from the worker process without shutting it down.
        /// Used when UI closes while the Rust background worker continues running.
        /// </summary>
        public void Detach()
        {
            _intentionalStop = true;
            if (_workerProcess != null)
            {
                try
                {
                    _workerProcess.Exited -= OnWorkerProcessExited;
                    _workerProcess.Dispose();
                }
                catch { }
                _workerProcess = null;
            }
            _attachedPid = null;
            State = WorkerHealthState.Stopped;
        }

        private void OnWorkerProcessExited(object? sender, EventArgs e)
        {
            if (_intentionalStop || _disposed) return;

            int exitCode = -1;
            try
            {
                if (sender is Process p) exitCode = p.ExitCode;
            }
            catch { }

            _attachedPid = null;
            State = WorkerHealthState.Crashed;
            LoggerService.LogWarning($"NotiGlow.Background exited unexpectedly (exitCode={exitCode}).");
            WorkerCrashed?.Invoke(this, exitCode);

            if (AutoRestartOnCrash)
            {
                _ = Task.Run(async () =>
                {
                    try
                    {
                        if (await TryRestartAfterCrashAsync().ConfigureAwait(false) && WorkerPid.HasValue)
                        {
                            WorkerRestarted?.Invoke(this, WorkerPid.Value);
                        }
                    }
                    catch { }
                });
            }
        }

        public async Task<bool> CheckHealthAsync(int timeoutMs = 1000, CancellationToken cancellationToken = default)
        {
            var ping = await IpcClient.PingAsync(timeoutMs, cancellationToken).ConfigureAwait(false);
            if (ping != null && ping.Protocol == IpcProtocolConstants.ProtocolVersion)
            {
                State = WorkerHealthState.Healthy;
                return true;
            }

            if (_workerProcess is { HasExited: true })
            {
                State = WorkerHealthState.Crashed;
            }
            return false;
        }

        public async Task<bool> TryRestartAfterCrashAsync(CancellationToken cancellationToken = default)
        {
            DateTime now = DateTime.UtcNow;
            while (_restartTimestamps.Count > 0 && (now - _restartTimestamps.Peek()).TotalSeconds > 60)
            {
                _restartTimestamps.Dequeue();
            }

            if (_restartTimestamps.Count >= MaxRestartsPerMinute)
            {
                LoggerService.LogWarning("Worker restart rate limit reached (3/min). Entering fallback mode.");
                State = WorkerHealthState.FallbackUnavailable;
                return false;
            }

            _restartTimestamps.Enqueue(now);
            RestartCount++;
            return await EnsureStartedAsync(4000, cancellationToken).ConfigureAwait(false);
        }

        public async Task<bool> ShutdownAsync(int waitExitMs = 3000, CancellationToken cancellationToken = default)
        {
            await _lifecycleLock.WaitAsync(cancellationToken).ConfigureAwait(false);
            try
            {
                _intentionalStop = true;
                bool byeOk = await IpcClient.ShutdownAsync("SupervisorShutdown", Math.Min(waitExitMs, 2000), cancellationToken)
                    .ConfigureAwait(false);

                bool cleanExit = byeOk;
                if (_workerProcess != null)
                {
                    try
                    {
                        if (!_workerProcess.HasExited)
                        {
                            bool exited = _workerProcess.WaitForExit(waitExitMs);
                            cleanExit = exited && _workerProcess.ExitCode == 0;
                            if (!exited)
                            {
                                _workerProcess.Kill(entireProcessTree: true);
                            }
                        }
                        else
                        {
                            cleanExit = _workerProcess.ExitCode == 0;
                        }
                    }
                    catch { }
                    finally
                    {
                        _workerProcess.Exited -= OnWorkerProcessExited;
                        _workerProcess.Dispose();
                        _workerProcess = null;
                    }
                }

                State = WorkerHealthState.Stopped;
                return cleanExit;
            }
            finally
            {
                _lifecycleLock.Release();
            }
        }

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            _intentionalStop = true;
            if (_workerProcess != null)
            {
                try
                {
                    _workerProcess.Exited -= OnWorkerProcessExited;
                    _workerProcess.Dispose();
                }
                catch { }
                _workerProcess = null;
            }
            _lifecycleLock.Dispose();
        }
    }
}
