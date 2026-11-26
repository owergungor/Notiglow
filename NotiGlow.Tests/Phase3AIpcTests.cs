using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NotiGlow.Models;
using NotiGlow.Services.Ipc;

namespace NotiGlow.Tests
{
    [TestClass]
    public class Phase3AIpcTests
    {
        private static string ResolveTestWorkerExe()
        {
            string? dir = AppContext.BaseDirectory;
            for (int i = 0; i < 8 && !string.IsNullOrEmpty(dir); i++)
            {
                string candidate = Path.Combine(dir, "native", "NotiGlow.Background", "target", "release", "NotiGlow_Background.exe");
                if (File.Exists(candidate)) return candidate;
                dir = Directory.GetParent(dir)?.FullName;
            }
            return string.Empty;
        }

        private static (string PipeName, string MutexName, string TempConfigDir) CreateIsolatedContext()
        {
            string tag = Guid.NewGuid().ToString("N")[..10];
            string pipe = $"NotiGlow_Test_IPC_{tag}";
            string mutex = $"Local\\NotiGlow_Test_Mutex_{tag}";
            string tempDir = Path.Combine(Path.GetTempPath(), $"NotiGlow_Phase3A_Test_{tag}");
            Directory.CreateDirectory(tempDir);
            return (pipe, mutex, tempDir);
        }

        private static void SafeDeleteDir(string path)
        {
            try
            {
                if (Directory.Exists(path)) Directory.Delete(path, true);
            }
            catch { }
        }

        [TestMethod]
        [Timeout(10000)]
        public void FeatureFlag_DefaultIsFalse_AndJsonRoundTripPreserved()
        {
            var settings = new AppSettings();
            Assert.IsFalse(settings.UseRustBackgroundWorker);

            string jsonFalse = JsonSerializer.Serialize(settings);
            var restoredFalse = JsonSerializer.Deserialize<AppSettings>(jsonFalse);
            Assert.IsNotNull(restoredFalse);
            Assert.IsFalse(restoredFalse!.UseRustBackgroundWorker);

            settings.UseRustBackgroundWorker = true;
            string jsonTrue = JsonSerializer.Serialize(settings);
            var restoredTrue = JsonSerializer.Deserialize<AppSettings>(jsonTrue);
            Assert.IsNotNull(restoredTrue);
            Assert.IsTrue(restoredTrue!.UseRustBackgroundWorker);
        }

        [TestMethod]
        [Timeout(10000)]
        public async Task FeatureFlag_FallbackWhenWorkerBinaryMissing_DoesNotThrow()
        {
            var (pipe, mutex, tempDir) = CreateIsolatedContext();
            try
            {
                using var sup = new WorkerSupervisor(
                    workerExecutablePath: Path.Combine(tempDir, "NonExistent_Worker.exe"),
                    pipeName: pipe,
                    mutexName: mutex,
                    configDir: tempDir,
                    ipcOnly: true);

                bool started = await sup.EnsureStartedAsync(1000);
                Assert.IsFalse(started);
                Assert.AreEqual(WorkerHealthState.FallbackUnavailable, sup.State);
                Assert.IsNull(sup.WorkerPid);
            }
            finally
            {
                SafeDeleteDir(tempDir);
            }
        }

        [TestMethod]
        [Timeout(20000)]
        public async Task Ipc_Ping_GetStatus_ReloadSettings_AndAllCommands_WorkEndToEnd()
        {
            string exe = ResolveTestWorkerExe();
            Assert.IsTrue(File.Exists(exe), $"Rust worker binary not built at expected path: {exe}");

            var (pipe, mutex, tempDir) = CreateIsolatedContext();
            try
            {
                // Seed isolated temp settings.json & profiles.json (NEVER touches %APPDATA%\NotiGlow)
                var seedSettings = new AppSettings { MasterEnabled = true, GamingModeEnabled = true };
                seedSettings.TrackedGames.Add("cs2.exe");
                await File.WriteAllTextAsync(Path.Combine(tempDir, "settings.json"), JsonSerializer.Serialize(seedSettings));
                await File.WriteAllTextAsync(Path.Combine(tempDir, "profiles.json"), JsonSerializer.Serialize(new[]
                {
                    new AppProfile { AppId = "discord.exe", Name = "Discord", ColorHex = "#5865F2" }
                }));

                using var sup = new WorkerSupervisor(exe, pipe, mutex, tempDir, ipcOnly: true);
                Assert.IsTrue(await sup.EnsureStartedAsync(4000));
                Assert.AreEqual(WorkerHealthState.Healthy, sup.State);
                Assert.IsNotNull(sup.WorkerPid);

                // 1. PING
                var ping = await sup.IpcClient.PingAsync();
                Assert.IsNotNull(ping);
                Assert.AreEqual(IpcProtocolConstants.ProtocolVersion, ping!.Protocol);
                Assert.AreEqual(sup.WorkerPid, ping.Pid);
                Assert.IsTrue(ping.IpcOnly);

                // 2. GET_STATUS
                var status = await sup.IpcClient.GetStatusAsync();
                Assert.IsNotNull(status);
                Assert.AreEqual(1, status!.ProfilesCount);
                Assert.AreEqual(1, status.TrackedGamesCount);
                Assert.IsTrue(status.GamingModeEnabled);
                Assert.IsTrue(status.Monitors.Count > 0);

                // 3. RELOAD_SETTINGS (update temp settings.json atomically)
                seedSettings.TrackedGames.Add("dota2.exe");
                await File.WriteAllTextAsync(Path.Combine(tempDir, "settings.json"), JsonSerializer.Serialize(seedSettings));
                var reload = await sup.IpcClient.ReloadSettingsAsync(clearPendingUiSync: true);
                Assert.IsNotNull(reload);
                Assert.IsTrue(reload!.Reloaded);
                Assert.AreEqual(2, reload.TrackedGamesCount);
                Assert.IsTrue(reload.SettingsRevision >= 2);

                // 4. Corrupt JSON on disk must fail RELOAD_SETTINGS cleanly without losing prior valid config
                await File.WriteAllTextAsync(Path.Combine(tempDir, "settings.json"), "{corrupt-json");
                var badReload = await sup.IpcClient.SendCommandAsync("RELOAD_SETTINGS", new { }, 1500, maxRetries: 0);
                Assert.IsFalse(badReload.Ok);
                Assert.AreEqual(IpcErrorCodes.ConfigParse, badReload.Error?.Code);

                // 5. OPEN_UI, SCAN_GAMES, GET_GAMES, STOP_GLOW
                Assert.IsTrue(await sup.IpcClient.OpenUiAsync("General"));
                var scanResp = await sup.IpcClient.ScanGamesAsync();
                Assert.IsTrue(scanResp.Ok);
                var gamesResp = await sup.IpcClient.GetGamesAsync();
                Assert.IsTrue(gamesResp.Ok);
                Assert.IsTrue(await sup.IpcClient.StopGlowAsync());

                // 6. Clean Shutdown (ExitCode == 0 without force kill)
                bool cleanExit = await sup.ShutdownAsync(3000);
                Assert.IsTrue(cleanExit);
                Assert.AreEqual(WorkerHealthState.Stopped, sup.State);
            }
            finally
            {
                SafeDeleteDir(tempDir);
            }
        }

        [TestMethod]
        [Timeout(20000)]
        public async Task Ipc_MalformedJson_UnknownCmd_BadVersion_AndOversized64KB_HandledSafely()
        {
            string exe = ResolveTestWorkerExe();
            Assert.IsTrue(File.Exists(exe), $"Rust worker binary not built: {exe}");

            var (pipe, mutex, tempDir) = CreateIsolatedContext();
            using var sup = new WorkerSupervisor(exe, pipe, mutex, tempDir, ipcOnly: true);
            try
            {
                Assert.IsTrue(await sup.EnsureStartedAsync(4000));

                // Malformed JSON
                var badJson = await sup.IpcClient.SendRawAsync("{broken-json-payload");
                Assert.IsFalse(badJson.Ok);
                Assert.AreEqual(IpcErrorCodes.BadPayload, badJson.Error?.Code);

                // Unknown command
                var unkCmd = await sup.IpcClient.SendCommandAsync("NON_EXISTENT_CMD", null, 1500, maxRetries: 0);
                Assert.IsFalse(unkCmd.Ok);
                Assert.AreEqual(IpcErrorCodes.UnknownCommand, unkCmd.Error?.Code);

                // Unsupported protocol version
                var badVer = await sup.IpcClient.SendRawAsync("{\"v\":99,\"id\":\"ver99\",\"cmd\":\"PING\",\"payload\":{}}");
                Assert.IsFalse(badVer.Ok);
                Assert.AreEqual(IpcErrorCodes.UnsupportedVersion, badVer.Error?.Code);

                // Oversized (>64 KB) client-side & server-side guard
                string huge = new string('A', IpcProtocolConstants.MaxMessageBytes + 1024);
                var oversizedClient = await sup.IpcClient.SendCommandAsync("PING", new { blob = huge }, 1500, maxRetries: 0);
                Assert.IsFalse(oversizedClient.Ok);
                Assert.AreEqual(IpcErrorCodes.MessageTooLarge, oversizedClient.Error?.Code);

                var oversizedServer = await sup.IpcClient.SendRawAsync("{\"v\":1,\"id\":\"big\",\"cmd\":\"PING\",\"payload\":{\"x\":\"" + huge + "\"}}");
                Assert.IsFalse(oversizedServer.Ok);
                Assert.AreEqual(IpcErrorCodes.MessageTooLarge, oversizedServer.Error?.Code);

                // Worker must still be healthy after all malformed/oversized inputs!
                Assert.IsTrue(await sup.CheckHealthAsync());
            }
            finally
            {
                await sup.ShutdownAsync(2000);
                SafeDeleteDir(tempDir);
            }
        }

        [TestMethod]
        [Timeout(20000)]
        public async Task Ipc_ConcurrentRequests_AndDuplicateWorkerGuard_WorkSafely()
        {
            string exe = ResolveTestWorkerExe();
            Assert.IsTrue(File.Exists(exe), $"Rust worker binary not built: {exe}");

            var (pipe, mutex, tempDir) = CreateIsolatedContext();
            using var sup = new WorkerSupervisor(exe, pipe, mutex, tempDir, ipcOnly: true);
            try
            {
                Assert.IsTrue(await sup.EnsureStartedAsync(4000));

                // 8 concurrent requests simultaneously
                var tasks = Enumerable.Range(0, 8)
                    .Select(i => i % 2 == 0
                        ? sup.IpcClient.SendCommandAsync("PING", null, 2000, maxRetries: 1)
                        : sup.IpcClient.SendCommandAsync("GET_STATUS", null, 2000, maxRetries: 1))
                    .ToArray();

                var responses = await Task.WhenAll(tasks);
                Assert.IsTrue(responses.All(r => r.Ok), "All 8 concurrent IPC requests must succeed.");

                // Launch a duplicate worker with the exact same mutex -> must exit cleanly with code 2
                using var dup = Process.Start(new ProcessStartInfo
                {
                    FileName = exe,
                    Arguments = $"--ipc-only --pipe-name \"{pipe}\" --mutex-name \"{mutex}\"",
                    UseShellExecute = false,
                    CreateNoWindow = true
                });
                Assert.IsNotNull(dup);
                Assert.IsTrue(dup!.WaitForExit(3000));
                Assert.AreEqual(2, dup.ExitCode);
            }
            finally
            {
                await sup.ShutdownAsync(2000);
                SafeDeleteDir(tempDir);
            }
        }

        [TestMethod]
        [Timeout(20000)]
        public async Task Worker_CrashDetection_PipeDisconnect_AndControlledRestart_RecoverCleanly()
        {
            string exe = ResolveTestWorkerExe();
            Assert.IsTrue(File.Exists(exe), $"Rust worker binary not built: {exe}");

            var (pipe, mutex, tempDir) = CreateIsolatedContext();
            using var sup = new WorkerSupervisor(exe, pipe, mutex, tempDir, ipcOnly: true);
            try
            {
                var crashTcs = new TaskCompletionSource<int>(TaskCreationOptions.RunContinuationsAsynchronously);
                sup.WorkerCrashed += (_, code) => crashTcs.TrySetResult(code);

                Assert.IsTrue(await sup.EnsureStartedAsync(4000));
                int firstPid = sup.WorkerPid!.Value;

                // Simulate unexpected crash via force kill
                using (var proc = Process.GetProcessById(firstPid))
                {
                    proc.Kill(entireProcessTree: true);
                    proc.WaitForExit(3000);
                }

                var completed = await Task.WhenAny(crashTcs.Task, Task.Delay(3000));
                Assert.AreEqual(crashTcs.Task, completed);
                Assert.AreEqual(WorkerHealthState.Crashed, sup.State);

                // Sending command to dead pipe returns Timeout / PipeDisconnected cleanly
                var deadResp = await sup.IpcClient.SendCommandAsync("PING", null, timeoutMs: 250, maxRetries: 0);
                Assert.IsFalse(deadResp.Ok);
                Assert.IsTrue(deadResp.Error?.Code is IpcErrorCodes.Timeout or IpcErrorCodes.PipeDisconnected);

                // Controlled restart recovers worker with new PID
                Assert.IsTrue(await sup.TryRestartAfterCrashAsync());
                Assert.AreEqual(WorkerHealthState.Healthy, sup.State);
                Assert.AreEqual(1, sup.RestartCount);
                Assert.IsNotNull(sup.WorkerPid);
                Assert.AreNotEqual(firstPid, sup.WorkerPid!.Value);

                var pingAfter = await sup.IpcClient.PingAsync();
                Assert.IsNotNull(pingAfter);
                Assert.AreEqual(sup.WorkerPid, pingAfter!.Pid);
            }
            finally
            {
                await sup.ShutdownAsync(2000);
                SafeDeleteDir(tempDir);
            }
        }

        [TestMethod]
        [Timeout(10000)]
        public async Task Ipc_Timeout_AndUiThreadResponsiveness_DoesNotBlockCallingThread()
        {
            // Point client at a pipe name with no server -> must return asynchronously without synchronous blocking
            var client = new WorkerIpcClient($"NotiGlow_NoServer_{Guid.NewGuid():N}");

            var sw = Stopwatch.StartNew();
            Task<IpcResponse> pendingTask = client.SendCommandAsync("PING", null, timeoutMs: 350, maxRetries: 0);
            long callReturnMs = sw.ElapsedMilliseconds;

            // Calling thread returned immediately (<150ms) while async connect/timeout runs off-thread
            Assert.IsTrue(callReturnMs < 150, $"IPC call blocked calling thread for {callReturnMs}ms");

            var resp = await pendingTask;
            Assert.IsFalse(resp.Ok);
            Assert.AreEqual(IpcErrorCodes.Timeout, resp.Error?.Code);
        }
    }
}
