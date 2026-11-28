using System.Threading;
using System.Threading.Tasks;
using NotiGlow.Models;

namespace NotiGlow.Services.Ipc
{
    public interface IWorkerIpcClient
    {
        string PipeName { get; }

        Task<IpcResponse> SendCommandAsync(
            string command,
            object? payload = null,
            int timeoutMs = 1500,
            int maxRetries = 2,
            CancellationToken cancellationToken = default);

        Task<IpcResponse> SendRawAsync(
            string rawMessage,
            int timeoutMs = 1500,
            CancellationToken cancellationToken = default);

        Task<WorkerPingData?> PingAsync(int timeoutMs = 1000, CancellationToken cancellationToken = default);
        Task<WorkerStatusData?> GetStatusAsync(int timeoutMs = 1500, CancellationToken cancellationToken = default);
        Task<WorkerReloadData?> ReloadSettingsAsync(bool clearPendingUiSync = false, long? clientRevision = null, int timeoutMs = 2000, CancellationToken cancellationToken = default);
        Task<WorkerReloadData?> NotifySettingsChangedAsync(long clientRevision, bool clearPendingUiSync = true, int timeoutMs = 2000, CancellationToken cancellationToken = default);
        Task<bool> SetMasterEnabledAsync(bool enabled, bool pendingUiSync = true, int timeoutMs = 1500, CancellationToken cancellationToken = default);
        Task<bool> SetGamingModeAsync(bool enabled, bool pendingUiSync = true, int timeoutMs = 1500, CancellationToken cancellationToken = default);
        Task<WorkerNotificationBatchData?> DrainNotificationEventsAsync(long ackSeq, int maxCount = 32, int timeoutMs = 1500, CancellationToken cancellationToken = default);
        Task<WorkerTrayActionBatchData?> DrainTrayActionsAsync(long ackSeq, int timeoutMs = 1500, CancellationToken cancellationToken = default);
        Task<IpcResponse> ExecuteTrayActionAsync(string action, int timeoutMs = 1500, CancellationToken cancellationToken = default);
        Task<WorkerGlowResultData?> PlayGlowAsync(AppProfile profile, AppSettings? settings = null, bool isTest = false, string? activeGameId = null, int timeoutMs = 1500, CancellationToken cancellationToken = default);
        Task<bool> StopGlowAsync(int timeoutMs = 1000, CancellationToken cancellationToken = default);
        Task<bool> OpenUiAsync(string? navigateTag = null, int timeoutMs = 2000, CancellationToken cancellationToken = default);
        Task<IpcResponse> ScanGamesAsync(bool forceRefresh = false, int timeoutMs = 15000, CancellationToken cancellationToken = default);
        Task<IpcResponse> GetGamesAsync(int timeoutMs = 1500, CancellationToken cancellationToken = default);
        Task<WorkerGameStatusData?> GetGameStatusAsync(long ackSeq = 0, int maxCount = 32, int timeoutMs = 1500, CancellationToken cancellationToken = default);
        Task<WorkerGlowStatusData?> GetGlowStatusAsync(long ackSeq = 0, int maxCount = 32, int timeoutMs = 1500, CancellationToken cancellationToken = default);
        Task<IpcResponse> SetGlowConfigAsync(object configPayload, int timeoutMs = 1500, CancellationToken cancellationToken = default);
        Task<IpcResponse> SetGameGlowOverrideAsync(string gameIdentifier, GameGlowOverride? overrideSettings, int timeoutMs = 1500, CancellationToken cancellationToken = default);
        Task<IpcResponse> NotifyMonitorChangedAsync(bool trigger = false, int timeoutMs = 1500, CancellationToken cancellationToken = default);
        Task<WorkerPollEventsBatchData?> PollEventsAsync(
            long notifAckSeq,
            long trayAckSeq,
            long gameAckSeq,
            long glowAckSeq,
            bool includeGame = true,
            bool includeGlow = true,
            int maxCount = 32,
            int timeoutMs = 1200,
            CancellationToken cancellationToken = default) =>
            Task.FromResult<WorkerPollEventsBatchData?>(null);
        Task<bool> ShutdownAsync(string reason = "UserExit", int timeoutMs = 3000, CancellationToken cancellationToken = default);
    }
}
