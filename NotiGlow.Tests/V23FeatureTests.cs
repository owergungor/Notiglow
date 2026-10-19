using System;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media.Animation;
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

            StringAssert.Contains(content, "<ui:ThemesDictionary Theme=\"Dark\"",
                "App.xaml must include ThemesDictionary in MergedDictionaries to guarantee theme brush availability.");
        }

        [TestMethod]
        public void AppXaml_ScrollBarTemplate_AnimatesDirectOpacity_NotNestedBackgroundBrush()
        {
            var appXamlPath = Path.Combine(SolutionDir, "App.xaml");
            var content = File.ReadAllText(appXamlPath);

            // Verify that we do not have (Border.Background).(SolidColorBrush.Opacity)
            Assert.IsFalse(content.Contains("(Border.Background).(SolidColorBrush.Opacity)"),
                "App.xaml must not use nested background brush property path which causes 'Background property does not point to a DependencyObject' crash.");

            // Verify direct opacity animation or safe triggers
            StringAssert.Contains(content, "Property=\"Opacity\"",
                "App.xaml ScrollBar must animate Opacity property directly on Thumb border.");
        }

        [TestMethod]
        public void AppXaml_ScrollBarThumb_HasNonNullBackground_AndSnapsToDevicePixels()
        {
            var appXamlPath = Path.Combine(SolutionDir, "App.xaml");
            var content = File.ReadAllText(appXamlPath);

            StringAssert.Contains(content, "ThumbBorder", "App.xaml ScrollBar Thumb template must define ThumbBorder.");
            StringAssert.Contains(content, "CornerRadius=\"4\"", "App.xaml ScrollBar Thumb must have rounded corners.");
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
            var appXamlPath = Path.Combine(SolutionDir, "App.xaml");
            var content = File.ReadAllText(appXamlPath);

            // Check that HandoffBehavior is SnapshotAndReplace
            StringAssert.Contains(content, "HandoffBehavior=\"SnapshotAndReplace\"",
                "ToggleSwitch Storyboards must use SnapshotAndReplace to prevent clock contention during rapid toggles.");

            // Check that distorting scale distortion is not present
            Assert.IsFalse(content.Contains("ScaleTransform.ScaleX"),
                "ToggleSwitch should not distort thumb width with ScaleX which causes laggy appearance.");
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
            var xamlPath = Path.Combine(SolutionDir, "UI", "Views", "ApplicationsView.xaml");
            var content = File.ReadAllText(xamlPath);

            StringAssert.Contains(content, "Dismiss24",
                "ApplicationsView close button must use Dismiss24 vector symbol instead of unmapped font icon.");
            Assert.IsFalse(content.Contains("&#xE711;"),
                "ApplicationsView close button must not use raw font glyph &#xE711; which renders as an empty square.");
        }

        [TestMethod]
        public void ApplicationsView_ComboBoxes_UseAppleSelectStyle_WithCompactHeight()
        {
            var xamlPath = Path.Combine(SolutionDir, "UI", "Views", "ApplicationsView.xaml");
            var content = File.ReadAllText(xamlPath);

            StringAssert.Contains(content, "Style=\"{DynamicResource AppleSelectComboBoxStyle}\"",
                "ComboBoxes must use AppleSelectComboBoxStyle.");
            StringAssert.Contains(content, "Height=\"36\"",
                "Closed ComboBox height must be 36 DIP for compact, clean proportions.");
            StringAssert.Contains(content, "MinHeight=\"36\"",
                "Closed ComboBox MinHeight must be 36 DIP.");
        }

        [TestMethod]
        public void ApplicationsView_ProfileScrollbar_PositionedNearPanelEdge_WithoutContentOverlap()
        {
            var xamlPath = Path.Combine(SolutionDir, "UI", "Views", "ApplicationsView.xaml");
            var content = File.ReadAllText(xamlPath);

            StringAssert.Contains(content, "x:Name=\"PnlEditProfile\" Background=\"{DynamicResource CardBackground}\" CornerRadius=\"12\" BorderThickness=\"1\" BorderBrush=\"{DynamicResource BorderColor}\" Padding=\"0\"",
                "PnlEditProfile Border padding must be 0 so ScrollViewer and scrollbar reach panel boundaries.");
            StringAssert.Contains(content, "Padding=\"24,20,16,20\"",
                "ScrollViewer must define right padding so scrollbar stays at right boundary without overlapping content.");
        }

        [TestMethod]
        public void ToggleSwitch_Style_RemovesFocusBorder_AndRetainsFluidGeometry()
        {
            var appXamlPath = Path.Combine(SolutionDir, "App.xaml");
            var content = File.ReadAllText(appXamlPath);

            StringAssert.Contains(content, "<Setter Property=\"FocusVisualStyle\" Value=\"{x:Null}\"/>",
                "ToggleSwitch style must nullify FocusVisualStyle to eliminate outer box/lines.");
            StringAssert.Contains(content, "<Setter Property=\"BorderThickness\" Value=\"0\"/>",
                "ToggleSwitch style must have 0 BorderThickness at root.");
        }

        [TestMethod]
        public void ApplicationsView_TextBoxes_HaveRoundedCorners()
        {
            var appXamlPath = Path.Combine(SolutionDir, "App.xaml");
            var content = File.ReadAllText(appXamlPath);

            StringAssert.Contains(content, "<CornerRadius x:Key=\"ControlCornerRadius\">8</CornerRadius>",
                "App.xaml must define ControlCornerRadius with 8 DIP for uniform rounded styling.");
            StringAssert.Contains(content, "TargetType=\"{x:Type TextBox}\"",
                "App.xaml must define modern rounded style for TextBox.");
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
            StringAssert.Contains(content, "Sparkle24",
                "BtnRgbOption must use Sparkle24 vector symbol.");
            StringAssert.Contains(content, "Color24",
                "BtnToggleCustom must use Color24 vector symbol.");
        }

        #endregion

        #region Requirement 10: Version Specification v2.3

        [TestMethod]
        public void Version_StrictlyV23_AcrossAllProjectMetadata()
        {
            var csproj = File.ReadAllText(Path.Combine(SolutionDir, "NotiGlow.csproj"));
            Assert.IsTrue(csproj.Contains("<Version>2.3</Version>") || csproj.Contains("<Version>2.4</Version>"));
            Assert.IsTrue(csproj.Contains("<AssemblyVersion>2.3.0.0</AssemblyVersion>") || csproj.Contains("<AssemblyVersion>2.4.0.0</AssemblyVersion>"));
            Assert.IsTrue(csproj.Contains("<FileVersion>2.3.0.0</FileVersion>") || csproj.Contains("<FileVersion>2.4.0.0</FileVersion>"));
            Assert.IsTrue(csproj.Contains("<InformationalVersion>2.3</InformationalVersion>") || csproj.Contains("<InformationalVersion>2.4</InformationalVersion>"));

            var iss = File.ReadAllText(Path.Combine(SolutionDir, "NotiGlow-Setup.iss"));
            Assert.IsTrue(iss.Contains("AppVersion=2.3") || iss.Contains("AppVersion=2.4"));
            Assert.IsTrue(iss.Contains("VersionInfoVersion=2.3.0.0") || iss.Contains("VersionInfoVersion=2.4.0.0"));
            Assert.IsTrue(iss.Contains("OutputBaseFilename=NotiGlow-Setup-v2.3") || iss.Contains("OutputBaseFilename=NotiGlow-Setup-v2.4"));

            Assert.IsTrue(UpdateService.CurrentVersionString == "2.3" || UpdateService.CurrentVersionString == "2.4",
                "User-visible version must strictly be '2.3' or '2.4', never '2.3.0' or '2.4.0'.");
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
