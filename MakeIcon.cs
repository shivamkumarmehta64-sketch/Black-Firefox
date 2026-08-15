using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.IO;

class MakeIcon
{
    const int MASTER = 512;

    static void Main()
    {
        string outputIco = @"C:\Users\shiva\projects\Black-Firefox\icon.ico";
        string outputPng = @"C:\Users\shiva\projects\Black-Firefox\icon.png";

        using (Bitmap master = RenderMaster())
        {
            master.Save(outputPng, ImageFormat.Png);

            int[] sizes = new int[] { 256, 128, 96, 64, 48, 32, 16 };

            using (FileStream fs = new FileStream(outputIco, FileMode.Create, FileAccess.Write))
            using (BinaryWriter bw = new BinaryWriter(fs))
            {
                bw.Write((ushort)0); // Reserved
                bw.Write((ushort)1); // Type: ICO
                bw.Write((ushort)sizes.Length); // Count

                int offset = 6 + (16 * sizes.Length);

                byte[][] pngBuffers = new byte[sizes.Length][];

                for (int i = 0; i < sizes.Length; i++)
                {
                    int sz = sizes[i];
                    using (Bitmap resized = new Bitmap(sz, sz, PixelFormat.Format32bppArgb))
                    {
                        using (Graphics g = Graphics.FromImage(resized))
                        {
                            g.InterpolationMode = InterpolationMode.HighQualityBicubic;
                            g.SmoothingMode = SmoothingMode.HighQuality;
                            g.PixelOffsetMode = PixelOffsetMode.HighQuality;
                            g.CompositingQuality = CompositingQuality.HighQuality;
                            g.DrawImage(master, 0, 0, sz, sz);
                        }

                        using (MemoryStream ms = new MemoryStream())
                        {
                            resized.Save(ms, ImageFormat.Png);
                            pngBuffers[i] = ms.ToArray();
                        }
                    }

                    int bWidth = sz >= 256 ? 0 : sz;
                    int bHeight = sz >= 256 ? 0 : sz;

                    bw.Write((byte)bWidth);
                    bw.Write((byte)bHeight);
                    bw.Write((byte)0); // Colors
                    bw.Write((byte)0); // Reserved
                    bw.Write((ushort)1); // Planes
                    bw.Write((ushort)32); // BPP
                    bw.Write((uint)pngBuffers[i].Length); // Size
                    bw.Write((uint)offset); // Offset

                    offset += pngBuffers[i].Length;
                }

                for (int i = 0; i < sizes.Length; i++)
                {
                    bw.Write(pngBuffers[i]);
                }
            }

            Console.WriteLine("Black Firefox icon written: " + outputIco + " (" + new FileInfo(outputIco).Length + " bytes)");
        }
    }

    static Bitmap RenderMaster()
    {
        Bitmap bmp = new Bitmap(MASTER, MASTER, PixelFormat.Format32bppArgb);
        using (Graphics g = Graphics.FromImage(bmp))
        {
            g.SmoothingMode = SmoothingMode.HighQuality;
            g.PixelOffsetMode = PixelOffsetMode.HighQuality;
            g.CompositingQuality = CompositingQuality.HighQuality;
            g.Clear(Color.Transparent);

            float tilePad = 24;
            RectangleF tile = new RectangleF(tilePad, tilePad, MASTER - tilePad * 2, MASTER - tilePad * 2);
            GraphicsPath tilePath = RoundedRect(tile, 96);

            // Tile background: charcoal, clamped so it stays dark
            using (LinearGradientBrush bg = new LinearGradientBrush(tile,
                Color.FromArgb(26, 30, 42), Color.FromArgb(4, 5, 9), 90f))
            {
                g.FillPath(bg, tilePath);
            }

            // Border
            using (Pen border = new Pen(Color.FromArgb(70, 255, 255, 255), 3))
            { border.LineJoin = LineJoin.Round; g.DrawPath(border, tilePath); }

            float cx = 256, cy = 292;

            // Ember glow behind fox (no tiling possible on PathGradient)
            using (GraphicsPath glowPath = new GraphicsPath())
            {
                glowPath.AddEllipse(cx - 150, cy - 150, 300, 300);
                PathGradientBrush glow = new PathGradientBrush(glowPath);
                glow.CenterColor = Color.FromArgb(150, 255, 122, 26);
                glow.SurroundColors = new Color[] { Color.FromArgb(0, 255, 122, 26) };
                g.FillPath(glow, glowPath);
                glow.Dispose();
            }

            // Orbit arc (fox wrapping a world) behind lower half
            using (Pen arc = new Pen(Color.FromArgb(110, 255, 122, 26), 16))
            {
                arc.LineJoin = LineJoin.Round;
                arc.StartCap = LineCap.Round; arc.EndCap = LineCap.Round;
                g.DrawArc(arc, cx - 138, cy - 138, 276, 276, 150, 240);
            }

            // Fox head silhouette (ember gradient, clamped)
            PointF[] fox = new PointF[] {
                new PointF(178, 132),  // left ear tip
                new PointF(132, 228),  // left ear outer base
                new PointF(196, 210),  // left ear inner base
                new PointF(256, 176),  // forehead dip
                new PointF(316, 210),  // right ear inner base
                new PointF(380, 228),  // right ear outer base
                new PointF(334, 132),  // right ear tip
                new PointF(408, 306),  // right cheek
                new PointF(330, 392),  // right jaw
                new PointF(256, 430),  // chin
                new PointF(182, 392),  // left jaw
                new PointF(104, 306)   // left cheek
            };

            using (GraphicsPath foxPath = new GraphicsPath())
            {
                foxPath.AddPolygon(fox);
                PathGradientBrush fb = new PathGradientBrush(foxPath);
                fb.CenterColor = Color.FromArgb(255, 255, 158, 60);
                fb.CenterPoint = new PointF(256, 256);
                fb.SurroundColors = new Color[] { Color.FromArgb(255, 226, 92, 20) };
                g.FillPath(fb, foxPath);
                fb.Dispose();
                g.DrawPath(new Pen(Color.FromArgb(70, 255, 205, 130), 3) { LineJoin = LineJoin.Round }, foxPath);
            }

            // Dark inner face mask (fox markings) - narrower so ember cheeks show
            PointF[] mask = new PointF[] {
                new PointF(210, 212),
                new PointF(302, 212),
                new PointF(322, 330),
                new PointF(256, 402),
                new PointF(190, 330)
            };
            using (GraphicsPath maskPath = new GraphicsPath())
            {
                maskPath.AddPolygon(mask);
                g.FillPath(new SolidBrush(Color.FromArgb(255, 46, 16, 2)), maskPath);
            }

            // Eye glints
            using (SolidBrush eye = new SolidBrush(Color.FromArgb(255, 255, 209, 102)))
            {
                g.FillEllipse(eye, 210, 264, 34, 22);
                g.FillEllipse(eye, 268, 264, 34, 22);
            }

            // Nose
            using (GraphicsPath nose = new GraphicsPath())
            {
                nose.AddPolygon(new PointF[] { new PointF(242, 396), new PointF(270, 396), new PointF(256, 418) });
                g.FillPath(new SolidBrush(Color.FromArgb(255, 12, 4, 0)), nose);
            }

            // Ember cheek accents
            using (Pen cheek = new Pen(Color.FromArgb(170, 255, 180, 90), 10))
            {
                cheek.StartCap = LineCap.Round; cheek.EndCap = LineCap.Round;
                g.DrawArc(cheek, 120, 300, 70, 70, 90, 130);
                g.DrawArc(cheek, 322, 300, 70, 70, -40, 130);
            }
        }
        return bmp;
    }

    static GraphicsPath RoundedRect(RectangleF rect, float radius)
    {
        float d = radius * 2;
        GraphicsPath path = new GraphicsPath();
        path.AddArc(rect.X, rect.Y, d, d, 180, 90);
        path.AddArc(rect.Right - d, rect.Y, d, d, 270, 90);
        path.AddArc(rect.Right - d, rect.Bottom - d, d, d, 0, 90);
        path.AddArc(rect.X, rect.Bottom - d, d, d, 90, 90);
        path.CloseFigure();
        return path;
    }
}