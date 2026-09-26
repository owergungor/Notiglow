using System;

namespace NotiGlow.UI.Animations
{
    /// <summary>
    /// Centralized policy governing UI animation execution, transitions, and accessibility reduced-motion state.
    /// When Reduce Motion is enabled, all non-essential UI animations, button press scales, transitions,
    /// popups, and shimmers collapse to duration zero (instant static layout changes).
    /// </summary>
    public static class MotionPolicy
    {
        private static bool _isReduceMotion = false;

        public static bool IsReduceMotion => _isReduceMotion;

        public static event EventHandler? PolicyChanged;

        public static void Update(bool reduceMotion, bool reduceAnimations = false)
        {
            bool shouldReduce = reduceMotion || reduceAnimations;
            if (_isReduceMotion != shouldReduce)
            {
                _isReduceMotion = shouldReduce;
                PolicyChanged?.Invoke(null, EventArgs.Empty);
            }
        }

        /// <summary>
        /// Returns TimeSpan.Zero when Reduce Motion is active, otherwise returns the specified duration.
        /// </summary>
        public static TimeSpan GetDuration(TimeSpan standardDuration)
        {
            return _isReduceMotion ? TimeSpan.Zero : standardDuration;
        }

        /// <summary>
        /// Returns 0ms when Reduce Motion is active, otherwise returns the specified milliseconds.
        /// </summary>
        public static int GetDurationMs(int standardMs)
        {
            return _isReduceMotion ? 0 : standardMs;
        }

        /// <summary>
        /// Returns 0.0s when Reduce Motion is active, otherwise returns the specified seconds.
        /// </summary>
        public static double GetDurationSeconds(double standardSeconds)
        {
            return _isReduceMotion ? 0.0 : standardSeconds;
        }
    }
}
