using System;
using System.Runtime.InteropServices;
using System.Windows.Media;

namespace DiplomPrintGenerate.Services
{
    public static class EyedropperService
    {
        [DllImport("user32.dll")]
        private static extern IntPtr GetDC(IntPtr hwnd);

        [DllImport("user32.dll")]
        private static extern int ReleaseDC(IntPtr hwnd, IntPtr hdc);

        [DllImport("gdi32.dll")]
        private static extern uint GetPixel(IntPtr hdc, int nXPos, int nYPos);

        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool GetCursorPos(out POINT lpPoint);

        [StructLayout(LayoutKind.Sequential)]
        public struct POINT
        {
            public int X;
            public int Y;
        }

        public static bool IsActive { get; set; } = false;

        public static Color GetColorAtCursor()
        {
            POINT pt;
            if (!GetCursorPos(out pt))
                return Colors.Black;

            return GetColorAt(pt.X, pt.Y);
        }

        public static Color GetColorAt(int screenX, int screenY)
        {
            IntPtr hdc = GetDC(IntPtr.Zero);
            if (hdc == IntPtr.Zero)
                return Colors.Black;

            try
            {
                uint pixel = GetPixel(hdc, screenX, screenY);
                if (pixel == 0xFFFFFFFF) // CLR_INVALID
                    return Colors.Black;

                byte r = (byte)(pixel & 0x000000FF);
                byte g = (byte)((pixel & 0x0000FF00) >> 8);
                byte b = (byte)((pixel & 0x00FF0000) >> 16);
                return Color.FromRgb(r, g, b);
            }
            catch (Exception ex)
            {
                Logger.LogError("Ошибка чтения цвета пикселя", ex);
                return Colors.Black;
            }
            finally
            {
                ReleaseDC(IntPtr.Zero, hdc);
            }
        }

        public static string ColorToHex(Color color)
        {
            return string.Format("#{0:X2}{1:X2}{2:X2}", color.R, color.G, color.B);
        }

        public static Color? HexToColor(string hex)
        {
            if (string.IsNullOrWhiteSpace(hex)) return null;
            hex = hex.Trim().TrimStart('#');

            if (hex.Length == 6)
            {
                try
                {
                    byte r = Convert.ToByte(hex.Substring(0, 2), 16);
                    byte g = Convert.ToByte(hex.Substring(2, 2), 16);
                    byte b = Convert.ToByte(hex.Substring(4, 2), 16);
                    return Color.FromRgb(r, g, b);
                }
                catch { }
            }
            else if (hex.Length == 8)
            {
                try
                {
                    byte a = Convert.ToByte(hex.Substring(0, 2), 16);
                    byte r = Convert.ToByte(hex.Substring(2, 2), 16);
                    byte g = Convert.ToByte(hex.Substring(4, 2), 16);
                    byte b = Convert.ToByte(hex.Substring(6, 2), 16);
                    return Color.FromArgb(a, r, g, b);
                }
                catch { }
            }

            return null;
        }
    }
}
