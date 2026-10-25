using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;

namespace NotiGlow.Services
{
    public enum DistributionChannel
    {
        GitHub,
        MicrosoftStore
    }

    public static class PackageService
    {
        [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        private static extern int GetCurrentPackageFullName(ref int packageFullNameLength, StringBuilder? packageFullName);

        [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        private static extern int GetCurrentPackageFamilyName(ref int packageFamilyNameLength, StringBuilder? packageFamilyName);

        private const int APPMODEL_ERROR_NO_PACKAGE = 15700;
        private const int ERROR_INSUFFICIENT_BUFFER = 122;
        private const int ERROR_SUCCESS = 0;

        // Microsoft Store Official Constants
        public const string ExpectedPackageName = "crius.Notiglow";
        public const string ExpectedPublisher = "CN=2F9A5694-FADC-405A-AF51-BB9269D3FFF1";
        public const string ExpectedPublisherDisplayName = "crius";
        public const string ExpectedPackageFamily = "crius.Notiglow_tvqx0hh5fwha";
        public const string StoreId = "9P9L33HG5V64";
        public const string MsaAppId = "9a7abc5d-4339-446f-8fca-09cf11920b01";
        public const string StoreUrl = "https://apps.microsoft.com/detail/9P9L33HG5V64";
        public const string StoreProtocolUri = "ms-windows-store://pdp/?productid=9P9L33HG5V64";

        private static bool? _isPackagedOverride;
        private static bool? _detectedPackaged;
        private static string? _packageFullName;
        private static string? _packageFamilyName;

        /// <summary>
        /// Allows test suites to simulate packaged or unpackaged execution environments safely.
        /// </summary>
        public static void SetPackagedOverrideForTesting(bool? isPackaged)
        {
            _isPackagedOverride = isPackaged;
        }

        /// <summary>
        /// Returns true if the application is running inside a packaged MSIX environment with Package Identity.
        /// </summary>
        public static bool IsPackaged
        {
            get
            {
                if (_isPackagedOverride.HasValue)
                {
                    return _isPackagedOverride.Value;
                }

                if (_detectedPackaged.HasValue)
                {
                    return _detectedPackaged.Value;
                }

                _detectedPackaged = DetectPackageIdentity();
                return _detectedPackaged.Value;
            }
        }

        public static DistributionChannel Channel => IsPackaged ? DistributionChannel.MicrosoftStore : DistributionChannel.GitHub;

        public static string? PackageFullName
        {
            get
            {
                if (_packageFullName == null && IsPackaged)
                {
                    _packageFullName = ResolvePackageFullName();
                }
                return _packageFullName;
            }
        }

        public static string? PackageFamilyName
        {
            get
            {
                if (_packageFamilyName == null && IsPackaged)
                {
                    _packageFamilyName = ResolvePackageFamilyName();
                }
                return _packageFamilyName;
            }
        }

        private static bool DetectPackageIdentity()
        {
            try
            {
                int length = 0;
                int result = GetCurrentPackageFullName(ref length, null);
                if (result == APPMODEL_ERROR_NO_PACKAGE)
                {
                    return false;
                }

                if (result == ERROR_INSUFFICIENT_BUFFER || result == ERROR_SUCCESS)
                {
                    return true;
                }

                // WinRT fallback
                try
                {
                    return Windows.ApplicationModel.Package.Current != null;
                }
                catch
                {
                    return false;
                }
            }
            catch
            {
                return false;
            }
        }

        private static string? ResolvePackageFullName()
        {
            try
            {
                int length = 0;
                GetCurrentPackageFullName(ref length, null);
                if (length > 0)
                {
                    var sb = new StringBuilder(length);
                    if (GetCurrentPackageFullName(ref length, sb) == ERROR_SUCCESS)
                    {
                        return sb.ToString();
                    }
                }

                try
                {
                    return Windows.ApplicationModel.Package.Current?.Id?.FullName;
                }
                catch
                {
                    return null;
                }
            }
            catch
            {
                return null;
            }
        }

        private static string? ResolvePackageFamilyName()
        {
            try
            {
                int length = 0;
                GetCurrentPackageFamilyName(ref length, null);
                if (length > 0)
                {
                    var sb = new StringBuilder(length);
                    if (GetCurrentPackageFamilyName(ref length, sb) == ERROR_SUCCESS)
                    {
                        return sb.ToString();
                    }
                }

                try
                {
                    return Windows.ApplicationModel.Package.Current?.Id?.FamilyName;
                }
                catch
                {
                    return null;
                }
            }
            catch
            {
                return null;
            }
        }

        public static void OpenStorePage()
        {
            try
            {
                Process.Start(new ProcessStartInfo
                {
                    FileName = StoreProtocolUri,
                    UseShellExecute = true
                });
            }
            catch
            {
                try
                {
                    Process.Start(new ProcessStartInfo
                    {
                        FileName = StoreUrl,
                        UseShellExecute = true
                    });
                }
                catch (Exception ex)
                {
                    LoggerService.LogError("Failed to open Microsoft Store URL", ex);
                }
            }
        }
    }
}
