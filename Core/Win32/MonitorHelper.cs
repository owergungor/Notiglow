using System.Collections.Generic;
using System.Drawing;
using System.Windows.Forms;

namespace GlowBorder.Core.Win32
{
    public static class MonitorHelper
    {
        public static Screen GetActiveScreen()
        {
            if (NativeMethods.GetCursorPos(out var pt))
            {
                var point = new Point(pt.X, pt.Y);
                return Screen.FromPoint(point);
            }
            return Screen.PrimaryScreen ?? Screen.AllScreens[0];
        }

        public static Screen GetPrimaryScreen()
        {
            return Screen.PrimaryScreen ?? Screen.AllScreens[0];
        }

        public static IEnumerable<Screen> GetAllScreens()
        {
            return Screen.AllScreens;
        }
    }
}
