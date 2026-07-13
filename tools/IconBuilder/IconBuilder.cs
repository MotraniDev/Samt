using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.IO;
using System.Runtime.InteropServices;

internal static class IconBuilder
{
    private static int Main(string[] args)
    {
        var outPath = args.Length > 0
            ? args[0]
            : Path.GetFullPath(Path.Combine(
                AppContext.BaseDirectory, "..", "..", "..", "..", "src", "Samt.App", "Assets", "AppIcon.ico"));

        Directory.CreateDirectory(Path.GetDirectoryName(outPath)!);

        int[] sizes = { 16, 24, 32, 48, 64, 256 };
        using (var fs = File.Create(outPath))
        using (var bw = new BinaryWriter(fs))
        {
            bw.Write((ushort)0);
            bw.Write((ushort)1);
            bw.Write((ushort)sizes.Length);
            var images = new byte[sizes.Length][];
            for (var i = 0; i < sizes.Length; i++)
            {
                images[i] = RenderBmp(sizes[i]);
            }

            var offset = 6 + 16 * sizes.Length;
            for (var i = 0; i < sizes.Length; i++)
            {
                var dim = sizes[i] >= 256 ? (byte)0 : (byte)sizes[i];
                bw.Write(dim);
                bw.Write(dim);
                bw.Write((byte)0);
                bw.Write((byte)0);
                bw.Write((ushort)1);
                bw.Write((ushort)32);
                bw.Write(images[i].Length);
                bw.Write(offset);
                offset += images[i].Length;
            }

            foreach (var img in images)
            {
                bw.Write(img);
            }
        }

        Console.WriteLine("Wrote " + outPath + " (" + new FileInfo(outPath).Length + " bytes)");

        // Also emit a few PNG sizes for package assets when requested.
        if (args.Length > 1 && string.Equals(args[1], "--pngs", StringComparison.OrdinalIgnoreCase))
        {
            var dir = Path.GetDirectoryName(outPath)!;
            SavePng(RenderBitmap(256), Path.Combine(dir, "StoreLogo.png"));
            SavePng(RenderBitmap(150), Path.Combine(dir, "Square150x150Logo.scale-200.png"));
            SavePng(RenderBitmap(44), Path.Combine(dir, "Square44x44Logo.scale-200.png"));
            SavePng(RenderBitmap(48), Path.Combine(dir, "Square44x44Logo.targetsize-48_altform-lightunplated.png"));
            SavePng(RenderBitmap(24), Path.Combine(dir, "Square44x44Logo.targetsize-24_altform-unplated.png"));
            Console.WriteLine("Wrote package PNG logos");
        }

        return 0;
    }

    private static void SavePng(Bitmap bmp, string path)
    {
        bmp.Save(path, ImageFormat.Png);
        bmp.Dispose();
    }

    private static Bitmap RenderBitmap(int size)
    {
        var bmp = new Bitmap(size, size, PixelFormat.Format32bppArgb);
        using var g = Graphics.FromImage(bmp);
        g.SmoothingMode = SmoothingMode.AntiAlias;
        g.PixelOffsetMode = PixelOffsetMode.HighQuality;
        g.Clear(Color.FromArgb(255, 0x0B, 0x1F, 0x33));
        var gold = Color.FromArgb(255, 0xC4, 0xA3, 0x5A);
        var goldSoft = Color.FromArgb(255, 0xE0, 0xC9, 0x89);
        var m = size / 256f;
        using var pen = new Pen(gold, Math.Max(1.2f, 6f * m));
        using var brush = new SolidBrush(gold);
        using var softBrush = new SolidBrush(goldSoft);

        var pad = 18f * m;
        g.DrawEllipse(pen, pad, pad, size - 2 * pad, size - 2 * pad);

        var cx = size * 0.5f;
        var baseY = size * 0.62f;
        var baseW = size * 0.42f;
        var baseH = size * 0.12f;
        g.FillRectangle(brush, cx - baseW / 2, baseY, baseW, baseH);

        var domeW = size * 0.38f;
        var domeH = size * 0.28f;
        var domeX = cx - domeW / 2;
        var domeY = baseY - domeH * 0.85f;
        using (var path = new GraphicsPath())
        {
            path.AddArc(domeX, domeY, domeW, domeH, 180, 180);
            path.CloseFigure();
            g.FillPath(brush, path);
        }

        var cSize = size * 0.28f;
        var cX = cx + size * 0.02f;
        var cY = size * 0.18f;
        using (var path = new GraphicsPath())
        {
            path.AddEllipse(cX - cSize / 2, cY, cSize, cSize);
            using var region = new Region(path);
            using (var cut = new GraphicsPath())
            {
                cut.AddEllipse(cX - cSize * 0.15f, cY - cSize * 0.05f, cSize * 0.85f, cSize * 0.85f);
                region.Exclude(cut);
            }

            g.FillRegion(softBrush, region);
        }

        var finR = Math.Max(1.5f, 4f * m);
        g.FillEllipse(softBrush, cx - finR, domeY - finR * 1.2f, finR * 2, finR * 2);
        return bmp;
    }

    private static byte[] RenderBmp(int size)
    {
        using var bmp = RenderBitmap(size);
        var rect = new Rectangle(0, 0, size, size);
        var data = bmp.LockBits(rect, ImageLockMode.ReadOnly, PixelFormat.Format32bppArgb);
        var stride = data.Stride;
        var pixels = new byte[stride * size];
        Marshal.Copy(data.Scan0, pixels, 0, pixels.Length);
        bmp.UnlockBits(data);

        var flipped = new byte[pixels.Length];
        for (var y = 0; y < size; y++)
        {
            Buffer.BlockCopy(pixels, y * stride, flipped, (size - 1 - y) * stride, stride);
        }

        var maskRow = ((size + 31) / 32) * 4;
        var mask = new byte[maskRow * size];

        using var ms = new MemoryStream();
        using (var bw = new BinaryWriter(ms))
        {
            bw.Write(40);
            bw.Write(size);
            bw.Write(size * 2);
            bw.Write((short)1);
            bw.Write((short)32);
            bw.Write(0);
            bw.Write(flipped.Length + mask.Length);
            bw.Write(0);
            bw.Write(0);
            bw.Write(0);
            bw.Write(0);
            bw.Write(flipped);
            bw.Write(mask);
        }

        return ms.ToArray();
    }
}
