using System;
using System.IO;
using System.Threading.Tasks;
using System.Xml.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NotiGlow.Core.Helpers;
using NotiGlow.Models;
using NotiGlow.Services.Ipc;

namespace NotiGlow.Tests
{
    [TestClass]
    public class Phase3EPackagingTests
    {
        private static string FindRepoRoot()
        {
            string? dir = AppContext.BaseDirectory;
            for (int i = 0; i < 8 && !string.IsNullOrEmpty(dir); i++)
            {
                if (File.Exists(Path.Combine(dir, "NotiGlow.csproj")))
                    return dir;
                dir = Directory.GetParent(dir)?.FullName;
            }
            throw new DirectoryNotFoundException("Repository root not found.");
        }

        [TestMethod]
        public void AppxManifest_DeclaresFullTrustWorker_AndStartupTask()
        {
            string repo = FindRepoRoot();
            string manifestPath = Path.Combine(repo, "Package", "AppxManifest.xml");
            Assert.IsTrue(File.Exists(manifestPath));

            var doc = XDocument.Load(manifestPath);
            string xml = doc.ToString();
            Assert.IsTrue(xml.Contains("windows.startupTask", StringComparison.Ordinal), "Missing windows.startupTask extension");
            Assert.IsTrue(xml.Contains(AutoStartHelper.MsixStartupTaskId, StringComparison.Ordinal), "Missing NotiGlowStartupTask id");
            Assert.IsTrue(xml.Contains("windows.fullTrustProcess", StringComparison.Ordinal), "Missing windows.fullTrustProcess extension");
            Assert.IsTrue(xml.Contains("NotiGlow_Background.exe", StringComparison.Ordinal), "Missing NotiGlow_Background.exe reference");
            Assert.IsTrue(xml.Contains("runFullTrust", StringComparison.Ordinal), "Missing runFullTrust capability");
        }

        [TestMethod]
        public void InnoSetupScript_UsesLowestPrivileges_CleansRunKey_AndPreservesUserData()
        {
            string repo = FindRepoRoot();
            string issPath = Path.Combine(repo, "NotiGlow-Setup.iss");
            Assert.IsTrue(File.Exists(issPath));
            string iss = File.ReadAllText(issPath);

            Assert.IsTrue(iss.Contains("PrivilegesRequired=lowest", StringComparison.OrdinalIgnoreCase));
            Assert.IsTrue(iss.Contains("uninsdeletevalue", StringComparison.OrdinalIgnoreCase));
            Assert.IsTrue(iss.Contains("NotiGlow_Background.exe", StringComparison.OrdinalIgnoreCase));
            Assert.IsFalse(iss.Contains("{userappdata}\\NotiGlow", StringComparison.OrdinalIgnoreCase), "Installer must never delete user AppData");
        }

        [TestMethod]
        public void FeatureFlags_RemainDefaultFalse_ForPhase3E()
        {
            var s = new AppSettings();
            Assert.IsFalse(s.UseRustBackgroundWorker);
            Assert.IsFalse(s.UseRustGameDetection);
            Assert.IsFalse(s.UseRustGlowEngine);
        }

        [TestMethod]
        public async Task WorkerSupervisor_RecyclesOldWorker_WhenInstallPathChangesDuringUpdate()
        {
            string repo = FindRepoRoot();
            string workerSrc = Path.Combine(repo, "native", "NotiGlow.Background", "target", "release", "NotiGlow_Background.exe");
            Assert.IsTrue(File.Exists(workerSrc), "Release worker binary missing");

            string dirV1 = Path.Combine(Path.GetTempPath(), $"NotiGlow_P3E_V1_{Guid.NewGuid():N}");
            string dirV2 = Path.Combine(Path.GetTempPath(), $"NotiGlow_P3E_V2_{Guid.NewGuid():N}");
            Directory.CreateDirectory(dirV1);
            Directory.CreateDirectory(dirV2);
            string exeV1 = Path.Combine(dirV1, "NotiGlow_Background.exe");
            string exeV2 = Path.Combine(dirV2, "NotiGlow_Background.exe");
            File.Copy(workerSrc, exeV1, true);
            File.Copy(workerSrc, exeV2, true);

            string pipe = $"NotiGlow_P3E_Upd_{Guid.NewGuid():N}";
            string mutex = $"Local\\NotiGlow_P3E_Upd_{Guid.NewGuid():N}";

            using var supV1 = new WorkerSupervisor(workerExecutablePath: exeV1, pipeName: pipe, mutexName: mutex, ipcOnly: true);
            using var supV2 = new WorkerSupervisor(workerExecutablePath: exeV2, pipeName: pipe, mutexName: mutex, ipcOnly: true);

            try
            {
                Assert.IsTrue(await supV1.EnsureStartedAsync(4000));
                int pidV1 = supV1.WorkerPid!.Value;

                // Detach v1 supervisor (simulating old version UI closed, old worker still running)
                supV1.Detach();

                // New version v2 starts from a different directory -> must recycle v1 worker and launch v2!
                Assert.IsTrue(await supV2.EnsureStartedAsync(5000));
                int pidV2 = supV2.WorkerPid!.Value;
                Assert.AreNotEqual(pidV1, pidV2, "Update must recycle old worker from previous install directory");
                Assert.IsFalse(supV2.AttachedToExisting, "Should have spawned fresh v2 worker");
            }
            finally
            {
                await supV2.ShutdownAsync(2500);
                try { Directory.Delete(dirV1, true); } catch { }
                try { Directory.Delete(dirV2, true); } catch { }
            }
        }
    }
}
