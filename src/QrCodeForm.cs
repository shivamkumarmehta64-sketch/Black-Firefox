using System;
using System.Collections.Generic;
using System.Drawing;
using System.Text;
using System.Windows.Forms;

namespace BlackBrowser
{
    public class QrCodeForm : Form
    {
        private readonly PictureBox qrBox;
        private readonly TextBox urlBox;

        public QrCodeForm(string url)
        {
            string safeUrl = url ?? "";
            this.Text = "QR Code — Black Firefox";
            this.StartPosition = FormStartPosition.CenterParent;
            this.FormBorderStyle = FormBorderStyle.FixedDialog;
            this.MaximizeBox = false;
            this.MinimizeBox = false;
            this.BackColor = Color.FromArgb(22, 24, 32);
            this.ForeColor = Color.White;
            this.ClientSize = new Size(340, 430);
            this.Font = new Font("Segoe UI", 10f);
            this.ShowInTaskbar = false;

            Label title = new Label
            {
                Text = "Scan QR to open on phone (Offline)",
                ForeColor = Color.FromArgb(232, 234, 237),
                Location = new Point(20, 12),
                Size = new Size(300, 24),
                TextAlign = ContentAlignment.MiddleCenter,
                Font = new Font("Segoe UI", 10f, FontStyle.Bold)
            };

            qrBox = new PictureBox
            {
                SizeMode = PictureBoxSizeMode.Zoom,
                Location = new Point(20, 44),
                Size = new Size(300, 300),
                BackColor = Color.White
            };

            urlBox = new TextBox
            {
                ReadOnly = true,
                TabStop = false,
                Location = new Point(20, 354),
                Size = new Size(202, 28),
                BackColor = Color.FromArgb(34, 36, 46),
                ForeColor = Color.FromArgb(215, 222, 240),
                BorderStyle = BorderStyle.FixedSingle,
                Font = new Font("Segoe UI", 9.5f),
                Text = safeUrl
            };

            Button copyBtn = new Button
            {
                Text = "Copy URL",
                Location = new Point(230, 353),
                Size = new Size(90, 30),
                BackColor = Color.FromArgb(12, 99, 232),
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat,
                Font = new Font("Segoe UI", 9.5f, FontStyle.Bold),
                Cursor = Cursors.Hand
            };
            copyBtn.FlatAppearance.BorderSize = 0;
            copyBtn.Click += (s, e) => { try { Clipboard.SetText(urlBox.Text); } catch { } };
            this.Shown += (s, e) => { urlBox.SelectionStart = 0; urlBox.SelectionLength = 0; copyBtn.Focus(); };

            this.Controls.Add(title);
            this.Controls.Add(qrBox);
            this.Controls.Add(urlBox);
            this.Controls.Add(copyBtn);

            try
            {
                qrBox.Image = OfflineQrEncoder.RenderBitmap(safeUrl, 300);
            }
            catch
            {
                title.Text = "⚠️ URL too long for offline QR";
            }
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                try
                {
                    if (qrBox != null && qrBox.Image != null)
                    {
                        qrBox.Image.Dispose();
                        qrBox.Image = null;
                    }
                }
                catch { }
            }
            base.Dispose(disposing);
        }
    }

    // Minimal ISO/IEC 18004 Byte-mode QR Code encoder (ECC-L, Versions 1-12, zero external network calls)
    public static class OfflineQrEncoder
    {
        private static readonly byte[] GfExp = new byte[512];
        private static readonly byte[] GfLog = new byte[256];

        // Version 1..12 ECC-L block descriptors: { totalDataCodewords, eccPerBlock, b1Count, b1Data, b2Count, b2Data }
        private static readonly int[,] VersionTable = new int[,]
        {
            {  19,  7, 1,  19, 0,  0 }, // V1
            {  34, 10, 1,  34, 0,  0 }, // V2
            {  55, 15, 1,  55, 0,  0 }, // V3
            {  80, 20, 1,  80, 0,  0 }, // V4
            { 108, 26, 1, 108, 0,  0 }, // V5
            { 136, 18, 2,  68, 0,  0 }, // V6
            { 156, 20, 2,  78, 0,  0 }, // V7
            { 194, 24, 2,  97, 0,  0 }, // V8
            { 232, 30, 2, 116, 0,  0 }, // V9
            { 274, 18, 2,  68, 2, 69 }, // V10
            { 324, 20, 4,  81, 0,  0 }, // V11
            { 370, 24, 2,  92, 2, 93 }  // V12
        };

        private static readonly int[][] AlignmentPositions = new int[][]
        {
            new int[0],
            new int[] { 6, 18 },
            new int[] { 6, 22 },
            new int[] { 6, 26 },
            new int[] { 6, 30 },
            new int[] { 6, 34 },
            new int[] { 6, 22, 38 },
            new int[] { 6, 24, 42 },
            new int[] { 6, 26, 46 },
            new int[] { 6, 28, 50 },
            new int[] { 6, 30, 54 },
            new int[] { 6, 32, 58 }
        };

        static OfflineQrEncoder()
        {
            int x = 1;
            for (int i = 0; i < 255; i++)
            {
                GfExp[i] = (byte)x;
                GfLog[x] = (byte)i;
                x <<= 1;
                if ((x & 0x100) != 0) x ^= 0x11D;
            }
            for (int i = 255; i < 512; i++)
            {
                GfExp[i] = GfExp[i - 255];
            }
        }

        public static Bitmap RenderBitmap(string text, int targetPixels)
        {
            bool[,] modules = Encode(text);
            int n = modules.GetLength(0);
            int quiet = 4;
            int total = n + quiet * 2;
            int scale = Math.Max(2, targetPixels / total);
            int imgSize = total * scale;
            Bitmap bmp = new Bitmap(imgSize, imgSize);
            using (Graphics g = Graphics.FromImage(bmp))
            {
                g.Clear(Color.White);
                using (Brush black = new SolidBrush(Color.Black))
                {
                    for (int y = 0; y < n; y++)
                    {
                        for (int x = 0; x < n; x++)
                        {
                            if (modules[y, x])
                            {
                                g.FillRectangle(black, (x + quiet) * scale, (y + quiet) * scale, scale, scale);
                            }
                        }
                    }
                }
            }
            return bmp;
        }

        public static bool[,] Encode(string text)
        {
            byte[] payload = Encoding.UTF8.GetBytes(text ?? "");
            int version = -1;
            for (int v = 1; v <= 12; v++)
            {
                int ccBits = (v >= 10) ? 16 : 8;
                int capBits = VersionTable[v - 1, 0] * 8;
                if (4 + ccBits + payload.Length * 8 <= capBits)
                {
                    version = v;
                    break;
                }
            }
            if (version < 0) throw new ArgumentException("Payload exceeds max supported QR version.");

            int dataBytesTotal = VersionTable[version - 1, 0];
            int eccLen = VersionTable[version - 1, 1];
            int b1Count = VersionTable[version - 1, 2];
            int b1Data = VersionTable[version - 1, 3];
            int b2Count = VersionTable[version - 1, 4];
            int b2Data = VersionTable[version - 1, 5];

            List<int> bits = new List<int>(dataBytesTotal * 8);
            AppendBits(bits, 0x4, 4); // Byte mode
            AppendBits(bits, payload.Length, version >= 10 ? 16 : 8);
            for (int i = 0; i < payload.Length; i++)
                AppendBits(bits, payload[i], 8);

            int maxBits = dataBytesTotal * 8;
            int term = Math.Min(4, maxBits - bits.Count);
            for (int i = 0; i < term; i++) bits.Add(0);
            while ((bits.Count % 8) != 0) bits.Add(0);

            byte[] data = new byte[dataBytesTotal];
            for (int i = 0; i < bits.Count / 8; i++)
            {
                int val = 0;
                for (int b = 0; b < 8; b++) val = (val << 1) | bits[i * 8 + b];
                data[i] = (byte)val;
            }
            bool toggle = true;
            for (int i = bits.Count / 8; i < dataBytesTotal; i++)
            {
                data[i] = (byte)(toggle ? 0xEC : 0x11);
                toggle = !toggle;
            }

            // Split into blocks and compute Reed-Solomon ECC
            int totalBlocks = b1Count + b2Count;
            byte[][] dataBlocks = new byte[totalBlocks][];
            byte[][] eccBlocks = new byte[totalBlocks][];
            byte[] gen = RsGenerator(eccLen);
            int offset = 0;
            for (int i = 0; i < totalBlocks; i++)
            {
                int len = (i < b1Count) ? b1Data : b2Data;
                byte[] blk = new byte[len];
                Array.Copy(data, offset, blk, 0, len);
                offset += len;
                dataBlocks[i] = blk;
                eccBlocks[i] = RsCompute(blk, gen);
            }

            List<byte> interleaved = new List<byte>(dataBytesTotal + totalBlocks * eccLen);
            int maxDataLen = Math.Max(b1Data, b2Data);
            for (int i = 0; i < maxDataLen; i++)
                for (int b = 0; b < totalBlocks; b++)
                    if (i < dataBlocks[b].Length) interleaved.Add(dataBlocks[b][i]);

            for (int i = 0; i < eccLen; i++)
                for (int b = 0; b < totalBlocks; b++)
                    interleaved.Add(eccBlocks[b][i]);

            int size = 17 + 4 * version;
            bool[,] bestMatrix = null;
            int bestPenalty = int.MaxValue;

            for (int mask = 0; mask < 8; mask++)
            {
                bool[,] m = new bool[size, size];
                bool[,] isFunc = new bool[size, size];
                DrawFunctionPatterns(m, isFunc, version, size);
                PlaceDataBits(m, isFunc, interleaved, size, mask);
                DrawFormatBits(m, isFunc, size, mask);
                if (version >= 7) DrawVersionBits(m, isFunc, version, size);

                int pen = ComputePenalty(m, size);
                if (pen < bestPenalty)
                {
                    bestPenalty = pen;
                    bestMatrix = m;
                }
            }
            return bestMatrix;
        }

        private static void AppendBits(List<int> bits, int val, int len)
        {
            for (int i = len - 1; i >= 0; i--)
                bits.Add((val >> i) & 1);
        }

        private static byte GfMul(byte a, byte b)
        {
            if (a == 0 || b == 0) return 0;
            return GfExp[GfLog[a] + GfLog[b]];
        }

        private static byte[] RsGenerator(int degree)
        {
            byte[] poly = new byte[degree];
            poly[degree - 1] = 1;
            byte root = 1;
            for (int i = 0; i < degree; i++)
            {
                for (int j = 0; j < degree; j++)
                {
                    poly[j] = GfMul(poly[j], root);
                    if (j + 1 < degree) poly[j] ^= poly[j + 1];
                }
                root = GfMul(root, 2);
            }
            return poly;
        }

        private static byte[] RsCompute(byte[] data, byte[] gen)
        {
            int eccLen = gen.Length;
            byte[] res = new byte[eccLen];
            for (int i = 0; i < data.Length; i++)
            {
                byte factor = (byte)(data[i] ^ res[0]);
                Array.Copy(res, 1, res, 0, eccLen - 1);
                res[eccLen - 1] = 0;
                for (int j = 0; j < eccLen; j++)
                    res[j] ^= GfMul(gen[j], factor);
            }
            return res;
        }

        private static void SetFunc(bool[,] m, bool[,] isFunc, int y, int x, bool dark)
        {
            m[y, x] = dark;
            isFunc[y, x] = true;
        }

        private static void DrawFinder(bool[,] m, bool[,] isFunc, int cy, int cx, int size)
        {
            for (int dy = -4; dy <= 4; dy++)
            {
                for (int dx = -4; dx <= 4; dx++)
                {
                    int y = cy + dy;
                    int x = cx + dx;
                    if (y < 0 || y >= size || x < 0 || x >= size) continue;
                    int dist = Math.Max(Math.Abs(dx), Math.Abs(dy));
                    SetFunc(m, isFunc, y, x, dist != 2 && dist != 4);
                }
            }
        }

        private static void DrawAlignment(bool[,] m, bool[,] isFunc, int cy, int cx)
        {
            for (int dy = -2; dy <= 2; dy++)
            {
                for (int dx = -2; dx <= 2; dx++)
                {
                    int dist = Math.Max(Math.Abs(dx), Math.Abs(dy));
                    SetFunc(m, isFunc, cy + dy, cx + dx, dist != 1);
                }
            }
        }

        private static void DrawFunctionPatterns(bool[,] m, bool[,] isFunc, int version, int size)
        {
            DrawFinder(m, isFunc, 3, 3, size);
            DrawFinder(m, isFunc, 3, size - 4, size);
            DrawFinder(m, isFunc, size - 4, 3, size);

            for (int i = 8; i < size - 8; i++)
            {
                bool dark = (i % 2) == 0;
                SetFunc(m, isFunc, 6, i, dark);
                SetFunc(m, isFunc, i, 6, dark);
            }

            int[] align = AlignmentPositions[version - 1];
            for (int i = 0; i < align.Length; i++)
            {
                for (int j = 0; j < align.Length; j++)
                {
                    if ((i == 0 && j == 0) || (i == 0 && j == align.Length - 1) || (i == align.Length - 1 && j == 0))
                        continue;
                    DrawAlignment(m, isFunc, align[i], align[j]);
                }
            }

            // Reserve format areas + dark module
            for (int i = 0; i <= 8; i++)
            {
                if (i != 6) { isFunc[8, i] = true; isFunc[i, 8] = true; }
            }
            for (int i = 0; i < 8; i++)
            {
                isFunc[8, size - 1 - i] = true;
                if (i < 7) isFunc[size - 1 - i, 8] = true;
            }
            SetFunc(m, isFunc, size - 8, 8, true);

            if (version >= 7)
            {
                for (int i = 0; i < 6; i++)
                {
                    for (int j = 0; j < 3; j++)
                    {
                        isFunc[i, size - 11 + j] = true;
                        isFunc[size - 11 + j, i] = true;
                    }
                }
            }
        }

        private static void DrawFormatBits(bool[,] m, bool[,] isFunc, int size, int mask)
        {
            int data = (1 << 3) | mask; // ECC-L = 01
            int rem = data;
            for (int i = 0; i < 10; i++) rem = (rem << 1) ^ (((rem >> 9) & 1) * 0x537);
            int bits = ((data << 10) | rem) ^ 0x5412;

            // Top-left copy (MSB bit 14 at (8,0) .. LSB bit 0 at (0,8))
            for (int i = 0; i <= 5; i++) SetFunc(m, isFunc, 8, i, ((bits >> (14 - i)) & 1) != 0);
            SetFunc(m, isFunc, 8, 7, ((bits >> 8) & 1) != 0);
            SetFunc(m, isFunc, 8, 8, ((bits >> 7) & 1) != 0);
            SetFunc(m, isFunc, 7, 8, ((bits >> 6) & 1) != 0);
            for (int i = 0; i <= 5; i++) SetFunc(m, isFunc, 5 - i, 8, ((bits >> (5 - i)) & 1) != 0);

            // Split copy (MSB bits 14..8 at bottom-left col 8; LSB bits 7..0 at top-right row 8)
            for (int i = 0; i < 7; i++) SetFunc(m, isFunc, size - 1 - i, 8, ((bits >> (14 - i)) & 1) != 0);
            for (int i = 0; i < 8; i++) SetFunc(m, isFunc, 8, size - 8 + i, ((bits >> (7 - i)) & 1) != 0);
        }

        private static void DrawVersionBits(bool[,] m, bool[,] isFunc, int version, int size)
        {
            int rem = version;
            for (int i = 0; i < 12; i++) rem = (rem << 1) ^ (((rem >> 11) & 1) * 0x1F25);
            int bits = (version << 12) | rem;
            for (int i = 0; i < 18; i++)
            {
                bool bit = ((bits >> i) & 1) != 0;
                int a = size - 11 + (i % 3);
                int b = i / 3;
                SetFunc(m, isFunc, b, a, bit);
                SetFunc(m, isFunc, a, b, bit);
            }
        }

        private static bool MaskBit(int mask, int y, int x)
        {
            switch (mask)
            {
                case 0: return ((y + x) % 2) == 0;
                case 1: return (y % 2) == 0;
                case 2: return (x % 3) == 0;
                case 3: return ((y + x) % 3) == 0;
                case 4: return (((y / 2) + (x / 3)) % 2) == 0;
                case 5: return ((y * x) % 2 + (y * x) % 3) == 0;
                case 6: return (((y * x) % 2 + (y * x) % 3) % 2) == 0;
                default: return (((y + x) % 2 + (y * x) % 3) % 2) == 0;
            }
        }

        private static void PlaceDataBits(bool[,] m, bool[,] isFunc, List<byte> bytes, int size, int mask)
        {
            int bitIdx = 0;
            int totalBits = bytes.Count * 8;
            for (int right = size - 1; right >= 1; right -= 2)
            {
                if (right == 6) right = 5;
                for (int vert = 0; vert < size; vert++)
                {
                    for (int j = 0; j < 2; j++)
                    {
                        int x = right - j;
                        bool upward = ((right + 1) & 2) == 0;
                        int y = upward ? (size - 1 - vert) : vert;
                        if (!isFunc[y, x])
                        {
                            bool dark = false;
                            if (bitIdx < totalBits)
                            {
                                dark = ((bytes[bitIdx >> 3] >> (7 - (bitIdx & 7))) & 1) != 0;
                                bitIdx++;
                            }
                            m[y, x] = dark ^ MaskBit(mask, y, x);
                        }
                    }
                }
            }
        }

        private static int ComputePenalty(bool[,] m, int size)
        {
            int penalty = 0;
            // Rule 1: horizontal & vertical runs
            for (int y = 0; y < size; y++)
            {
                int runX = 1, runY = 1;
                for (int x = 1; x < size; x++)
                {
                    if (m[y, x] == m[y, x - 1]) runX++;
                    else { if (runX >= 5) penalty += 3 + (runX - 5); runX = 1; }
                    if (m[x, y] == m[x - 1, y]) runY++;
                    else { if (runY >= 5) penalty += 3 + (runY - 5); runY = 1; }
                }
                if (runX >= 5) penalty += 3 + (runX - 5);
                if (runY >= 5) penalty += 3 + (runY - 5);
            }
            // Rule 2: 2x2 blocks
            for (int y = 0; y < size - 1; y++)
                for (int x = 0; x < size - 1; x++)
                    if (m[y, x] == m[y, x + 1] && m[y, x] == m[y + 1, x] && m[y, x] == m[y + 1, x + 1])
                        penalty += 3;
            // Rule 4: dark ratio
            int darkCount = 0;
            for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                    if (m[y, x]) darkCount++;
            int pct = (darkCount * 100) / (size * size);
            penalty += (Math.Abs(pct - 50) / 5) * 10;
            return penalty;
        }
    }
}