using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace NotiGlow.Services
{
    /// <summary>
    /// Reusable WinUI 3 native modal ContentDialog service.
    /// Guarantees single-instance dialog display using a non-blocking semaphore lock,
    /// binds to the host window's XamlRoot, and defaults safely to Cancel for destructive actions.
    /// </summary>
    public static class DialogService
    {
        private static readonly SemaphoreSlim _dialogLock = new(1, 1);

        public static bool IsDialogOpen => _dialogLock.CurrentCount == 0;

        /// <summary>
        /// Displays a native modal Windows 11 ContentDialog for destructive confirmation.
        /// Cancel is set as the default button so Enter/Escape never trigger accidental deletion.
        /// Returns true only when the user explicitly clicks the primary destructive button.
        /// </summary>
        public static async Task<bool> ShowDestructiveConfirmAsync(
            XamlRoot? xamlRoot,
            string title,
            string message,
            string primaryButtonText = "Remove",
            string cancelButtonText = "Cancel")
        {
            if (xamlRoot == null)
            {
                LoggerService.LogWarning("DialogService: XamlRoot was null; cannot display ContentDialog.");
                return false;
            }

            // Ensure only one ContentDialog can be open at any time
            if (!await _dialogLock.WaitAsync(0))
            {
                return false;
            }

            try
            {
                var contentBlock = new TextBlock
                {
                    Text = message,
                    TextWrapping = TextWrapping.Wrap,
                    MaxWidth = 420
                };

                var dialog = new ContentDialog
                {
                    XamlRoot = xamlRoot,
                    Title = title,
                    Content = contentBlock,
                    PrimaryButtonText = primaryButtonText,
                    CloseButtonText = cancelButtonText,
                    DefaultButton = ContentDialogButton.Close
                };

                if (xamlRoot.Content is FrameworkElement rootElement)
                {
                    dialog.RequestedTheme = rootElement.ActualTheme;
                }

                if (Application.Current?.Resources != null &&
                    Application.Current.Resources.TryGetValue("DestructiveDialogPrimaryButtonStyle", out var styleObj) &&
                    styleObj is Style destructiveStyle)
                {
                    dialog.PrimaryButtonStyle = destructiveStyle;
                }

                ContentDialogResult result = await dialog.ShowAsync();
                return result == ContentDialogResult.Primary;
            }
            catch (Exception ex)
            {
                LoggerService.LogError("DialogService: Failed to display ContentDialog.", ex);
                return false;
            }
            finally
            {
                _dialogLock.Release();
            }
        }
    }
}
