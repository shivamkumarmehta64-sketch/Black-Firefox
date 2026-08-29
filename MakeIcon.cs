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
            GraphicsPath tilePath = RoundedRect(tile, 108);

            // 1. Sleek Obsidian Glass Backplate with subtle blue-purple cyber sheen
            using (LinearGradientBrush bg = new LinearGradientBrush(tile,
                Color.FromArgb(28, 30, 44), Color.FromArgb(8, 10, 18), 45f))
            {
                g.FillPath(bg, tilePath);
            }

            // Outer Neon Rim Accent
            using (Pen rimPen = new Pen(Color.FromArgb(100, 255, 110, 20), 3.5f))
            {
                rimPen.LineJoin = LineJoin.Round;
                g.DrawPath(rimPen, tilePath);
            }

            float cx = 256, cy = 268;

            // 2. Cosmic / Cyber Fire Radial Glow
            using (GraphicsPath glowPath = new GraphicsPath())
            {
                glowPath.AddEllipse(cx - 180, cy - 180, 360, 360);
                using (PathGradientBrush glow = new PathGradientBrush(glowPath))
                {
                    glow.CenterColor = Color.FromArgb(160, 255, 95, 0);
                    glow.SurroundColors = new Color[] { Color.FromArgb(0, 160, 30, 240) };
                    g.FillPath(glow, glowPath);
                }
            }

            // 3. Encircling Cyber Flame Tail (Firefox Orbit)
            using (Pen fireTail = new Pen(Color.FromArgb(230, 255, 130, 20), 28f))
            {
                fireTail.StartCap = LineCap.Round;
                fireTail.EndCap = LineCap.Round;
                g.DrawArc(fireTail, cx - 142, cy - 142, 284, 284, 135, 260);
            }

            using (Pen innerFlame = new Pen(Color.FromArgb(255, 255, 210, 70), 12f))
            {
                innerFlame.StartCap = LineCap.Round;
                innerFlame.EndCap = LineCap.Round;
                g.DrawArc(innerFlame, cx - 138, cy - 138, 276, 276, 150, 220);
            }

            // 4. Dark Cyber World Orb
            RectangleF orbRect = new RectangleF(cx - 110, cy - 110, 220, 220);
            using (LinearGradientBrush orbBg = new LinearGradientBrush(orbRect,
                Color.FromArgb(32, 22, 54), Color.FromArgb(12, 10, 22), 90f))
            {
                g.FillEllipse(orbBg, orbRect);
            }
            using (Pen orbBorder = new Pen(Color.FromArgb(120, 130, 80, 255), 2f))
            {
                g.DrawEllipse(orbBorder, orbRect);
            }

            // 5. Stylized Obsidian Fox Face Geometry
            PointF[] foxEarsAndCheeks = new PointF[] {
                new PointF(175, 120),  // Left ear tip
                new PointF(140, 210),  // Left ear outer
                new PointF(195, 195),  // Left ear notch
                new PointF(256, 160),  // Forehead flame crest
                new PointF(317, 195),  // Right ear notch
                new PointF(372, 210),  // Right ear outer
                new PointF(337, 120),  // Right ear tip
                new PointF(395, 285),  // Right flame cheek
                new PointF(325, 375),  // Right jaw
                new PointF(256, 420),  // Chin tip
                new PointF(187, 375),  // Left jaw
                new PointF(117, 285)   // Left flame cheek
            };

            using (GraphicsPath foxPath = new GraphicsPath())
            {
                foxPath.AddPolygon(foxEarsAndCheeks);
                using (LinearGradientBrush foxBrush = new LinearGradientBrush(
                    new RectangleF(117, 120, 278, 300),
                    Color.FromArgb(255, 255, 140, 30),
                    Color.FromArgb(255, 210, 50, 10),
                    60f))
                {
                    g.FillPath(foxBrush, foxPath);
                }

                using (Pen foxOutline = new Pen(Color.FromArgb(255, 255, 220, 140), 3f) { LineJoin = LineJoin.Round })
                {
                    g.DrawPath(foxOutline, foxPath);
                }
            }

            // 6. Black Obsidian Fox Inner Mask
            PointF[] obsidianMask = new PointF[] {
                new PointF(212, 195),
                new PointF(300, 195),
                new PointF(318, 305),
                new PointF(256, 385),
                new PointF(194, 305)
            };
            using (GraphicsPath maskPath = new GraphicsPath())
            {
                maskPath.AddPolygon(obsidianMask);
                using (LinearGradientBrush maskBrush = new LinearGradientBrush(
                    new RectangleF(194, 195, 124, 190),
                    Color.FromArgb(255, 20, 16, 28),
                    Color.FromArgb(255, 8, 6, 12),
                    90f))
                {
                    g.FillPath(maskBrush, maskPath);
                }
            }

            // 7. Piercing Cyber-Gold Eyes
            using (SolidBrush eyeGlow = new SolidBrush(Color.FromArgb(255, 255, 220, 80)))
            {
                g.FillEllipse(eyeGlow, 212, 250, 32, 20);
                g.FillEllipse(eyeGlow, 268, 250, 32, 20);
            }
            using (SolidBrush pupil = new SolidBrush(Color.FromArgb(255, 18, 12, 8)))
            {
                g.FillEllipse(pupil, 222, 252, 12, 16);
                g.FillEllipse(pupil, 278, 252, 12, 16);
            }

            // 8. Nose Tip
            using (GraphicsPath nose = new GraphicsPath())
            {
                nose.AddPolygon(new PointF[] { new PointF(244, 375), new PointF(268, 375), new PointF(256, 396) });
                g.FillPath(new SolidBrush(Color.FromArgb(255, 255, 160, 40)), nose);
            }

            // 9. Cyber Embers Accents
            using (Pen spark = new Pen(Color.FromArgb(200, 255, 240, 160), 6f))
            {
                spark.StartCap = LineCap.Round; spark.EndCap = LineCap.Round;
                g.DrawLine(spark, 100, 190, 106, 190);
                g.DrawLine(spark, 400, 180, 408, 180);
                g.DrawLine(spark, 380, 360, 388, 360);
                g.DrawLine(spark, 130, 370, 136, 370);
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