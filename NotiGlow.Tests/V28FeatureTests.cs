using System;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Xml.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NotiGlow.Models;
using NotiGlow.Services;

namespace NotiGlow.Tests
{
    [TestClass]
    public class V28FeatureTests
    {
        private string _repoRoot = string.Empty;

        [TestInitialize]
        public void Setup()
        {
            LocalizationService.Instance.SetLanguage("en-US");
            PackageService.SetPackagedOverrideForTesting(null);

            // Locate repo root
            var current = AppDomain.CurrentDomain.BaseDirectory;
            while (!string.IsNullOrEmpty(current) && !File.Exists(Path.Combine(current, "NotiGlow.sln")))
            {
                var parent = Directory.GetParent(current);
                current = parent?.FullName;
            }
            _repoRoot = current ?? Directory.GetCurrentDirectory();
        }

        [TestCleanup]
        public void Cleanup()
        {
            PackageService.SetPackagedOverrideForTesting(null);
        }

        #region Requirement 1: Version Metadata Consistency across Project

        [TestMethod]
        public void Test_VersionMetadata_Csproj()
        {
            var csprojPath = Path.Combine(_repoRoot, "NotiGlow.csproj");
            Assert.IsTrue(File.Exists(csprojPath), "NotiGlow.csproj must exist.");

            var content = File.ReadAllText(csprojPath);
            StringAssert.Contains(content, "<Version>2.8</Version>");
            StringAssert.Contains(content, "<AssemblyVersion>2.8.0.0</AssemblyVersion>");
            StringAssert.Contains(content, "<FileVersion>2.8.0.0</FileVersion>");
            StringAssert.Contains(content, "<InformationalVersion>2.8</InformationalVersion>");
        }

        [TestMethod]
        public void Test_VersionMetadata_InnoSetup()
        {
            var issPath = Path.Combine(_repoRoot, "NotiGlow-Setup.iss");
            Assert.IsTrue(File.Exists(issPath), "NotiGlow-Setup.iss must exist.");

            var content = File.ReadAllText(issPath);
            StringAssert.Contains(content, "AppVersion=2.8");
            StringAssert.Contains(content, "VersionInfoVersion=2.8.0.0");
            StringAssert.Contains(content, "VersionInfoTextVersion=2.8");
            StringAssert.Contains(content, "OutputBaseFilename=NotiGlow-Setup-v2.8");
        }

        [TestMethod]
        public void Test_VersionMetadata_AppxManifest()
        {
            var manifestPath = Path.Combine(_repoRoot, "Package", "AppxManifest.xml");
            Assert.IsTrue(File.Exists(manifestPath), "AppxManifest.xml must exist.");

            var doc = XDocument.Load(manifestPath);
            XNamespace ns = "http://schemas.microsoft.com/appx/manifest/foundation/windows10";
            var identity = doc.Descendants(ns + "Identity").FirstOrDefault();

            Assert.IsNotNull(identity, "Identity element must exist in AppxManifest.");
            Assert.AreEqual("2.8.0.0", identity.Attribute("Version")?.Value);
        }

        [TestMethod]
        public void Test_VersionMetadata_UpdateServiceAndUI()
        {
            var updateServicePath = Path.Combine(_repoRoot, "Services", "UpdateService.cs");
            var content = File.ReadAllText(updateServicePath);
            StringAssert.Contains(content, "_cachedVersion = \"2.8\";");

            var advancedXamlPath = Path.Combine(_repoRoot, "UI", "Views", "AdvancedView.xaml");
            var xamlContent = File.ReadAllText(advancedXamlPath);
            StringAssert.Contains(xamlContent, "Current Version: v2.8");
        }

        #endregion

        #region Requirement 2: Microsoft Store Identity Constants

        [TestMethod]
        public void Test_StoreIdentityConstants_MatchPartnerCenter()
        {
            Assert.AreEqual("crius.Notiglow", PackageService.ExpectedPackageName);
            Assert.AreEqual("CN=2F9A5694-FADC-405A-AF51-BB9269D3FFF1", PackageService.ExpectedPublisher);
            Assert.AreEqual("crius", PackageService.ExpectedPublisherDisplayName);
            Assert.AreEqual("crius.Notiglow_tvqx0hh5fwha", PackageService.ExpectedPackageFamily);
            Assert.AreEqual("9P9L33HG5V64", PackageService.StoreId);
            Assert.AreEqual("9a7abc5d-4339-446f-8fca-09cf11920b01", PackageService.MsaAppId);
            Assert.AreEqual("https://apps.microsoft.com/detail/9P9L33HG5V64", PackageService.StoreUrl);
        }

        #endregion

        #region Requirement 3: Distribution Channel & Update Separation

        [TestMethod]
        public void Test_PackageService_PackagedOverride_Behavior()
        {
            PackageService.SetPackagedOverrideForTesting(true);
            Assert.IsTrue(PackageService.IsPackaged);
            Assert.AreEqual(DistributionChannel.MicrosoftStore, PackageService.Channel);

            PackageService.SetPackagedOverrideForTesting(false);
            Assert.IsFalse(PackageService.IsPackaged);
            Assert.AreEqual(DistributionChannel.GitHub, PackageService.Channel);
        }

        [TestMethod]
        public void Test_StorePackaged_DisablesGitHubUpdates()
        {
            PackageService.SetPackagedOverrideForTesting(true);
            var settings = new AppSettings { AutoCheckUpdates = true, UpdateFrequency = UpdateCheckFrequency.Daily };

            // ShouldCheckForUpdates must return false regardless of settings
            Assert.IsFalse(UpdateService.ShouldCheckForUpdates(settings, DateTime.UtcNow));

            // ExecuteUpdateAndRestart must reject execution in packaged mode
            bool executed = UpdateService.ExecuteUpdateAndRestart("dummy_path.exe", () => { });
            Assert.IsFalse(executed, "ExecuteUpdateAndRestart must return false when packaged.");
        }

        [TestMethod]
        public async Task Test_StorePackaged_CheckForUpdates_ReturnsStoreManagedMessage()
        {
            PackageService.SetPackagedOverrideForTesting(true);
            var settings = new SettingsService();
            var updater = new UpdateService(settings);

            string? lastMessage = null;
            updater.StatusChanged += (s, e) => lastMessage = e.Message;

            var result = await updater.CheckForUpdatesAsync();

            Assert.IsNotNull(result);
            Assert.IsFalse(result.IsUpdateAvailable);
            Assert.IsTrue(lastMessage?.Contains("Microsoft Store") == true, "StatusChanged message should mention Microsoft Store.");
        }

        #endregion

        #region Requirement 4: Localization for Store Keys

        [TestMethod]
        public void Test_StoreLocalizationKeys_ExistInAllLanguages()
        {
            var languages = new[] { "en-US", "es-ES", "fr-FR", "de-DE", "tr-TR" };
            var requiredStoreKeys = new[]
            {
                "Advanced.StoreVersionFormat",
                "Advanced.StoreManaged"
            };

            foreach (var lang in languages)
            {
                var dict = LocalizationService.Instance.GetRawDictionary(lang);
                foreach (var k in requiredStoreKeys)
                {
                    Assert.IsTrue(dict.ContainsKey(k), $"Key '{k}' must exist in dictionary for '{lang}'.");
                    Assert.IsFalse(string.IsNullOrWhiteSpace(dict[k]), $"Key '{k}' in '{lang}' must not be empty.");
                }
            }
        }

        #endregion

        #region Requirement 5: AppxManifest & Assets Integrity

        [TestMethod]
        public void Test_AppxManifest_PropertiesAndCapabilities()
        {
            var manifestPath = Path.Combine(_repoRoot, "Package", "AppxManifest.xml");
            var doc = XDocument.Load(manifestPath);

            XNamespace ns = "http://schemas.microsoft.com/appx/manifest/foundation/windows10";
            XNamespace rescap = "http://schemas.microsoft.com/appx/manifest/foundation/windows10/restrictedcapabilities";

            var identity = doc.Descendants(ns + "Identity").FirstOrDefault();
            Assert.IsNotNull(identity);
            Assert.AreEqual(PackageService.ExpectedPackageName, identity.Attribute("Name")?.Value);
            Assert.AreEqual(PackageService.ExpectedPublisher, identity.Attribute("Publisher")?.Value);
            Assert.AreEqual("2.8.0.0", identity.Attribute("Version")?.Value);
            Assert.AreEqual("x64", identity.Attribute("ProcessorArchitecture")?.Value);

            var properties = doc.Descendants(ns + "Properties").FirstOrDefault();
            Assert.IsNotNull(properties);
            Assert.AreEqual("NotiGlow", properties.Element(ns + "DisplayName")?.Value);
            Assert.AreEqual(PackageService.ExpectedPublisherDisplayName, properties.Element(ns + "PublisherDisplayName")?.Value);

            // Validate only runFullTrust capability is declared
            var capabilities = doc.Descendants(rescap + "Capability").ToList();
            Assert.AreEqual(1, capabilities.Count, "Only runFullTrust restricted capability should be declared.");
            Assert.AreEqual("runFullTrust", capabilities[0].Attribute("Name")?.Value);
        }

        [TestMethod]
        public void Test_PackageAssets_AllExist()
        {
            var assetsDir = Path.Combine(_repoRoot, "Package", "Assets");
            Assert.IsTrue(Directory.Exists(assetsDir), "Package/Assets directory must exist.");

            var requiredAssets = new[]
            {
                "Square44x44Logo.png",
                "Square150x150Logo.png",
                "Wide310x150Logo.png",
                "Square310x310Logo.png",
                "StoreLogo.png",
                "SplashScreen.png",
                "Square44x44Logo.targetsize-24_altform-unplated.png",
                "Square44x44Logo.targetsize-48_altform-unplated.png"
            };

            foreach (var asset in requiredAssets)
            {
                var filePath = Path.Combine(assetsDir, asset);
                Assert.IsTrue(File.Exists(filePath), $"Asset '{asset}' must exist at {filePath}");
                var fileInfo = new FileInfo(filePath);
                Assert.IsTrue(fileInfo.Length > 0, $"Asset '{asset}' must not be empty.");
            }
        }

        #endregion

        #region Requirement 6: Zero Native MessageBox Calls

        [TestMethod]
        public void Test_ZeroMessageBox_InApplicationSource()
        {
            var csFiles = Directory.GetFiles(_repoRoot, "*.cs", SearchOption.AllDirectories)
                .Where(f => !f.Contains("NotiGlow.Tests") && !f.Contains("\\obj\\") && !f.Contains("\\bin\\"))
                .ToList();

            foreach (var file in csFiles)
            {
                var lines = File.ReadAllLines(file);
                for (int i = 0; i < lines.Length; i++)
                {
                    var line = lines[i];
                    if (Regex.IsMatch(line, @"\bMessageBox\.Show\b"))
                    {
                        Assert.Fail($"Forbidden native MessageBox call found in {Path.GetFileName(file)} at line {i + 1}: {line.Trim()}");
                    }
                }
            }
        }

        #endregion
    }
}
