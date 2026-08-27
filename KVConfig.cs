using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityModManagerNet;

namespace ADOFAI.AgentKeyViewer
{
    /// <summary>
    /// 单个按键的配置（JSON可序列化，供KV配置保存/加载与AI生成使用）
    /// </summary>
    [Serializable]
    public class KVKeyConfig
    {
        public string id = "";
        public string keyCode = "D";       // Unity KeyCode 名称字符串
        public string label = "";
        public float x, y, w, h;
        public int nodeType;               // 0=主按键，2=鼠标，3=自定义

        // 颜色：8位hex，空字符串=跟随全局
        public string idleColor = "";
        public string pressedColor = "";
        public string borderColor = "";
        public string textColor = "";
        public string textPressedColor = "";

        // 键雨
        public bool useCustomRain;
        public string rainColor = "";
        public float rainWidthRatio = 0.12f;
        public float rainHeightOffset;
        public int rainRow;

        // 圆角 + 背景图片 + 显示模式
        public float cornerRadius;
        public float borderThickness;
        public int displayMode;          // 0=键模式，1=图片模式
        public bool useImage;
        public string imageFile = "";
        public float imageOpacity = 1f;

        /// <summary>转换为运行时按键定义</summary>
        public KeyDefinition ToKeyDefinition()
        {
            var kd = new KeyDefinition(
                string.IsNullOrEmpty(id) ? label : id,
                ParseKeyCode(keyCode, KeyCode.D),
                string.IsNullOrEmpty(label) ? id : label,
                x, y,
                w > 0 ? w : 70f,
                h > 0 ? h : 70f,
                nodeType);
            if (!string.IsNullOrEmpty(idleColor)) kd.IdleColor = SerializableColor.FromHtml(idleColor);
            if (!string.IsNullOrEmpty(pressedColor)) kd.PressedColor = SerializableColor.FromHtml(pressedColor);
            if (!string.IsNullOrEmpty(borderColor)) kd.BorderColor = SerializableColor.FromHtml(borderColor);
            if (!string.IsNullOrEmpty(textColor)) kd.TextColor = SerializableColor.FromHtml(textColor);
            if (!string.IsNullOrEmpty(textPressedColor)) kd.PressedTextColor = SerializableColor.FromHtml(textPressedColor);

            kd.UseCustomRain = useCustomRain;
            if (!string.IsNullOrEmpty(rainColor)) kd.RainColor = SerializableColor.FromHtml(rainColor);
            kd.RainWidthRatio = rainWidthRatio;
            kd.RainHeightOffset = rainHeightOffset;
            kd.RainRow = rainRow;

            kd.CornerRadius = cornerRadius;
            kd.BorderThickness = borderThickness;
            kd.DisplayMode = displayMode;
            kd.UseImage = useImage;
            kd.ImageFile = imageFile ?? "";
            kd.ImageOpacity = imageOpacity;
            return kd;
        }

        /// <summary>从运行时按键定义导出</summary>
        public static KVKeyConfig FromKey(KeyDefinition k)
        {
            return new KVKeyConfig
            {
                id = k.Id,
                keyCode = k.KeyCode.ToString(),
                label = k.Label,
                x = k.OffsetX,
                y = k.OffsetY,
                w = k.Width,
                h = k.Height,
                nodeType = k.NodeType,
                idleColor = k.IdleColor.ToHtml(),
                pressedColor = k.PressedColor.ToHtml(),
                borderColor = k.BorderColor.ToHtml(),
                textColor = k.TextColor.ToHtml(),
                textPressedColor = k.PressedTextColor.ToHtml(),
                useCustomRain = k.UseCustomRain,
                rainColor = k.RainColor.ToHtml(),
                rainWidthRatio = k.RainWidthRatio,
                rainHeightOffset = k.RainHeightOffset,
                rainRow = k.RainRow,
                cornerRadius = k.CornerRadius,
                borderThickness = k.BorderThickness,
                displayMode = k.DisplayMode,
                useImage = k.UseImage,
                imageFile = k.ImageFile ?? "",
                imageOpacity = k.ImageOpacity,
            };
        }

        public static KeyCode ParseKeyCode(string s, KeyCode fallback)
        {
            if (string.IsNullOrWhiteSpace(s)) return fallback;
            if (Enum.TryParse(s.Trim(), out KeyCode c)) return c;
            return fallback;
        }
    }

    /// <summary>
    /// 一份完整的KV（按键显示）配置：显示、配色、键雨、按键布局
    /// </summary>
    [Serializable]
    public class KVConfig
    {
        public string name = "未命名配置";
        public int layoutType;
        public bool includeMouse;

        // 显示
        public float displayX = 60f;
        public float displayY = 60f;
        public float scale = 1f;
        public float opacity = 0.85f;
        public bool showKpsTotal = true;
        public bool showPerKeyCount = true;

        // 全局配色（8位hex）
        public string keyIdleColor = "#1A1A26EB";
        public string keyPressedColor = "#F2BF26FF";
        public string keyBorderColor = "#8C8C8CFF";
        public string keyTextColor = "#FFFFFFFF";
        public string keyTextPressedColor = "#000000FF";
        public string panelBgColor = "#0D0D1A99";

        // 键雨
        public bool enableRain = true;
        public int rainFadeMode = 1;
        public float rainSpeed = 700f;
        public float rainDistance = 260f;
        public float rainGrowSpeed = 600f;
        public float rainWidthRatio = 0.12f;
        public float rainWidthPx = 0f;
        public float rainHeightOffset = 0f;
        public string rainColor = "#FFD966E6";

        // 圆角 + 键背景图片
        public bool enableRoundedCorners = true;
        public float keyCornerRadius = 12f;
        public float statsCornerRadius = 10f;
        public float panelCornerRadius = 14f;
        public bool enableKeyImages = true;
        public int keyImageFit = 0;

        // 按键列表
        public List<KVKeyConfig> keys = new List<KVKeyConfig>();

        /// <summary>从当前 Mod 设置与布局导出</summary>
        public static KVConfig FromCurrent()
        {
            var s = Main.Settings;
            var c = new KVConfig
            {
                layoutType = s.LayoutType,
                includeMouse = s.IncludeMouse,
                displayX = s.DisplayX,
                displayY = s.DisplayY,
                scale = s.Scale,
                opacity = s.Opacity,
                showKpsTotal = s.ShowKpsTotal,
                showPerKeyCount = s.ShowPerKeyCount,
                keyIdleColor = s.KeyIdleColor.ToHtml(),
                keyPressedColor = s.KeyPressedColor.ToHtml(),
                keyBorderColor = s.KeyBorderColor.ToHtml(),
                keyTextColor = s.KeyTextColor.ToHtml(),
                keyTextPressedColor = s.KeyPressedTextColor.ToHtml(),
                panelBgColor = s.PanelBgColor.ToHtml(),
                enableRain = s.EnableRain,
                rainFadeMode = s.RainFadeMode,
                rainSpeed = s.RainSpeed,
                rainDistance = s.RainDistance,
                rainGrowSpeed = s.RainGrowSpeed,
                rainWidthRatio = s.RainWidthRatio,
                rainWidthPx = s.RainWidthPx,
                rainHeightOffset = s.RainHeightOffset,
                rainColor = s.RainColor.ToHtml(),
                enableRoundedCorners = s.EnableRoundedCorners,
                keyCornerRadius = s.KeyCornerRadius,
                statsCornerRadius = s.StatsCornerRadius,
                panelCornerRadius = s.PanelCornerRadius,
                enableKeyImages = s.EnableKeyImages,
                keyImageFit = s.KeyImageFit,
            };

            var layout = Main.DisplayRenderer?.CurrentLayout;
            if (layout != null)
            {
                foreach (var k in layout) c.keys.Add(KVKeyConfig.FromKey(k));
            }
            return c;
        }

        /// <summary>应用配置到当前 Mod 设置与布局</summary>
        public void ApplyTo()
        {
            var s = Main.Settings;
            if (s == null) return;

            // 数值安全化
            if (scale <= 0.01f) scale = 1f;
            if (opacity < 0.1f || opacity > 1f) opacity = 0.85f;
            if (displayX < 0f) displayX = 0f;
            if (displayY < 0f) displayY = 0f;
            if (rainSpeed <= 0f) rainSpeed = 700f;
            if (rainDistance <= 0f) rainDistance = 260f;
            if (rainGrowSpeed < 0f) rainGrowSpeed = 600f;

            s.LayoutType = 5; // AI/预设提供的键直接作为自定义布局
            s.IncludeMouse = includeMouse;
            s.DisplayX = displayX;
            s.DisplayY = displayY;
            s.Scale = scale;
            s.Opacity = opacity;
            s.ShowKpsTotal = showKpsTotal;
            s.ShowPerKeyCount = showPerKeyCount;

            if (!string.IsNullOrEmpty(keyIdleColor)) s.KeyIdleColor = SerializableColor.FromHtml(keyIdleColor);
            if (!string.IsNullOrEmpty(keyPressedColor)) s.KeyPressedColor = SerializableColor.FromHtml(keyPressedColor);
            if (!string.IsNullOrEmpty(keyBorderColor)) s.KeyBorderColor = SerializableColor.FromHtml(keyBorderColor);
            if (!string.IsNullOrEmpty(keyTextColor)) s.KeyTextColor = SerializableColor.FromHtml(keyTextColor);
            if (!string.IsNullOrEmpty(keyTextPressedColor)) s.KeyPressedTextColor = SerializableColor.FromHtml(keyTextPressedColor);
            if (!string.IsNullOrEmpty(panelBgColor)) s.PanelBgColor = SerializableColor.FromHtml(panelBgColor);

            s.EnableRain = enableRain;
            s.RainFadeMode = rainFadeMode;
            s.RainSpeed = rainSpeed;
            s.RainDistance = rainDistance;
            s.RainGrowSpeed = rainGrowSpeed;
            s.RainWidthRatio = rainWidthRatio;
            s.RainWidthPx = rainWidthPx;
            s.RainHeightOffset = rainHeightOffset;
            if (!string.IsNullOrEmpty(rainColor)) s.RainColor = SerializableColor.FromHtml(rainColor);

            s.EnableRoundedCorners = enableRoundedCorners;
            s.KeyCornerRadius = keyCornerRadius;
            s.StatsCornerRadius = statsCornerRadius;
            s.PanelCornerRadius = panelCornerRadius;
            s.EnableKeyImages = enableKeyImages;
            s.KeyImageFit = keyImageFit;

            // 应用按键布局
            s.CustomKeys = new List<KeyDefinition>();
            if (keys != null && keys.Count > 0)
            {
                foreach (var k in keys)
                {
                    var kd = k.ToKeyDefinition();
                    if (!string.IsNullOrEmpty(kd.Id)) s.CustomKeys.Add(kd);
                }
            }
            if (s.CustomKeys.Count == 0)
                s.CustomKeys = new List<KeyDefinition>(KeyLayoutPresets.Create4KLayout());

            Main.DisplayRenderer?.RebuildLayout();
            Main.DisplayRenderer?.SyncCustomKeysToLayout();
        }

        public string ToJson() => JsonUtility.ToJson(this, true);

        public static KVConfig FromJson(string json)
        {
            try
            {
                // 手动解析：提取 keys 数组后逐个反序列化（解决 JsonUtility 嵌套 List 问题）
                int keysStart = json.IndexOf("\"keys\"");
                if (keysStart < 0) return null;

                // 找到 keys 数组的起始 [，跳过字符串值内的括号
                int arrStart = FindArrayStart(json, keysStart);
                if (arrStart < 0) return null;

                // 找到对应的 ]，跳过字符串值内的括号
                int arrEnd = FindMatchingBracket(json, arrStart, '[', ']');
                if (arrEnd < 0) return null;

                // 将原 JSON 中的 keys 数组替换为空，先反序列化父对象
                string parentJson = json.Substring(0, arrStart) + "[]" + json.Substring(arrEnd + 1);
                var cfg = JsonUtility.FromJson<KVConfig>(parentJson);
                if (cfg == null) return null;

                // 逐个反序列化每个键，跳过字符串值内的括号
                cfg.keys = new List<KVKeyConfig>();
                int i2 = arrStart + 1;
                while (i2 < arrEnd)
                {
                    int objStart = FindObjectStart(json, i2, arrEnd);
                    if (objStart < 0) break;
                    int objEnd = FindMatchingBracket(json, objStart, '{', '}');
                    if (objEnd < 0 || objEnd > arrEnd) break;
                    string keyJson = json.Substring(objStart, objEnd - objStart + 1);
                    var key = JsonUtility.FromJson<KVKeyConfig>(keyJson);
                    if (key != null) cfg.keys.Add(key);
                    i2 = objEnd + 1;
                }

                if (cfg.keys.Count == 0) return null;
                return cfg;
            }
            catch (Exception ex)
            {
                Main.ModEntry?.Logger.Error($"[KVConfig] FromJson 失败: {ex.Message}");
                return null;
            }
        }

        /// <summary>查找数组起始位置 [，跳过字符串值内的字符</summary>
        private static int FindArrayStart(string json, int afterKeys)
        {
            bool inString = false;
            bool escaped = false;
            for (int i = afterKeys; i < json.Length; i++)
            {
                char c = json[i];
                if (escaped) { escaped = false; continue; }
                if (c == '\\') { escaped = true; continue; }
                if (c == '"') { inString = !inString; continue; }
                if (!inString && c == '[') return i;
            }
            return -1;
        }

        /// <summary>查找对象起始位置 {，跳过字符串值内的字符</summary>
        private static int FindObjectStart(string json, int from, int limit)
        {
            bool inString = false;
            bool escaped = false;
            for (int i = from; i < limit && i < json.Length; i++)
            {
                char c = json[i];
                if (escaped) { escaped = false; continue; }
                if (c == '\\') { escaped = true; continue; }
                if (c == '"') { inString = !inString; continue; }
                if (!inString && c == '{') return i;
            }
            return -1;
        }

        /// <summary>查找匹配的闭合括号，跳过字符串值内的字符</summary>
        private static int FindMatchingBracket(string json, int openPos, char open, char close)
        {
            bool inString = false;
            bool escaped = false;
            int depth = 0;
            for (int i = openPos; i < json.Length; i++)
            {
                char c = json[i];
                if (escaped) { escaped = false; continue; }
                if (c == '\\') { escaped = true; continue; }
                if (c == '"') { inString = !inString; continue; }
                if (!inString)
                {
                    if (c == open) depth++;
                    else if (c == close)
                    {
                        depth--;
                        if (depth == 0) return i;
                    }
                }
            }
            return -1;
        }

        /// <summary>解析AI输出：剥离```围栏、截取最外层JSON对象后反序列化</summary>
        public static KVConfig FromAiJson(string content)
        {
            if (string.IsNullOrWhiteSpace(content)) return null;
            content = content.Trim();

            // 去掉 ```json ... ``` 围栏
            int fence = content.IndexOf("```");
            if (fence >= 0)
            {
                int close = content.LastIndexOf("```");
                if (close > fence + 3)
                {
                    content = content.Substring(fence + 3, close - fence - 3);
                }
                else
                {
                    content = content.Substring(fence + 3);
                }
            }

            // 截取最外层 JSON 对象
            int start = content.IndexOf('{');
            int end = content.LastIndexOf('}');
            if (start < 0 || end <= start) return null;
            content = content.Substring(start, end - start + 1);

            return FromJson(content);
        }
    }

    /// <summary>KVConfig 的包装类（JsonUtility 对嵌套 List 序列化不稳定）</summary>
    [Serializable]
    public class KVConfigWrapper
    {
        public KVConfig data;
    }

    /// <summary>命名配置集合（保存到 config/kv_configs.json）</summary>
    [Serializable]
    public class KVConfigList
    {
        public List<KVConfig> configs = new List<KVConfig>();
    }

    /// <summary>KVConfigList 的包装类（JsonUtility 对嵌套 List 序列化不稳定）</summary>
    [Serializable]
    public class KVConfigListWrapper
    {
        public KVConfigList data;
    }

    /// <summary>KV配置的磁盘存取</summary>
    public static class KVConfigStore
    {
        public static string GetPath(UnityModManager.ModEntry modEntry)
        {
            return Path.Combine(ConfigDir(modEntry), "kv_configs.json");
        }

        public static string ConfigDir(UnityModManager.ModEntry modEntry)
        {
            var dir = Path.Combine(Path.GetDirectoryName(modEntry.Path) ?? ".", "config");
            try { if (!Directory.Exists(dir)) Directory.CreateDirectory(dir); } catch { }
            return dir;
        }

        public static KVConfigList Load(string path)
        {
            try
            {
                if (File.Exists(path))
                {
                    string json = File.ReadAllText(path, System.Text.Encoding.UTF8);
                    var wrapper = JsonUtility.FromJson<KVConfigListWrapper>(json);
                    if (wrapper != null && wrapper.data != null)
                        return wrapper.data;
                }
            }
            catch (Exception ex)
            {
                Main.ModEntry?.Logger.Error($"[KVConfig] 加载失败: {ex.Message}");
            }
            return new KVConfigList();
        }

        public static void Save(string path, KVConfigList list)
        {
            try
            {
                var dir = Path.GetDirectoryName(path);
                if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir)) Directory.CreateDirectory(dir);
                var wrapper = new KVConfigListWrapper { data = list };
                string json = JsonUtility.ToJson(wrapper, true);
                File.WriteAllText(path, json, System.Text.Encoding.UTF8);
                Main.ModEntry?.Logger.Log($"[KVConfig] 已保存 {list.configs.Count} 个配置到: {path}");
            }
            catch (Exception ex)
            {
                Main.ModEntry?.Logger.Error($"[KVConfig] 保存失败: {ex.Message}");
            }
        }
    }
}
