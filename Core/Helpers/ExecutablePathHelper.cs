using System;
using System.IO;

namespace NotiGlow.Core.Helpers
{
    public static class ExecutablePathHelper
    {
        /// <summary>
        /// Validates that the path is an executable file (.exe) and extracts the filename only (e.g., "chrome.exe").
        /// </summary>
        /// <param name="filePath">Full or partial file path.</param>
        /// <param name="executableName">The extracted executable filename if valid.</param>
        /// <returns>True if the file has a .exe extension and a valid filename; otherwise false.</returns>
        public static bool TryGetExecutableName(string? filePath, out string executableName)
        {
            executableName = string.Empty;
            if (string.IsNullOrWhiteSpace(filePath))
                return false;

            string trimmed = filePath.Trim();
            if (!trimmed.EndsWith(".exe", StringComparison.OrdinalIgnoreCase))
                return false;

            try
            {
                executableName = Path.GetFileName(trimmed);
                return !string.IsNullOrWhiteSpace(executableName) && executableName.EndsWith(".exe", StringComparison.OrdinalIgnoreCase);
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// Resolves the app identifier to use for profile matching or display given a potential file path or user input.
        /// If input is an absolute/relative path to a .exe, returns the filename (e.g. "chrome.exe").
        /// </summary>
        public static string ResolveAppIdentifier(string? input)
        {
            if (string.IsNullOrWhiteSpace(input))
                return string.Empty;

            if (TryGetExecutableName(input, out string exeName))
                return exeName;

            return input.Trim();
        }

        /// <summary>
        /// Applies the selected file to the current AppId text.
        /// If selection is cancelled, null, empty, or not a .exe, the original AppId is preserved.
        /// </summary>
        /// <param name="currentAppId">The current AppId in the TextBox.</param>
        /// <param name="selectedFilePath">The file path from the file dialog (or null if cancelled).</param>
        /// <param name="newAppId">The resulting AppId to set in the TextBox.</param>
        /// <param name="errorMessage">Error description if invalid non-exe was selected, or null.</param>
        /// <returns>True if the AppId was updated; false if unchanged (e.g. cancelled or rejected).</returns>
        public static bool TryApplySelectedExecutable(string currentAppId, string? selectedFilePath, out string newAppId, out string? errorMessage)
        {
            newAppId = currentAppId;
            errorMessage = null;

            // If selection cancelled or empty, preserve existing value without error
            if (string.IsNullOrWhiteSpace(selectedFilePath))
            {
                return false;
            }

            if (!TryGetExecutableName(selectedFilePath, out string exeName))
            {
                errorMessage = "The selected file is not an executable (*.exe). Please select a valid .exe file.";
                return false;
            }

            newAppId = exeName;
            return true;
        }
    }
}
