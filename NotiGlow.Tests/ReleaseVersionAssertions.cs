using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NotiGlow.Services;

namespace NotiGlow.Tests
{
    internal static class ReleaseVersionAssertions
    {
        private static string SolutionDirectory
        {
            get
            {
                var directory = new DirectoryInfo(AppDomain.CurrentDomain.BaseDirectory);
                while (directory != null)
                {
                    if (File.Exists(Path.Combine(directory.FullName, "NotiGlow.sln")))
                        return directory.FullName;
                    directory = directory.Parent;
                }

                Assert.Fail("Could not find NotiGlow.sln from the test output directory.");
                return string.Empty;
            }
        }

        public static string AssertApplicationVersionMetadata()
        {
            var currentVersion = UpdateService.CurrentVersionString;
            Assert.IsTrue(Regex.IsMatch(currentVersion, @"^\d+\.\d+$"),
                $"User-visible version must contain exactly major.minor; got '{currentVersion}'.");

            var project = XDocument.Load(Path.Combine(SolutionDirectory, "NotiGlow.csproj"));
            var properties = project.Root!.Elements("PropertyGroup")
                .First(group => group.Element("Version") != null);

            Assert.AreEqual(currentVersion, (string?)properties.Element("Version"), "Package version must match the user-visible version.");
            Assert.AreEqual(currentVersion, (string?)properties.Element("InformationalVersion"), "InformationalVersion must match the user-visible version.");

            var assemblyVersion = currentVersion + ".0.0";
            Assert.AreEqual(assemblyVersion, (string?)properties.Element("AssemblyVersion"), "AssemblyVersion must use the four-part version rule.");
            Assert.AreEqual(assemblyVersion, (string?)properties.Element("FileVersion"), "FileVersion must use the four-part version rule.");

            var assembly = typeof(UpdateService).Assembly;
            Assert.AreEqual(assemblyVersion, assembly.GetName().Version?.ToString(), "Built assembly version must match the project metadata.");
            Assert.AreEqual(assemblyVersion, assembly.GetCustomAttribute<AssemblyFileVersionAttribute>()?.Version,
                "Built file version must match the project metadata.");

            var informationalVersion = assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion;
            Assert.AreEqual(currentVersion, informationalVersion?.Split('+')[0], "Built informational version must match CurrentVersionString.");

            return currentVersion;
        }

        public static void AssertInstallerVersionMetadata(string currentVersion)
        {
            var installer = File.ReadAllText(Path.Combine(SolutionDirectory, "NotiGlow-Setup.iss"));
            var assemblyVersion = currentVersion + ".0.0";

            StringAssert.Contains(installer, $"AppVersion={currentVersion}");
            StringAssert.Contains(installer, $"VersionInfoTextVersion={currentVersion}");
            StringAssert.Contains(installer, $"VersionInfoVersion={assemblyVersion}");
            StringAssert.Contains(installer, $"OutputBaseFilename=NotiGlow-Setup-v{currentVersion}");
        }

        public static void AssertPackageManifestVersionMetadata(string currentVersion)
        {
            var manifest = XDocument.Load(Path.Combine(SolutionDirectory, "Package", "AppxManifest.xml"));
            XNamespace ns = "http://schemas.microsoft.com/appx/manifest/foundation/windows10";
            var identity = manifest.Descendants(ns + "Identity").FirstOrDefault();

            Assert.IsNotNull(identity, "AppxManifest must contain an Identity element.");
            Assert.AreEqual(currentVersion + ".0.0", identity!.Attribute("Version")?.Value,
                "MSIX package version must match the four-part application version.");
        }

        public static void AssertAdvancedViewUsesCurrentVersion()
        {
            var xamlPath = Path.Combine(SolutionDirectory, "UI", "Views", "AdvancedView.xaml");
            var codePath = Path.Combine(SolutionDirectory, "UI", "Views", "AdvancedView.xaml.cs");
            var xaml = File.ReadAllText(xamlPath);
            var code = File.ReadAllText(codePath);

            StringAssert.Contains(xaml, "x:Name=\"TxtUpdateStatus\"");
            Assert.IsFalse(Regex.IsMatch(xaml, @"Text=""[^""]*Current Version:\s*v\d+\.\d+"),
                "The view must not hardcode a release number in its initial status text.");
            Assert.IsTrue(Regex.IsMatch(code,
                @"GetString\(\s*""Advanced\.(?:CurrentVersionFormat|StoreVersionFormat)""\s*,\s*UpdateService\.CurrentVersionString\s*\)"),
                "The visible update status must be formatted with CurrentVersionString.");
        }
    }
}
