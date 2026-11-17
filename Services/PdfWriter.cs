using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace DiplomPrintGenerate.Services
{
    public class PdfWriter
    {
        public class PageImageInfo
        {
            public byte[] JpegBytes { get; set; }
            public int PixelWidth { get; set; }
            public int PixelHeight { get; set; }
        }

        public static byte[] CreateMultiPagePdf(List<PageImageInfo> pages)
        {
            if (pages == null || pages.Count == 0)
                throw new ArgumentException("Pages collection cannot be empty.");

            var ms = new MemoryStream();
            var offsets = new List<long>();

            void WriteString(string s)
            {
                byte[] bytes = Encoding.ASCII.GetBytes(s);
                ms.Write(bytes, 0, bytes.Length);
            }

            void WriteObject(int id, string body)
            {
                while (offsets.Count < id) offsets.Add(0);
                offsets[id - 1] = ms.Position;
                WriteString(string.Format("{0} 0 obj\n{1}\nendobj\n", id, body));
            }

            void WriteObjectWithStream(int id, string dict, byte[] streamData)
            {
                while (offsets.Count < id) offsets.Add(0);
                offsets[id - 1] = ms.Position;
                WriteString(string.Format("{0} 0 obj\n{1}\nstream\n", id, dict));
                ms.Write(streamData, 0, streamData.Length);
                WriteString("\nendobj\n");
            }

            WriteString("%PDF-1.4\n%\xE2\xE3\xCF\xD3\n");

            int pageCount = pages.Count;
            // Object IDs allocation:
            // 1: Catalog
            // 2: Pages
            // For page i (0-based):
            //   Page obj: 3 + i * 3
            //   Content obj: 3 + i * 3 + 1
            //   Image obj: 3 + i * 3 + 2

            // 1. Catalog
            WriteObject(1, "<< /Type /Catalog /Pages 2 0 R >>");

            // Build Kids array
            var kids = new StringBuilder();
            for (int i = 0; i < pageCount; i++)
            {
                int pageObjId = 3 + i * 3;
                kids.AppendFormat("{0} 0 R ", pageObjId);
            }

            // 2. Pages
            WriteObject(2, string.Format("<< /Type /Pages /Kids [{0}] /Count {1} >>", kids.ToString().Trim(), pageCount));

            // Write each page
            for (int i = 0; i < pageCount; i++)
            {
                var page = pages[i];
                int pageObjId = 3 + i * 3;
                int contentObjId = 3 + i * 3 + 1;
                int imageObjId = 3 + i * 3 + 2;

                // Points calculation: 72 points per inch. Assuming 300 DPI image:
                // widthPt = pixelWidth * 72 / 300 = pixelWidth * 0.24
                // Or if we want standard A4 (595.28 x 841.89) or scaled to aspect ratio:
                double widthPt = page.PixelWidth * 72.0 / 300.0;
                double heightPt = page.PixelHeight * 72.0 / 300.0;

                string wStr = widthPt.ToString("0.##", System.Globalization.CultureInfo.InvariantCulture);
                string hStr = heightPt.ToString("0.##", System.Globalization.CultureInfo.InvariantCulture);

                // Page Object
                string pageDict = string.Format(
                    System.Globalization.CultureInfo.InvariantCulture,
                    "<< /Type /Page /Parent 2 0 R /MediaBox [0 0 {0} {1}] /Contents {2} 0 R /Resources << /XObject << /Im1 {3} 0 R >> >> >>",
                    wStr, hStr, contentObjId, imageObjId);
                WriteObject(pageObjId, pageDict);

                // Content stream (scales and draws the image)
                string contentStream = string.Format(
                    System.Globalization.CultureInfo.InvariantCulture,
                    "q\n{0} 0 0 {1} 0 0 cm\n/Im1 Do\nQ\n",
                    wStr, hStr);
                byte[] contentBytes = Encoding.ASCII.GetBytes(contentStream);
                WriteObjectWithStream(contentObjId, "<< /Length " + contentBytes.Length + " >>", contentBytes);

                // Image Object
                string imgDict = string.Format(
                    System.Globalization.CultureInfo.InvariantCulture,
                    "<< /Type /XObject /Subtype /Image /Width {0} /Height {1} /ColorSpace /DeviceRGB /BitsPerComponent 8 /Filter /DCTDecode /Length {2} >>",
                    page.PixelWidth, page.PixelHeight, page.JpegBytes.Length);
                WriteObjectWithStream(imageObjId, imgDict, page.JpegBytes);
            }

            int totalObjects = 2 + pageCount * 3;
            long xrefOffset = ms.Position;

            WriteString(string.Format("xref\n0 {0}\n", totalObjects + 1));
            WriteString("0000000000 65535 f \n");
            for (int i = 0; i < totalObjects; i++)
            {
                WriteString(string.Format("{0:D10} 00000 n \n", offsets[i]));
            }

            WriteString(string.Format(
                "trailer\n<< /Size {0} /Root 1 0 R >>\nstartxref\n{1}\n%%EOF\n",
                totalObjects + 1, xrefOffset));

            return ms.ToArray();
        }

        public static byte[] CreateSinglePagePdf(byte[] jpegBytes, int pixelWidth, int pixelHeight)
        {
            var list = new List<PageImageInfo>
            {
                new PageImageInfo { JpegBytes = jpegBytes, PixelWidth = pixelWidth, PixelHeight = pixelHeight }
            };
            return CreateMultiPagePdf(list);
        }
    }
}
