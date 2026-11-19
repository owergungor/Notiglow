using System;
using System.IO;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NotiGlow.Models;
using NotiGlow.Services;

namespace NotiGlow.Tests
{
    [TestClass]
    public class V40WindowsSettingsRepairTests
    {
        private static string SolutionDir => TestPathHelper.SolutionDir;

        [TestMethod]
        public void Test_AllViews_HaveScrollViewers_WithInertiaAndScrollModeEnabled()
        {
            var views = new[]
            {
                "UI/Views/GeneralView.xaml",
                "UI/Views/AppearanceView.xaml",
                "UI/Views/DisplayView.xaml",
                "UI/Views/GamingView.xaml",
                "UI/Views/NotificationsView.xaml",
                "UI/Views/AdvancedView.xaml"
            };

            foreach (var viewRel in views)
            {
                string path = Path.Combine(SolutionDir, viewRel);
                Assert.IsTrue(File.Exists(path), $"File must exist: {viewRel}");
                string content = File.ReadAllText(path);

                StringAssert.Contains(content, "VerticalScrollBarVisibility=\"Auto\"", $"View {viewRel} must have auto vertical scrollbar.");
                StringAssert.Contains(content, "IsScrollInertiaEnabled=\"True\"", $"View {viewRel} must have scroll inertia enabled.");
                StringAssert.Contains(content, "ScrollViewer.VerticalScrollMode=\"Enabled\"", $"View {viewRel} must have vertical scroll mode enabled.");
            }
        }

        [TestMethod]
        public void Test_NestedGridViews_HaveInternalScrollingDisabled()
        {
            // ColorPickerControl PresetPalette
            string colorPickerPath = Path.Combine(SolutionDir, "UI/Controls/ColorPickerControl.xaml");
            Assert.IsTrue(File.Exists(colorPickerPath));
            string colorPickerContent = File.ReadAllText(colorPickerPath);
            StringAssert.Contains(colorPickerContent, "ScrollViewer.VerticalScrollMode=\"Disabled\"", "PresetPalette must disable vertical scroll mode to prevent swallowing mouse wheel events.");
            StringAssert.Contains(colorPickerContent, "ScrollViewer.HorizontalScrollMode=\"Disabled\"", "PresetPalette must disable horizontal scroll mode.");

            // ApplicationsView is List-Only
            string appsPath = Path.Combine(SolutionDir, "UI/Views/ApplicationsView.xaml");
            Assert.IsTrue(File.Exists(appsPath));
            string appsContent = File.ReadAllText(appsPath);
            StringAssert.Contains(appsContent, "ListScrollViewer", "ApplicationsView must use ListScrollViewer.");
            Assert.IsFalse(appsContent.Contains("PnlAppGridCards"), "ApplicationsView must not contain GridView cards.");
        }

        [TestMethod]
        public void Test_WindowsSettings_GroupContainersAndDividers_Configured()
        {
            var views = new[]
            {
                "UI/Views/GeneralView.xaml",
                "UI/Views/AppearanceView.xaml",
                "UI/Views/DisplayView.xaml",
                "UI/Views/GamingView.xaml",
                "UI/Views/NotificationsView.xaml",
                "UI/Views/AdvancedView.xaml"
            };

            foreach (var viewRel in views)
            {
                string path = Path.Combine(SolutionDir, viewRel);
                string content = File.ReadAllText(path);

                StringAssert.Contains(content, "CornerRadius=\"8\"", $"View {viewRel} must use Windows 11 8px corner radius cards.");
                StringAssert.Contains(content, "BorderThickness=\"1\"", $"View {viewRel} must use subtle 1px border stroke.");
                StringAssert.Contains(content, "Fill=\"{ThemeResource DividerColor}\"", $"View {viewRel} must use subtle divider lines between grouped rows.");
            }
        }

        [TestMethod]
        public void Test_ThemeService_NavigationViewBrushes_NotTransparent()
        {
            string themeServicePath = Path.Combine(SolutionDir, "Services/ThemeService.cs");
            Assert.IsTrue(File.Exists(themeServicePath));
            string content = File.ReadAllText(themeServicePath);

            // Verify selection indicator and item background are NOT set to #00000000
            Assert.IsFalse(content.Contains("SetBrush(res, \"NavigationViewSelectionIndicatorForeground\", \"#00000000\");"),
                "NavigationViewSelectionIndicatorForeground must not be set to transparent #00000000.");
            Assert.IsFalse(content.Contains("SetBrush(res, \"NavigationViewItemBackgroundSelected\", \"#00000000\");"),
                "NavigationViewItemBackgroundSelected must not be set to transparent #00000000.");
            StringAssert.Contains(content, "SetBrush(res, \"NavigationViewSelectionIndicatorForeground\", palette.NavIndicatorColor);");
            StringAssert.Contains(content, "SetBrush(res, \"NavigationViewItemBackgroundSelected\", palette.NavActiveBackground);");
        }

        [TestMethod]
        public void Test_ThemeService_RaisesThemeChangedEvent()
        {
            bool raised = false;
            EventHandler handler = (s, e) => { raised = true; };
            ThemeService.ThemeChanged += handler;
            try
            {
                // Force a theme application
                ThemeService.ApplyTheme(ColorTheme.Amber, ThemeMode.Dark);
                Assert.IsTrue(raised, "ThemeService.ThemeChanged must be raised on ApplyTheme.");
            }
            finally
            {
                ThemeService.ThemeChanged -= handler;
                // Restore standard theme
                ThemeService.ApplyTheme(ColorTheme.Standard, ThemeMode.Dark);
            }
        }

        [TestMethod]
        public void Test_SectionTitles_ExistInAll5Languages()
        {
            var languages = new[] { "en-US", "es-ES", "fr-FR", "de-DE", "tr-TR" };
            var keys = new[]
            {
                "General.SystemSection",
                "General.PermissionsSection",
                "General.PersonalizationSection",
                "Appearance.AnimationSection",
                "Appearance.GeometrySection",
                "Appearance.ColorSection",
                "Display.ModeSection",
                "Gaming.ActivationSection",
                "Notifications.OverlapSection"
            };

            foreach (var lang in languages)
            {
                var dict = LocalizationService.Instance.GetRawDictionary(lang);
                foreach (var key in keys)
                {
                    Assert.IsTrue(dict.ContainsKey(key), $"Language '{lang}' must define key '{key}'.");
                    Assert.IsFalse(string.IsNullOrWhiteSpace(dict[key]), $"Language '{lang}' key '{key}' must not be empty.");
                }
            }
        }

        [TestMethod]
        public void Test_MainWindow_OpenPaneLength_IsSettingsWidth()
        {
            string mainWindowXamlPath = Path.Combine(SolutionDir, "UI/MainWindow.xaml");
            Assert.IsTrue(File.Exists(mainWindowXamlPath));
            string content = File.ReadAllText(mainWindowXamlPath);

            StringAssert.Contains(content, "OpenPaneLength=\"236\"", "MainWindow NavigationView must have OpenPaneLength=236 for reduced sidebar width.");
            StringAssert.Contains(content, "CompactPaneLength=\"52\"", "MainWindow NavigationView must maintain CompactPaneLength=52.");
        }

        [TestMethod]
        public void Test_WindowMaterial_Enum_And_JsonSerialization()
        {
            var settings = new AppSettings();
            Assert.AreEqual(WindowMaterial.Acrylic, settings.WindowMaterial, "Default WindowMaterial must be Acrylic.");

            string jsonAcrylic = System.Text.Json.JsonSerializer.Serialize(WindowMaterial.Acrylic);
            StringAssert.Contains(jsonAcrylic, "Acrylic");

            Assert.AreEqual(WindowMaterial.Mica, System.Text.Json.JsonSerializer.Deserialize<WindowMaterial>("\"Mica\""));
            Assert.AreEqual(WindowMaterial.MicaAlt, System.Text.Json.JsonSerializer.Deserialize<WindowMaterial>("\"MicaAlt\""));
            Assert.AreEqual(WindowMaterial.MicaAlt, System.Text.Json.JsonSerializer.Deserialize<WindowMaterial>("\"Mica Alt\""));
            // Legacy migration: Transparent migrates to default Acrylic
            Assert.AreEqual(WindowMaterial.Acrylic, System.Text.Json.JsonSerializer.Deserialize<WindowMaterial>("\"Transparent\""));
            Assert.AreEqual(WindowMaterial.Acrylic, System.Text.Json.JsonSerializer.Deserialize<WindowMaterial>("\"UnknownValue\""));
        }

        [TestMethod]
        public void Test_WindowMaterial_LocalizedStrings_ExistInAll5Languages()
        {
            var languages = new[] { "en-US", "es-ES", "fr-FR", "de-DE", "tr-TR" };
            var keys = new[]
            {
                "General.WindowMaterial",
                "General.WindowMaterialDesc",
                "Material.Acrylic",
                "Material.Mica",
                "Material.MicaAlt"
            };

            foreach (var lang in languages)
            {
                var dict = LocalizationService.Instance.GetRawDictionary(lang);
                foreach (var key in keys)
                {
                    Assert.IsTrue(dict.ContainsKey(key), $"Language '{lang}' must define key '{key}'.");
                    Assert.IsFalse(string.IsNullOrWhiteSpace(dict[key]), $"Language '{lang}' key '{key}' must not be empty.");
                }
            }
        }

        [TestMethod]
        public void Test_MainWindow_BackdropBackgrounds_AreTransparent()
        {
            string mainWindowXamlPath = Path.Combine(SolutionDir, "UI/MainWindow.xaml");
            Assert.IsTrue(File.Exists(mainWindowXamlPath));
            string content = File.ReadAllText(mainWindowXamlPath);

            Assert.IsTrue(content.Contains("<Grid Background=\"Transparent\""), "Root MainWindow Grid must have transparent background for backdrop materials.");
            StringAssert.Contains(content, "x:Name=\"RootContentGrid\" Background=\"Transparent\"", "RootContentGrid must have transparent background.");
        }

        [TestMethod]
        public void Test_OverlayWindow_HasTransparentBackdrop()
        {
            string overlayXamlPath = Path.Combine(SolutionDir, "Overlay/OverlayWindow.xaml");
            Assert.IsTrue(File.Exists(overlayXamlPath));
            string content = File.ReadAllText(overlayXamlPath);

            StringAssert.Contains(content, "TransparentBackdrop", "OverlayWindow must declare TransparentBackdrop to prevent black screen.");
        }

        [TestMethod]
        public void Test_All12ColorThemes_HaveValidPalettes_LightAndDark()
        {
            foreach (var theme in ThemeService.CanonicalThemes)
            {
                var darkPalette = ThemeService.GetPalette(theme, isDark: true);
                Assert.IsNotNull(darkPalette, $"Dark palette for {theme} must not be null.");
                Assert.IsFalse(string.IsNullOrWhiteSpace(darkPalette.AccentColor), $"Dark accent color for {theme} must not be empty.");
                Assert.IsFalse(string.IsNullOrWhiteSpace(darkPalette.CardBackground), $"Dark card background for {theme} must not be empty.");

                var lightPalette = ThemeService.GetPalette(theme, isDark: false);
                Assert.IsNotNull(lightPalette, $"Light palette for {theme} must not be null.");
                Assert.IsFalse(string.IsNullOrWhiteSpace(lightPalette.AccentColor), $"Light accent color for {theme} must not be empty.");
                Assert.IsFalse(string.IsNullOrWhiteSpace(lightPalette.CardBackground), $"Light card background for {theme} must not be empty.");
            }
        }

        [TestMethod]
        public void Test_All12ColorThemes_HaveNeutralSurfaces_CardsAndBordersAndDividers()
        {
            foreach (var theme in ThemeService.CanonicalThemes)
            {
                var dark = ThemeService.GetPalette(theme, isDark: true);
                Assert.AreEqual("#2B2B2B", dark.CardBackground, $"{theme} Dark CardBackground must be neutral #2B2B2B.");
                Assert.AreEqual("#3D3D3D", dark.BorderColor, $"{theme} Dark BorderColor must be neutral #3D3D3D.");
                Assert.AreEqual("#333333", dark.DividerColor, $"{theme} Dark DividerColor must be neutral #333333.");

                var light = ThemeService.GetPalette(theme, isDark: false);
                Assert.AreEqual("#FFFFFF", light.CardBackground, $"{theme} Light CardBackground must be neutral #FFFFFF.");
                Assert.AreEqual("#E5E5E5", light.BorderColor, $"{theme} Light BorderColor must be neutral #E5E5E5.");
                Assert.AreEqual("#EBEBEB", light.DividerColor, $"{theme} Light DividerColor must be neutral #EBEBEB.");
            }
        }

        [TestMethod]
        public void Test_BubblegumTheme_ControlsAccentOnly_DoesNotTintCardsOrBorders()
        {
            var dark = ThemeService.GetPalette(ColorTheme.Bubblegum, isDark: true);
            Assert.AreEqual("#EC4899", dark.AccentColor, "Bubblegum dark accent must be #EC4899.");
            Assert.AreEqual("#2B2B2B", dark.CardBackground, "Bubblegum dark card must be neutral #2B2B2B, not pink.");
            Assert.AreEqual("#3D3D3D", dark.BorderColor, "Bubblegum dark border must be neutral #3D3D3D, not pink.");

            var light = ThemeService.GetPalette(ColorTheme.Bubblegum, isDark: false);
            Assert.AreEqual("#DB2777", light.AccentColor, "Bubblegum light accent must be #DB2777.");
            Assert.AreEqual("#FFFFFF", light.CardBackground, "Bubblegum light card must be neutral #FFFFFF, not pink.");
            Assert.AreEqual("#E5E5E5", light.BorderColor, "Bubblegum light border must be neutral #E5E5E5, not pink.");
        }

        [TestMethod]
        public void Test_AppManifest_DeclaresPerMonitorV2_DpiAwareness()
        {
            string manifestPath = Path.Combine(SolutionDir, "app.manifest");
            Assert.IsTrue(File.Exists(manifestPath), "app.manifest must exist.");
            string content = File.ReadAllText(manifestPath);

            StringAssert.Contains(content, "PerMonitorV2, PerMonitor", "app.manifest must specify PerMonitorV2 DPI awareness.");
            StringAssert.Contains(content, "{8e0f7a12-bfb3-4fe8-b9a5-48fd50a15a9a}", "app.manifest must declare Windows 10/11 compatibility.");
        }

        [TestMethod]
        public void Test_AllViews_DeclareTransparentBackground_AndScrollViewerHelper()
        {
            string[] views = new[]
            {
                "GeneralView",
                "AppearanceView",
                "ApplicationsView",
                "DisplayView",
                "GamingView",
                "NotificationsView",
                "AdvancedView"
            };

            foreach (var view in views)
            {
                string xamlPath = Path.Combine(SolutionDir, "UI", "Views", $"{view}.xaml");
                string csPath = Path.Combine(SolutionDir, "UI", "Views", $"{view}.xaml.cs");

                Assert.IsTrue(File.Exists(xamlPath), $"{view}.xaml must exist.");
                Assert.IsTrue(File.Exists(csPath), $"{view}.xaml.cs must exist.");

                string xaml = File.ReadAllText(xamlPath);
                string cs = File.ReadAllText(csPath);

                StringAssert.Contains(xaml, "Background=\"Transparent\"", $"{view}.xaml must set Background=\"Transparent\".");
                StringAssert.Contains(cs, "ScrollViewerHelper.EnableReliableMouseWheel", $"{view}.xaml.cs must invoke ScrollViewerHelper.EnableReliableMouseWheel.");
            }
        }

        [TestMethod]
        public void Test_AppSettings_WindowGeometry_DefaultAndPersistence()
        {
            var settings = new AppSettings();
            Assert.IsNull(settings.WindowWidth, "Default WindowWidth should be null (indicating default ~1400x900 should apply).");
            Assert.IsNull(settings.WindowHeight, "Default WindowHeight should be null.");
            Assert.IsNull(settings.WindowX, "Default WindowX should be null.");
            Assert.IsNull(settings.WindowY, "Default WindowY should be null.");
            Assert.IsFalse(settings.WindowMaximized, "Default WindowMaximized should be false.");

            settings.WindowWidth = 1400;
            settings.WindowHeight = 900;
            settings.WindowX = 120;
            settings.WindowY = 80;
            settings.WindowMaximized = true;

            string json = System.Text.Json.JsonSerializer.Serialize(settings);
            var deserialized = System.Text.Json.JsonSerializer.Deserialize<AppSettings>(json);

            Assert.IsNotNull(deserialized);
            Assert.AreEqual(1400, deserialized.WindowWidth);
            Assert.AreEqual(900, deserialized.WindowHeight);
            Assert.AreEqual(120, deserialized.WindowX);
            Assert.AreEqual(80, deserialized.WindowY);
            Assert.IsTrue(deserialized.WindowMaximized);
        }

        [TestMethod]
        public void Test_ToggleSwitch_GlobalStyle_And_AppXaml_Resources()
        {
            string appXamlPath = Path.Combine(SolutionDir, "App.xaml");
            Assert.IsTrue(File.Exists(appXamlPath));
            string appXaml = File.ReadAllText(appXamlPath);

            // Verify global ToggleSwitch style has empty On/Off content and right alignment
            StringAssert.Contains(appXaml, "TargetType=\"ToggleSwitch\"");
            StringAssert.Contains(appXaml, "<Setter Property=\"OnContent\" Value=\"\" />");
            StringAssert.Contains(appXaml, "<Setter Property=\"OffContent\" Value=\"\" />");
            StringAssert.Contains(appXaml, "<Setter Property=\"HorizontalAlignment\" Value=\"Right\" />");

            // Verify Task Manager-style navigation selection indicator and corner radius resources
            StringAssert.Contains(appXaml, "x:Key=\"NavigationViewItemCornerRadius\"");
            StringAssert.Contains(appXaml, "x:Key=\"NavigationViewSelectionIndicatorWidth\"");
            StringAssert.Contains(appXaml, "x:Key=\"NavigationViewSelectionIndicatorHeight\"");
        }

        [TestMethod]
        public void Test_ApplicationsView_Header_HasResponsiveStructure()
        {
            string xamlPath = Path.Combine(SolutionDir, "UI", "Views", "ApplicationsView.xaml");
            string csPath = Path.Combine(SolutionDir, "UI", "Views", "ApplicationsView.xaml.cs");
            Assert.IsTrue(File.Exists(xamlPath));
            Assert.IsTrue(File.Exists(csPath));

            string xaml = File.ReadAllText(xamlPath);
            string cs = File.ReadAllText(csPath);

            StringAssert.Contains(xaml, "x:Name=\"HeaderGrid\"", "ApplicationsView must have HeaderGrid for responsive layout.");
            StringAssert.Contains(xaml, "x:Name=\"HeaderTitlePanel\"", "ApplicationsView must separate title into HeaderTitlePanel.");
            StringAssert.Contains(xaml, "x:Name=\"HeaderToolbarPanel\"", "ApplicationsView must separate controls into HeaderToolbarPanel.");
            StringAssert.Contains(xaml, "SizeChanged=\"HeaderGrid_SizeChanged\"", "ApplicationsView must hook HeaderGrid_SizeChanged.");
            StringAssert.Contains(cs, "void HeaderGrid_SizeChanged", "ApplicationsView.xaml.cs must implement HeaderGrid_SizeChanged.");
            StringAssert.Contains(cs, "HeaderTitlePanel.Measure", "HeaderGrid_SizeChanged must measure title panel dynamically without hardcoded coordinates.");
            StringAssert.Contains(cs, "HeaderToolbarPanel.Measure", "HeaderGrid_SizeChanged must measure toolbar panel dynamically without hardcoded coordinates.");
        }

        [TestMethod]
        public void Test_MainWindow_DefaultPhysicalSize_Is1550x950()
        {
            string csPath = Path.Combine(SolutionDir, "UI", "MainWindow.xaml.cs");
            Assert.IsTrue(File.Exists(csPath));
            string cs = File.ReadAllText(csPath);

            StringAssert.Contains(cs, "targetPhysicalW = 1550", "MainWindow must use 1550 physical px default width.");
            StringAssert.Contains(cs, "targetPhysicalH = 950", "MainWindow must use 950 physical px default height.");
            StringAssert.Contains(cs, "_isGeometryInitialized", "MainWindow must protect geometry initialization from early saves.");
        }

        [TestMethod]
        public void Test_DefaultColorTheme_IsWindowsSystemAccent()
        {
            var settings = new AppSettings();
            Assert.AreEqual(ColorTheme.Windows, settings.ColorTheme, "Default ColorTheme must be Windows.");
            Assert.AreEqual(ColorTheme.System, settings.ColorTheme, "Windows must equal System enum alias.");

            string json = System.Text.Json.JsonSerializer.Serialize(settings);
            var deserialized = System.Text.Json.JsonSerializer.Deserialize<AppSettings>(json);
            Assert.IsNotNull(deserialized);
            Assert.AreEqual(ColorTheme.Windows, deserialized.ColorTheme);
        }

        [TestMethod]
        public void Test_GeneralView_And_Views_HorizontalStretch_And_RightAlignedControls()
        {
            string generalXamlPath = Path.Combine(SolutionDir, "UI", "Views", "GeneralView.xaml");
            Assert.IsTrue(File.Exists(generalXamlPath));
            string generalXaml = File.ReadAllText(generalXamlPath);

            StringAssert.Contains(generalXaml, "HorizontalContentAlignment=\"Stretch\"", "GeneralView ScrollViewer must stretch content horizontally.");
            StringAssert.Contains(generalXaml, "x:Name=\"MasterToggle\"", "MasterToggle must exist in GeneralView.");
            StringAssert.Contains(generalXaml, "Grid.Column=\"1\" x:Name=\"MasterToggle\"", "MasterToggle must be in Column 1 (far right column).");
            StringAssert.Contains(generalXaml, "Grid.Column=\"1\" x:Name=\"ToggleStartWithWindows\"", "ToggleStartWithWindows must be in Column 1.");
            StringAssert.Contains(generalXaml, "Grid.Column=\"1\" x:Name=\"ToggleShowInTaskbar\"", "ToggleShowInTaskbar must be in Column 1.");
            StringAssert.Contains(generalXaml, "Grid.Column=\"1\" x:Name=\"ToggleReduceAnimations\"", "ToggleReduceAnimations must be in Column 1.");
        }

        [TestMethod]
        public void Test_SharedProfileEditorControl_UsedByBothApplicationsAndGaming()
        {
            string editorXaml = File.ReadAllText(Path.Combine(SolutionDir, "UI", "Controls", "ProfileEditorControl.xaml"));
            string appsXaml = File.ReadAllText(Path.Combine(SolutionDir, "UI", "Views", "ApplicationsView.xaml"));
            string gamingXaml = File.ReadAllText(Path.Combine(SolutionDir, "UI", "Views", "GamingView.xaml"));

            StringAssert.Contains(editorXaml, "x:Name=\"EditColorPicker\"");
            StringAssert.Contains(editorXaml, "x:Name=\"EditEdgePreview\"");
            StringAssert.Contains(editorXaml, "x:Name=\"CmbEditStyle\"");
            StringAssert.Contains(editorXaml, "x:Name=\"CmbEditCategory\"");
            StringAssert.Contains(editorXaml, "x:Name=\"ToggleCustomGameGlow\"");

            StringAssert.Contains(appsXaml, "<controls:ProfileEditorControl x:Name=\"PnlEditProfile\"");
            StringAssert.Contains(gamingXaml, "<controls:ProfileEditorControl x:Name=\"PnlEditGameOverride\"");
        }

        [TestMethod]
        public void Test_GamingView_UsesPencilAndTrashIcons_WithRedHoverDestructiveStyle()
        {
            string gamingCs = File.ReadAllText(Path.Combine(SolutionDir, "UI", "Views", "GamingView.xaml.cs"));
            string appXaml = File.ReadAllText(Path.Combine(SolutionDir, "App.xaml"));

            StringAssert.Contains(gamingCs, "Glyph = \"\\uE70F\"", "Gaming customize button must use Edit/Pencil glyph.");
            StringAssert.Contains(gamingCs, "Glyph = \"\\uE74D\"", "Gaming remove button must use Trash glyph.");
            StringAssert.Contains(gamingCs, "AutomationProperties.SetName(btnEdit, \"Customize\")");
            StringAssert.Contains(gamingCs, "AutomationProperties.SetName(btnRemove, \"Remove\")");
            StringAssert.Contains(gamingCs, "DestructiveIconButtonStyle");
            StringAssert.Contains(gamingCs, "DialogService.ShowDestructiveConfirmAsync");
            Assert.IsFalse(gamingCs.Contains("ConfirmationDialogWindow"), "GamingView must use native ContentDialog via DialogService, not a separate ConfirmationDialogWindow.");

            StringAssert.Contains(appXaml, "x:Key=\"DestructiveIconButtonStyle\"");
            StringAssert.Contains(appXaml, "x:Key=\"DestructiveDialogPrimaryButtonStyle\"");
            StringAssert.Contains(appXaml, "Value=\"#C42B1C\"", "Destructive button PointerOver must transition to critical red.");
        }

        [TestMethod]
        public void Test_DialogService_NativeModalContentDialog_AndSingleGameRemoval()
        {
            string dialogCs = File.ReadAllText(Path.Combine(SolutionDir, "Services", "DialogService.cs"));
            string appsCs = File.ReadAllText(Path.Combine(SolutionDir, "UI", "Views", "ApplicationsView.xaml.cs"));
            StringAssert.Contains(dialogCs, "new ContentDialog");
            StringAssert.Contains(dialogCs, "XamlRoot = xamlRoot");
            StringAssert.Contains(dialogCs, "DefaultButton = ContentDialogButton.Close");
            StringAssert.Contains(dialogCs, "SemaphoreSlim");
            StringAssert.Contains(dialogCs, "DestructiveDialogPrimaryButtonStyle");
            StringAssert.Contains(appsCs, "DialogService.ShowDestructiveConfirmAsync", "ApplicationsView profile deletion must use native ContentDialog via DialogService.");
            Assert.IsFalse(appsCs.Contains("ConfirmationDialogWindow"), "ApplicationsView must not use separate ConfirmationDialogWindow.");

            // Null XamlRoot safely returns false without crashing
            bool nullRootResult = DialogService.ShowDestructiveConfirmAsync(null, "Remove Game?", "Confirm").GetAwaiter().GetResult();
            Assert.IsFalse(nullRootResult);

            // Verify removing one game leaves other tracked games intact
            string tempDir = Path.Combine(Path.GetTempPath(), "NotiGlowRemoveTest_" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(tempDir);
            try
            {
                var settingsService = new SettingsService(Path.Combine(tempDir, "settings.json"));
                settingsService.Current.TrackedGames = new System.Collections.Generic.List<string>
                {
                    @"C:\Games\KeepMe.exe",
                    @"C:\Games\DeleteMe.exe"
                };
                settingsService.Current.GameOverrides[AppSettings.NormalizeGameIdentifier(@"C:\Games\KeepMe.exe")] = new GameGlowOverride { ColorHex = "#00FF00" };
                settingsService.Current.GameOverrides[AppSettings.NormalizeGameIdentifier(@"C:\Games\DeleteMe.exe")] = new GameGlowOverride { ColorHex = "#FF0000" };
                settingsService.Save(settingsService.Current);

                NotiGlow.Core.Helpers.AppCategoryHelper.RemoveGameCompletely(@"C:\Games\DeleteMe.exe", null, settingsService);

                Assert.AreEqual(1, settingsService.Current.TrackedGames.Count);
                Assert.AreEqual(@"C:\Games\KeepMe.exe", settingsService.Current.TrackedGames[0]);
                Assert.IsTrue(settingsService.Current.GameOverrides.ContainsKey(AppSettings.NormalizeGameIdentifier(@"C:\Games\KeepMe.exe")));
                Assert.IsFalse(settingsService.Current.GameOverrides.ContainsKey(AppSettings.NormalizeGameIdentifier(@"C:\Games\DeleteMe.exe")));
            }
            finally
            {
                try { Directory.Delete(tempDir, true); } catch { }
            }
        }

        [TestMethod]
        public void Test_ApplicationsView_HasNoGridViewControlsOrHandlers()
        {
            string appsXaml = File.ReadAllText(Path.Combine(SolutionDir, "UI", "Views", "ApplicationsView.xaml"));
            string appsCs = File.ReadAllText(Path.Combine(SolutionDir, "UI", "Views", "ApplicationsView.xaml.cs"));

            Assert.IsFalse(appsXaml.Contains("RbGridView"), "Grid RadioButton must be removed.");
            Assert.IsFalse(appsXaml.Contains("RbListView"), "List RadioButton must be removed.");
            Assert.IsFalse(appsXaml.Contains("GridScrollViewer"), "GridScrollViewer must be removed.");
            Assert.IsFalse(appsXaml.Contains("PnlAppGridCards"), "PnlAppGridCards must be removed.");
            Assert.IsFalse(appsCs.Contains("AppProfileGridCard"), "AppProfileGridCard must not be referenced.");
            Assert.IsFalse(appsCs.Contains("RbViewMode_Click"), "View mode switcher handler must be removed.");
        }

        [TestMethod]
        public void Test_CategoryRouting_MovesProfileBetweenGeneralAndGaming_PreservingSettingsAndUniqueness()
        {
            string tempDir = Path.Combine(Path.GetTempPath(), "NotiGlowCatTest_" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(tempDir);
            try
            {
                string settingsPath = Path.Combine(tempDir, "settings.json");
                var settingsService = new SettingsService(settingsPath);
                var profileService = new ProfileService();

                var app = new AppProfile
                {
                    AppId = "TestGameRoute.exe",
                    Name = "Test Game Route",
                    ExecutablePath = @"C:\Games\TestGameRoute.exe",
                    Category = "General",
                    ColorHex = "#FF00AA",
                    DurationMs = 6500,
                    Intensity = 0.92,
                    GlowSize = 48.0,
                    CornerRadius = 12.0,
                    Style = GlowStyle.Comet,
                    Priority = NotificationPriority.High,
                    Enabled = true,
                    IsCustom = true
                };

                // 1. Save as General App -> must NOT be in TrackedGames
                NotiGlow.Core.Helpers.AppCategoryHelper.ApplyCategorySync(app, profileService, settingsService);
                Assert.IsFalse(NotiGlow.Core.Helpers.AppCategoryHelper.IsGamingProfile(app, settingsService.Current.TrackedGames));
                Assert.IsFalse(settingsService.Current.TrackedGames.Contains(@"C:\Games\TestGameRoute.exe"));

                // 2. Move to Gaming -> must appear in TrackedGames & GameOverrides, no duplicates, settings preserved
                app.Category = "Gaming";
                NotiGlow.Core.Helpers.AppCategoryHelper.ApplyCategorySync(app, profileService, settingsService, customGlowEnabled: true);
                NotiGlow.Core.Helpers.AppCategoryHelper.ApplyCategorySync(app, profileService, settingsService, customGlowEnabled: true); // Idempotent

                Assert.IsTrue(NotiGlow.Core.Helpers.AppCategoryHelper.IsGamingProfile(app, settingsService.Current.TrackedGames));
                int countInTracked = 0;
                foreach (var g in settingsService.Current.TrackedGames)
                {
                    if (AppSettings.NormalizeGameIdentifier(g) == AppSettings.NormalizeGameIdentifier(@"C:\Games\TestGameRoute.exe"))
                        countInTracked++;
                }
                Assert.AreEqual(1, countInTracked, "Game must appear exactly once in TrackedGames without duplication.");

                string normKey = AppSettings.NormalizeGameIdentifier(@"C:\Games\TestGameRoute.exe");
                Assert.IsTrue(settingsService.Current.GameOverrides.TryGetValue(normKey, out var ov));
                Assert.IsNotNull(ov);
                Assert.AreEqual("#FF00AA", ov.ColorHex);
                Assert.AreEqual(6500, ov.DurationMs);
                Assert.AreEqual(0.92, ov.Intensity!.Value, 0.001);
                Assert.AreEqual(48.0, ov.GlowSize!.Value, 0.001);
                Assert.AreEqual(12.0, ov.CornerRadius!.Value, 0.001);
                Assert.AreEqual(GlowStyle.Comet, ov.Style);

                // 3. Move back to General App -> removed from TrackedGames & GameOverrides, profile settings preserved
                app.Category = "General";
                NotiGlow.Core.Helpers.AppCategoryHelper.ApplyCategorySync(app, profileService, settingsService);
                Assert.IsFalse(NotiGlow.Core.Helpers.AppCategoryHelper.IsGamingProfile(app, settingsService.Current.TrackedGames));
                Assert.IsFalse(settingsService.Current.GameOverrides.ContainsKey(normKey));

                var stored = profileService.GetProfile("TestGameRoute.exe");
                Assert.IsNotNull(stored);
                Assert.AreEqual("General", stored.Category);
                Assert.AreEqual("#FF00AA", stored.ColorHex);
                Assert.AreEqual(6500, stored.DurationMs);
                Assert.AreEqual(GlowStyle.Comet, stored.Style);

                // Cleanup test profile
                profileService.RemoveProfile("TestGameRoute.exe");
            }
            finally
            {
                try { Directory.Delete(tempDir, true); } catch { }
            }
        }

        [TestMethod]
        public void Test_LegacyGridSettingsJson_BackwardCompatibility_NormalizesToList()
        {
            string legacyJson = "{ \"ApplicationsViewMode\": \"Grid\" }";
            var settings = System.Text.Json.JsonSerializer.Deserialize<AppSettings>(legacyJson);
            Assert.IsNotNull(settings);
            Assert.AreEqual(ApplicationViewMode.Grid, settings.ApplicationsViewMode);

            var normalized = NotiGlow.Core.Helpers.AppCategoryHelper.NormalizeApplicationsViewMode(settings);
            Assert.AreEqual(ApplicationViewMode.List, normalized);
            Assert.AreEqual(ApplicationViewMode.List, settings.ApplicationsViewMode);
        }
    }
}
