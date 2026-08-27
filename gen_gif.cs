using System;
using System.Collections.Generic;
using System.IO;
using System.Drawing;

/// <summary>生成 2 帧 GIF 动画测试图（整帧红 -> 整帧绿），纯 C# 手工构造字节流。</summary>
public static class GifTestGen
{
    public static void Main()
    {
        string outDir = @"D:\Steam\steamapps\common\A Dance of Fire and Ice\AgentKeyViewer_config\images";
        int gifSize = 64;
        var gct = new Color[] { Color.White, Color.OrangeRed, Color.ForestGreen, Color.Blue };
        var frames = new int[][]
        {
            Fill(gifSize, 1), // 帧1：全红
            Fill(gifSize, 2)  // 帧2：全绿
        };
        var delays = new int[] { 50, 50 }; // 0.5s / 帧
        byte[] gif = BuildGif(gifSize, gifSize, gct, frames, delays);
        File.WriteAllBytes(Path.Combine(outDir, "demo_gif.gif"), gif);
        Console.WriteLine("GIF written: " + Path.Combine(outDir, "demo_gif.gif") + " (" + gif.Length + " bytes)");
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
        var ms = new MemoryStream();
        var bw = new BinaryWriter(ms);

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

        var dict = new Dictionary<long, int>(1024);
        var bytes = new List<byte>();
        int bitBuf = 0, bitCnt = 0;

        WriteCode(bytes, ref bitBuf, ref bitCnt, ref codeSize, clearCode);
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
                WriteCode(bytes, ref bitBuf, ref bitCnt, ref codeSize, prev);
                if (nextCode < 4096)
                {
                    dict[key] = nextCode;
                    nextCode++;
                    if (nextCode == (1 << codeSize) && codeSize < 12) codeSize++;
                }
                prev = p;
            }
        }
        WriteCode(bytes, ref bitBuf, ref bitCnt, ref codeSize, prev);
        WriteCode(bytes, ref bitBuf, ref bitCnt, ref codeSize, endCode);
        // 刷新剩余位
        if (bitCnt > 0) bytes.Add((byte)(bitBuf & 0xFF));
        return bytes.ToArray();
    }

    private static void WriteCode(List<byte> bytes, ref int bitBuf, ref int bitCnt, ref int codeSize, int code)
    {
        bitBuf |= code << bitCnt;
        bitCnt += codeSize;
        while (bitCnt >= 8)
        {
            bytes.Add((byte)(bitBuf & 0xFF));
            bitBuf >>= 8;
            bitCnt -= 8;
        }
    }
}
