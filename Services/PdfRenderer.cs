using System;
using System.IO;
using System.Windows.Media.Imaging;
using Windows.Data.Pdf;
using Windows.Storage;
using Windows.Storage.Streams;

namespace DiplomPrintGenerate.Services
{
    public static class PdfRenderer
    {
        public static BitmapSource RenderPdfToBitmap(string pdfPath, int pageIndex = 0, double targetDpi = 300.0)
        {
            if (!File.Exists(pdfPath))
                throw new FileNotFoundException("PDF файл не найден", pdfPath);

            string fullPath = Path.GetFullPath(pdfPath);
            StorageFile file = StorageFile.GetFileFromPathAsync(fullPath).GetAwaiter().GetResult();
            PdfDocument pdfDoc = PdfDocument.LoadFromFileAsync(file).GetAwaiter().GetResult();

            if (pdfDoc.PageCount == 0)
                throw new InvalidOperationException("PDF документ не содержит страниц.");

            uint pageNum = (uint)Math.Max(0, Math.Min(pageIndex, (int)pdfDoc.PageCount - 1));

            using (PdfPage page = pdfDoc.GetPage(pageNum))
            {
                using (var memStream = new InMemoryRandomAccessStream())
                {
                    var options = new PdfPageRenderOptions();
                    // Scale from 72 pt (default PDF page size) to target DPI (default 300 DPI for crisp print)
                    double scale = targetDpi / 72.0;
                    options.DestinationWidth = (uint)Math.Round(page.Size.Width * scale);
                    options.DestinationHeight = (uint)Math.Round(page.Size.Height * scale);

                    page.RenderToStreamAsync(memStream, options).GetAwaiter().GetResult();

                    byte[] bytes = new byte[memStream.Size];
                    using (var reader = new DataReader(memStream.GetInputStreamAt(0)))
                    {
                        reader.LoadAsync((uint)memStream.Size).GetAwaiter().GetResult();
                        reader.ReadBytes(bytes);
                    }

                    using (var ms = new MemoryStream(bytes))
                    {
                        var bitmap = new BitmapImage();
                        bitmap.BeginInit();
                        bitmap.CacheOption = BitmapCacheOption.OnLoad;
                        bitmap.StreamSource = ms;
                        bitmap.EndInit();
                        bitmap.Freeze();
                        return bitmap;
                    }
                }
            }
        }
    }
}
