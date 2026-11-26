using System;
using System.IO;
using System.IO.Pipes;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using NotiGlow.Models;

namespace NotiGlow.Services.Ipc
{
    /// <summary>
    /// Asynchronous Named Pipe IPC client for communicating with NotiGlow.Background.exe.
    /// Never blocks the UI thread; enforces 64 KB message size cap, timeouts, and bounded reconnect backoff.
    /// </summary>
    public sealed class WorkerIpcClient : IWorkerIpcClient
    {
        private static readonly UTF8Encoding Utf8NoBom = new(false);
        private static readonly JsonSerializerOptions JsonOptions = new()
        {
            PropertyNameCaseInsensitive = true
        };

        public string PipeName { get; }

        public WorkerIpcClient(string? pipeName = null)
        {
            string raw = string.IsNullOrWhiteSpace(pipeName)
                ? IpcProtocolConstants.GetDefaultPipeName()
                : pipeName.Trim();

            const string prefix = @"\\.\pipe\";
            PipeName = raw.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)
                ? raw[prefix.Length..]
                : raw;
        }

        public async Task<IpcResponse> SendCommandAsync(
            string command,
            object? payload = null,
            int timeoutMs = 1500,
            int maxRetries = 2,
            CancellationToken cancellationToken = default)
        {
            var req = new IpcRequest
            {
                Version = IpcProtocolConstants.ProtocolVersion,
                Command = command,
                Payload = payload ?? new { }
            };

            string json = JsonSerializer.Serialize(req, JsonOptions);
            byte[] encoded = Utf8NoBom.GetBytes(json + "\n");
            if (encoded.Length > IpcProtocolConstants.MaxMessageBytes)
            {
                return IpcResponse.CreateClientError(
                    req.RequestId,
                    IpcErrorCodes.MessageTooLarge,
                    $"Request exceeds {IpcProtocolConstants.MaxMessageBytes} bytes limit.");
            }

            IpcResponse? lastErr = null;
            int attempts = Math.Max(1, maxRetries + 1);
            for (int attempt = 0; attempt < attempts; attempt++)
            {
                if (attempt > 0)
                {
                    int backoffMs = attempt == 1 ? 80 : 200;
                    await Task.Delay(backoffMs, cancellationToken).ConfigureAwait(false);
                }

                var resp = await SendBytesOnceAsync(encoded, req.RequestId, timeoutMs, cancellationToken)
                    .ConfigureAwait(false);

                if (resp.Ok)
                {
                    return resp;
                }

                lastErr = resp;
                string? code = resp.Error?.Code;
                bool retryable = code == IpcErrorCodes.PipeDisconnected || code == IpcErrorCodes.Timeout;
                if (!retryable || string.Equals(command, "SHUTDOWN", StringComparison.OrdinalIgnoreCase))
                {
                    return resp;
                }
            }

            return lastErr ?? IpcResponse.CreateClientError(req.RequestId, IpcErrorCodes.PipeDisconnected, "IPC failed");
        }

        public async Task<IpcResponse> SendRawAsync(
            string rawMessage,
            int timeoutMs = 1500,
            CancellationToken cancellationToken = default)
        {
            string line = rawMessage.EndsWith('\n') ? rawMessage : rawMessage + "\n";
            byte[] bytes = Utf8NoBom.GetBytes(line);
            return await SendBytesOnceAsync(bytes, string.Empty, timeoutMs, cancellationToken)
                .ConfigureAwait(false);
        }

        private async Task<IpcResponse> SendBytesOnceAsync(
            byte[] payloadBytes,
            string requestId,
            int timeoutMs,
            CancellationToken cancellationToken)
        {
            using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            timeoutCts.CancelAfter(Math.Max(100, timeoutMs));
            var ct = timeoutCts.Token;

            try
            {
                await using var client = new NamedPipeClientStream(
                    ".",
                    PipeName,
                    PipeDirection.InOut,
                    PipeOptions.Asynchronous);

                await client.ConnectAsync(ct).ConfigureAwait(false);
                await client.WriteAsync(payloadBytes, ct).ConfigureAwait(false);
                await client.FlushAsync(ct).ConfigureAwait(false);

                using var reader = new StreamReader(client, Utf8NoBom, detectEncodingFromByteOrderMarks: false, bufferSize: 4096, leaveOpen: true);
                string? line = await reader.ReadLineAsync(ct).ConfigureAwait(false);
                if (string.IsNullOrWhiteSpace(line))
                {
                    return IpcResponse.CreateClientError(requestId, IpcErrorCodes.PipeDisconnected, "Pipe closed without response.");
                }

                if (Utf8NoBom.GetByteCount(line) > IpcProtocolConstants.MaxMessageBytes)
                {
                    return IpcResponse.CreateClientError(requestId, IpcErrorCodes.MessageTooLarge, "Response exceeded 64 KB limit.");
                }

                var parsed = JsonSerializer.Deserialize<IpcResponse>(line, JsonOptions);
                return parsed ?? IpcResponse.CreateClientError(requestId, IpcErrorCodes.BadPayload, "Empty JSON response.");
            }
            catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
            {
                return IpcResponse.CreateClientError(requestId, IpcErrorCodes.Timeout, $"IPC request timed out after {timeoutMs}ms.");
            }
            catch (TimeoutException)
            {
                return IpcResponse.CreateClientError(requestId, IpcErrorCodes.Timeout, $"IPC connect timed out after {timeoutMs}ms.");
            }
            catch (IOException ex)
            {
                return IpcResponse.CreateClientError(requestId, IpcErrorCodes.PipeDisconnected, ex.Message);
            }
            catch (JsonException ex)
            {
                return IpcResponse.CreateClientError(requestId, IpcErrorCodes.BadPayload, $"Invalid JSON response: {ex.Message}");
            }
            catch (Exception ex)
            {
                return IpcResponse.CreateClientError(requestId, IpcErrorCodes.Internal, ex.Message);
            }
        }

        public async Task<WorkerPingData?> PingAsync(int timeoutMs = 1000, CancellationToken cancellationToken = default)
        {
            var resp = await SendCommandAsync("PING", null, timeoutMs, maxRetries: 1, cancellationToken).ConfigureAwait(false);
            return resp.Ok && resp.Data.HasValue
                ? JsonSerializer.Deserialize<WorkerPingData>(resp.Data.Value.GetRawText(), JsonOptions)
                : null;
        }

        public async Task<WorkerStatusData?> GetStatusAsync(int timeoutMs = 1500, CancellationToken cancellationToken = default)
        {
            var resp = await SendCommandAsync("GET_STATUS", null, timeoutMs, maxRetries: 1, cancellationToken).ConfigureAwait(false);
            return resp.Ok && resp.Data.HasValue
                ? JsonSerializer.Deserialize<WorkerStatusData>(resp.Data.Value.GetRawText(), JsonOptions)
                : null;
        }

        public async Task<WorkerReloadData?> ReloadSettingsAsync(bool clearPendingUiSync = false, long? clientRevision = null, int timeoutMs = 2000, CancellationToken cancellationToken = default)
        {
            object payload = clientRevision.HasValue
                ? new { clearPendingUiSync, clientRevision = clientRevision.Value, schemaVersion = 1 }
                : new { clearPendingUiSync, schemaVersion = 1 };
            var resp = await SendCommandAsync("RELOAD_SETTINGS", payload, timeoutMs, maxRetries: 1, cancellationToken).ConfigureAwait(false);
            return resp.Ok && resp.Data.HasValue
                ? JsonSerializer.Deserialize<WorkerReloadData>(resp.Data.Value.GetRawText(), JsonOptions)
                : null;
        }

        public async Task<WorkerReloadData?> NotifySettingsChangedAsync(long clientRevision, bool clearPendingUiSync = true, int timeoutMs = 2000, CancellationToken cancellationToken = default)
        {
            var payload = new { clearPendingUiSync, clientRevision, schemaVersion = 1 };
            var resp = await SendCommandAsync("SETTINGS_CHANGED", payload, timeoutMs, maxRetries: 1, cancellationToken).ConfigureAwait(false);
            return resp.Ok && resp.Data.HasValue
                ? JsonSerializer.Deserialize<WorkerReloadData>(resp.Data.Value.GetRawText(), JsonOptions)
                : null;
        }

        public async Task<bool> SetMasterEnabledAsync(bool enabled, bool pendingUiSync = true, int timeoutMs = 1500, CancellationToken cancellationToken = default)
        {
            var resp = await SendCommandAsync("SET_MASTER_ENABLED", new { enabled, pendingUiSync }, timeoutMs, maxRetries: 1, cancellationToken).ConfigureAwait(false);
            return resp.Ok;
        }

        public async Task<bool> SetGamingModeAsync(bool enabled, bool pendingUiSync = true, int timeoutMs = 1500, CancellationToken cancellationToken = default)
        {
            var resp = await SendCommandAsync("SET_GAMING_MODE", new { enabled, pendingUiSync }, timeoutMs, maxRetries: 1, cancellationToken).ConfigureAwait(false);
            return resp.Ok;
        }

        public async Task<WorkerNotificationBatchData?> DrainNotificationEventsAsync(long ackSeq, int maxCount = 32, int timeoutMs = 1500, CancellationToken cancellationToken = default)
        {
            var resp = await SendCommandAsync("NOTIFICATION_EVENT", new { ackSeq, maxCount }, timeoutMs, maxRetries: 0, cancellationToken).ConfigureAwait(false);
            return resp.Ok && resp.Data.HasValue
                ? JsonSerializer.Deserialize<WorkerNotificationBatchData>(resp.Data.Value.GetRawText(), JsonOptions)
                : null;
        }

        public async Task<WorkerTrayActionBatchData?> DrainTrayActionsAsync(long ackSeq, int timeoutMs = 1500, CancellationToken cancellationToken = default)
        {
            var resp = await SendCommandAsync("TRAY_ACTION", new { action = "Drain", ackSeq }, timeoutMs, maxRetries: 0, cancellationToken).ConfigureAwait(false);
            return resp.Ok && resp.Data.HasValue
                ? JsonSerializer.Deserialize<WorkerTrayActionBatchData>(resp.Data.Value.GetRawText(), JsonOptions)
                : null;
        }

        public Task<IpcResponse> ExecuteTrayActionAsync(string action, int timeoutMs = 1500, CancellationToken cancellationToken = default) =>
            SendCommandAsync("TRAY_ACTION", new { action }, timeoutMs, maxRetries: 0, cancellationToken);

        public async Task<WorkerGlowResultData?> PlayGlowAsync(
            AppProfile profile,
            AppSettings? settings = null,
            bool isTest = false,
            string? activeGameId = null,
            int timeoutMs = 1500,
            CancellationToken cancellationToken = default)
        {
            var payload = new
            {
                profile,
                settings,
                is_game_running = !string.IsNullOrWhiteSpace(activeGameId),
                active_game_id = activeGameId,
                is_test = isTest
            };
            var resp = await SendCommandAsync("PLAY_GLOW", payload, timeoutMs, maxRetries: 1, cancellationToken).ConfigureAwait(false);
            return resp.Ok && resp.Data.HasValue
                ? JsonSerializer.Deserialize<WorkerGlowResultData>(resp.Data.Value.GetRawText(), JsonOptions)
                : null;
        }

        public async Task<bool> StopGlowAsync(int timeoutMs = 1000, CancellationToken cancellationToken = default)
        {
            var resp = await SendCommandAsync("STOP_GLOW", null, timeoutMs, maxRetries: 0, cancellationToken).ConfigureAwait(false);
            return resp.Ok;
        }

        public async Task<bool> OpenUiAsync(string? navigateTag = null, int timeoutMs = 2000, CancellationToken cancellationToken = default)
        {
            var resp = await SendCommandAsync("OPEN_UI", new { navigateTag }, timeoutMs, maxRetries: 0, cancellationToken).ConfigureAwait(false);
            return resp.Ok;
        }

        public Task<IpcResponse> ScanGamesAsync(bool forceRefresh = false, int timeoutMs = 15000, CancellationToken cancellationToken = default) =>
            SendCommandAsync("SCAN_GAMES", new { forceRefresh }, timeoutMs, maxRetries: 0, cancellationToken);

        public Task<IpcResponse> GetGamesAsync(int timeoutMs = 1500, CancellationToken cancellationToken = default) =>
            SendCommandAsync("GET_GAMES", null, timeoutMs, maxRetries: 1, cancellationToken);

        public async Task<WorkerGameStatusData?> GetGameStatusAsync(long ackSeq = 0, int maxCount = 32, int timeoutMs = 1500, CancellationToken cancellationToken = default)
        {
            var resp = await SendCommandAsync("GAME_STATUS", new { ackSeq, maxCount }, timeoutMs, maxRetries: 0, cancellationToken).ConfigureAwait(false);
            return resp.Ok && resp.Data.HasValue
                ? JsonSerializer.Deserialize<WorkerGameStatusData>(resp.Data.Value.GetRawText(), JsonOptions)
                : null;
        }

        public async Task<WorkerGlowStatusData?> GetGlowStatusAsync(long ackSeq = 0, int maxCount = 32, int timeoutMs = 1500, CancellationToken cancellationToken = default)
        {
            var resp = await SendCommandAsync("GLOW_STATUS", new { ackSeq, maxCount }, timeoutMs, maxRetries: 0, cancellationToken).ConfigureAwait(false);
            return resp.Ok && resp.Data.HasValue
                ? JsonSerializer.Deserialize<WorkerGlowStatusData>(resp.Data.Value.GetRawText(), JsonOptions)
                : null;
        }

        public Task<IpcResponse> SetGlowConfigAsync(object configPayload, int timeoutMs = 1500, CancellationToken cancellationToken = default) =>
            SendCommandAsync("SET_GLOW_CONFIG", configPayload, timeoutMs, maxRetries: 1, cancellationToken);

        public Task<IpcResponse> SetGameGlowOverrideAsync(string gameIdentifier, GameGlowOverride? overrideSettings, int timeoutMs = 1500, CancellationToken cancellationToken = default)
        {
            object payload = overrideSettings == null
                ? new { gameIdentifier, remove = true }
                : new { gameIdentifier, remove = false, @override = overrideSettings };
            return SendCommandAsync("SET_GAME_GLOW_OVERRIDE", payload, timeoutMs, maxRetries: 1, cancellationToken);
        }

        public Task<IpcResponse> NotifyMonitorChangedAsync(bool trigger = false, int timeoutMs = 1500, CancellationToken cancellationToken = default) =>
            SendCommandAsync("MONITOR_CHANGED", new { trigger }, timeoutMs, maxRetries: 0, cancellationToken);

        public async Task<bool> ShutdownAsync(string reason = "UserExit", int timeoutMs = 3000, CancellationToken cancellationToken = default)
        {
            var resp = await SendCommandAsync("SHUTDOWN", new { reason }, timeoutMs, maxRetries: 0, cancellationToken).ConfigureAwait(false);
            return resp.Ok;
        }
    }
}
