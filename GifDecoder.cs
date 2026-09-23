using System;
using System.Collections.Generic;
using UnityEngine;

namespace ADOFAI.AgentKeyViewer
{
    /// <summary>GIF 解码后的一帧（已合成为完整画布）</summary>
    public class GifFrame
    {
        public Texture2D texture;
        /// <summary>该帧显示时长（秒）</summary>
        public float delaySec;
    }

    /// <summary>
    /// 纯 C# GIF 解码器（无 System.Drawing 依赖）：
    /// 支持 GIF87a / GIF89a、LZW 解压（变长编码）、隔行、透明色、帧延迟、帧合成（disposal 0~3）。
    /// 输出可直接用于 GUI.DrawTexture 的 Texture2D 帧序列。
    /// </summary>
    public static class GifDecoder
    {
        /// <summary>解码 GIF 字节流为帧序列；失败返回 null。</summary>
        public static List<GifFrame> Decode(byte[] data)
        {
            try
            {
                if (data == null || data.Length < 14) return null;
                return DecodeInternal(data);
            }
            catch (Exception ex)
            {
                CoreEntry.ModEntry?.Logger.Error($"[GifDecoder] 解码失败: {ex.Message}");
                return null;
            }
        }

        private static List<GifFrame> DecodeInternal(byte[] data)
        {
            int pos = 0;
            // 1. Header
            string sig = System.Text.Encoding.ASCII.GetString(data, 0, 6);
            if (sig != "GIF87a" && sig != "GIF89a") return null;
            pos = 6;

            // 2. Logical Screen Descriptor
            int screenW = data[pos] | (data[pos + 1] << 8); pos += 2;
            int screenH = data[pos] | (data[pos + 1] << 8); pos += 2;
            byte packed = data[pos]; pos++;
            bool hasGlobalCT = (packed & 0x80) != 0;
            int gctSize = hasGlobalCT ? (1 << ((packed & 0x07) + 1)) : 0;
            int bgColorIndex = data[pos]; pos++;
            pos++; // aspect

            // 3. Global Color Table
            Color32[] gct = null;
            if (hasGlobalCT)
            {
                gct = ReadColorTable(data, ref pos, gctSize);
            }

            // 画布（RGBA），初始为透明（若需要可先填充背景色）
            var canvas = new Color32[screenW * screenH];
            for (int i = 0; i < canvas.Length; i++) canvas[i] = new Color32(0, 0, 0, 0);

            var frames = new List<GifFrame>();
            bool done = false;
            int frameDelayCs = 0;      // 当前帧延迟（百分之一秒）
            bool hasTransparency = false;
            int transparentIdx = -1;
            int disposal = 0;
            Color32[] previousCanvas = null;
            bool previousStored = false;

            while (pos < data.Length && !done)
            {
                byte block = data[pos]; pos++;
                switch (block)
                {
                    case 0x3B: // Trailer
                        done = true;
                        break;

                    case 0x21: // Extension
                        byte label = data[pos]; pos++;
                        if (label == 0xF9) // Graphic Control Extension
                        {
                            int size = data[pos]; pos++; // should be 4
                            byte gcePacked = data[pos]; pos++;
                            hasTransparency = (gcePacked & 0x01) != 0;
                            disposal = (gcePacked >> 2) & 0x07;
                            frameDelayCs = data[pos] | (data[pos + 1] << 8); pos += 2;
                            transparentIdx = data[pos]; pos++;
                            pos++; // terminator 0x00
                            SkipSubBlocks(data, ref pos);
                        }
                        else
                        {
                            // 其它扩展（Comment/Application/PlainText）跳过子块
                            SkipSubBlocks(data, ref pos);
                        }
                        break;

                    case 0x2C: // Image Descriptor
                        {
                            int left = data[pos] | (data[pos + 1] << 8); pos += 2;
                            int top = data[pos] | (data[pos + 1] << 8); pos += 2;
                            int imgW = data[pos] | (data[pos + 1] << 8); pos += 2;
                            int imgH = data[pos] | (data[pos + 1] << 8); pos += 2;
                            byte imgPacked = data[pos]; pos++;
                            bool hasLocalCT = (imgPacked & 0x80) != 0;
                            bool interlaced = (imgPacked & 0x40) != 0;
                            int lctSize = hasLocalCT ? (1 << ((imgPacked & 0x07) + 1)) : 0;

                            Color32[] ct = gct;
                            if (hasLocalCT) ct = ReadColorTable(data, ref pos, lctSize);

                            byte lzwMinCodeSize = data[pos]; pos++;
                            byte[] lzwData = ReadSubBlocks(data, ref pos);

                            // 解压
                            int[] indices = LzwDecode(lzwData, lzwMinCodeSize);
                            if (indices == null) return null;

                            // 合成到画布
                            if (disposal == 3) // 先保存当前画布（restore to previous）
                            {
                                previousCanvas = (Color32[])canvas.Clone();
                                previousStored = true;
                            }

                            CompositeFrame(canvas, screenW, screenH,
                                           indices, imgW, imgH, left, top,
                                           interlaced, ct, hasTransparency, transparentIdx);

                            // 生成帧
                            float delay = frameDelayCs <= 0 ? 0.05f : frameDelayCs / 100f;
                            frames.Add(new GifFrame { texture = MakeTexture(canvas, screenW, screenH), delaySec = delay });

                            // 处理 disposal
                            if (disposal == 2) // restore to background -> 清为透明
                            {
                                ClearCanvas(canvas, screenW, screenH, gct, bgColorIndex, hasGlobalCT);
                            }
                            else if (disposal == 3 && previousStored)
                            {
                                previousCanvas.CopyTo(canvas, 0);
                            }

                            // 每帧后重置（防 GCE 缺失时沿用）
                            hasTransparency = false;
                            transparentIdx = -1;
                            frameDelayCs = 0;
                            disposal = 0;
                            break;
                        }
                }
            }

            if (frames.Count == 0) return null;
            return frames;
        }

        private static Color32[] ReadColorTable(byte[] data, ref int pos, int count)
        {
            var table = new Color32[count];
            for (int i = 0; i < count; i++)
            {
                if (pos + 2 >= data.Length) break;
                table[i] = new Color32(data[pos], data[pos + 1], data[pos + 2], 255);
                pos += 3;
            }
            return table;
        }

        private static void SkipSubBlocks(byte[] data, ref int pos)
        {
            while (pos < data.Length)
            {
                int size = data[pos]; pos++;
                if (size == 0) break;
                pos += size;
            }
        }

        private static byte[] ReadSubBlocks(byte[] data, ref int pos)
        {
            var list = new List<byte>();
            while (pos < data.Length)
            {
                int size = data[pos]; pos++;
                if (size == 0) break;
                for (int i = 0; i < size && pos < data.Length; i++)
                {
                    list.Add(data[pos]);
                    pos++;
                }
            }
            return list.ToArray();
        }

        /// <summary>GIF LZW 解压，返回像素索引序列。</summary>
        private static int[] LzwDecode(byte[] data, int minCodeSize)
        {
            if (data == null || data.Length == 0) return null;
            if (minCodeSize < 2 || minCodeSize > 8) return null;

            int clearCode = 1 << minCodeSize;
            int endCode = clearCode + 1;
            int codeSize = minCodeSize + 1;
            int nextCode = endCode + 1;

            // 字典（前缀+后缀，最多 4096 项）
            var prefix = new int[4096];
            var suffix = new byte[4096];
            for (int i = 0; i < clearCode; i++) { prefix[i] = -1; suffix[i] = (byte)i; }

            var output = new List<int>(1024);
            var stack = new byte[4096];

            int bitBuf = 0, bitCnt = 0, bytePos = 0;
            int prevCode = -1;

            int ReadCode()
            {
                while (bitCnt < codeSize && bytePos < data.Length)
                {
                    bitBuf |= data[bytePos++] << bitCnt;
                    bitCnt += 8;
                }
                if (bitCnt < codeSize) return endCode; // 数据耗尽
                int code = bitBuf & ((1 << codeSize) - 1);
                bitBuf >>= codeSize;
                bitCnt -= codeSize;
                return code;
            }

            int code = ReadCode();
            while (code != endCode && code != -1)
            {
                if (code == clearCode)
                {
                    codeSize = minCodeSize + 1;
                    nextCode = endCode + 1;
                    for (int i = 0; i < clearCode; i++) { prefix[i] = -1; suffix[i] = (byte)i; }
                    code = ReadCode();
                    if (code == endCode) break;
                    prevCode = code;
                    if (code < clearCode) output.Add(code);
                    code = ReadCode();
                    continue;
                }

                int stackTop = 0;
                int cur = code;
                if (code >= nextCode || code < 0)
                {
                    // KwKwK 情形
                    if (prevCode < 0) break;
                    stack[stackTop++] = suffix[prevCode];
                    cur = prevCode;
                }
                while (cur >= 0 && cur < 4096)
                {
                    stack[stackTop++] = suffix[cur];
                    cur = prefix[cur];
                }
                // 逆序输出
                for (int i = stackTop - 1; i >= 0; i--) output.Add(stack[i]);

                if (prevCode >= 0 && nextCode < 4096)
                {
                    suffix[nextCode] = stack[stackTop - 1];
                    prefix[nextCode] = prevCode;
                    nextCode++;
                    if (nextCode == (1 << codeSize) && codeSize < 12) codeSize++;
                }
                prevCode = code;
                code = ReadCode();
            }

            return output.ToArray();
        }

        /// <summary>把解压出的索引帧合成到画布（支持隔行与透明色）。</summary>
        private static void CompositeFrame(Color32[] canvas, int screenW, int screenH,
                                           int[] indices, int imgW, int imgH,
                                           int left, int top, bool interlaced,
                                           Color32[] colorTable, bool hasTransparency, int transparentIdx)
        {
            int pixelCount = Mathf.Min(indices.Length, imgW * imgH);
            int[] rowOrder = new int[imgH];
            if (interlaced)
            {
                // 隔行顺序：0,8,16... 4,12,20... 2,6,10... 1,3,5,7...
                int n = 0;
                for (int start = 0; start < 8; start += 4)
                {
                    int stride = 8 - start;
                    for (int y = start; y < imgH; y += stride)
                        if (n < imgH) rowOrder[n++] = y;
                }
                for (int y = 0; y < imgH && n < imgH; y++) rowOrder[n++] = y;
            }
            else
            {
                for (int y = 0; y < imgH; y++) rowOrder[y] = y;
            }

            int idx = 0;
            for (int r = 0; r < imgH; r++)
            {
                int y = top + rowOrder[r];
                for (int x = 0; x < imgW; x++)
                {
                    if (idx >= pixelCount) break;
                    int colorIdx = indices[idx]; idx++;
                    if (colorIdx < 0 || colorIdx >= colorTable.Length) continue;
                    if (hasTransparency && colorIdx == transparentIdx) continue; // 保持画布原样

                    int sx = left + x;
                    int sy = y;
                    if (sx < 0 || sx >= screenW || sy < 0 || sy >= screenH) continue;
                    canvas[sy * screenW + sx] = colorTable[colorIdx];
                }
            }
        }

        private static void ClearCanvas(Color32[] canvas, int w, int h,
                                        Color32[] gct, int bgIdx, bool hasGct)
        {
            if (hasGct && gct != null && bgIdx >= 0 && bgIdx < gct.Length)
            {
                var c = gct[bgIdx];
                for (int i = 0; i < canvas.Length; i++) canvas[i] = c;
            }
            else
            {
                for (int i = 0; i < canvas.Length; i++) canvas[i] = new Color32(0, 0, 0, 0);
            }
        }

        private static Texture2D MakeTexture(Color32[] pixels, int w, int h)
        {
            var tex = new Texture2D(w, h, TextureFormat.RGBA32, false);
            tex.wrapMode = TextureWrapMode.Clamp;
            tex.filterMode = FilterMode.Bilinear;
            tex.SetPixels32(pixels);
            tex.Apply(false, true);
            return tex;
        }
    }
}
