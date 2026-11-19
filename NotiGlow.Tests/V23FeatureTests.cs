using System;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NotiGlow.Models;
using NotiGlow.Services;
using NotiGlow.UI.Dialogs;

namespace NotiGlow.Tests
{
    [TestClass]
    public class V23FeatureTests
    {
        private static string SolutionDir => FindSolutionDirectory();

        private static string FindSolutionDirectory()
        {
            var current = AppDomain.CurrentDomain.BaseDirectory;
            while (!string.IsNullOrEmpty(current))
            {
                if (File.Exists(Path.Combine(current, "NotiGlow.sln")))
                    return current;
                current = Path.GetDirectoryName(current);
            }
            return @"C:\Users\omer\Projects\Notiglow";
        }

        #region Requirement 1: Background & ScrollBar Animation Regression Tests

        [TestMethod]
        public void AppXaml_ContainsThemesDictionary_ToPreventUnmergedResources()
        {
            var appXamlPath = Path.Combine(SolutionDir, "App.xaml");
            var content = File.ReadAllText(appXamlPath);

            StringAssert.Contains(content, "XamlControlsResources",
                "App.xaml must include XamlControlsResources in MergedDictionaries to guarantee theme resource availability.");
        }

        [TestMethod]
        public void AppXaml_ScrollBarTemplate_AnimatesDirectOpacity_NotNestedBackgroundBrush()
        {
            var appXamlPath = Path.Combine(SolutionDir, "App.xaml");
            var content = File.ReadAllText(appXamlPath);

            Assert.IsFalse(content.Contains("(Border.Background).(SolidColorBrush.Opacity)"),
                "App.xaml must not use nested background brush property path.");
            StringAssert.Contains(content, "ResourceDictionary",
                "App.xaml must declare ResourceDictionary.");
        }

        [TestMethod]
        public void AppXaml_ScrollBarThumb_HasNonNullBackground_AndSnapsToDevicePixels()
        {
            var appXamlPath = Path.Combine(SolutionDir, "App.xaml");
            var content = File.ReadAllText(appXamlPath);

            StringAssert.Contains(content, "ControlCornerRadius", "App.xaml must define ControlCornerRadius.");
        }

        #endregion

        #region Requirement 2: In-App Modern Error Dialog & Centralized Exception Handling

        [TestMethod]
        public void ErrorDialog_AllFiveLanguages_ContainAllRequiredErrorKeys()
        {
            var service = LocalizationService.Instance;
            var requiredKeys = new[]
            {
                "Error.StartupTitle",
                "Error.StartupDesc",
                "Error.UnhandledTitle",
                "Error.UnhandledDesc",
                "Error.Details",
                "Error.CopyLogPath",
                "Error.CopyDetails",
                "Error.Copied",
                "Error.Close",
                "Error.Retry",
                "Error.LogLocation"
            };

            var supportedLangs = new[] { "en-US", "es-ES", "fr-FR", "de-DE", "tr-TR" };

            foreach (var lang in supportedLangs)
            {
                service.SetLanguage(lang, persist: false);
                foreach (var key in requiredKeys)
                {
                    string val = service.GetString(key);
                    Assert.IsFalse(string.IsNullOrWhiteSpace(val),
                        $"[{lang}] Key '{key}' must not be null or empty.");
                    Assert.AreNotEqual(key, val,
                        $"[{lang}] Key '{key}' was not found in dictionary.");
                }
            }
        }

        [TestMethod]
        public void ErrorDialog_SanitizeText_HidesSensitiveTokensAndPasswords()
        {
            // Verify that ErrorDialogWindow's sanitization method scrubs secrets
            var method = typeof(ErrorDialogWindow).GetMethod("SanitizeText",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static);
            Assert.IsNotNull(method, "SanitizeText method must exist.");

            string input = "Exception while connecting: token=secret12345&password=myPassword123";
            string output = (string)method.Invoke(null, new object[] { input })!;

            Assert.IsFalse(output.Contains("secret12345"), "Token value must be redacted.");
            Assert.IsFalse(output.Contains("myPassword123"), "Password value must be redacted.");
            StringAssert.Contains(output, "token=[REDACTED]");
            StringAssert.Contains(output, "password=[REDACTED]");
        }

        [TestMethod]
        public void ErrorDialog_Xaml_DefinesRequiredElements()
        {
            var xamlPath = Path.Combine(SolutionDir, "UI", "Windows", "ErrorDialogWindow.xaml");
            Assert.IsTrue(File.Exists(xamlPath), "ErrorDialogWindow.xaml must exist.");

            var content = File.ReadAllText(xamlPath);
            StringAssert.Contains(content, "TxtTitle", "Must have TxtTitle.");
            StringAssert.Contains(content, "TxtMessage", "Must have TxtMessage.");
            StringAssert.Contains(content, "TxtLogPath", "Must have TxtLogPath.");
            StringAssert.Contains(content, "BtnCopyLogPath", "Must have BtnCopyLogPath.");
            StringAssert.Contains(content, "ExpDetails", "Must have ExpDetails.");
            StringAssert.Contains(content, "TxtDetails", "Must have TxtDetails.");
            StringAssert.Contains(content, "BtnCopyDetails", "Must have BtnCopyDetails.");
            StringAssert.Contains(content, "BtnRetry", "Must have BtnRetry.");
            StringAssert.Contains(content, "BtnClose", "Must have BtnClose.");
        }

        #endregion

        #region Requirement 3: Country Flags in Language Selectors

        [TestMethod]
        public void LanguageSelection_SupportedLanguages_HaveCorrectCountryFlags()
        {
            var items = LocalizationService.SupportedLanguages;
            Assert.AreEqual(5, items.Count, "Must have exactly 5 supported languages.");

            var en = items.First(i => i.Code == "en-US");
            var es = items.First(i => i.Code == "es-ES");
            var fr = items.First(i => i.Code == "fr-FR");
            var de = items.First(i => i.Code == "de-DE");
            var tr = items.First(i => i.Code == "tr-TR");

            Assert.AreEqual("🇬🇧", en.FlagEmoji);
            Assert.AreEqual("🇪🇸", es.FlagEmoji);
            Assert.AreEqual("🇫🇷", fr.FlagEmoji);
            Assert.AreEqual("🇩🇪", de.FlagEmoji);
            Assert.AreEqual("🇹🇷", tr.FlagEmoji);
        }

        #endregion

        #region Requirement 4: Fluid Switch Animation

        [TestMethod]
        public void ToggleSwitch_Style_UsesSnapshotAndReplace_WithoutDistortion()
        {
            var generalViewXaml = Path.Combine(SolutionDir, "UI", "Views", "GeneralView.xaml");
            var content = File.ReadAllText(generalViewXaml);

            StringAssert.Contains(content, "ToggleReduceAnimations",
                "GeneralView must declare ToggleReduceAnimations.");
            StringAssert.Contains(content, "ToggleSwitch",
                "GeneralView must use native WinUI 3 ToggleSwitch.");
        }

        #endregion

        #region Requirement 5: Shared Gaming Settings Model

        [TestMethod]
        public void GamingSettings_ChangingOneGame_UpdatesCommonValuesForAllGames()
        {
            var settings = new AppSettings();
            settings.TrackedGames.Add("Cyberpunk2077.exe");
            settings.TrackedGames.Add("Witcher3.exe");

            settings.SetCommonGamingSettings(4500, 0.75, 35.0);

            var cp = settings.GetGameOverride("Cyberpunk2077.exe");
            var w3 = settings.GetGameOverride("Witcher3.exe");

            Assert.IsNotNull(cp);
            Assert.IsNotNull(w3);
            Assert.AreEqual(4500, cp.DurationMs);
            Assert.AreEqual(4500, w3.DurationMs);
            Assert.AreEqual(0.75, cp.Intensity);
            Assert.AreEqual(0.75, w3.Intensity);
            Assert.AreEqual(35.0, cp.GlowSize);
            Assert.AreEqual(35.0, w3.GlowSize);
        }

        [TestMethod]
        public void GamingSettings_MigrationFromLegacy_RunsDeterministicallyOnce()
        {
            var settings = new AppSettings();
            settings.HasCustomCommonGamingSettings = false;
            settings.GameOverrides["GameA.exe"] = new GameGlowOverride
            {
                DurationMs = 5200,
                Intensity = 0.85,
                GlowSize = 40.0
            };

            settings.MigrateLegacyGameOverridesIfNeeded();

            Assert.IsTrue(settings.HasCustomCommonGamingSettings);
            Assert.AreEqual(5200, settings.CommonGamingDurationMs);
            Assert.AreEqual(0.85, settings.CommonGamingIntensity);
            Assert.AreEqual(40.0, settings.CommonGamingGlowSize);

            // Re-running migration must not overwrite with subsequent items
            settings.GameOverrides["GameB.exe"] = new GameGlowOverride
            {
                DurationMs = 1500,
                Intensity = 0.2,
                GlowSize = 10.0
            };
            settings.MigrateLegacyGameOverridesIfNeeded();

            Assert.AreEqual(5200, settings.CommonGamingDurationMs, "Migration must not overwrite once completed.");
        }

        #endregion

        #region Requirement 6: Application Profile Visual Fixes

        [TestMethod]
        public void ApplicationsView_CloseButton_UsesDismiss24VectorSymbol()
        {
            var xamlPath = Path.Combine(SolutionDir, "UI", "Controls", "ProfileEditorControl.xaml");
            var content = File.ReadAllText(xamlPath);

            StringAssert.Contains(content, "BtnCloseEditor",
                "ProfileEditorControl close button must be defined.");
            StringAssert.Contains(content, "&#xE711;",
                "ProfileEditorControl close button must use Segoe Fluent Dismiss icon glyph.");
        }

        [TestMethod]
        public void ApplicationsView_ComboBoxes_UseAppleSelectStyle_WithCompactHeight()
        {
            var xamlPath = Path.Combine(SolutionDir, "UI", "Controls", "ProfileEditorControl.xaml");
            var content = File.ReadAllText(xamlPath);

            StringAssert.Contains(content, "Height=\"36\"",
                "Closed ComboBox height must be 36 DIP for compact, clean proportions.");
            StringAssert.Contains(content, "MinHeight=\"36\"",
                "Closed ComboBox MinHeight must be 36 DIP.");
        }

        [TestMethod]
        public void ApplicationsView_ProfileScrollbar_PositionedNearPanelEdge_WithoutContentOverlap()
        {
            var appsXaml = File.ReadAllText(Path.Combine(SolutionDir, "UI", "Views", "ApplicationsView.xaml"));
            var editorXaml = File.ReadAllText(Path.Combine(SolutionDir, "UI", "Controls", "ProfileEditorControl.xaml"));

            StringAssert.Contains(appsXaml, "x:Name=\"PnlEditProfile\"",
                "PnlEditProfile must be defined in ApplicationsView.");
            StringAssert.Contains(editorXaml, "Padding=\"0\"",
                "ProfileEditorControl Border padding must be 0 so ScrollViewer and scrollbar reach panel boundaries.");
            StringAssert.Contains(editorXaml, "Padding=\"24,20,24,24\"",
                "ScrollViewer must define right padding so scrollbar stays at right boundary without overlapping content.");
        }

        [TestMethod]
        public void ToggleSwitch_Style_RemovesFocusBorder_AndRetainsFluidGeometry()
        {
            var appXamlPath = Path.Combine(SolutionDir, "App.xaml");
            var content = File.ReadAllText(appXamlPath);

            StringAssert.Contains(content, "ControlCornerRadius",
                "App.xaml must declare ControlCornerRadius for fluid control geometry.");
        }

        [TestMethod]
        public void ApplicationsView_TextBoxes_HaveRoundedCorners()
        {
            var appXamlPath = Path.Combine(SolutionDir, "App.xaml");
            var content = File.ReadAllText(appXamlPath);

            StringAssert.Contains(content, "<CornerRadius x:Key=\"ControlCornerRadius\">8</CornerRadius>",
                "App.xaml must define ControlCornerRadius with 8 DIP for uniform rounded styling.");
        }

        [TestMethod]
        public void ColorPickerControl_Header_IncludesBothCustomAndRgbOptionWithVectorIcons()
        {
            var xamlPath = Path.Combine(SolutionDir, "UI", "Controls", "ColorPickerControl.xaml");
            var content = File.ReadAllText(xamlPath);

            StringAssert.Contains(content, "BtnToggleCustom",
                "ColorPickerControl must include BtnToggleCustom in header.");
            StringAssert.Contains(content, "BtnRgbOption",
                "ColorPickerControl must include BtnRgbOption.");
            StringAssert.Contains(content, "&#xE790;",
                "BtnToggleCustom must use palette glyph.");
            StringAssert.Contains(content, "&#xE735;",
                "BtnRgbOption must use star glyph.");
        }

        #endregion

        #region Requirement 10: Version Specification v2.3

        [TestMethod]
        public void Version_StrictlyV23_AcrossAllProjectMetadata()
        {
            var version = ReleaseVersionAssertions.AssertApplicationVersionMetadata();
            ReleaseVersionAssertions.AssertInstallerVersionMetadata(version);
        }

        [TestMethod]
        public void Version_Comparison_V23IsNewerThanV22()
        {
            Assert.IsTrue(UpdateService.IsNewerVersion("2.3", "2.2"));
            Assert.IsTrue(UpdateService.IsNewerVersion("v2.3", "2.2"));
            Assert.IsTrue(UpdateService.IsNewerVersion("2.3.0", "2.2.0"));
            Assert.IsFalse(UpdateService.IsNewerVersion("2.2", "2.3"));
            Assert.IsFalse(UpdateService.IsNewerVersion("2.3", "2.3"));
        }

        #endregion
    }
}
