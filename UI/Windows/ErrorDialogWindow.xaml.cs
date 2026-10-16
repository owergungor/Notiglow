using System;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using NotiGlow.Services;

namespace NotiGlow.UI.Dialogs
{
    public partial class ErrorDialogWindow : Window
    {
        private static bool _isShowingDialog = false;
        private Action? _retryAction;

        public ErrorDialogWindow()
        {
            InitializeComponent();
        }

        public static bool ShowError(
            string title,
            string message,
            Exception? ex = null,
            string? logFilePath = null,
            Action? retryAction = null,
            Window? owner = null)
        {
            // If already showing, guard against recursive error loops
            if (_isShowingDialog)
            {
                return false;
            }

            // Ensure we are on the UI thread
            if (System.Windows.Application.Current?.Dispatcher != null && !System.Windows.Application.Current.Dispatcher.CheckAccess())
            {
                try
                {
                    return System.Windows.Application.Current.Dispatcher.Invoke(() =>
                        ShowError(title, message, ex, logFilePath, retryAction, owner));
                }
                catch
                {
                    // Fallback to standard message box if Dispatcher invoke fails
                    FallbackShowMessageBox(title, message, ex, logFilePath);
                    return false;
                }
            }

            _isShowingDialog = true;
            try
            {
                var dialog = new ErrorDialogWindow();

                // Set Title
                if (!string.IsNullOrWhiteSpace(title))
                {
                    dialog.TxtTitle.Text = title;
                }

                // Set User Message
                if (!string.IsNullOrWhiteSpace(message))
                {
                    dialog.TxtMessage.Text = message;
                }

                // Set Log File Path
                string logPath = logFilePath ?? Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                    "NotiGlow",
                    "startup.log");
                dialog.TxtLogPath.Text = logPath;

                // Format Details
                if (ex != null)
                {
                    var sb = new StringBuilder();
                    sb.AppendLine($"Timestamp: {DateTime.Now:yyyy-MM-dd HH:mm:ss.fff}");
                    sb.AppendLine($"Exception Type: {ex.GetType().FullName}");
                    sb.AppendLine($"Message: {SanitizeText(ex.Message)}");
                    sb.AppendLine();
                    sb.AppendLine("Stack Trace:");
                    sb.AppendLine(SanitizeText(ex.StackTrace ?? "No stack trace available."));

                    Exception? inner = ex.InnerException;
                    int depth = 1;
                    while (inner != null && depth <= 5)
                    {
                        sb.AppendLine();
                        sb.AppendLine($"--- Inner Exception {depth} ({inner.GetType().FullName}) ---");
                        sb.AppendLine($"Message: {SanitizeText(inner.Message)}");
                        sb.AppendLine("Stack Trace:");
                        sb.AppendLine(SanitizeText(inner.StackTrace ?? "No stack trace."));
                        inner = inner.InnerException;
                        depth++;
                    }

                    dialog.TxtDetails.Text = sb.ToString();
                    dialog.ExpDetails.Visibility = Visibility.Visible;
                }
                else
                {
                    dialog.TxtDetails.Text = message;
                    dialog.ExpDetails.Visibility = Visibility.Collapsed;
                }

                // Configure Retry action if provided
                if (retryAction != null)
                {
                    dialog._retryAction = retryAction;
                    dialog.BtnRetry.Visibility = Visibility.Visible;
                }
                else
                {
                    dialog.BtnRetry.Visibility = Visibility.Collapsed;
                }

                // Set Owner if visible
                if (owner != null && owner.IsVisible)
                {
                    dialog.Owner = owner;
                    dialog.WindowStartupLocation = WindowStartupLocation.CenterOwner;
                }
                else
                {
                    dialog.WindowStartupLocation = WindowStartupLocation.CenterScreen;
                }

                dialog.ShowDialog();
                return true;
            }
            catch (Exception dialogEx)
            {
                // Safe fallback if ErrorDialogWindow fails to render or initialize
                try
                {
                    LoggerService.LogError("Failed to show ErrorDialogWindow, using fallback.", dialogEx);
                }
                catch { }

                FallbackShowMessageBox(title, message, ex, logFilePath);
                return false;
            }
            finally
            {
                _isShowingDialog = false;
            }
        }

        private static void FallbackShowMessageBox(string title, string message, Exception? ex, string? logFilePath)
        {
            try
            {
                string logPath = logFilePath ?? Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                    "NotiGlow",
                    "startup.log");

                string formattedMsg = $"{message}\n\nError: {ex?.Message}\n\nLog: {logPath}";
                System.Windows.MessageBox.Show(formattedMsg, title, MessageBoxButton.OK, MessageBoxImage.Error);
            }
            catch
            {
                // Last ditch effort
            }
        }

        private static string SanitizeText(string text)
        {
            if (string.IsNullOrEmpty(text)) return string.Empty;
            return System.Text.RegularExpressions.Regex.Replace(
                text,
                @"(token|password|secret|pwd)=([^&\s]+)",
                "$1=[REDACTED]",
                System.Text.RegularExpressions.RegexOptions.IgnoreCase);
        }

        private void BtnCopyLogPath_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                System.Windows.Clipboard.SetDataObject(TxtLogPath.Text, true);
                ShowCopiedFeedback(TxtCopyLogLabel, "Error.Copied");
            }
            catch
            {
                // Clipboard can occasionally be locked by another process
            }
        }

        private void BtnCopyDetails_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                System.Windows.Clipboard.SetDataObject(TxtDetails.Text, true);
                ShowCopiedFeedback(TxtCopyDetailsLabel, "Error.Copied");
            }
            catch
            {
            }
        }

        private async void ShowCopiedFeedback(System.Windows.Controls.TextBlock label, string resourceKey)
        {
            string originalText = label.Text;
            string copiedText = LocalizationService.Instance.GetString(resourceKey);
            label.Text = string.IsNullOrEmpty(copiedText) ? "Copied!" : copiedText;

            await Task.Delay(1800);
            try
            {
                label.Text = originalText;
            }
            catch { }
        }

        private void BtnRetry_Click(object sender, RoutedEventArgs e)
        {
            Close();
            _retryAction?.Invoke();
        }

        private void BtnClose_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }

        private void Window_KeyDown(object sender, System.Windows.Input.KeyEventArgs e)
        {
            if (e.Key == Key.Escape)
            {
                Close();
                e.Handled = true;
            }
            else if (e.Key == Key.Enter)
            {
                if (BtnRetry.Visibility == Visibility.Visible && Keyboard.Modifiers == ModifierKeys.Control)
                {
                    BtnRetry_Click(sender, e);
                    e.Handled = true;
                }
                else
                {
                    BtnClose_Click(sender, e);
                    e.Handled = true;
                }
            }
        }
    }
}
