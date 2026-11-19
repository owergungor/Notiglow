using System;
using System.IO;

namespace NotiGlow.Tests
{
    internal static class TestPathHelper
    {
        private static string? _solutionDir;

        public static string SolutionDir
        {
            get
            {
                if (_solutionDir != null) return _solutionDir;

                var current = AppDomain.CurrentDomain.BaseDirectory;
                while (!string.IsNullOrEmpty(current))
                {
                    if (File.Exists(Path.Combine(current, "NotiGlow.sln")))
                    {
                        _solutionDir = current;
                        return current;
                    }
                    current = Path.GetDirectoryName(current);
                }

                _solutionDir = @"C:\Users\omer\Projects\Notiglow";
                return _solutionDir;
            }
        }
    }
}
