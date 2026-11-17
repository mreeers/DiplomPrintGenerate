using System;
using System.IO;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace DiplomPrintGenerate.Services
{
    public static class ScreenshotService
    {
        private static int _counter = 0;
        private static readonly string _screenshotsDir;
        public static bool IsEnabled { get; set; } = false;

        static ScreenshotService()
        {
            try
            {
                string baseDir = AppDomain.CurrentDomain.BaseDirectory;
                _screenshotsDir = Path.Combine(baseDir, "screenshots");
                if (!Directory.Exists(_screenshotsDir))
                {
                    Directory.CreateDirectory(_screenshotsDir);
                }
            }
            catch (Exception ex)
            {
                Logger.LogError("Не удалось создать папку скриншотов", ex);
            }
        }

        public static string ScreenshotsDirectory => _screenshotsDir;
        public static int ScreenshotCount => _counter;

        public static string CaptureWindow(Window window, string actionTag)
        {
            if (!IsEnabled || window == null)
                return null;

            try
            {
                Visual visual = (window.Content as Visual) ?? window;
                var fe = visual as FrameworkElement;

                double w = (fe != null && fe.ActualWidth > 10) ? fe.ActualWidth : window.ActualWidth;
                double h = (fe != null && fe.ActualHeight > 10) ? fe.ActualHeight : window.ActualHeight;

                int pixelW = (int)Math.Round(w);
                int pixelH = (int)Math.Round(h);

                if (pixelW <= 10 || pixelH <= 10)
                    return null;

                var rtb = new RenderTargetBitmap(pixelW, pixelH, 96, 96, PixelFormats.Pbgra32);
                rtb.Render(visual);

                var encoder = new PngBitmapEncoder();
                encoder.Frames.Add(BitmapFrame.Create(rtb));

                _counter++;
                string safeTag = SanitizeFileName(actionTag);
                string filename = string.Format("step_{0:D4}_{1}_{2}.png",
                    _counter,
                    DateTime.Now.ToString("yyyyMMdd_HHmmss_fff"),
                    safeTag);

                if (!Directory.Exists(_screenshotsDir))
                {
                    Directory.CreateDirectory(_screenshotsDir);
                }

                string fullPath = Path.Combine(_screenshotsDir, filename);

                using (var fs = new FileStream(fullPath, FileMode.Create, FileAccess.Write))
                {
                    encoder.Save(fs);
                }

                Logger.Log($"[СКРИНШОТ #{_counter:D4}] Сохранен: {filename}");
                return fullPath;
            }
            catch (Exception ex)
            {
                Logger.LogError($"Не удалось сделать скриншот ({actionTag})", ex);
                return null;
            }
        }

        private static string SanitizeFileName(string tag)
        {
            if (string.IsNullOrEmpty(tag))
                return "click";

            foreach (char c in Path.GetInvalidFileNameChars())
            {
                tag = tag.Replace(c, '_');
            }
            if (tag.Length > 35)
            {
                tag = tag.Substring(0, 35);
            }
            return tag;
        }
    }
}
