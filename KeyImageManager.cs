using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;

namespace ADOFAI.AgentKeyViewer
{
    /// <summary>已加载的键背景图片</summary>
    public class LoadedImage
    {
        public bool isGif;
        /// <summary>静态图（JPG/PNG），或 GIF 的首帧</summary>
        public Texture2D staticTexture;
        /// <summary>GIF 帧序列（仅 isGif 时非空）</summary>
        public List<GifFrame> gifFrames;
        /// <summary>GIF 总时长（秒）</summary>
        public float totalDuration;
        public string filePath;
    }

    /// <summary>
    /// 键背景图片管理器（参考 CT 的"背景贴图"节点）：
    /// - 图片统一放在 游戏目录/AgentKeyViewer_config/images/ 下
    /// - 支持 JPG / PNG（透明）/ GIF（按下播放动画，空闲显示首帧）
    /// - 扫描文件夹 + 按文件名加载 + 缓存
    /// </summary>
    public static class KeyImageManager
    {
        private static readonly Dictionary<string, LoadedImage> _cache = new Dictionary<string, LoadedImage>(StringComparer.OrdinalIgnoreCase);
        private static string[] _scannedFiles = new string[0];
        private static readonly HashSet<string> _supportedExt = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        { ".jpg", ".jpeg", ".png", ".gif" };

        /// <summary>图片文件夹绝对路径（游戏目录/AgentKeyViewer_config/images）</summary>
        public static string GetImagesFolder()
        {
            string gameDir = ModPathHelper.GetGameDir();
            return Path.Combine(gameDir, "AgentKeyViewer_config", "images");
        }

        /// <summary>确保图片文件夹存在</summary>
        public static void EnsureFolder()
        {
            try
            {
                string dir = GetImagesFolder();
                if (!Directory.Exists(dir)) Directory.CreateDirectory(dir);
            }
            catch (Exception ex)
            {
                CoreEntry.ModEntry?.Logger.Error($"[KeyImageManager] 创建图片文件夹失败: {ex.Message}");
            }
        }

        /// <summary>在资源管理器中打开图片文件夹</summary>
        public static void OpenFolder()
        {
            try
            {
                EnsureFolder();
                System.Diagnostics.Process.Start("explorer.exe", GetImagesFolder());
            }
            catch (Exception ex)
            {
                CoreEntry.ModEntry?.Logger.Error($"[KeyImageManager] 打开文件夹失败: {ex.Message}");
            }
        }

        /// <summary>扫描图片文件夹，返回支持格式的文件名（已排序）</summary>
        public static string[] GetImageFiles()
        {
            try
            {
                EnsureFolder();
                string dir = GetImagesFolder();
                _scannedFiles = Directory.GetFiles(dir)
                    .Where(f => _supportedExt.Contains(Path.GetExtension(f)))
                    .Select(Path.GetFileName)
                    .OrderBy(n => n, StringComparer.OrdinalIgnoreCase)
                    .ToArray();
            }
            catch (Exception ex)
            {
                CoreEntry.ModEntry?.Logger.Error($"[KeyImageManager] 扫描图片文件夹失败: {ex.Message}");
                _scannedFiles = new string[0];
            }
            return _scannedFiles;
        }

        /// <summary>获取某图片的加载结果（按文件名），失败返回 null</summary>
        public static LoadedImage GetImage(string fileName)
        {
            if (string.IsNullOrWhiteSpace(fileName)) return null;
            if (_cache.TryGetValue(fileName, out var cached)) return cached;

            LoadedImage result = null;
            try
            {
                EnsureFolder();
                string path = Path.Combine(GetImagesFolder(), fileName);
                if (!File.Exists(path)) return null;

                string ext = Path.GetExtension(path);
                if (ext.Equals(".gif", StringComparison.OrdinalIgnoreCase))
                {
                    var frames = GifDecoder.Decode(File.ReadAllBytes(path));
                    if (frames != null && frames.Count > 0)
                    {
                        float total = 0f;
                        foreach (var f in frames) total += f.delaySec;
                        result = new LoadedImage
                        {
                            isGif = true,
                            staticTexture = frames[0].texture,
                            gifFrames = frames,
                            totalDuration = total > 0f ? total : 0.1f,
                            filePath = path,
                        };
                    }
                }
                else
                {
                    // 使用 System.Drawing 加载 JPG/PNG（兼容 net48，避免 ImageConversion 对 ReadOnlySpan 的依赖）
                    var tex = LoadStaticWithSystemDrawing(path);
                    if (tex != null)
                    {
                        result = new LoadedImage
                        {
                            isGif = false,
                            staticTexture = tex,
                            gifFrames = null,
                            totalDuration = 0f,
                            filePath = path,
                        };
                    }
                }
            }
            catch (Exception ex)
            {
                CoreEntry.ModEntry?.Logger.Error($"[KeyImageManager] 加载图片失败 {fileName}: {ex.Message}");
                result = null;
            }

            if (result != null) _cache[fileName] = result;
            return result;
        }

        /// <summary>获取图片在键内的绘制矩形（按缩放模式）。</summary>
        /// <param name="texW/H">纹理尺寸</param>
        /// <param name="fit">0=拉伸 1=适应(留边) 2=填充(裁剪)</param>
        public static Rect ComputeDrawRect(Rect keyRect, int texW, int texH, int fit)
        {
            if (texW <= 0 || texH <= 0) return keyRect;
            float ratio = (float)texW / texH;

            switch (fit)
            {
                case 1: // 适应：整图可见，居中，留透明边
                    {
                        float rectRatio = keyRect.width / keyRect.height;
                        float w, h;
                        if (ratio > rectRatio) { w = keyRect.width; h = w / ratio; }
                        else { h = keyRect.height; w = h * ratio; }
                        return new Rect(keyRect.x + (keyRect.width - w) * 0.5f,
                                        keyRect.y + (keyRect.height - h) * 0.5f, w, h);
                    }
                case 2: // 填充：铺满并裁剪溢出，居中
                    {
                        float rectRatio = keyRect.width / keyRect.height;
                        float w, h;
                        if (ratio < rectRatio) { w = keyRect.width; h = w / ratio; }
                        else { h = keyRect.height; w = h * ratio; }
                        return new Rect(keyRect.x + (keyRect.width - w) * 0.5f,
                                        keyRect.y + (keyRect.height - h) * 0.5f, w, h);
                    }
                default: // 拉伸
                    return keyRect;
            }
        }

        /// <summary>
        /// 使用 System.Drawing 加载静态图片（JPG/PNG），返回 Texture2D（RGBA32，Alpha 通道保留）。
        /// 兼容 net48 运行时，避免 UnityEngine.ImageConversion 对 ReadOnlySpan 的依赖。
        /// </summary>
        private static Texture2D LoadStaticWithSystemDrawing(string path)
        {
            try
            {
                using (var stream = new MemoryStream(File.ReadAllBytes(path)))
                using (var bmp = new System.Drawing.Bitmap(stream))
                {
                    var tex = new Texture2D(bmp.Width, bmp.Height, TextureFormat.RGBA32, false);
                    tex.wrapMode = TextureWrapMode.Clamp;
                    tex.filterMode = FilterMode.Bilinear;
                    tex.name = "KeyImage_" + Path.GetFileName(path);

                    var data = bmp.LockBits(new System.Drawing.Rectangle(0, 0, bmp.Width, bmp.Height),
                                          System.Drawing.Imaging.ImageLockMode.ReadOnly,
                                          System.Drawing.Imaging.PixelFormat.Format32bppArgb);
                    try
                    {
                        int length = data.Stride * data.Height;
                        byte[] bytes = new byte[length];
                        System.Runtime.InteropServices.Marshal.Copy(data.Scan0, bytes, 0, length);

                        // Bitmap 行序自上而下，Texture2D 行序自下而上，需要上下翻转；BGRA -> RGBA
                        Color32[] pixels = new Color32[bmp.Width * bmp.Height];
                        for (int y = 0; y < bmp.Height; y++)
                        {
                            for (int x = 0; x < bmp.Width; x++)
                            {
                                int idx = (bmp.Height - 1 - y) * bmp.Width + x;
                                int byteIdx = y * data.Stride + x * 4;
                                pixels[idx] = new Color32(
                                    bytes[byteIdx + 2], // R
                                    bytes[byteIdx + 1], // G
                                    bytes[byteIdx],     // B
                                    bytes[byteIdx + 3]  // A
                                );
                            }
                        }
                        tex.SetPixels32(pixels);
                        tex.Apply(false, true);
                    }
                    finally
                    {
                        bmp.UnlockBits(data);
                    }
                    return tex;
                }
            }
            catch (Exception ex)
            {
                CoreEntry.ModEntry?.Logger.Error($"[KeyImageManager] System.Drawing 加载失败 {path}: {ex.Message}");
                return null;
            }
        }

        /// <summary>清空缓存并释放纹理（禁用/切换时调用）</summary>
        public static void ClearCache()
        {
            foreach (var kv in _cache)
            {
                var img = kv.Value;
                if (img.staticTexture != null) UnityEngine.Object.Destroy(img.staticTexture);
                if (img.gifFrames != null)
                    foreach (var f in img.gifFrames)
                        if (f.texture != null) UnityEngine.Object.Destroy(f.texture);
            }
            _cache.Clear();
        }
    }
}
