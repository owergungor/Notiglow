using System;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NotiGlow.Services;
using NotiGlow.UI.Dialogs;

namespace NotiGlow.Tests
{
    [TestClass]
    public class V27FeatureTests
    {
        [TestInitialize]
        public void Setup()
        {
            LocalizationService.Instance.SetLanguage("en-US");
        }

        #region Requirement 1: Localization Completeness & Zero Dotted Keys

        [TestMethod]
        public void Test_AllReferencedKeys_ExistInAllLanguages()
        {
            var languages = new[] { "en-US", "es-ES", "fr-FR", "de-DE", "tr-TR" };
            var enDict = LocalizationService.Instance.GetRawDictionary("en-US");

            // Key examples specifically requested
            var requiredKeys = new[]
            {
                "Advanced.InstallNow",
                "Advanced.ResetToDefaults",
                "Applications.CardCopy",
                "Applications.CardDelete",
                "Applications.CardDurationAndIntensity",
                "Applications.CardEdit",
                "Applications.CardPreview",
                "Applications.CardToggleToolTip",
                "Applications.Delete",
                "Applications.ExecutableOrAppId",
                "EdgePreview.LivePreviewBanner",
                "Toast.AccessDenied",
                "Toast.ErrorTitle",
                "Toast.FileNotFound",
                "Toast.GenericError",
                "Toast.InfoTitle",
                "Toast.IoError",
                "Toast.SuccessTitle",
                "Toast.WarningTitle",
                "Tray.Exit",
                "Tray.OpenSettings",
                "Common.Confirm",
                "Common.Apply",
                "Common.Warning",
                "Common.Error",
                "Common.Information",
                "Common.Info",
                "Common.Advanced",
                "Common.Reset",
                "Common.Cancel",
                "Common.Save",
                "Common.Close"
            };

            foreach (var lang in languages)
            {
                var dict = LocalizationService.Instance.GetRawDictionary(lang);
                foreach (var k in requiredKeys)
                {
                    Assert.IsTrue(dict.ContainsKey(k), $"Key '{k}' must be present in dictionary for '{lang}'.");
                    Assert.IsFalse(string.IsNullOrWhiteSpace(dict[k]), $"Key '{k}' in '{lang}' must not be empty.");
                    Assert.IsFalse(dict[k].Contains("."), $"Value for key '{k}' in '{lang}' must not be a dotted technical string.");
                }
            }
        }

        [TestMethod]
        public void Test_SafeFallback_NeverReturnsTechnicalDottedKey()
        {
            // Unknown key with dots
            string fallback = LocalizationService.Instance.GetString("CustomModule.DoSpecialAction");
            Assert.AreEqual("Do Special Action", fallback, "PascalCase leaf must be split into readable words.");

            // Known canonical alias fallback even if not in dictionary
            string resetFallback = LocalizationService.GenerateSafeFallbackText("CustomCategory.ResetToDefaults");
            Assert.AreEqual("Reset to Defaults", resetFallback, "Canonical alias must be resolved.");

            // Dialog button text fallback
            string confirmFallback = LocalizationService.GenerateSafeFallbackText("Common.Confirm");
            Assert.AreEqual("Confirm", confirmFallback);

            // Plain text is preserved
            string plainText = LocalizationService.GenerateSafeFallbackText("Hello World");
            Assert.AreEqual("Hello World", plainText);
        }

        [TestMethod]
        public void Test_SplitPascalCaseToWords()
        {
            Assert.AreEqual("Reset To Defaults", LocalizationService.SplitPascalCaseToWords("ResetToDefaults"));
            Assert.AreEqual("Install Now", LocalizationService.SplitPascalCaseToWords("InstallNow"));
            Assert.AreEqual("XML Parser", LocalizationService.SplitPascalCaseToWords("XMLParser"));
            Assert.AreEqual("Simple", LocalizationService.SplitPascalCaseToWords("Simple"));
        }

        #endregion

        #region Requirement 2: ComboBox Item Metrics & No Text Clipping

        [TestMethod]
        public void Test_AppXaml_ComboBoxItemMetrics()
        {
            string appXamlPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "..", "..", "..", "..", "App.xaml");
            if (!File.Exists(appXamlPath))
            {
                appXamlPath = Path.Combine(Directory.GetCurrentDirectory(), "App.xaml");
            }

            Assert.IsTrue(File.Exists(appXamlPath), "App.xaml must exist.");
            string xaml = File.ReadAllText(appXamlPath);

            // Ensure AppleSelectComboBoxStyle and ColorThemeComboBoxStyle do not fix Height="32" on items
            int appleIdx = xaml.IndexOf("AppleSelectComboBoxStyle");
            int colorIdx = xaml.IndexOf("ColorThemeComboBoxStyle");
            string appleSection = xaml.Substring(appleIdx, Math.Min(1200, xaml.Length - appleIdx));
            string colorSection = xaml.Substring(colorIdx, Math.Min(1200, xaml.Length - colorIdx));

            Assert.IsFalse(appleSection.Contains("<Setter Property=\"Height\" Value=\"32\"/>"),
                "AppleSelectComboBoxStyle ComboBox item containers must not enforce a hardcoded fixed Height='32'.");
            Assert.IsFalse(colorSection.Contains("<Setter Property=\"Height\" Value=\"32\"/>"),
                "ColorThemeComboBoxStyle ComboBox item containers must not enforce a hardcoded fixed Height='32'.");

            // Ensure MinHeight >= 34 is set
            Assert.IsTrue(xaml.Contains("<Setter Property=\"MinHeight\" Value=\"34\"/>"),
                "ComboBox item containers must have MinHeight of at least 34.");

            // Ensure comfortable padding (10,4)
            Assert.IsTrue(xaml.Contains("<Setter Property=\"Padding\" Value=\"10,4\"/>"),
                "ComboBox item containers must have comfortable vertical padding (10,4).");
        }

        [TestMethod]
        public void Test_ApplicationsViewXaml_NoExcessiveItemPadding()
        {
            string viewXamlPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "..", "..", "..", "..", "UI", "Views", "ApplicationsView.xaml");
            if (!File.Exists(viewXamlPath))
            {
                viewXamlPath = Path.Combine(Directory.GetCurrentDirectory(), "UI", "Views", "ApplicationsView.xaml");
            }

            Assert.IsTrue(File.Exists(viewXamlPath), "ApplicationsView.xaml must exist.");
            string xaml = File.ReadAllText(viewXamlPath);

            Assert.IsFalse(xaml.Contains("Padding=\"12,7\""),
                "Hardcoded vertical padding of 7px on ComboBoxItem in ApplicationsView.xaml must be removed to prevent clipping.");
        }

        #endregion

        #region Requirement 3: Dead Code Removal

        [TestMethod]
        public void Test_DeadCode_MethodsRemoved()
        {
            string baseDir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "..", "..", "..", "..");
            if (!File.Exists(Path.Combine(baseDir, "App.xaml.cs")))
            {
                baseDir = Directory.GetCurrentDirectory();
            }

            string appCs = File.ReadAllText(Path.Combine(baseDir, "App.xaml.cs"));
            Assert.IsFalse(appCs.Contains("GetWindowsSystemThemeIsDark"),
                "Unused private method GetWindowsSystemThemeIsDark must be removed from App.xaml.cs.");

            string profileCs = File.ReadAllText(Path.Combine(baseDir, "Services", "ProfileService.cs"));
            Assert.IsFalse(profileCs.Contains("GetInitialStarterProfiles"),
                "Unused private method GetInitialStarterProfiles must be removed from ProfileService.cs.");

            string appsCs = File.ReadAllText(Path.Combine(baseDir, "UI", "Views", "ApplicationsView.xaml.cs"));
            Assert.IsFalse(appsCs.Contains("BtnTestNotification_Click"),
                "Unused private method BtnTestNotification_Click must be removed from ApplicationsView.xaml.cs.");
        }

        #endregion

        #region Requirement 4: Lazy/Dirty Loading in GamingView

        [TestMethod]
        public void Test_GamingView_HasEnsureLoadedAndDirtyTracking()
        {
            string baseDir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "..", "..", "..", "..");
            if (!File.Exists(Path.Combine(baseDir, "UI", "Views", "GamingView.xaml.cs")))
            {
                baseDir = Directory.GetCurrentDirectory();
            }

            string gamingCs = File.ReadAllText(Path.Combine(baseDir, "UI", "Views", "GamingView.xaml.cs"));

            Assert.IsTrue(gamingCs.Contains("public void EnsureLoaded()"),
                "GamingView must provide public void EnsureLoaded() to avoid card reconstruction churn.");
            Assert.IsTrue(gamingCs.Contains("public bool IsDirty => _isDirty;"),
                "GamingView must provide IsDirty property.");
            Assert.IsTrue(gamingCs.Contains("_isDirty = false;"),
                "RenderTrackedGameCards must reset _isDirty flag to false.");

            string mainCs = File.ReadAllText(Path.Combine(baseDir, "UI", "MainWindow.xaml.cs"));
            Assert.IsTrue(mainCs.Contains("ViewGaming.EnsureLoaded()"),
                "MainWindow must invoke ViewGaming.EnsureLoaded() when navigating to Gaming tab.");
        }

        #endregion

        #region Requirement 5: Zero Windows MessageBox

        [TestMethod]
        public void Test_Zero_Native_MessageBox_In_Source()
        {
            string baseDir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "..", "..", "..", "..");
            if (!File.Exists(Path.Combine(baseDir, "NotiGlow.sln")))
            {
                baseDir = Directory.GetCurrentDirectory();
            }

            var csFiles = Directory.GetFiles(baseDir, "*.cs", SearchOption.AllDirectories)
                .Where(f => !f.Contains("NotiGlow.Tests") && !f.Contains("bin") && !f.Contains("obj"))
                .ToList();

            string forbidden = "Message" + "Box.Show";
            foreach (var file in csFiles)
            {
                string text = File.ReadAllText(file);
                Assert.IsFalse(text.Contains(forbidden),
                    $"File '{Path.GetFileName(file)}' must NOT call native Windows MessageBox. Found in {file}");
            }
        }

        #endregion

        #region Requirement 6: Version 2.7 Metadata Verification

        [TestMethod]
        public void Test_Version_2_7_Metadata()
        {
            var version = ReleaseVersionAssertions.AssertApplicationVersionMetadata();
            Assert.AreEqual(version, UpdateService.FallbackVersion);
            ReleaseVersionAssertions.AssertInstallerVersionMetadata(version);
            ReleaseVersionAssertions.AssertPackageManifestVersionMetadata(version);
            ReleaseVersionAssertions.AssertAdvancedViewUsesCurrentVersion();
        }

        #endregion
    
        #region Requirement 7: Automatic Update Restart and Safety Verification

        [TestMethod]
        public void Test_GenerateInstallerUpdaterScript_ContainsSilentAndRestartParameters()
        {
            int testPid = 54321;
            string testExe = @"C:\Program Files\NotiGlow\NotiGlow.exe";
            string testDir = @"C:\Program Files\NotiGlow";
            string installerPath = @"C:\Users\User\AppData\Roaming\NotiGlow\updates\NotiGlow-Setup-v2.7.exe";

            string script = UpdateService.GenerateInstallerUpdaterScript(testPid, testExe, testDir, installerPath);

            Assert.IsNotNull(script);
            Assert.IsTrue(script.Contains("pid = 54321"), "Script must wait for current process ID to terminate.");
            Assert.IsTrue(script.Contains("/SILENT /SUPPRESSMSGBOXES /NORESTART"), "Script must run Inno Setup silently without native prompt dialogs.");
            Assert.IsTrue(script.Contains("shell.Run") && script.Contains("0, True"), "Script must execute installer synchronously and hidden.");
            Assert.IsTrue(script.Contains("--updated"), "Script must relaunch target executable with --updated argument.");
            Assert.IsTrue(script.Contains("shell.CurrentDirectory = targetDir"), "Script must set current working directory.");
        }

        [TestMethod]
        public void Test_GenerateDirectExeUpdaterScript_ContainsBackupRollbackAndRestart()
        {
            int testPid = 65432;
            string testExe = @"C:\NotiGlow\NotiGlow.exe";
            string testDir = @"C:\NotiGlow";
            string newExePath = @"C:\Users\User\AppData\Roaming\NotiGlow\updates\NotiGlow.exe";

            string script = UpdateService.GenerateDirectExeUpdaterScript(testPid, testExe, testDir, newExePath);

            Assert.IsNotNull(script);
            Assert.IsTrue(script.Contains("pid = 65432"), "Script must wait for process ID to terminate.");
            Assert.IsTrue(script.Contains(".bak"), "Script must create a .bak backup before overwriting.");
            Assert.IsTrue(script.Contains("CopyFile"), "Script must copy new executable over target.");
            Assert.IsTrue(script.Contains("If Err.Number <> 0 Then"), "Script must check for copy errors.");
            Assert.IsTrue(script.Contains("--updated"), "Script must relaunch application with --updated argument.");
        }

        [TestMethod]
        public void Test_GenerateVbsUpdaterScript_ContainsRelaunchWithUpdatedArgument()
        {
            int testPid = 76543;
            string testExe = @"C:\NotiGlow\NotiGlow.exe";
            string testDir = @"C:\NotiGlow";
            string testStaged = @"C:\Users\User\AppData\Roaming\NotiGlow\updates\staged_123";
            string testBackup = @"C:\Users\User\AppData\Roaming\NotiGlow\updates\backup_2.7";

            string script = UpdateService.GenerateVbsUpdaterScript(testPid, testExe, testDir, testStaged, testBackup);

            Assert.IsNotNull(script);
            Assert.IsTrue(script.Contains("--updated"), "ZIP updater script must launch target executable with --updated argument.");
        }

        [TestMethod]
        public void Test_ExecuteUpdateAndRestart_FailsSafelyIfPackageDoesNotExist()
        {
            bool closeCalled = false;
            bool result = UpdateService.ExecuteUpdateAndRestart(@"C:\NonExistentPath\NotiGlow-Setup-v999.exe", () =>
            {
                closeCalled = true;
            });

            Assert.IsFalse(result, "ExecuteUpdateAndRestart must return false when package file does not exist.");
            Assert.IsFalse(closeCalled, "onAppCloseRequested must NOT be invoked when package file is missing.");
        }

        [TestMethod]
        public void Test_ExecuteUpdateAndRestart_FailsSafelyIfPackageIsEmpty()
        {
            string emptyFile = Path.Combine(Path.GetTempPath(), "NotiGlow_EmptyPackageTest_" + Guid.NewGuid().ToString("N") + ".exe");
            File.WriteAllBytes(emptyFile, Array.Empty<byte>());

            try
            {
                bool closeCalled = false;
                bool result = UpdateService.ExecuteUpdateAndRestart(emptyFile, () =>
                {
                    closeCalled = true;
                });

                Assert.IsFalse(result, "ExecuteUpdateAndRestart must return false when package file is 0 bytes.");
                Assert.IsFalse(closeCalled, "onAppCloseRequested must NOT be invoked when package file is empty.");
            }
            finally
            {
                if (File.Exists(emptyFile)) File.Delete(emptyFile);
            }
        }

        [TestMethod]
        public void Test_AppStartup_HandlesUpdatedArgumentAndSuppressesLoop()
        {
            string baseDir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "..", "..", "..", "..");
            if (!File.Exists(Path.Combine(baseDir, "App.xaml.cs")))
            {
                baseDir = Directory.GetCurrentDirectory();
            }

            string appCs = File.ReadAllText(Path.Combine(baseDir, "App.xaml.cs"));

            Assert.IsTrue(appCs.Contains("e.Args.Contains(\"--updated\")"),
                "App.xaml.cs must detect --updated argument to identify automatic update restart.");
            Assert.IsTrue(appCs.Contains("!isUpdatedRestart"),
                "App.xaml.cs must suppress immediate background update check on update restart to prevent restart loops.");
        }

        #endregion
    }
}
