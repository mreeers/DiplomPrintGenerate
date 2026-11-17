using System;
using System.Globalization;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace DiplomPrintGenerate.Services
{
    public class TemplateGeneratorService
    {
        public static BitmapSource CreateDefaultDiplomaTemplate(int width = 2480, int height = 3508)
        {
            DrawingVisual visual = new DrawingVisual();
            using (DrawingContext dc = visual.RenderOpen())
            {
                // Background cream paper
                var bgBrush = new SolidColorBrush(Color.FromRgb(252, 250, 242));
                dc.DrawRectangle(bgBrush, null, new Rect(0, 0, width, height));

                // Outer decorative border (Navy)
                var navyPen = new Pen(new SolidColorBrush(Color.FromRgb(24, 43, 73)), 8);
                dc.DrawRectangle(null, navyPen, new Rect(70, 70, width - 140, height - 140));

                // Inner decorative border (Gold)
                var goldPen = new Pen(new SolidColorBrush(Color.FromRgb(205, 160, 75)), 4);
                dc.DrawRectangle(null, goldPen, new Rect(95, 95, width - 190, height - 190));

                // Corner decorative ornaments
                DrawCornerOrnament(dc, 95, 95, 60, 60);
                DrawCornerOrnament(dc, width - 155, 95, 60, 60);
                DrawCornerOrnament(dc, 95, height - 155, 60, 60);
                DrawCornerOrnament(dc, width - 155, height - 155, 60, 60);

                // Header icon / laurel badge
                var goldBrush = new SolidColorBrush(Color.FromRgb(205, 160, 75));
                dc.DrawEllipse(goldBrush, null, new Point(width / 2.0, 380), 55, 55);
                var innerBadge = new SolidColorBrush(Color.FromRgb(24, 43, 73));
                dc.DrawEllipse(innerBadge, null, new Point(width / 2.0, 380), 45, 45);

                // Header text "ДИПЛОМ"
                var headerTypeface = new Typeface(new FontFamily("Georgia"), FontStyles.Normal, FontWeights.Bold, FontStretches.Normal);
                var headerText = new FormattedText(
                    "ДИПЛОМ",
                    CultureInfo.CurrentCulture,
                    FlowDirection.LeftToRight,
                    headerTypeface,
                    150,
                    innerBadge);
                dc.DrawText(headerText, new Point((width - headerText.Width) / 2.0, 520));

                // Subheader "НАГРАЖДАЕТСЯ"
                var subTypeface = new Typeface(new FontFamily("Georgia"), FontStyles.Normal, FontWeights.Normal, FontStretches.Normal);
                var subText = new FormattedText(
                    "— НАГРАЖДАЕТСЯ —",
                    CultureInfo.CurrentCulture,
                    FlowDirection.LeftToRight,
                    subTypeface,
                    55,
                    goldBrush);
                dc.DrawText(subText, new Point((width - subText.Width) / 2.0, 730));

                // Decorative horizontal divider
                dc.DrawLine(goldPen, new Point(width / 2.0 - 250, 830), new Point(width / 2.0 + 250, 830));
                dc.DrawEllipse(goldBrush, null, new Point(width / 2.0, 830), 8, 8);

                // Bottom signatures placeholders
                var smallFont = new Typeface(new FontFamily("Times New Roman"), FontStyles.Italic, FontWeights.Normal, FontStretches.Normal);
                var sign1 = new FormattedText("Председатель оргкомитета", CultureInfo.CurrentCulture, FlowDirection.LeftToRight, smallFont, 40, Brushes.Gray);
                dc.DrawText(sign1, new Point(300, height - 450));
                dc.DrawLine(new Pen(Brushes.Gray, 2), new Point(300, height - 370), new Point(800, height - 370));

                var sign2 = new FormattedText("Секретарь", CultureInfo.CurrentCulture, FlowDirection.LeftToRight, smallFont, 40, Brushes.Gray);
                dc.DrawText(sign2, new Point(width - 800, height - 450));
                dc.DrawLine(new Pen(Brushes.Gray, 2), new Point(width - 800, height - 370), new Point(width - 300, height - 370));
            }

            RenderTargetBitmap rtb = new RenderTargetBitmap(width, height, 96, 96, PixelFormats.Pbgra32);
            rtb.Render(visual);
            rtb.Freeze();
            return rtb;
        }

        private static void DrawCornerOrnament(DrawingContext dc, double x, double y, double w, double h)
        {
            var pen = new Pen(new SolidColorBrush(Color.FromRgb(205, 160, 75)), 3);
            dc.DrawRectangle(null, pen, new Rect(x + 5, y + 5, w - 10, h - 10));
        }
    }
}
