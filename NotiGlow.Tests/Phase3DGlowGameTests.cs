using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NotiGlow.Models;
using NotiGlow.Services;
using NotiGlow.Services.Ipc;

namespace NotiGlow.Tests
{
    [TestClass]
    public class Phase3DGlowGameTests
    {
        private static string GetWorkerExePath()
        {
            string? dir = AppContext.BaseDirectory;
            for (int i = 0; i < 8 && !string.IsNullOrEmpty(dir); i++)
            {
                string direct = Path.Combine(dir, "NotiGlow_Background.exe");
                if (File.Exists(direct)) return direct;
                string rel = Path.Combine(dir, "native", "NotiGlow.Background", "target", "release", "NotiGlow_Background.exe");
                if (File.Exists(rel)) return rel;
                dir = Directory.GetParent(dir)?.FullName;
            }
            throw new FileNotFoundException("NotiGlow_Background.exe not found in ancestor directories.");
        }

        private static string CreateTempConfigDir(AppSettings settings, List<AppProfile>? profiles = null)
        {
            string dir = Path.Combine(Path.GetTempPath(), $"NotiGlow_P3D_{Guid.NewGuid():N}");
            Directory.CreateDirectory(dir);
            File.WriteAllText(
                Path.Combine(dir, "settings.json"),
                JsonSerializer.Serialize(settings, new JsonSerializerOptions { WriteIndented = true }));
            File.WriteAllText(
                Path.Combine(dir, "profiles.json"),
                JsonSerializer.Serialize(profiles ?? new List<AppProfile>(), new JsonSerializerOptions { WriteIndented = true }));
            return dir;
        }

        private static string ComputeSha256(string filePath)
        {
            using var sha = SHA256.Create();
            return Convert.ToHexString(sha.ComputeHash(File.ReadAllBytes(filePath)));
        }

        [TestMethod]
        public void FeatureFlags_DefaultTrueInV5_AndExternalGlowPreventsDoubleOverlay()
        {
            var settings = new AppSettings();
            Assert.IsTrue(settings.UseRustBackgroundWorker, "UseRustBackgroundWorker must default to true in v5.0");
            Assert.IsTrue(settings.UseRustGameDetection, "UseRustGameDetection must default to true in v5.0");
            Assert.IsTrue(settings.UseRustGlowEngine, "UseRustGlowEngine must default to true in v5.0");

            string cfgDir = CreateTempConfigDir(settings, new List<AppProfile>
            {
                new() { AppId = "Discord", Name = "Discord", Enabled = true, ColorHex = "#5865F2" }
            });

            try
            {
                var settingsSvc = new SettingsService(Path.Combine(cfgDir, "settings.json"));
                var profileSvc = new ProfileService(Path.Combine(cfgDir, "profiles.json"));
                var glowMgr = new GlowManager(settingsSvc, profileSvc) { SuppressOverlayWindowsForTesting = true };

                int externalDispatchCount = 0;
                int externalStopCount = 0;
                glowMgr.EnableExternalGlowOwnership(
                    dispatcher: (_, _, _, _) => { externalDispatchCount++; return true; },
                    stopAction: () => { externalStopCount++; });

                // Notification trigger must be suppressed on C# side when Rust owns Glow autonomously (no double overlay!)
                glowMgr.TriggerNotification(new NotificationItem { AppId = "Discord", AppName = "Discord" });
                Assert.AreEqual(0, externalDispatchCount, "Notification trigger should not double-dispatch from C#");
                Assert.AreEqual(0, glowMgr.ActiveOverlayWindowCount);

                // Explicit UI TriggerProfile routes to external Rust dispatcher
                glowMgr.TriggerProfile(new AppProfile { AppId = "Test", Name = "Test", ColorHex = "#00E5FF" });
                Assert.AreEqual(1, externalDispatchCount);
                Assert.AreEqual(0, glowMgr.ActiveOverlayWindowCount);

                glowMgr.StopAllOverlays();
                Assert.AreEqual(1, externalStopCount);

                // Fallback restores C# ownership cleanly
                glowMgr.DisableExternalGlowOwnership();
                Assert.IsFalse(glowMgr.IsExternalGlowOwnerActive);
            }
            finally
            {
                try { Directory.Delete(cfgDir, true); } catch { }
            }
        }

        [TestMethod]
        public async Task WorkerIpc_GameLifecycle_Scan_Overrides_AndSingleWriterInvariant()
        {
            string pipe = $"NotiGlow_P3D_Game_{Guid.NewGuid():N}";
            string mutex = $"Local\\NotiGlow_P3D_Game_{Guid.NewGuid():N}";
            var settings = new AppSettings
            {
                MasterEnabled = true,
                GamingModeEnabled = true,
                UseRustBackgroundWorker = true,
                UseRustGameDetection = true,
                UseRustGlowEngine = true
            };
            string cfgDir = CreateTempConfigDir(settings);
            string settingsPath = Path.Combine(cfgDir, "settings.json");
            string initialHash = ComputeSha256(settingsPath);

            using var sup = new WorkerSupervisor(
                workerExecutablePath: GetWorkerExePath(),
                pipeName: pipe,
                mutexName: mutex,
                configDir: cfgDir,
                ipcOnly: true);

            try
            {
                Assert.IsTrue(await sup.EnsureStartedAsync(4000), "Worker failed to start");

                // 1. SCAN_GAMES is no longer a stub
                var scanResp = await sup.IpcClient.ScanGamesAsync(forceRefresh: true, timeoutMs: 15000);
                Assert.IsTrue(scanResp.Ok, "SCAN_GAMES failed");
                Assert.IsTrue(scanResp.Data.HasValue);
                Assert.IsFalse(scanResp.Data.Value.GetProperty("phase3aStub").GetBoolean(), "SCAN_GAMES must not be a stub");

                // 2. SET_GLOW_CONFIG & SET_GAME_GLOW_OVERRIDE must update memory ONLY (never write settings.json!)
                var cfgResp = await sup.IpcClient.SetGlowConfigAsync(new
                {
                    oledMode = true,
                    reduceMotion = true,
                    cornerRadius = 18.0,
                    monitorMode = "PrimaryMonitor",
                    burstMode = "Queue"
                });
                Assert.IsTrue(cfgResp.Ok);

                var ovResp = await sup.IpcClient.SetGameGlowOverrideAsync("cs2.exe", new GameGlowOverride
                {
                    GameIdentifier = "cs2.exe",
                    CustomGlowEnabled = true,
                    DurationMs = 1500,
                    Intensity = 0.75,
                    ColorHex = "#FF3366",
                    Style = GlowStyle.Sweep
                });
                Assert.IsTrue(ovResp.Ok);
                Assert.AreEqual(initialHash, ComputeSha256(settingsPath), "Rust worker MUST NOT modify settings.json on disk");

                // 3. Simulated GAME_DETECTED -> dedup -> GAME_CLOSED via GAME_STATUS
                var simStart = await sup.IpcClient.SendCommandAsync("GAME_STATUS", new
                {
                    setSimulatedGame = "Counter-Strike 2",
                    executablePath = @"C:\Games\cs2.exe",
                    launcher = "Steam",
                    ackSeq = 0
                });
                Assert.IsTrue(simStart.Ok);
                var startData = JsonSerializer.Deserialize<WorkerGameStatusData>(
                    simStart.Data!.Value.GetRawText(),
                    new JsonSerializerOptions { PropertyNameCaseInsensitive = true })!;
                Assert.IsTrue(startData.IsGameRunning);
                Assert.AreEqual("cs2.exe", startData.ActiveGame?.Identifier);
                Assert.IsTrue(startData.Events.Any(e => e.Kind == "GAME_DETECTED"));

                long ack1 = startData.LatestSeq;
                var dupCheck = await sup.IpcClient.GetGameStatusAsync(ackSeq: ack1);
                Assert.IsNotNull(dupCheck);
                Assert.AreEqual(0, dupCheck!.Events.Count, "Acknowledged GAME_DETECTED must not repeat");

                var simStop = await sup.IpcClient.SendCommandAsync("GAME_STATUS", new
                {
                    setSimulatedGame = (string?)null,
                    ackSeq = ack1
                });
                Assert.IsTrue(simStop.Ok);
                var stopData = JsonSerializer.Deserialize<WorkerGameStatusData>(
                    simStop.Data!.Value.GetRawText(),
                    new JsonSerializerOptions { PropertyNameCaseInsensitive = true })!;
                Assert.IsFalse(stopData.IsGameRunning);
                Assert.IsTrue(stopData.Events.Any(e => e.Kind == "GAME_CLOSED"));
            }
            finally
            {
                await sup.ShutdownAsync(2500);
                try { Directory.Delete(cfgDir, true); } catch { }
            }
        }

        [TestMethod]
        public async Task WorkerIpc_AllSixGlowModes_CleanResources_AndMonitorChange()
        {
            string pipe = $"NotiGlow_P3D_Glow_{Guid.NewGuid():N}";
            string mutex = $"Local\\NotiGlow_P3D_Glow_{Guid.NewGuid():N}";
            var settings = new AppSettings
            {
                MasterEnabled = true,
                MonitorMode = MonitorMode.PrimaryMonitor,
                BurstMode = BurstMode.Restart,
                UseRustBackgroundWorker = true,
                UseRustGlowEngine = true
            };
            string cfgDir = CreateTempConfigDir(settings);

            using var sup = new WorkerSupervisor(
                workerExecutablePath: GetWorkerExePath(),
                pipeName: pipe,
                mutexName: mutex,
                configDir: cfgDir,
                ipcOnly: true);

            try
            {
                Assert.IsTrue(await sup.EnsureStartedAsync(4000));

                var cases = new (string Label, GlowStyle Style, string ColorHex)[]
                {
                    ("Pulse", GlowStyle.Pulse, "#00E5FF"),
                    ("Ambient", GlowStyle.Ambient, "#7C3AED"),
                    ("Sweep", GlowStyle.Sweep, "#FF7A00"),
                    ("Comet", GlowStyle.Comet, "#00E676"),
                    ("Ripple", GlowStyle.Ripple, "#2979FF"),
                    ("RGB Spectrum", GlowStyle.Pulse, "RGB")
                };

                foreach (var c in cases)
                {
                    var res = await sup.IpcClient.PlayGlowAsync(new AppProfile
                    {
                        AppId = c.Label,
                        Name = c.Label,
                        Style = c.Style,
                        ColorHex = c.ColorHex,
                        DurationMs = 500,
                        Intensity = 0.8,
                        GlowSize = 32,
                        CornerRadius = 12
                    }, settings, isTest: false);
                    Assert.IsNotNull(res, $"PLAY_GLOW returned null for {c.Label}");
                    Assert.AreEqual("STARTED", res!.Action, $"Expected STARTED for {c.Label}");
                    await Task.Delay(80);
                    Assert.IsTrue(await sup.IpcClient.StopGlowAsync(), $"STOP_GLOW failed for {c.Label}");
                }

                // Allow overlay thread teardown
                await Task.Delay(200);

                var monResp = await sup.IpcClient.NotifyMonitorChangedAsync(trigger: true);
                Assert.IsTrue(monResp.Ok);

                var glowStatus = await sup.IpcClient.GetGlowStatusAsync(ackSeq: 0);
                Assert.IsNotNull(glowStatus);
                Assert.AreEqual(0, glowStatus!.ActiveOverlayWindows, "All overlay windows must be closed");
                Assert.AreEqual(glowStatus.WindowsCreated, glowStatus.WindowsDestroyed, "Zero HWND leaks expected");
                Assert.AreEqual(glowStatus.DibsCreated, glowStatus.DibsDestroyed, "Zero DIB/GDI leaks expected");
                Assert.IsTrue(glowStatus.TotalTriggers >= 6);
                Assert.IsTrue(glowStatus.MonitorChanges >= 1);
                Assert.IsTrue(glowStatus.Events.Any(e => e.Kind == "GLOW_STARTED"));
                Assert.IsTrue(glowStatus.Events.Any(e => e.Kind == "GLOW_STOPPED"));
                Assert.IsTrue(glowStatus.Events.Any(e => e.Kind == "MONITOR_CHANGED"));
            }
            finally
            {
                await sup.ShutdownAsync(2500);
                try { Directory.Delete(cfgDir, true); } catch { }
            }
        }

        [TestMethod]
        public async Task WorkerSyncCoordinator_SyncsGameEvents_AndHandlesDisconnectCleanly()
        {
            string pipe = $"NotiGlow_P3D_Sync_{Guid.NewGuid():N}";
            string mutex = $"Local\\NotiGlow_P3D_Sync_{Guid.NewGuid():N}";
            var settings = new AppSettings
            {
                MasterEnabled = true,
                GamingModeEnabled = true,
                UseRustBackgroundWorker = true,
                UseRustGameDetection = true,
                UseRustGlowEngine = true
            };
            string cfgDir = CreateTempConfigDir(settings);

            var settingsSvc = new SettingsService(Path.Combine(cfgDir, "settings.json"));
            var notifSvc = new NotificationService();
            notifSvc.EnableExternalWorkerOwnership();
            var gameSvc = new GameDetectionService(settingsSvc);
            gameSvc.EnableExternalWorkerOwnership();

            using var sup = new WorkerSupervisor(
                workerExecutablePath: GetWorkerExePath(),
                pipeName: pipe,
                mutexName: mutex,
                configDir: cfgDir,
                ipcOnly: true);

            using var coord = new WorkerSyncCoordinator(
                sup,
                settingsSvc,
                notifSvc,
                gameDetectionService: gameSvc);

            var receivedGameEvents = new List<string>();
            coord.GameEventReceived += (_, ev) => receivedGameEvents.Add(ev.Kind);

            try
            {
                Assert.IsTrue(await sup.EnsureStartedAsync(4000));

                await sup.IpcClient.SendCommandAsync("GAME_STATUS", new
                {
                    setSimulatedGame = "Halo Infinite",
                    executablePath = @"C:\XboxGames\HaloInfinite\HaloInfinite.exe",
                    launcher = "Xbox"
                });

                int count = await coord.PumpEventsOnceAsync();
                Assert.IsTrue(count >= 1);
                Assert.IsTrue(gameSvc.IsGameRunning(), "GameDetectionService should mirror Rust active game");
                Assert.AreEqual("haloinfinite.exe", gameSvc.ActiveGameIdentifier);
                Assert.IsTrue(receivedGameEvents.Contains("GAME_DETECTED"));

                // Shutdown worker and verify IPC disconnect returns clean error without throwing
                await sup.ShutdownAsync(2500);
                var disconnectedResp = await sup.IpcClient.SendCommandAsync("PING", null, timeoutMs: 400, maxRetries: 0);
                Assert.IsFalse(disconnectedResp.Ok);
                Assert.IsTrue(
                    disconnectedResp.Error?.Code == IpcErrorCodes.PipeDisconnected ||
                    disconnectedResp.Error?.Code == IpcErrorCodes.Timeout);
            }
            finally
            {
                await sup.ShutdownAsync(1500);
                try { Directory.Delete(cfgDir, true); } catch { }
            }
        }
    }
}
