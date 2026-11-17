Add-Type -AssemblyName PresentationCore, PresentationFramework, WindowsBase, System.Drawing

$source = @"
using System;
using System.Collections.Generic;
using System.IO;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;

public class AppIconGenerator
{
    public static void Generate(string outputPath, string previewPngPath)
    {
        int[] sizes = new int[] { 256, 128, 64, 48, 32, 16 };
        var pngStreams = new List<byte[]>();

        foreach (int size in sizes)
        {
            byte[] bytes = RenderSize(size);
            pngStreams.Add(bytes);
            if (size == 256 && !string.IsNullOrEmpty(previewPngPath))
            {
                File.WriteAllBytes(previewPngPath, bytes);
            }
        }

        // Write ICO file
        using (var fs = new FileStream(outputPath, FileMode.Create, FileAccess.Write))
        using (var bw = new BinaryWriter(fs))
        {
            // ICO Header: 6 bytes
            bw.Write((ushort)0); // reserved
            bw.Write((ushort)1); // type 1 = icon
            bw.Write((ushort)sizes.Length); // count

            // Header offset: 6 + 16 * count
            int offset = 6 + (16 * sizes.Length);

            for (int i = 0; i < sizes.Length; i++)
            {
                int sz = sizes[i];
                byte[] data = pngStreams[i];

                bw.Write((byte)(sz >= 256 ? 0 : sz)); // width
                bw.Write((byte)(sz >= 256 ? 0 : sz)); // height
                bw.Write((byte)0); // color count
                bw.Write((byte)0); // reserved
                bw.Write((ushort)1); // planes
                bw.Write((ushort)32); // bpp
                bw.Write((uint)data.Length); // data size
                bw.Write((uint)offset); // offset

                offset += data.Length;
            }

            // Write image byte streams
            for (int i = 0; i < sizes.Length; i++)
            {
                bw.Write(pngStreams[i]);
            }
        }
    }

    private static byte[] RenderSize(int targetSize)
    {
        double baseSize = 256.0;
        double scale = targetSize / baseSize;

        var visual = new DrawingVisual();
        using (var dc = visual.RenderOpen())
        {
            dc.PushTransform(new ScaleTransform(scale, scale));

            // 1. Squircle background (rounded card)
            var bgBrush = new LinearGradientBrush(
                Color.FromRgb(15, 23, 42),   // #0F172A
                Color.FromRgb(30, 41, 59),   // #1E293B
                new Point(0, 0),
                new Point(1, 1)
            );
            var borderPen = new Pen(
                new LinearGradientBrush(
                    Color.FromArgb(240, 56, 189, 248),  // Sky 400
                    Color.FromArgb(240, 245, 158, 11),  // Amber 500
                    new Point(0, 0),
                    new Point(1, 1)
                ),
                3.5
            );
            dc.DrawRoundedRectangle(bgBrush, borderPen, new Rect(6, 6, 244, 244), 52, 52);

            // Subtle inner glow
            var innerGlowPen = new Pen(new SolidColorBrush(Color.FromArgb(25, 255, 255, 255)), 1.5);
            dc.DrawRoundedRectangle(null, innerGlowPen, new Rect(10, 10, 236, 236), 48, 48);

            // 2. Certificate Paper
            var paperRect = new Rect(44, 52, 168, 162);
            var paperBrush = new LinearGradientBrush(
                Color.FromRgb(255, 255, 255),
                Color.FromRgb(241, 245, 249),
                new Point(0, 0),
                new Point(0, 1)
            );
            var paperShadow = new Pen(new SolidColorBrush(Color.FromArgb(40, 0, 0, 0)), 3.0);
            dc.DrawRoundedRectangle(null, paperShadow, new Rect(43, 53, 170, 163), 12, 12);
            dc.DrawRoundedRectangle(paperBrush, null, paperRect, 10, 10);

            // Paper outer gold trim
            var goldPen = new Pen(new SolidColorBrush(Color.FromRgb(217, 119, 6)), 1.5); // Amber-600
            dc.DrawRoundedRectangle(null, goldPen, new Rect(52, 60, 152, 146), 6, 6);

            // Inner thin gold frame
            var thinGoldPen = new Pen(new SolidColorBrush(Color.FromArgb(120, 245, 158, 11)), 1.0);
            dc.DrawRoundedRectangle(null, thinGoldPen, new Rect(56, 64, 144, 138), 4, 4);

            // Certificate title accent bar (Blue)
            var titleBrush = new LinearGradientBrush(
                Color.FromRgb(37, 99, 235), // Blue 600
                Color.FromRgb(30, 64, 175), // Blue 800
                new Point(0, 0),
                new Point(1, 0)
            );
            dc.DrawRoundedRectangle(titleBrush, null, new Rect(80, 74, 96, 12), 6, 6);

            // Certificate text lines
            var textLineBrush = new SolidColorBrush(Color.FromRgb(148, 163, 184)); // Slate 400
            var lightLineBrush = new SolidColorBrush(Color.FromRgb(203, 213, 225)); // Slate 300
            dc.DrawRoundedRectangle(textLineBrush, null, new Rect(68, 96, 120, 5), 2.5, 2.5);
            dc.DrawRoundedRectangle(lightLineBrush, null, new Rect(76, 108, 104, 5), 2.5, 2.5);
            dc.DrawRoundedRectangle(lightLineBrush, null, new Rect(72, 120, 112, 5), 2.5, 2.5);
            dc.DrawRoundedRectangle(lightLineBrush, null, new Rect(84, 132, 88, 5), 2.5, 2.5);

            // 3. Gold Seal Medallion with Ribbon at bottom center
            // Ribbon tails
            var ribbonGeo = new StreamGeometry();
            using (var ctx = ribbonGeo.Open())
            {
                // Left ribbon tail
                ctx.BeginFigure(new Point(116, 160), true, true);
                ctx.LineTo(new Point(102, 196), true, false);
                ctx.LineTo(new Point(114, 190), true, false);
                ctx.LineTo(new Point(124, 196), true, false);
                ctx.LineTo(new Point(122, 160), true, false);

                // Right ribbon tail
                ctx.BeginFigure(new Point(134, 160), true, true);
                ctx.LineTo(new Point(132, 196), true, false);
                ctx.LineTo(new Point(142, 190), true, false);
                ctx.LineTo(new Point(154, 196), true, false);
                ctx.LineTo(new Point(140, 160), true, false);
            }
            ribbonGeo.Freeze();
            var ribbonBrush = new LinearGradientBrush(
                Color.FromRgb(220, 38, 38), // Red 600
                Color.FromRgb(185, 28, 28), // Red 700
                new Point(0, 0),
                new Point(0, 1)
            );
            dc.DrawGeometry(ribbonBrush, new Pen(new SolidColorBrush(Color.FromRgb(153, 27, 27)), 1.0), ribbonGeo);

            // Gold seal outer medal
            var sealCenter = new Point(128, 160);
            var goldRadial = new RadialGradientBrush(
                Color.FromRgb(254, 240, 138), // Yellow 200
                Color.FromRgb(217, 119, 6)    // Amber 600
            );
            goldRadial.Center = new Point(0.4, 0.4);
            goldRadial.GradientOrigin = new Point(0.4, 0.4);
            var sealBorderPen = new Pen(new SolidColorBrush(Color.FromRgb(180, 83, 9)), 2.0); // Amber 700
            dc.DrawEllipse(goldRadial, sealBorderPen, sealCenter, 22, 22);

            // Gold seal inner ring
            var sealInnerPen = new Pen(new SolidColorBrush(Color.FromArgb(200, 254, 240, 138)), 1.2);
            dc.DrawEllipse(null, sealInnerPen, sealCenter, 16, 16);

            // Gold seal star in center
            var starGeo = new StreamGeometry();
            using (var ctx = starGeo.Open())
            {
                Point[] starPts = new Point[10];
                double rOuter = 9.0;
                double rInner = 4.2;
                for (int i = 0; i < 10; i++)
                {
                    double angle = -Math.PI / 2.0 + (i * Math.PI / 5.0);
                    double r = (i % 2 == 0) ? rOuter : rInner;
                    starPts[i] = new Point(sealCenter.X + r * Math.Cos(angle), sealCenter.Y + r * Math.Sin(angle));
                }
                ctx.BeginFigure(starPts[0], true, true);
                for (int i = 1; i < 10; i++)
                {
                    ctx.LineTo(starPts[i], true, false);
                }
            }
            starGeo.Freeze();
            dc.DrawGeometry(new SolidColorBrush(Color.FromRgb(255, 255, 255)), null, starGeo);

            // 4. Academic Graduate Cap (Mortarboard) on top
            // Cap skullcap base
            var skullGeo = new StreamGeometry();
            using (var ctx = skullGeo.Open())
            {
                ctx.BeginFigure(new Point(98, 44), true, true);
                ctx.QuadraticBezierTo(new Point(128, 56), new Point(158, 44), true, false);
                ctx.LineTo(new Point(156, 52), true, false);
                ctx.QuadraticBezierTo(new Point(128, 66), new Point(100, 52), true, false);
            }
            skullGeo.Freeze();
            dc.DrawGeometry(new SolidColorBrush(Color.FromRgb(15, 23, 42)), null, skullGeo);

            // Cap diamond top
            var capGeo = new StreamGeometry();
            using (var ctx = capGeo.Open())
            {
                ctx.BeginFigure(new Point(128, 18), true, true); // Top tip
                ctx.LineTo(new Point(190, 36), true, false);     // Right tip
                ctx.LineTo(new Point(128, 52), true, false);     // Bottom tip
                ctx.LineTo(new Point(66, 36), true, false);      // Left tip
            }
            capGeo.Freeze();

            var capBrush = new LinearGradientBrush(
                Color.FromRgb(51, 65, 85),  // Slate 700
                Color.FromRgb(15, 23, 42),  // Slate 900
                new Point(0, 0),
                new Point(1, 1)
            );
            var capPen = new Pen(new SolidColorBrush(Color.FromRgb(71, 85, 105)), 1.5);
            dc.DrawGeometry(capBrush, capPen, capGeo);

            // Cap center button
            var buttonBrush = new RadialGradientBrush(Color.FromRgb(254, 240, 138), Color.FromRgb(217, 119, 6));
            dc.DrawEllipse(buttonBrush, null, new Point(128, 35), 4.5, 4.5);

            // Golden tassel draping to left
            var tasselGeo = new StreamGeometry();
            using (var ctx = tasselGeo.Open())
            {
                ctx.BeginFigure(new Point(128, 35), false, false);
                ctx.QuadraticBezierTo(new Point(88, 38), new Point(78, 62), true, false);
            }
            tasselGeo.Freeze();
            var tasselPen = new Pen(new SolidColorBrush(Color.FromRgb(245, 158, 11)), 2.2);
            dc.DrawGeometry(null, tasselPen, tasselGeo);

            // Tassel fringe hanging down
            var fringeRect = new Rect(74, 62, 8, 16);
            var fringeBrush = new LinearGradientBrush(
                Color.FromRgb(251, 191, 36),
                Color.FromRgb(217, 119, 6),
                new Point(0, 0),
                new Point(0, 1)
            );
            dc.DrawRoundedRectangle(fringeBrush, null, fringeRect, 2, 2);
            dc.DrawEllipse(new SolidColorBrush(Color.FromRgb(254, 240, 138)), null, new Point(78, 62), 3, 3);

            dc.Pop(); // ScaleTransform
        }

        var rtb = new RenderTargetBitmap(targetSize, targetSize, 96, 96, PixelFormats.Pbgra32);
        rtb.Render(visual);

        var encoder = new PngBitmapEncoder();
        encoder.Frames.Add(BitmapFrame.Create(rtb));
        using (var ms = new MemoryStream())
        {
            encoder.Save(ms);
            return ms.ToArray();
        }
    }
}
"@

Add-Type -TypeDefinition $source -ReferencedAssemblies "PresentationCore", "PresentationFramework", "WindowsBase", "System.Xaml"

$projDir = "c:\Repos\Job\Краеведение\DiplomPrintGenerate"
$icoPath = Join-Path $projDir "app.ico"
$pngPath = Join-Path $projDir "app_icon.png"

[AppIconGenerator]::Generate($icoPath, $pngPath)
Write-Host "Generated ICO: $icoPath ($((Get-Item $icoPath).Length) bytes)" -ForegroundColor Green
Write-Host "Generated PNG preview: $pngPath ($((Get-Item $pngPath).Length) bytes)" -ForegroundColor Green
