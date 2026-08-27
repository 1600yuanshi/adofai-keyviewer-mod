# 生成 AgentKeyViewer 键背景测试图片（PNG 透明背景 + JPG + 2帧 GIF 动画）
# 通过 Add-Type 内联 C# 生成，规避 PowerShell 脚本语言的类型解析问题
$ErrorActionPreference = "Stop"
Add-Type -AssemblyName System.Drawing

$outDir = "D:\Steam\steamapps\common\A Dance of Fire and Ice\AgentKeyViewer_config\images"
if (-not (Test-Path $outDir)) { New-Item -ItemType Directory -Force -Path $outDir | Out-Null }

$csharp = @"
using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;

public static class TestImageGen
{
    public static void Generate(string outDir, int size)
    {
        var font = new Font("Arial", 14, FontStyle.Bold);
        var sf = new StringFormat();
        sf.Alignment = StringAlignment.Center;
        sf.LineAlignment = StringAlignment.Center;

        // ---- PNG：透明背景 + 渐变圆角方块 + 圆环 ----
        using (var bmp = new Bitmap(size, size, PixelFormat.Format32bppArgb))
        {
            using (var g = Graphics.FromImage(bmp))
            {
                g.SmoothingMode = SmoothingMode.AntiAlias;
                g.Clear(Color.Transparent);

                var rect = new Rectangle(10, 10, size - 20, size - 20);
                using (var brush = new LinearGradientBrush(rect,
                       Color.FromArgb(255, 30, 120, 220), Color.FromArgb(255, 220, 60, 120),
                       LinearGradientMode.ForwardDiagonal))
                using (var path = RoundedRect(rect, 30))
                {
                    g.FillPath(brush, path);
                }

                using (var pen = new Pen(Color.FromArgb(220, 255, 255, 255), 8))
                    g.DrawEllipse(pen, 40, 40, size - 80, size - 80);
                using (var pen2 = new Pen(Color.FromArgb(120, 255, 255, 255), 4))
                    g.DrawEllipse(pen2, 60, 60, size - 120, size - 120);

                g.DrawString("PNG", font, Brushes.White, new RectangleF(0, 0, size, size), sf);
            }
            bmp.Save(System.IO.Path.Combine(outDir, "demo_png.png"), ImageFormat.Png);
        }

        // ---- JPG：渐变背景 ----
        using (var bmp = new Bitmap(size, size))
        {
            using (var g = Graphics.FromImage(bmp))
            {
                var rect = new Rectangle(0, 0, size, size);
                using (var brush = new LinearGradientBrush(rect,
                       Color.FromArgb(40, 60, 100), Color.FromArgb(200, 160, 60), 45f))
                {
                    g.FillRectangle(brush, rect);
                }
                g.DrawString("JPG", font, Brushes.White, new RectangleF(0, 0, size, size), sf);
            }
            bmp.Save(System.IO.Path.Combine(outDir, "demo_jpg.jpg"), ImageFormat.Jpeg);
        }

        // ---- GIF：2 帧动画（整帧红 -> 整帧绿），纯 C# 手工构造 ----
        int gifSize = 64;
        var gct = new Color[] { Color.White, Color.OrangeRed, Color.ForestGreen, Color.Blue };
        var frames = new int[][]
        {
            Fill(gifSize, 1), // 帧1：全红
            Fill(gifSize, 2)  // 帧2：全绿
        };
        var delays = new int[] { 50, 50 }; // 0.5s / 帧
        var gif = BuildGif(gifSize, gifSize, gct, frames, delays);
        System.IO.File.WriteAllBytes(System.IO.Path.Combine(outDir, "demo_gif.gif"), gif);
    }

    private static int[] Fill(int size, int color)
    {
        var a = new int[size * size];
        for (int i = 0; i < a.Length; i++) a[i] = color;
        return a;
    }

    /// <summary>构造多帧 GIF 字节流（仅基础特性：全局颜色表 + 每帧全幅图 + GCE 延迟）。</summary>
    private static byte[] BuildGif(int w, int h, Color[] gct, int[][] frames, int[] delays)
    {
        var ms = new System.IO.MemoryStream();
        var bw = new System.IO.BinaryWriter(ms);

        // Header
        bw.Write(System.Text.Encoding.ASCII.GetBytes("GIF89a"));
        // Logical Screen Descriptor
        bw.Write((ushort)w);
        bw.Write((ushort)h);
        int gctSizeBits = 0;
        while ((1 << (gctSizeBits + 1)) < gct.Length) gctSizeBits++;
        bw.Write((byte)(0x80 | gctSizeBits)); // GCT flag + size
        bw.Write((byte)0); // bg color index
        bw.Write((byte)0); // aspect
        // Global Color Table
        foreach (var c in gct) { bw.Write(c.R); bw.Write(c.G); bw.Write(c.B); }

        int minCodeSize = 2; // 4 色
        for (int fi = 0; fi < frames.Length; fi++)
        {
            // Graphic Control Extension
            bw.Write((byte)0x21);
            bw.Write((byte)0xF9);
            bw.Write((byte)0x04);
            bw.Write((byte)0x00); // no transparency, disposal 0
            bw.Write((ushort)delays[fi]);
            bw.Write((byte)0);
            bw.Write((byte)0x00);

            // Image Descriptor（全幅）
            bw.Write((byte)0x2C);
            bw.Write((ushort)0); bw.Write((ushort)0); // left/top
            bw.Write((ushort)w); bw.Write((ushort)h);
            bw.Write((byte)0x00); // no local CT

            // LZW
            bw.Write((byte)minCodeSize);
            var lzw = EncodeLzw(frames[fi], minCodeSize);
            for (int p = 0; p < lzw.Length; p += 255)
            {
                int chunk = Math.Min(255, lzw.Length - p);
                bw.Write((byte)chunk);
                bw.Write(lzw, p, chunk);
            }
            bw.Write((byte)0x00); // block terminator
        }
        bw.Write((byte)0x3B); // Trailer
        bw.Flush();
        return ms.ToArray();
    }

    /// <summary>GIF LZW 编码（变长码，最小编码长度 minCodeSize）。</summary>
    private static byte[] EncodeLzw(int[] indices, int minCodeSize)
    {
        int clearCode = 1 << minCodeSize;
        int endCode = clearCode + 1;
        int codeSize = minCodeSize + 1;
        int nextCode = endCode + 1;

        var dict = new System.Collections.Generic.Dictionary<long, int>(1024);
        for (int i = 0; i < clearCode; i++) dict[((long)-1 * 4096) + i] = i; // 占位，实际用不到

        var bytes = new System.Collections.Generic.List<byte>();
        int bitBuf = 0, bitCnt = 0;
        Action<int> writeCode = (code) =>
        {
            bitBuf |= code << bitCnt;
            bitCnt += codeSize;
            while (bitCnt >= 8) { bytes.Add((byte)(bitBuf & 0xFF)); bitBuf >>= 8; bitCnt -= 8; }
        };

        writeCode(clearCode);
        int prev = indices[0];
        for (int i = 1; i < indices.Length; i++)
        {
            int p = indices[i];
            long key = (long)prev * 4096 + p;
            int entry;
            if (dict.TryGetValue(key, out entry))
            {
                prev = entry;
            }
            else
            {
                writeCode(prev);
                if (nextCode < 4096)
                {
                    dict[key] = nextCode;
                    nextCode++;
                    if (nextCode == (1 << codeSize) && codeSize < 12) codeSize++;
                }
                prev = p;
            }
        }
        writeCode(prev);
        writeCode(endCode);
        // 刷新剩余位
        if (bitCnt > 0) bytes.Add((byte)(bitBuf & 0xFF));
        return bytes.ToArray();
    }

    private static GraphicsPath RoundedRect(Rectangle r, int radius)
    {
        var path = new GraphicsPath();
        int d = 2 * radius;
        path.AddArc(r.X, r.Y, d, d, 180, 90);
        path.AddArc(r.Right - d, r.Y, d, d, 270, 90);
        path.AddArc(r.Right - d, r.Bottom - d, d, d, 0, 90);
        path.AddArc(r.X, r.Bottom - d, d, d, 90, 90);
        path.CloseFigure();
        return path;
    }
}
"@

Add-Type -TypeDefinition $csharp -ReferencedAssemblies System.Drawing
[TestImageGen]::Generate($outDir, 200)

Write-Host "已生成测试图片:"
Get-ChildItem $outDir | Select-Object Name, Length | Format-Table -AutoSize
