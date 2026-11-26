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
    public class Phase3BIntegrationTests
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
            string pipe = $"NotiGlow_3B_IPC_{tag}";
            string mutex = $"Local\\NotiGlow_3B_Mutex_{tag}";
            string tempDir = Path.Combine(Path.GetTempPath(), $"NotiGlow_Phase3B_Test_{tag}");
            Directory.CreateDirectory(tempDir);
            return (pipe, mutex, tempDir);
        }

        private static void SafeDeleteDir(string path)
        {
            try { if (Directory.Exists(path)) Directory.Delete(path, true); } catch { }
        }

        [TestMethod]
        [Timeout(20000)]
        public async Task SettingsSnapshot_MonotonicRevision_IgnoresStaleClientRevision_AndPreservesSchemaVersion()
        {
            string exe = ResolveTestWorkerExe();
            Assert.IsTrue(File.Exists(exe), $"Worker binary missing: {exe}");
            var (pipe, mutex, tempDir) = CreateIsolatedContext();

            using var sup = new WorkerSupervisor(exe, pipe, mutex, tempDir, ipcOnly: true);
            try
            {
                var s1 = new AppSettings { MasterEnabled = true, GamingModeEnabled = false };
                s1.TrackedGames.Add("game1.exe");
                await File.WriteAllTextAsync(Path.Combine(tempDir, "settings.json"), JsonSerializer.Serialize(s1));

                Assert.IsTrue(await sup.EnsureStartedAsync(4000));

                // Reload with clientRevision = 5 -> applied
                s1.TrackedGames.Add("game2.exe");
                await File.WriteAllTextAsync(Path.Combine(tempDir, "settings.json"), JsonSerializer.Serialize(s1));
                var r5 = await sup.IpcClient.ReloadSettingsAsync(clientRevision: 5);
                Assert.IsNotNull(r5);
                Assert.IsTrue(r5!.Reloaded);
                Assert.IsFalse(r5.StaleIgnored);
                Assert.AreEqual(5, r5.ClientRevision);
                Assert.AreEqual(1, r5.SchemaVersion);
                Assert.AreEqual(2, r5.TrackedGamesCount);

                // Stale clientRevision = 3 arrives out-of-order -> MUST be ignored without overwriting!
                var r3 = await sup.IpcClient.ReloadSettingsAsync(clientRevision: 3);
                Assert.IsNotNull(r3);
                Assert.IsFalse(r3!.Reloaded);
                Assert.IsTrue(r3.StaleIgnored);
                Assert.AreEqual(5, r3.ClientRevision);
                Assert.AreEqual(r5.SettingsRevision, r3.SettingsRevision);

                // Newer clientRevision = 6 -> applied cleanly
                s1.TrackedGames.Add("game3.exe");
                await File.WriteAllTextAsync(Path.Combine(tempDir, "settings.json"), JsonSerializer.Serialize(s1));
                var r6 = await sup.IpcClient.ReloadSettingsAsync(clientRevision: 6);
                Assert.IsNotNull(r6);
                Assert.IsTrue(r6!.Reloaded);
                Assert.AreEqual(6, r6.ClientRevision);
                Assert.AreEqual(3, r6.TrackedGamesCount);
            }
            finally
            {
                await sup.ShutdownAsync(2500);
                SafeDeleteDir(tempDir);
            }
        }

        [TestMethod]
        [Timeout(20000)]
        public async Task ReloadSettings_Concurrent10Requests_NeverRegressesSnapshotOrCorrupts()
        {
            string exe = ResolveTestWorkerExe();
            Assert.IsTrue(File.Exists(exe), $"Worker binary missing: {exe}");
            var (pipe, mutex, tempDir) = CreateIsolatedContext();

            using var sup = new WorkerSupervisor(exe, pipe, mutex, tempDir, ipcOnly: true);
            try
            {
                var settings = new AppSettings { MasterEnabled = true };
                settings.TrackedGames.Add("eldenring.exe");
                await File.WriteAllTextAsync(Path.Combine(tempDir, "settings.json"), JsonSerializer.Serialize(settings));

                Assert.IsTrue(await sup.EnsureStartedAsync(4000));

                // Fire 10 concurrent RELOAD_SETTINGS requests with mixed revisions
                var tasks = Enumerable.Range(1, 10)
                    .Select(i => sup.IpcClient.ReloadSettingsAsync(clientRevision: i))
                    .ToArray();

                var results = await Task.WhenAll(tasks);
                Assert.IsTrue(results.All(r => r != null), "All 10 concurrent reloads must return valid responses.");
                long maxClientRev = results.Max(r => r!.ClientRevision);
                Assert.AreEqual(10, maxClientRev);

                var status = await sup.IpcClient.GetStatusAsync();
                Assert.IsNotNull(status);
                Assert.AreEqual(1, status!.TrackedGamesCount);
            }
            finally
            {
                await sup.ShutdownAsync(2500);
                SafeDeleteDir(tempDir);
            }
        }

        [TestMethod]
        [Timeout(20000)]
        public async Task UiCloseAndReopen_WorkerSurvivesDetach_AndSecondUiReattachesToSamePid()
        {
            string exe = ResolveTestWorkerExe();
            Assert.IsTrue(File.Exists(exe), $"Worker binary missing: {exe}");
            var (pipe, mutex, tempDir) = CreateIsolatedContext();

            WorkerSupervisor? uiSession2 = null;
            try
            {
                // UI Session #1 starts worker
                int firstPid;
                using (var uiSession1 = new WorkerSupervisor(exe, pipe, mutex, tempDir, ipcOnly: true))
                {
                    Assert.IsTrue(await uiSession1.EnsureStartedAsync(4000));
                    Assert.IsFalse(uiSession1.AttachedToExisting);
                    Assert.IsNotNull(uiSession1.WorkerPid);
                    Assert.IsNotNull(uiSession1.ConnectedWorkerVersion);
                    Assert.IsNotNull(uiSession1.LastStatus);
                    firstPid = uiSession1.WorkerPid!.Value;

                    // UI Session #1 closes -> Detach() leaves worker alive!
                    uiSession1.Detach();
                }

                // Verify worker is STILL running after UI #1 closed
                using (var procCheck = Process.GetProcessById(firstPid))
                {
                    Assert.IsFalse(procCheck.HasExited, "Worker must continue running after UI closes.");
                }

                // UI Session #2 reopens -> must attach to the EXACT SAME PID (no 2nd worker!)
                uiSession2 = new WorkerSupervisor(exe, pipe, mutex, tempDir, ipcOnly: true);
                Assert.IsTrue(await uiSession2.EnsureStartedAsync(4000));
                Assert.IsTrue(uiSession2.AttachedToExisting, "Reopened UI must attach to existing worker.");
                Assert.AreEqual(firstPid, uiSession2.WorkerPid);
                Assert.AreEqual(WorkerHealthState.Healthy, uiSession2.State);
                Assert.IsNotNull(uiSession2.LastStatus);
            }
            finally
            {
                if (uiSession2 != null)
                {
                    await uiSession2.ShutdownAsync(2500);
                    uiSession2.Dispose();
                }
                SafeDeleteDir(tempDir);
            }
        }

        [TestMethod]
        [Timeout(20000)]
        public async Task Worker_AutoRestartOnCrash_RecoversAttachedWorkerAutomatically()
        {
            string exe = ResolveTestWorkerExe();
            Assert.IsTrue(File.Exists(exe), $"Worker binary missing: {exe}");
            var (pipe, mutex, tempDir) = CreateIsolatedContext();

            using var sup = new WorkerSupervisor(exe, pipe, mutex, tempDir, ipcOnly: true)
            {
                AutoRestartOnCrash = true
            };
            try
            {
                var restartedTcs = new TaskCompletionSource<int>(TaskCreationOptions.RunContinuationsAsynchronously);
                sup.WorkerRestarted += (_, newPid) => restartedTcs.TrySetResult(newPid);

                Assert.IsTrue(await sup.EnsureStartedAsync(4000));
                int firstPid = sup.WorkerPid!.Value;

                // Force-kill worker to simulate crash
                using (var p = Process.GetProcessById(firstPid))
                {
                    p.Kill(entireProcessTree: true);
                    p.WaitForExit(3000);
                }

                // AutoRestartOnCrash must automatically restart and raise WorkerRestarted with a new PID
                var done = await Task.WhenAny(restartedTcs.Task, Task.Delay(5000));
                Assert.AreEqual(restartedTcs.Task, done, "WorkerSupervisor did not auto-restart within 5s.");
                int newPid = await restartedTcs.Task;
                Assert.AreNotEqual(firstPid, newPid);
                Assert.AreEqual(WorkerHealthState.Healthy, sup.State);
                Assert.IsTrue(await sup.CheckHealthAsync());
            }
            finally
            {
                await sup.ShutdownAsync(2500);
                SafeDeleteDir(tempDir);
            }
        }

        [TestMethod]
        [Timeout(20000)]
        public async Task CorruptSettingsFile_PreservesLastValidSnapshot_AndUnknownFieldsSurvive()
        {
            string exe = ResolveTestWorkerExe();
            Assert.IsTrue(File.Exists(exe), $"Worker binary missing: {exe}");
            var (pipe, mutex, tempDir) = CreateIsolatedContext();

            using var sup = new WorkerSupervisor(exe, pipe, mutex, tempDir, ipcOnly: true);
            try
            {
                // Valid settings with an unknown future JSON field
                string validJson = "{\"MasterEnabled\":true,\"GamingModeEnabled\":true,\"TrackedGames\":[\"hades.exe\"],\"FutureUnknownField\":\"preserve_me\"}";
                await File.WriteAllTextAsync(Path.Combine(tempDir, "settings.json"), validJson);

                Assert.IsTrue(await sup.EnsureStartedAsync(4000));
                var st1 = await sup.IpcClient.GetStatusAsync();
                Assert.IsNotNull(st1);
                Assert.AreEqual(1, st1!.TrackedGamesCount);
                Assert.IsTrue(st1.GamingModeEnabled);

                // Write half-written / truncated JSON -> worker must NOT crash and must preserve valid snapshot!
                await File.WriteAllTextAsync(Path.Combine(tempDir, "settings.json"), "{\"MasterEnabled\":true,\"Tracke");
                var errResp = await sup.IpcClient.SendCommandAsync("RELOAD_SETTINGS", new { clientRevision = 2 }, 1500, maxRetries: 0);
                Assert.IsFalse(errResp.Ok);
                Assert.AreEqual(IpcErrorCodes.ConfigParse, errResp.Error?.Code);

                // Prior valid state must still be intact
                var st2 = await sup.IpcClient.GetStatusAsync();
                Assert.IsNotNull(st2);
                Assert.AreEqual(1, st2!.TrackedGamesCount);
                Assert.IsTrue(st2.GamingModeEnabled);

                // Repair file -> next reload succeeds cleanly
                string repairedJson = "{\"MasterEnabled\":true,\"GamingModeEnabled\":false,\"TrackedGames\":[\"hades.exe\",\"celeste.exe\"],\"FutureUnknownField\":\"still_ok\"}";
                await File.WriteAllTextAsync(Path.Combine(tempDir, "settings.json"), repairedJson);
                var okResp = await sup.IpcClient.ReloadSettingsAsync(clientRevision: 3);
                Assert.IsNotNull(okResp);
                Assert.IsTrue(okResp!.Reloaded);
                Assert.AreEqual(2, okResp.TrackedGamesCount);
            }
            finally
            {
                await sup.ShutdownAsync(2500);
                SafeDeleteDir(tempDir);
            }
        }
    }
}
