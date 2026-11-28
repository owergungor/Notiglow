using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace NotiGlow.Services.Ipc
{
    public static class IpcProtocolConstants
    {
        public const int ProtocolVersion = 1;
        public const int MaxMessageBytes = 65_536; // 64 KB hard limit

        public static string GetDefaultPipeName()
        {
            int sessionId = System.Diagnostics.Process.GetCurrentProcess().SessionId;
            return $"NotiGlow_IPC_v1_{sessionId}";
        }
    }

    public static class IpcErrorCodes
    {
        public const string UnsupportedVersion = "ERR_UNSUPPORTED_VERSION";
        public const string BadPayload = "ERR_BAD_PAYLOAD";
        public const string MessageTooLarge = "ERR_MESSAGE_TOO_LARGE";
        public const string Unauthorized = "ERR_UNAUTHORIZED";
        public const string UnknownCommand = "ERR_UNKNOWN_CMD";
        public const string ConfigIo = "ERR_CONFIG_IO";
        public const string ConfigParse = "ERR_CONFIG_PARSE";
        public const string ShuttingDown = "ERR_SHUTTING_DOWN";
        public const string Timeout = "ERR_TIMEOUT";
        public const string PipeDisconnected = "ERR_PIPE_DISCONNECTED";
        public const string Internal = "ERR_INTERNAL";
    }

    public sealed class IpcRequest
    {
        [JsonPropertyName("v")]
        public int Version { get; set; } = IpcProtocolConstants.ProtocolVersion;

        [JsonPropertyName("id")]
        public string RequestId { get; set; } = Guid.NewGuid().ToString("N")[..12];

        [JsonPropertyName("cmd")]
        public string Command { get; set; } = string.Empty;

        [JsonPropertyName("payload")]
        public object? Payload { get; set; }
    }

    public sealed class IpcError
    {
        [JsonPropertyName("code")]
        public string Code { get; set; } = string.Empty;

        [JsonPropertyName("message")]
        public string Message { get; set; } = string.Empty;
    }

    public sealed class IpcResponse
    {
        [JsonPropertyName("v")]
        public int Version { get; set; }

        [JsonPropertyName("id")]
        public string RequestId { get; set; } = string.Empty;

        [JsonPropertyName("ok")]
        public bool Ok { get; set; }

        [JsonPropertyName("data")]
        public JsonElement? Data { get; set; }

        [JsonPropertyName("error")]
        public IpcError? Error { get; set; }

        public static IpcResponse CreateClientError(string requestId, string code, string message) =>
            new()
            {
                Version = IpcProtocolConstants.ProtocolVersion,
                RequestId = requestId,
                Ok = false,
                Data = null,
                Error = new IpcError { Code = code, Message = message }
            };
    }

    public sealed class WorkerPingData
    {
        [JsonPropertyName("workerVersion")]
        public string WorkerVersion { get; set; } = string.Empty;

        [JsonPropertyName("protocol")]
        public int Protocol { get; set; }

        [JsonPropertyName("pid")]
        public int Pid { get; set; }

        [JsonPropertyName("uptimeSec")]
        public long UptimeSec { get; set; }

        [JsonPropertyName("ipcOnly")]
        public bool IpcOnly { get; set; }

        [JsonPropertyName("trayActive")]
        public bool TrayActive { get; set; }
    }

    public sealed class WorkerMonitorDto
    {
        [JsonPropertyName("deviceName")]
        public string DeviceName { get; set; } = string.Empty;

        [JsonPropertyName("left")]
        public int Left { get; set; }

        [JsonPropertyName("top")]
        public int Top { get; set; }

        [JsonPropertyName("width")]
        public int Width { get; set; }

        [JsonPropertyName("height")]
        public int Height { get; set; }

        [JsonPropertyName("dpi")]
        public int Dpi { get; set; }

        [JsonPropertyName("isPrimary")]
        public bool IsPrimary { get; set; }
    }

    public sealed class WorkerStatsDto
    {
        [JsonPropertyName("polls")]
        public long Polls { get; set; }

        [JsonPropertyName("newNotifs")]
        public long NewNotifs { get; set; }

        [JsonPropertyName("removedNotifs")]
        public long RemovedNotifs { get; set; }

        [JsonPropertyName("duplicatesSuppressed")]
        public long DuplicatesSuppressed { get; set; }

        [JsonPropertyName("deniedChecks")]
        public long DeniedChecks { get; set; }

        [JsonPropertyName("activeListenerCount")]
        public long ActiveListenerCount { get; set; }

        [JsonPropertyName("accessTransitions")]
        public long AccessTransitions { get; set; }

        [JsonPropertyName("glowsTriggered")]
        public long GlowsTriggered { get; set; }

        [JsonPropertyName("glowsCompleted")]
        public long GlowsCompleted { get; set; }

        [JsonPropertyName("glowsDropped")]
        public long GlowsDropped { get; set; }

        [JsonPropertyName("glowsQueued")]
        public long GlowsQueued { get; set; }

        [JsonPropertyName("glowsExtended")]
        public long GlowsExtended { get; set; }

        [JsonPropertyName("glowsRestarted")]
        public long GlowsRestarted { get; set; }

        [JsonPropertyName("lastStartLatencyUs")]
        public long LastStartLatencyUs { get; set; }

        [JsonPropertyName("lastFrameCount")]
        public long LastFrameCount { get; set; }

        [JsonPropertyName("lastAvgFrameUs")]
        public long LastAvgFrameUs { get; set; }

        [JsonPropertyName("windowsCreated")]
        public long WindowsCreated { get; set; }

        [JsonPropertyName("windowsDestroyed")]
        public long WindowsDestroyed { get; set; }

        [JsonPropertyName("dibsCreated")]
        public long DibsCreated { get; set; }

        [JsonPropertyName("dibsDestroyed")]
        public long DibsDestroyed { get; set; }

        [JsonPropertyName("monitorChanges")]
        public long MonitorChanges { get; set; }

        [JsonPropertyName("gameScans")]
        public long GameScans { get; set; }

        [JsonPropertyName("gameDetections")]
        public long GameDetections { get; set; }

        [JsonPropertyName("gameCloses")]
        public long GameCloses { get; set; }

        [JsonPropertyName("lastGameScanLatencyUs")]
        public long LastGameScanLatencyUs { get; set; }

        [JsonPropertyName("lastGameDetectLatencyUs")]
        public long LastGameDetectLatencyUs { get; set; }
    }

    public sealed class WorkerStatusData
    {
        [JsonPropertyName("workerVersion")]
        public string WorkerVersion { get; set; } = string.Empty;

        [JsonPropertyName("protocol")]
        public int Protocol { get; set; }

        [JsonPropertyName("pid")]
        public int Pid { get; set; }

        [JsonPropertyName("uptimeSec")]
        public long UptimeSec { get; set; }

        [JsonPropertyName("ipcOnly")]
        public bool IpcOnly { get; set; }

        [JsonPropertyName("trayActive")]
        public bool TrayActive { get; set; }

        [JsonPropertyName("taskbarRestores")]
        public long TaskbarRestores { get; set; }

        [JsonPropertyName("notifAccess")]
        public string NotifAccess { get; set; } = "Unspecified";

        [JsonPropertyName("notifMode")]
        public string NotifMode { get; set; } = string.Empty;

        [JsonPropertyName("historyCount")]
        public long HistoryCount { get; set; }

        [JsonPropertyName("monitors")]
        public List<WorkerMonitorDto> Monitors { get; set; } = new();

        [JsonPropertyName("activeGame")]
        public string? ActiveGame { get; set; }

        [JsonPropertyName("activeGameInfo")]
        public WorkerActiveGameDto? ActiveGameInfo { get; set; }

        [JsonPropertyName("useRustGameDetection")]
        public bool UseRustGameDetection { get; set; }

        [JsonPropertyName("useRustGlowEngine")]
        public bool UseRustGlowEngine { get; set; }

        [JsonPropertyName("glowActive")]
        public bool GlowActive { get; set; }

        [JsonPropertyName("activeOverlayWindows")]
        public long ActiveOverlayWindows { get; set; }

        [JsonPropertyName("masterEnabled")]
        public bool MasterEnabled { get; set; }

        [JsonPropertyName("gamingModeEnabled")]
        public bool GamingModeEnabled { get; set; }

        [JsonPropertyName("pendingUiSync")]
        public bool PendingUiSync { get; set; }

        [JsonPropertyName("pendingMasterEnabled")]
        public bool? PendingMasterEnabled { get; set; }

        [JsonPropertyName("pendingGamingMode")]
        public bool? PendingGamingMode { get; set; }

        [JsonPropertyName("settingsRevision")]
        public long SettingsRevision { get; set; }

        [JsonPropertyName("clientRevision")]
        public long ClientRevision { get; set; }

        [JsonPropertyName("profilesCount")]
        public int ProfilesCount { get; set; }

        [JsonPropertyName("trackedGamesCount")]
        public int TrackedGamesCount { get; set; }

        [JsonPropertyName("stats")]
        public WorkerStatsDto Stats { get; set; } = new();
    }

    public sealed class WorkerReloadData
    {
        [JsonPropertyName("reloaded")]
        public bool Reloaded { get; set; }

        [JsonPropertyName("staleIgnored")]
        public bool StaleIgnored { get; set; }

        [JsonPropertyName("profilesCount")]
        public int ProfilesCount { get; set; }

        [JsonPropertyName("trackedGamesCount")]
        public int TrackedGamesCount { get; set; }

        [JsonPropertyName("settingsRevision")]
        public long SettingsRevision { get; set; }

        [JsonPropertyName("clientRevision")]
        public long ClientRevision { get; set; }

        [JsonPropertyName("schemaVersion")]
        public int SchemaVersion { get; set; } = 1;
    }

    public sealed class WorkerGlowResultData
    {
        [JsonPropertyName("action")]
        public string Action { get; set; } = string.Empty;

        [JsonPropertyName("effectiveStyle")]
        public string EffectiveStyle { get; set; } = string.Empty;

        [JsonPropertyName("effectiveDurationMs")]
        public int EffectiveDurationMs { get; set; }

        [JsonPropertyName("effectiveIntensity")]
        public double EffectiveIntensity { get; set; }

        [JsonPropertyName("effectiveCornerRadius")]
        public double EffectiveCornerRadius { get; set; }

        [JsonPropertyName("monitors")]
        public int Monitors { get; set; }
    }

    public sealed class WorkerNotificationEventDto
    {
        [JsonPropertyName("seq")]
        public long Seq { get; set; }

        [JsonPropertyName("kind")]
        public string Kind { get; set; } = "Added";

        [JsonPropertyName("notificationId")]
        public uint NotificationId { get; set; }

        [JsonPropertyName("appId")]
        public string AppId { get; set; } = "UnknownApp";

        [JsonPropertyName("appName")]
        public string AppName { get; set; } = "UnknownApp";

        [JsonPropertyName("timestampUnixMs")]
        public long TimestampUnixMs { get; set; }
    }

    public sealed class WorkerNotificationBatchData
    {
        [JsonPropertyName("events")]
        public List<WorkerNotificationEventDto> Events { get; set; } = new();

        [JsonPropertyName("latestSeq")]
        public long LatestSeq { get; set; }
    }

    public sealed class WorkerTrayActionDto
    {
        [JsonPropertyName("seq")]
        public long Seq { get; set; }

        [JsonPropertyName("action")]
        public string Action { get; set; } = string.Empty;

        [JsonPropertyName("value")]
        public bool? Value { get; set; }
    }

    public sealed class WorkerTrayActionBatchData
    {
        [JsonPropertyName("actions")]
        public List<WorkerTrayActionDto> Actions { get; set; } = new();

        [JsonPropertyName("latestSeq")]
        public long LatestSeq { get; set; }
    }

    public sealed class WorkerActiveGameDto
    {
        [JsonPropertyName("name")]
        public string Name { get; set; } = string.Empty;

        [JsonPropertyName("executablePath")]
        public string ExecutablePath { get; set; } = string.Empty;

        [JsonPropertyName("identifier")]
        public string Identifier { get; set; } = string.Empty;

        [JsonPropertyName("launcher")]
        public string Launcher { get; set; } = string.Empty;

        [JsonPropertyName("pid")]
        public uint Pid { get; set; }
    }

    public sealed class WorkerGameEventDto
    {
        [JsonPropertyName("seq")]
        public long Seq { get; set; }

        [JsonPropertyName("kind")]
        public string Kind { get; set; } = string.Empty;

        [JsonPropertyName("gameName")]
        public string GameName { get; set; } = string.Empty;

        [JsonPropertyName("executablePath")]
        public string ExecutablePath { get; set; } = string.Empty;

        [JsonPropertyName("identifier")]
        public string Identifier { get; set; } = string.Empty;

        [JsonPropertyName("launcher")]
        public string Launcher { get; set; } = string.Empty;

        [JsonPropertyName("pid")]
        public uint Pid { get; set; }

        [JsonPropertyName("timestampUnixMs")]
        public long TimestampUnixMs { get; set; }
    }

    public sealed class WorkerGameStatusData
    {
        [JsonPropertyName("gamingModeEnabled")]
        public bool GamingModeEnabled { get; set; }

        [JsonPropertyName("isGameRunning")]
        public bool IsGameRunning { get; set; }

        [JsonPropertyName("activeGame")]
        public WorkerActiveGameDto? ActiveGame { get; set; }

        [JsonPropertyName("totalScans")]
        public long TotalScans { get; set; }

        [JsonPropertyName("totalDetections")]
        public long TotalDetections { get; set; }

        [JsonPropertyName("totalCloses")]
        public long TotalCloses { get; set; }

        [JsonPropertyName("lastScanLatencyUs")]
        public long LastScanLatencyUs { get; set; }

        [JsonPropertyName("lastDetectLatencyUs")]
        public long LastDetectLatencyUs { get; set; }

        [JsonPropertyName("events")]
        public List<WorkerGameEventDto> Events { get; set; } = new();

        [JsonPropertyName("latestSeq")]
        public long LatestSeq { get; set; }
    }

    public sealed class WorkerGlowEventDto
    {
        [JsonPropertyName("seq")]
        public long Seq { get; set; }

        [JsonPropertyName("kind")]
        public string Kind { get; set; } = string.Empty;

        [JsonPropertyName("style")]
        public string Style { get; set; } = string.Empty;

        [JsonPropertyName("colorHex")]
        public string ColorHex { get; set; } = string.Empty;

        [JsonPropertyName("durationMs")]
        public int DurationMs { get; set; }

        [JsonPropertyName("intensity")]
        public double Intensity { get; set; }

        [JsonPropertyName("monitors")]
        public int Monitors { get; set; }

        [JsonPropertyName("timestampUnixMs")]
        public long TimestampUnixMs { get; set; }
    }

    public sealed class WorkerGlowStatusData
    {
        [JsonPropertyName("glowActive")]
        public bool GlowActive { get; set; }

        [JsonPropertyName("activeOverlayWindows")]
        public long ActiveOverlayWindows { get; set; }

        [JsonPropertyName("totalTriggers")]
        public long TotalTriggers { get; set; }

        [JsonPropertyName("completedCount")]
        public long CompletedCount { get; set; }

        [JsonPropertyName("droppedCount")]
        public long DroppedCount { get; set; }

        [JsonPropertyName("queuedCount")]
        public long QueuedCount { get; set; }

        [JsonPropertyName("extendedCount")]
        public long ExtendedCount { get; set; }

        [JsonPropertyName("restartedCount")]
        public long RestartedCount { get; set; }

        [JsonPropertyName("lastStartLatencyUs")]
        public long LastStartLatencyUs { get; set; }

        [JsonPropertyName("lastDurationMs")]
        public long LastDurationMs { get; set; }

        [JsonPropertyName("lastFrameCount")]
        public long LastFrameCount { get; set; }

        [JsonPropertyName("lastAvgFrameUs")]
        public long LastAvgFrameUs { get; set; }

        [JsonPropertyName("windowsCreated")]
        public long WindowsCreated { get; set; }

        [JsonPropertyName("windowsDestroyed")]
        public long WindowsDestroyed { get; set; }

        [JsonPropertyName("dibsCreated")]
        public long DibsCreated { get; set; }

        [JsonPropertyName("dibsDestroyed")]
        public long DibsDestroyed { get; set; }

        [JsonPropertyName("monitorChanges")]
        public long MonitorChanges { get; set; }

        [JsonPropertyName("monitorsDetected")]
        public long MonitorsDetected { get; set; }

        [JsonPropertyName("events")]
        public List<WorkerGlowEventDto> Events { get; set; } = new();

        [JsonPropertyName("latestSeq")]
        public long LatestSeq { get; set; }
    }

    public sealed class WorkerPollEventsBatchData
    {
        [JsonPropertyName("notif")]
        public WorkerNotificationBatchData? Notif { get; set; }

        [JsonPropertyName("tray")]
        public WorkerTrayActionBatchData? Tray { get; set; }

        [JsonPropertyName("game")]
        public WorkerGameStatusData? Game { get; set; }

        [JsonPropertyName("glow")]
        public WorkerGlowStatusData? Glow { get; set; }
    }
}
