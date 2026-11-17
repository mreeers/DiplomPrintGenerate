using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using DiplomPrintGenerate.Models;

namespace DiplomPrintGenerate.Services
{
    public class RenderService
    {
        public static BitmapSource RenderDiploma(
            BitmapSource templateImage,
            double canvasWidth,
            double canvasHeight,
            IEnumerable<FieldDefinition> fields,
            IDictionary<string, string> participantData,
            bool isPreview = false)
        {
            if (templateImage == null)
                return null;

            int targetPixelWidth = templateImage.PixelWidth;
            int targetPixelHeight = templateImage.PixelHeight;

            // Fallback if dimensions are invalid
            if (targetPixelWidth <= 0) targetPixelWidth = 2480;
            if (targetPixelHeight <= 0) targetPixelHeight = 3508;

            double scaleX = targetPixelWidth / canvasWidth;
            double scaleY = targetPixelHeight / canvasHeight;

            DrawingVisual visual = new DrawingVisual();
            using (DrawingContext dc = visual.RenderOpen())
            {
                // 1. Draw template image
                dc.DrawImage(templateImage, new Rect(0, 0, targetPixelWidth, targetPixelHeight));

                // 2. Draw each text field
                if (fields != null)
                {
                    foreach (var field in fields)
                    {
                        string textToDraw = null;
                        if (participantData != null && participantData.ContainsKey(field.Name))
                        {
                            textToDraw = participantData[field.Name];
                        }

                        if (string.IsNullOrEmpty(textToDraw))
                        {
                            textToDraw = isPreview ? field.SampleText : "";
                        }

                        if (string.IsNullOrEmpty(textToDraw))
                            continue;

                        double fieldX = field.X * scaleX;
                        double fieldY = field.Y * scaleY;
                        double fieldW = field.Width * scaleX;
                        double fieldH = field.Height * scaleY;
                        double fieldFontSize = field.FontSize * scaleY;

                        Typeface typeface = new Typeface(
                            new FontFamily(field.FontFamily ?? "Times New Roman"),
                            field.IsItalic ? FontStyles.Italic : FontStyles.Normal,
                            field.IsBold ? FontWeights.Bold : FontWeights.Normal,
                            FontStretches.Normal);

                        Brush brush = field.TextColorBrush;

                        // Create FormattedText
                        FormattedText formattedText = CreateFormattedText(
                            textToDraw,
                            typeface,
                            fieldFontSize,
                            brush,
                            96.0);

                        // Auto-shrink if text overflows width
                        if (field.AutoShrink && formattedText.WidthIncludingTrailingWhitespace > fieldW && fieldW > 10)
                        {
                            double shrinkFactor = (fieldW / formattedText.WidthIncludingTrailingWhitespace) * 0.98;
                            double adjustedFontSize = Math.Max(8.0 * scaleY, fieldFontSize * shrinkFactor);
                            formattedText = CreateFormattedText(
                                textToDraw,
                                typeface,
                                adjustedFontSize,
                                brush,
                                96.0);
                        }

                        // Calculate horizontal position based on alignment
                        double drawX = fieldX;
                        double textWidth = formattedText.WidthIncludingTrailingWhitespace;

                        if (field.Alignment == TextAlignment.Center)
                        {
                            drawX = fieldX + (fieldW - textWidth) / 2.0;
                        }
                        else if (field.Alignment == TextAlignment.Right)
                        {
                            drawX = fieldX + (fieldW - textWidth);
                        }

                        // Vertically center inside field box
                        double textHeight = formattedText.Height;
                        double drawY = fieldY + Math.Max(0, (fieldH - textHeight) / 2.0);

                        dc.DrawText(formattedText, new Point(drawX, drawY));
                    }
                }
            }

            RenderTargetBitmap rtb = new RenderTargetBitmap(
                targetPixelWidth,
                targetPixelHeight,
                96, 96,
                PixelFormats.Pbgra32);

            rtb.Render(visual);
            rtb.Freeze();
            return rtb;
        }

        private static FormattedText CreateFormattedText(
            string text,
            Typeface typeface,
            double fontSize,
            Brush brush,
            double pixelsPerDip)
        {
            return new FormattedText(
                text,
                CultureInfo.CurrentCulture,
                FlowDirection.LeftToRight,
                typeface,
                fontSize,
                brush);
        }

        public static byte[] EncodeToJpeg(BitmapSource bitmap, int quality = 95)
        {
            var encoder = new JpegBitmapEncoder { QualityLevel = quality };
            encoder.Frames.Add(BitmapFrame.Create(bitmap));
            using (var ms = new MemoryStream())
            {
                encoder.Save(ms);
                return ms.ToArray();
            }
        }

        public static byte[] EncodeToPng(BitmapSource bitmap)
        {
            var encoder = new PngBitmapEncoder();
            encoder.Frames.Add(BitmapFrame.Create(bitmap));
            using (var ms = new MemoryStream())
            {
                encoder.Save(ms);
                return ms.ToArray();
            }
        }

        public static void SaveToFile(BitmapSource bitmap, string filePath, bool asJpeg = false)
        {
            byte[] bytes = asJpeg ? EncodeToJpeg(bitmap) : EncodeToPng(bitmap);
            File.WriteAllBytes(filePath, bytes);
        }
    }
}
