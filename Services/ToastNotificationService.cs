using System;
using System.Collections.Concurrent;

namespace NotiGlow.Services
{
    public enum ToastSeverity
    {
        Info,
        Success,
        Warning,
        Error
    }

    public sealed class ToastItem
    {
        public string Id { get; } = Guid.NewGuid().ToString("N");
        public string Title { get; init; } = string.Empty;
        public string Message { get; init; } = string.Empty;
        public ToastSeverity Severity { get; init; } = ToastSeverity.Info;
        public TimeSpan Duration { get; init; } = TimeSpan.FromSeconds(4);
        public string? ActionText { get; init; }
        public Action? ActionCallback { get; init; }
        public DateTime CreatedAt { get; } = DateTime.UtcNow;
    }

    /// <summary>
    /// Thread-safe service for presenting localized, animated, non-blocking toast notifications
    /// inspired by modern Hero UI / 21st.dev alert-toast components.
    /// Handles debouncing of repeated errors to prevent screen flooding.
    /// </summary>
    public sealed class ToastNotificationService
    {
        private static readonly Lazy<ToastNotificationService> _instance =
            new(() => new ToastNotificationService());

        public static ToastNotificationService Instance => _instance.Value;

        private readonly ConcurrentDictionary<string, DateTime> _recentMessages = new();

        public event Action<ToastItem>? ToastRequested;

        public void Show(
            string message,
            ToastSeverity severity = ToastSeverity.Info,
            string? title = null,
            string? actionText = null,
            Action? actionCallback = null,
            TimeSpan? duration = null)
        {
            if (string.IsNullOrWhiteSpace(message)) return;

            string dedupeKey = $"{severity}:{message.Trim()}";
            DateTime now = DateTime.UtcNow;

            // Debounce repeated identical notifications within 1.5s
            if (_recentMessages.TryGetValue(dedupeKey, out DateTime lastTime) && (now - lastTime).TotalSeconds < 1.5)
            {
                return;
            }
            _recentMessages[dedupeKey] = now;

            // Resolve default localized title if none supplied
            string resolvedTitle = title ?? severity switch
            {
                ToastSeverity.Error => LocalizationService.Instance.GetString("Toast.ErrorTitle"),
                ToastSeverity.Warning => LocalizationService.Instance.GetString("Toast.WarningTitle"),
                ToastSeverity.Success => LocalizationService.Instance.GetString("Toast.SuccessTitle"),
                _ => LocalizationService.Instance.GetString("Toast.InfoTitle")
            };

            var toast = new ToastItem
            {
                Title = resolvedTitle,
                Message = message.Trim(),
                Severity = severity,
                Duration = duration ?? (severity == ToastSeverity.Error ? TimeSpan.FromSeconds(5.5) : TimeSpan.FromSeconds(3.8)),
                ActionText = actionText,
                ActionCallback = actionCallback
            };

            // Log internally for diagnostics
            switch (severity)
            {
                case ToastSeverity.Error:
                    LoggerService.LogError($"[User Toast Error] {resolvedTitle}: {message}", null);
                    break;
                case ToastSeverity.Warning:
                    LoggerService.LogWarning($"[User Toast Warning] {resolvedTitle}: {message}");
                    break;
                default:
                    LoggerService.LogInfo($"[User Toast {severity}] {resolvedTitle}: {message}");
                    break;
            }

            ToastRequested?.Invoke(toast);
        }

        public void ShowSuccess(string message, string? title = null)
        {
            Show(message, ToastSeverity.Success, title);
        }

        public void ShowWarning(string message, string? title = null, string? actionText = null, Action? actionCallback = null)
        {
            Show(message, ToastSeverity.Warning, title, actionText, actionCallback);
        }

        public void ShowError(string message, string? title = null, string? actionText = null, Action? actionCallback = null)
        {
            Show(message, ToastSeverity.Error, title, actionText, actionCallback);
        }

        public void ShowInfo(string message, string? title = null)
        {
            Show(message, ToastSeverity.Info, title);
        }

        public void Show(string title, string message, ToastSeverity severity, string? actionText = null, Action? actionCallback = null)
        {
            Show(message, severity, title, actionText, actionCallback);
        }

        public void ShowException(string title, Exception ex, string? actionText = null, Action? actionCallback = null)
        {
            string friendlyMsg = ex switch
            {
                UnauthorizedAccessException => LocalizationService.Instance.GetString("Toast.AccessDenied"),
                System.IO.FileNotFoundException => LocalizationService.Instance.GetString("Toast.FileNotFound"),
                System.IO.IOException => LocalizationService.Instance.GetString("Toast.IoError"),
                _ => LocalizationService.Instance.GetString("Toast.GenericError")
            };

            if (string.IsNullOrWhiteSpace(friendlyMsg) || friendlyMsg.StartsWith("Toast."))
            {
                friendlyMsg = "An unexpected error occurred. The operation could not be completed.";
            }

            Show(friendlyMsg, ToastSeverity.Error, title, actionText, actionCallback);
        }
    }
}
