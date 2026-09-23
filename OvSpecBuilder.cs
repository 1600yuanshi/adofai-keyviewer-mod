using System;
using System.Collections.Generic;
using UnityEngine;

namespace ADOFAI.AgentKeyViewer
{
    /// <summary>
    /// Overlayer(OV) 配置的紧凑意图规格：AI 只输出本结构，再由 <see cref="OvSpecBuilder"/>
    /// 确定性地构建为 CT Sonnet 的 .ctov 包（清单 Overlayer.Sonnet.xml）。
    ///
    /// 坐标约定：屏幕左上角为原点，X 向右、Y 向下为正（与 CT 旧版 KV 一致）；
    /// 组件默认锚点 PivotX/PivotY 为 0..1，0=组件左上角对齐到该坐标。
    /// </summary>
    [Serializable]
    public class OvSpec
    {
        public string name = "Overlayer 配置";
        public List<OvSpecText> texts = new List<OvSpecText>();
        public List<OvSpecProgress> progressBars = new List<OvSpecProgress>();

        /// <summary>
        /// 解析 AI 输出：剥离 markdown 围栏、截取最外层 JSON 对象。
        /// 注意：Unity 的 JsonUtility 对「类里的 List&lt;T&gt; 字段」支持不可靠（会静默得到空列表），
        /// 因此与 <see cref="KVConfig.FromJson"/> 一样采用「先反序列化父对象、再逐个元素反序列化」的做法。
        /// </summary>
        public static OvSpec FromAiJson(string content)
        {
            if (string.IsNullOrWhiteSpace(content)) return null;
            content = content.Trim();

            int fence = content.IndexOf("```");
            if (fence >= 0)
            {
                int close = content.LastIndexOf("```");
                content = close > fence + 3
                    ? content.Substring(fence + 3, close - fence - 3)
                    : content.Substring(fence + 3);
            }

            int start = content.IndexOf('{');
            int end = content.LastIndexOf('}');
            if (start < 0 || end <= start) return null;
            content = content.Substring(start, end - start + 1);

            try
            {
                var spec = new OvSpec();
                spec.texts = ParseArray<OvSpecText>(content, "texts");
                spec.progressBars = ParseArray<OvSpecProgress>(content, "progressBars");
                spec.name = ExtractTopLevelName(content);
                return spec;
            }
            catch (Exception ex)
            {
                CoreEntry.ModEntry?.Logger.Error($"[OvSpec] JSON 解析失败: {ex.Message}");
                return null;
            }
        }

        /// <summary>逐个元素反序列化指定数组字段（跳过字符串值内的括号）</summary>
        private static List<T> ParseArray<T>(string json, string field) where T : class
        {
            var result = new List<T>();
            int keyPos = json.IndexOf("\"" + field + "\"", StringComparison.Ordinal);
            if (keyPos < 0) return result;

            int arrStart = KVConfig.FindArrayStart(json, keyPos);
            if (arrStart < 0) return result;
            int arrEnd = KVConfig.FindMatchingBracket(json, arrStart, '[', ']');
            if (arrEnd < 0) arrEnd = json.Length - 1;

            int i = arrStart + 1;
            while (i < arrEnd)
            {
                int objStart = KVConfig.FindObjectStart(json, i, arrEnd);
                if (objStart < 0) break;
                int objEnd = KVConfig.FindMatchingBracket(json, objStart, '{', '}');
                if (objEnd < 0 || objEnd > arrEnd) break;
                string itemJson = json.Substring(objStart, objEnd - objStart + 1);
                var item = JsonUtility.FromJson<T>(itemJson);
                if (item != null) result.Add(item);
                i = objEnd + 1;
            }
            return result;
        }

        /// <summary>取顶层 name（只找数组字段之前出现的第一个 name，避免误取组件内的 name）</summary>
        private static string ExtractTopLevelName(string json)
        {
            int limit = json.Length;
            foreach (var field in new[] { "\"texts\"", "\"progressBars\"", "\"images\"", "\"videos\"" })
            {
                int p = json.IndexOf(field, StringComparison.Ordinal);
                if (p >= 0 && p < limit) limit = p;
            }
            string head = json.Substring(0, limit);
            int keyPos = head.IndexOf("\"name\"", StringComparison.Ordinal);
            if (keyPos < 0) return "";
            int colon = head.IndexOf(':', keyPos);
            if (colon < 0) return "";
            int q1 = head.IndexOf('"', colon + 1);
            if (q1 < 0) return "";
            int q2 = head.IndexOf('"', q1 + 1);
            if (q2 < 0) return "";
            return head.Substring(q1 + 1, q2 - q1 - 1);
        }
    }

    [Serializable]
    public class OvSpecText
    {
        public string name = "文本";
        /// <summary>文本模板，使用 {token} 占位，如 "KPS {cbpm:2}"</summary>
        public string text = "{fps}";
        public float x = 50f;
        public float y = 50f;
        public float pivotX;
        public float pivotY;
        public float fontSize = 32f;
        /// <summary>0=左 1=中 2=右</summary>
        public int align = 1;
        public string color = "#FFFFFFFF";
        public bool outline;
        public string outlineColor = "#000000FF";
        public float outlineThickness = 1f;
        public bool shadow;
        public string shadowColor = "#000000B3";
        public bool showInGame = true;
    }

    [Serializable]
    public class OvSpecProgress
    {
        public string name = "进度条";
        /// <summary>取值 token，如 {progress} {xacc}</summary>
        public string valueTag = "{progress}";
        public float min = 0f;
        public float max = 100f;
        public float x = 50f;
        public float y = 100f;
        public float pivotX;
        public float pivotY;
        public float width = 300f;
        public float height = 20f;
        /// <summary>0=左到右 1=右到左 2=下到上 3=上到下</summary>
        public int fillDirection;
        public string backgroundColor = "#00000073";
        public string fillColor = "#33BFF2F2";
        public string borderColor = "#FFFFFFCC";
        public float borderThickness = 1f;
        public float cornerRadius;
        public bool showInGame = true;
    }

    /// <summary>OV 意图规格 → CT Sonnet Overlayer 包对象</summary>
    public static class OvSpecBuilder
    {
        /// <summary>TMP TextAlignmentOptions：0=左 1=中 2=右</summary>
        private static int ToTmpAlignment(int align)
        {
            switch (align)
            {
                case 0: return 513;   // Left（垂直居中·左对齐）
                case 2: return 516;   // Right
                default: return 514;  // Center
            }
        }

        /// <summary>构建 Overlayer 传输包</summary>
        public static OvTransferPackage BuildPackage(OvSpec spec)
        {
            if (spec == null) throw new ArgumentNullException(nameof(spec));

            var package = new OvTransferPackage
            {
                FormatVersion = 1,
                Kind = "Overlayer",
                ExportedAt = DateTime.Now.ToString("O"),
                ScreenWidth = Screen.width,
                ScreenHeight = Screen.height,
            };

            if (spec.texts != null)
            {
                foreach (var t in spec.texts)
                {
                    if (t == null) continue;
                    package.Texts.Add(new OvTextComponent
                    {
                        Name = string.IsNullOrWhiteSpace(t.name) ? "文本" : t.name,
                        Enabled = true,
                        ShowInGame = t.showInGame,
                        PositionX = t.x,
                        PositionY = t.y,
                        PivotX = Mathf.Clamp01(t.pivotX),
                        PivotY = Mathf.Clamp01(t.pivotY),
                        Opacity = 1f,
                        TextFormat = string.IsNullOrWhiteSpace(t.text) ? "{fps}" : t.text,
                        FontSize = t.fontSize > 0f ? t.fontSize : 32f,
                        Alignment = ToTmpAlignment(t.align),
                        TextColor = KvSpecBuilder.HexToRgba(t.color, new[] { 1f, 1f, 1f, 1f }),
                        OutlineEnabled = t.outline,
                        OutlineColor = KvSpecBuilder.HexToRgba(t.outlineColor, new[] { 0f, 0f, 0f, 1f }),
                        OutlineThickness = t.outlineThickness > 0f ? t.outlineThickness : 1f,
                        ShadowEnabled = t.shadow,
                        ShadowColor = KvSpecBuilder.HexToRgba(t.shadowColor, new[] { 0f, 0f, 0f, 0.7f }),
                        ShadowOffset = new float[2] { 2f, 2f },
                    });
                }
            }

            if (spec.progressBars != null)
            {
                foreach (var p in spec.progressBars)
                {
                    if (p == null) continue;
                    package.ProgressBars.Add(new OvProgressComponent
                    {
                        Name = string.IsNullOrWhiteSpace(p.name) ? "进度条" : p.name,
                        Enabled = true,
                        ShowInGame = p.showInGame,
                        PositionX = p.x,
                        PositionY = p.y,
                        PivotX = Mathf.Clamp01(p.pivotX),
                        PivotY = Mathf.Clamp01(p.pivotY),
                        Opacity = 1f,
                        ValueTag = string.IsNullOrWhiteSpace(p.valueTag) ? "{progress}" : p.valueTag,
                        Minimum = p.min,
                        Maximum = p.max > p.min ? p.max : p.min + 100f,
                        Width = p.width > 0f ? p.width : 300f,
                        Height = p.height > 0f ? p.height : 20f,
                        FillDirection = (OvProgressFillDirection)Mathf.Clamp(p.fillDirection, 0, 3),
                        BackgroundColor = KvSpecBuilder.HexToRgba(p.backgroundColor, new[] { 0f, 0f, 0f, 0.45f }),
                        FillColor = KvSpecBuilder.HexToRgba(p.fillColor, new[] { 0.2f, 0.75f, 1f, 0.95f }),
                        BorderColor = KvSpecBuilder.HexToRgba(p.borderColor, new[] { 1f, 1f, 1f, 0.8f }),
                        BorderThickness = p.borderThickness,
                        CornerRadius = p.cornerRadius,
                        ClampValue = true,
                    });
                }
            }

            return package;
        }

        /// <summary>校验规格是否至少含一个可见组件</summary>
        public static bool Validate(OvSpec spec, out int count, out string error)
        {
            count = 0;
            error = "";
            if (spec == null)
            {
                error = "AI 返回的 Overlayer 配置无法解析为 JSON";
                return false;
            }
            count = (spec.texts?.Count ?? 0) + (spec.progressBars?.Count ?? 0);
            if (count == 0)
            {
                error = "配置中没有任何文本或进度条组件";
                return false;
            }
            return true;
        }
    }
}
