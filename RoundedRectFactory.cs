using System;
using System.Collections.Generic;
using UnityEngine;

namespace ADOFAI.AgentKeyViewer
{
    /// <summary>
    /// 圆角矩形纹理工厂（现代 UI 圆角风格）。
    /// 生成"白色形状 + Alpha 覆盖"的纹理，绘制时通过 GUI.color 染色，
    /// 从而同尺寸同圆角只需生成一次，可复用不同颜色，性能友好。
    /// - GetFilled  : 圆角填充形状（画背景用）
    /// - GetBorder  : 圆角描边环（画边框用）
    /// 抗锯齿：按像素到圆角矩形边界的距离做 1px smoothstep。
    /// </summary>
    public static class RoundedRectFactory
    {
        // 缓存：key = "w_h_r"（填充）或 "w_h_r_t"（描边），value = 白色形状纹理
        private static readonly Dictionary<string, Texture2D> _fillCache = new Dictionary<string, Texture2D>();
        private static readonly Dictionary<string, Texture2D> _borderCache = new Dictionary<string, Texture2D>();
        // 角落遮罩缓存：key = "c_w_h_r"，value = 圆角矩形外部的角落为不透明的纹理
        private static readonly Dictionary<string, Texture2D> _cornerCache = new Dictionary<string, Texture2D>();

        private const int MaxCacheEntries = 256;

        /// <summary>获取圆角填充形状（白色，RGB=1，Alpha=覆盖）。w/h 至少为 1。</summary>
        public static Texture2D GetFilled(int w, int h, int radius)
        {
            if (w < 1) w = 1;
            if (h < 1) h = 1;
            radius = Mathf.Clamp(radius, 0, Mathf.Min(w, h) / 2);

            string key = w + "_" + h + "_" + radius;
            if (_fillCache.TryGetValue(key, out var tex)) return tex;

            tex = new Texture2D(w, h, TextureFormat.RGBA32, false);
            tex.wrapMode = TextureWrapMode.Clamp;
            tex.filterMode = FilterMode.Bilinear;
            tex.name = "RoundedFill_" + key;

            float halfW = w * 0.5f, halfH = h * 0.5f;
            float innerHalfW = Mathf.Max(0f, halfW - radius);
            float innerHalfH = Mathf.Max(0f, halfH - radius);
            var pixels = new Color32[w * h];
            for (int y = 0; y < h; y++)
            {
                for (int x = 0; x < w; x++)
                {
                    float cover = RoundedCover(x + 0.5f, y + 0.5f, halfW, halfH, innerHalfW, innerHalfH, radius);
                    byte a = (byte)Mathf.RoundToInt(Mathf.Clamp01(cover) * 255f);
                    pixels[y * w + x] = new Color32(255, 255, 255, a);
                }
            }
            tex.SetPixels32(pixels);
            tex.Apply(false, false);

            if (_fillCache.Count >= MaxCacheEntries) _fillCache.Clear();
            _fillCache[key] = tex;
            return tex;
        }

        /// <summary>获取圆角描边环（白色，RGB=1，Alpha=环覆盖）。thickness 至少为 1。</summary>
        public static Texture2D GetBorder(int w, int h, int radius, int thickness)
        {
            if (w < 1) w = 1;
            if (h < 1) h = 1;
            radius = Mathf.Clamp(radius, 0, Mathf.Min(w, h) / 2);
            if (thickness < 1) thickness = 1;
            if (thickness > Mathf.Min(w, h) / 2) thickness = Mathf.Max(1, Mathf.Min(w, h) / 2);

            string key = w + "_" + h + "_" + radius + "_" + thickness;
            if (_borderCache.TryGetValue(key, out var tex)) return tex;

            tex = new Texture2D(w, h, TextureFormat.RGBA32, false);
            tex.wrapMode = TextureWrapMode.Clamp;
            tex.filterMode = FilterMode.Bilinear;
            tex.name = "RoundedBorder_" + key;

            float halfW = w * 0.5f, halfH = h * 0.5f;
            // 外圆角矩形
            float outerHalfW = Mathf.Max(0f, halfW - radius);
            float outerHalfH = Mathf.Max(0f, halfH - radius);
            // 内圆角矩形（收缩 thickness）
            int innerRadius = Mathf.Max(0, radius - thickness);
            float innerHalfW = Mathf.Max(0f, outerHalfW - thickness);
            float innerHalfH = Mathf.Max(0f, outerHalfH - thickness);

            var pixels = new Color32[w * h];
            for (int y = 0; y < h; y++)
            {
                for (int x = 0; x < w; x++)
                {
                    float px = x + 0.5f, py = y + 0.5f;
                    float outer = RoundedCover(px, py, halfW, halfH, outerHalfW, outerHalfH, radius);
                    float inner = RoundedCover(px, py, halfW, halfH, innerHalfW, innerHalfH, innerRadius);
                    float ring = Mathf.Clamp01(outer - inner);
                    byte a = (byte)Mathf.RoundToInt(ring * 255f);
                    pixels[y * w + x] = new Color32(255, 255, 255, a);
                }
            }
            tex.SetPixels32(pixels);
            tex.Apply(false, false);

            if (_borderCache.Count >= MaxCacheEntries) _borderCache.Clear();
            _borderCache[key] = tex;
            return tex;
        }

        /// <summary>
        /// 获取圆角"角落遮罩"：圆角矩形外部的 4 个角落区域为不透明白色，内部透明。
        /// 用于把方形图片/纹理"切"出圆角（先画图片，再把该遮罩按背景色画上去覆盖图片的直角角落）。
        /// </summary>
        public static Texture2D GetCornerMask(int w, int h, int radius)
        {
            if (w < 1) w = 1;
            if (h < 1) h = 1;
            radius = Mathf.Clamp(radius, 0, Mathf.Min(w, h) / 2);

            string key = "c_" + w + "_" + h + "_" + radius;
            if (_cornerCache.TryGetValue(key, out var tex)) return tex;

            tex = new Texture2D(w, h, TextureFormat.RGBA32, false);
            tex.wrapMode = TextureWrapMode.Clamp;
            tex.filterMode = FilterMode.Bilinear;
            tex.name = "RoundedCornerMask_" + key;

            float halfW = w * 0.5f, halfH = h * 0.5f;
            float innerHalfW = Mathf.Max(0f, halfW - radius);
            float innerHalfH = Mathf.Max(0f, halfH - radius);
            var pixels = new Color32[w * h];
            for (int y = 0; y < h; y++)
            {
                for (int x = 0; x < w; x++)
                {
                    // cover：1=圆角矩形内部，0=外部。遮罩=内部透明、外部不透明（1-cover）。
                    float cover = RoundedCover(x + 0.5f, y + 0.5f, halfW, halfH, innerHalfW, innerHalfH, radius);
                    byte a = (byte)Mathf.RoundToInt(Mathf.Clamp01(1f - cover) * 255f);
                    pixels[y * w + x] = new Color32(255, 255, 255, a);
                }
            }
            tex.SetPixels32(pixels);
            tex.Apply(false, false);

            if (_cornerCache.Count >= MaxCacheEntries) _cornerCache.Clear();
            _cornerCache[key] = tex;
            return tex;
        }

        /// <summary>圆角矩形内部覆盖率：0=完全在外，1=完全在内，0~1=边缘抗锯齿过渡。</summary>
        private static float RoundedCover(float px, float py,
                                          float halfW, float halfH,
                                          float innerHalfW, float innerHalfH, float radius)
        {
            // 到中心点距离（考虑圆角：先去掉圆角内的直线边，再取到圆角圆弧的距离）
            float dx = Mathf.Abs(px - halfW) - innerHalfW;
            float dy = Mathf.Abs(py - halfH) - innerHalfH;

            float dist;
            if (dx <= 0f && dy <= 0f)
            {
                // 在核心矩形内
                dist = Mathf.Min(dx, dy); // 距最近直线边（<=0，越深越负）
            }
            else
            {
                // 在圆角区域：距离 = 到圆角圆心的距离 - radius
                float ox = Mathf.Max(dx, 0f);
                float oy = Mathf.Max(dy, 0f);
                dist = Mathf.Sqrt(ox * ox + oy * oy) - radius;
            }
            // 1px 抗锯齿
            return 0.5f - dist;
        }

        /// <summary>清空全部缓存（启用/布局切换时可调用，防止纹理堆积）</summary>
        public static void ClearAll()
        {
            foreach (var kv in _fillCache) UnityEngine.Object.Destroy(kv.Value);
            foreach (var kv in _borderCache) UnityEngine.Object.Destroy(kv.Value);
            foreach (var kv in _cornerCache) UnityEngine.Object.Destroy(kv.Value);
            _fillCache.Clear();
            _borderCache.Clear();
            _cornerCache.Clear();
        }
    }
}
