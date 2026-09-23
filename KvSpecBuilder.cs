using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Xml;
using System.Xml.Serialization;
using UnityEngine;

namespace ADOFAI.AgentKeyViewer
{
    /// <summary>
    /// 意图规格（<see cref="KVConfig"/>）→ CT 配置 XML 的确定性构建器。
    ///
    /// 同时支持两种目标格式：
    ///   - 旧格式 <c>&lt;KeyViewerPackage&gt;</c>（KeyViewer.xml），兼容旧版 CT；
    ///   - Sonnet 新格式 <c>&lt;CheryToolsSonnetKeyViewer&gt;</c>（KeyViewer.Sonnet.xml）。
    ///
    /// 坐标与字段映射严格镜像 CT 自身的 ConvertLegacy/ConvertLegacyNode 实现：
    ///   - 本模组内部 x/y 沿用旧格式语义（按键左上角、Y 向下为正）；
    ///   - 新格式 KvKey.PositionX/Y 是按键<b>中心</b>，且 <b>Y 轴取反</b>：
    ///       PositionX = x + w/2, PositionY = -(y + h/2)；
    ///   - 新格式 RainRow 语义为 <b>1=上排 / 2=下排</b>（旧格式是 1/0）；
    ///   - 键雨偏移量 KeyRainYOffsetRow1/2 与 RainYOffset/RainYOffset2 是<b>同值直传</b>，不取反；
    ///   - 各类 Y 向偏移（文字/计数/阴影/媒体）在新格式中取反。
    ///
    /// 两排键雨的几何对齐在<b>构建期</b>算好，不再依赖生成后的 XML 打补丁。
    /// </summary>
    public static class KvSpecBuilder
    {
        /// <summary>旧格式下每键的键雨修正结果</summary>
        private sealed class KeyRain
        {
            public int Row;          // 旧格式 1=上排/0=下排；新格式 1=上排/2=下排
            public float YOffset;
            public float WidthRatio;
            public float[] Color;    // 上排=该键边框色，下排=白色
        }

        private sealed class RowPlan
        {
            public readonly Dictionary<KVKeyConfig, KeyRain> PerKey = new Dictionary<KVKeyConfig, KeyRain>();
            public float Row1Offset;
            public float Row2Offset;
            public bool TwoRows;
        }

        // ====================================================================
        //  两排键雨几何对齐（构建期计算）
        // ====================================================================

        /// <summary>
        /// 按 PositionY 分排并计算键雨参数。
        /// 对齐公式（沿用用户确认的几何精确对齐）：Row2偏移 = (下排键底Y − 上排键底Y) + Row1偏移，
        /// 其中键底Y = PositionY + Height。上排取各自边框色，下排固定白色且更细(0.7)。
        /// </summary>
        private static RowPlan ComputeRows(KVConfig cfg)
        {
            var plan = new RowPlan();
            var keys = cfg?.keys;
            if (keys == null || keys.Count == 0) return plan;

            // 按 y 分组（y 小 = 上排）
            var byY = new SortedDictionary<float, List<KVKeyConfig>>();
            foreach (var k in keys)
            {
                float y = k?.y ?? 0f;
                if (!byY.TryGetValue(y, out var list)) { list = new List<KVKeyConfig>(); byY[y] = list; }
                list.Add(k);
            }

            float Height(KVKeyConfig k) => k.h > 0f ? k.h : 70f;

            if (byY.Count >= 2)
            {
                var ys = new List<float>(byY.Keys);
                var upper = byY[ys[0]];
                var lower = byY[ys[1]];
                plan.TwoRows = true;

                float upBottom = upper[0].y + Height(upper[0]);
                float lowBottom = lower[0].y + Height(lower[0]);
                plan.Row1Offset = 0f;
                plan.Row2Offset = (lowBottom - upBottom) + plan.Row1Offset;

                foreach (var k in upper)
                    plan.PerKey[k] = new KeyRain { Row = 1, YOffset = plan.Row1Offset, WidthRatio = 1f, Color = BorderColorOf(cfg, k) };
                foreach (var k in lower)
                    plan.PerKey[k] = new KeyRain { Row = 0, YOffset = plan.Row2Offset, WidthRatio = 0.7f, Color = new[] { 1f, 1f, 1f, 1f } };
            }
            else
            {
                // 单排：不强制逐键自定义雨，保留配置里的值
                foreach (var k in keys)
                    plan.PerKey[k] = new KeyRain { Row = k.rainRow, YOffset = k.rainHeightOffset, WidthRatio = k.rainWidthRatio > 0f ? k.rainWidthRatio : 0.72f, Color = null };
            }
            return plan;
        }

        /// <summary>取该键的边框色（优先逐键，其次全局），返回 RGBA float[4]</summary>
        private static float[] BorderColorOf(KVConfig cfg, KVKeyConfig k)
        {
            var hex = !string.IsNullOrEmpty(k.borderColor) ? k.borderColor : cfg.keyBorderColor;
            return HexToRgba(hex, new[] { 1f, 1f, 1f, 1f });
        }

        /// <summary>该键是否启用了逐键自定义雨色（单排时用于决定是否写节点级雨参数）</summary>
        private static bool UsesCustomRain(KVConfig cfg, KVKeyConfig k, RowPlan plan)
        {
            if (!plan.PerKey.TryGetValue(k, out var r)) return k.useCustomRain;
            // 两排布局一律强制逐键雨（红键红雨、蓝键蓝雨）
            if (plan.TwoRows) return true;
            return k.useCustomRain;
        }

        // ====================================================================
        //  Sonnet 新格式
        // ====================================================================

        /// <summary>构建 Sonnet 新格式 XML 字符串（KeyViewer.Sonnet.xml 的内容）</summary>
        public static string BuildSonnetXml(KVConfig cfg)
        {
            var bytes = BuildSonnetXmlBytes(cfg);
            return new UTF8Encoding(false).GetString(bytes).TrimStart('\uFEFF');
        }

        /// <summary>构建 Sonnet 新格式 XML 字节（与 CT 一致：直接 XmlSerializer 序列化到流，保留 BOM 与 utf-8 声明）</summary>
        public static byte[] BuildSonnetXmlBytes(KVConfig cfg)
        {
            var package = BuildSonnetPackage(cfg);
            var serializer = new XmlSerializer(typeof(KvTransferPackage));
            using (var ms = new MemoryStream())
            {
                serializer.Serialize(ms, package);
                return ms.ToArray();
            }
        }

        /// <summary>意图规格 → Sonnet 强类型包对象</summary>
        public static KvTransferPackage BuildSonnetPackage(KVConfig cfg)
        {
            if (cfg == null) throw new ArgumentNullException(nameof(cfg));

            var plan = ComputeRows(cfg);

            var profile = new KvProfile
            {
                Name = string.IsNullOrWhiteSpace(cfg.name) ? "AI生成配置" : cfg.name,
                Enabled = true,
                ShowInGame = true,
                OnlyShowPlaying = false,
                TotalHits = 0,
                // LayoutVersion=1 表示使用逐键显式坐标（与 CT 的 ConvertLegacyProfile 一致）
                LayoutVersion = 1,
                OffsetX = 0f,
                OffsetY = -330f,
                Scale = cfg.scale > 0.01f ? cfg.scale : 1f,
                CornerRadius = cfg.keyCornerRadius > 0f ? cfg.keyCornerRadius : 6f,
                BorderThickness = 2f,
                LabelSize = 17f,
                CountSize = 13f,
                HideCount = !cfg.showPerKeyCount,
                ShowKps = cfg.showKpsTotal,
                ShowTotal = cfg.showKpsTotal,
                FontPath = "Assets/Fonts/adofai.otf",
                BackgroundNormal = HexToRgba(cfg.keyIdleColor, new[] { 0.075f, 0.085f, 0.11f, 0.88f }),
                BackgroundPressed = HexToRgba(cfg.keyPressedColor, new[] { 0.3f, 0.76f, 1f, 0.96f }),
                BorderNormal = HexToRgba(cfg.keyBorderColor, new[] { 0.42f, 0.48f, 0.58f, 0.7f }),
                BorderPressed = HexToRgba(cfg.keyPressedColor, new[] { 0.72f, 0.92f, 1f, 1f }),
                TextNormal = HexToRgba(cfg.keyTextColor, new[] { 0.94f, 0.96f, 1f, 1f }),
                TextPressed = HexToRgba(cfg.keyTextPressedColor, new[] { 0.015f, 0.025f, 0.04f, 1f }),
                RainEnabled = cfg.enableRain,
                RainSpeed = cfg.rainSpeed > 0f ? cfg.rainSpeed : 620f,
                RainMaxHeight = cfg.rainDistance > 0f ? cfg.rainDistance : 420f,
                RainWidthRatio = 1f,
                RainWidthRatio2 = 0.7f,
                RainYOffset = plan.Row1Offset,
                RainYOffset2 = plan.Row2Offset,
                RainCornerRadius = 7f,
                RainFadeMode = cfg.rainFadeMode,
                RainFadeHeight = 0.22f,
                RainFadePower = 1f,
                RainGradientEnabled = false,
                RainColor = HexToRgba(cfg.rainColor, new[] { 0.3f, 0.76f, 1f, 0.74f }),
                RainColor2 = new[] { 1f, 1f, 1f, 1f },
            };

            foreach (var k in cfg.keys)
                profile.Keys.Add(BuildSonnetKey(cfg, k, plan));

            var package = new KvTransferPackage
            {
                FormatVersion = 1,
                Kind = "profile",
                ExportedAt = DateTime.Now.ToString("O"),
                ScreenWidth = Screen.width,
                ScreenHeight = Screen.height,
            };
            package.Profiles.Add(profile);
            return package;
        }

        private static KvKey BuildSonnetKey(KVConfig cfg, KVKeyConfig k, RowPlan plan)
        {
            int nodeType = (k.displayMode == 1 || k.useImage) ? 3 : k.nodeType;
            bool isCount = nodeType == 1 || nodeType == 2;
            var bind = isCount ? KeyCode.None : KVKeyConfig.ParseKeyCode(k.keyCode, KeyCode.D);

            float w = k.w > 0f ? k.w : 70f;
            float h = k.h > 0f ? k.h : 70f;
            float scale = cfg.scale > 0.01f ? cfg.scale : 1f;

            var label = !string.IsNullOrEmpty(k.label) ? k.label
                      : nodeType == 1 ? "KPS"
                      : nodeType == 2 ? "TOTAL"
                      : bind == KeyCode.None ? string.Empty : bind.ToString();

            plan.PerKey.TryGetValue(k, out var rain);
            bool customRain = UsesCustomRain(cfg, k, plan);

            var key = new KvKey
            {
                NodeType = nodeType,
                Bind = bind,
                Label = label,
                ImagePath = k.imageFile ?? string.Empty,
                VideoPath = string.Empty,
                VideoLoop = true,
                MediaScale = 1f,
                Opacity = Mathf.Clamp01(k.imageOpacity <= 0f ? 1f : k.imageOpacity),
                Depth = 0,
                Locked = false,
                HitCount = 0,
                RainEnabled = cfg.enableRain,
                // 新格式：1=上排 / 2=下排（旧格式为 1/0）
                RainRow = rain != null && plan.TwoRows ? (rain.Row == 1 ? 1 : 2) : 1,
                // 旧格式 x/y 是左上角，新格式是中心且 Y 取反
                PositionX = k.x + w * 0.5f * scale,
                PositionY = -(k.y + h * 0.5f * scale),
                Width = w * scale,
                Height = h * scale,
                CornerRadius = k.cornerRadius > 0f ? k.cornerRadius * scale : -1f,
                BorderThickness = k.borderThickness > 0f ? k.borderThickness * scale : -1f,
                LabelSize = 17f,
                CountSize = 13f,
                CountAlignment = isCount ? (nodeType == 1 ? 0 : 2) : 1,
                HideCount = !cfg.showPerKeyCount,
                RainCornerRadius = 7f,
                RainWidthRatio = rain != null ? rain.WidthRatio : (k.rainWidthRatio > 0f ? k.rainWidthRatio : 0.72f),
                RainYOffset = rain != null ? rain.YOffset : k.rainHeightOffset,
                UseCustomRain = customRain,
            };

            // 颜色：逐键自定义优先，否则跟随全局（与 CT ConvertLegacyNode 一致，始终写入显式值）
            key.BackgroundNormal = HexToRgba(Pick(k.idleColor, cfg.keyIdleColor), new[] { 0.075f, 0.085f, 0.11f, 0.88f });
            key.BackgroundPressed = HexToRgba(Pick(k.pressedColor, cfg.keyPressedColor), new[] { 0.3f, 0.76f, 1f, 0.96f });
            key.BorderNormal = HexToRgba(Pick(k.borderColor, cfg.keyBorderColor), new[] { 0.42f, 0.48f, 0.58f, 0.7f });
            key.BorderPressed = HexToRgba(Pick(k.pressedColor, cfg.keyPressedColor), new[] { 0.72f, 0.92f, 1f, 1f });
            key.TextNormal = HexToRgba(Pick(k.textColor, cfg.keyTextColor), new[] { 0.94f, 0.96f, 1f, 1f });
            key.TextPressed = HexToRgba(Pick(k.textPressedColor, cfg.keyTextPressedColor), new[] { 0.015f, 0.025f, 0.04f, 1f });

            // KPS/Total 与带自定义色的键需要进入自定义样式分支，否则 CT 会用全局样式覆盖
            key.UseCustomStyle = !string.IsNullOrEmpty(k.idleColor) || !string.IsNullOrEmpty(k.borderColor)
                                 || !string.IsNullOrEmpty(k.textColor) || isCount
                                 || key.CornerRadius >= 0f || key.BorderThickness >= 0f || key.HideCount;

            if (customRain)
            {
                var rc = rain?.Color ?? HexToRgba(k.rainColor, new[] { 1f, 1f, 1f, 1f });
                key.RainColor = rc;
                key.RainEndColor = new[] { rc[0], rc[1], rc[2], 0.08f };
                key.RainRightColor = new[] { rc[0], rc[1], rc[2], rc[3] };
            }
            else
            {
                key.RainColor = HexToRgba(cfg.rainColor, new[] { 0.3f, 0.76f, 1f, 0.74f });
            }

            return key;
        }

        // ====================================================================
        //  旧格式（<KeyViewerPackage> / KeyViewer.xml）
        // ====================================================================

        /// <summary>构建旧格式 CT XML（根 &lt;KeyViewerPackage&gt;），供旧版 CT 导入</summary>
        public static string BuildLegacyXml(KVConfig cfg)
        {
            if (cfg == null) throw new ArgumentNullException(nameof(cfg));
            var plan = ComputeRows(cfg);

            var doc = new XmlDocument();
            doc.AppendChild(doc.CreateXmlDeclaration("1.0", "utf-8", null));
            var root = doc.CreateElement("KeyViewerPackage");
            root.SetAttribute("xmlns:xsd", "http://www.w3.org/2001/XMLSchema");
            root.SetAttribute("xmlns:xsi", "http://www.w3.org/2001/XMLSchema-instance");
            doc.AppendChild(root);

            AppendElement(root, "FormatVersion", "3");
            AppendElement(root, "ExportedAt", DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"));
            AppendElement(root, "ExportScreenWidth", Screen.width.ToString());
            AppendElement(root, "ExportScreenHeight", Screen.height.ToString());

            var configs = doc.CreateElement("KeyViewerConfigurations");
            root.AppendChild(configs);
            var config = doc.CreateElement("KVConfiguration");
            configs.AppendChild(config);

            AppendElement(config, "Name", string.IsNullOrWhiteSpace(cfg.name) ? "未命名配置" : cfg.name);
            AppendElement(config, "IsEnabled", "true");
            AppendElement(config, "ShowInGame", "true");
            AppendElement(config, "OnlyShowPlaying", "false");
            AppendElement(config, "TotalHits", "0");

            var nodes = doc.CreateElement("Nodes");
            config.AppendChild(nodes);

            foreach (var k in cfg.keys)
                nodes.AppendChild(BuildLegacyNode(doc, cfg, k, plan));

            // ---- KVConfiguration 全局设置 ----
            AppendElement(config, "FontPath", "Assets/Fonts/adofai.otf");
            AppendElement(config, "Scale", (cfg.scale > 0.01f ? cfg.scale : 1f).ToString("0.####"));
            AppendElement(config, "BorderThickness", "2");
            AppendElement(config, "CornerRadius", (cfg.keyCornerRadius > 0f ? cfg.keyCornerRadius : 6f).ToString("0.####"));
            AppendElement(config, "HideCountText", (!cfg.showPerKeyCount).ToString().ToLowerInvariant());
            AppendColor(config, "ColorBgNormal", ParseColor(cfg.keyIdleColor, new Color(0.075f, 0.085f, 0.11f, 0.88f)));
            AppendColor(config, "ColorBgPressed", ParseColor(cfg.keyPressedColor, Color.white));
            AppendColor(config, "ColorBorderNormal", ParseColor(cfg.keyBorderColor, new Color(0.42f, 0.48f, 0.58f, 0.7f)));
            AppendColor(config, "ColorBorderPressed", ParseColor(cfg.keyPressedColor, Color.white));
            AppendColor(config, "ColorTextNormal", ParseColor(cfg.keyTextColor, Color.white));
            AppendColor(config, "ColorTextPressed", ParseColor(cfg.keyTextPressedColor, Color.black));
            AppendColor(config, "ColorKps", Color.white);
            AppendColor(config, "ColorTotal", Color.white);
            AppendElement(config, "EnableKeyRain", cfg.enableRain.ToString().ToLowerInvariant());
            AppendElement(config, "KeyRainSpeed", (cfg.rainSpeed > 0f ? cfg.rainSpeed : 620f).ToString("0.####"));
            AppendElement(config, "KeyRainMaxHeight", (cfg.rainDistance > 0f ? cfg.rainDistance : 420f).ToString("0.####"));
            AppendElement(config, "KeyRainFadeMode", cfg.rainFadeMode.ToString());
            AppendElement(config, "KeyRainWidthRatio1", "1");
            AppendElement(config, "KeyRainWidthRatio2", "0.7");
            AppendElement(config, "KeyRainYOffsetRow1", plan.Row1Offset.ToString("0.####"));
            AppendElement(config, "KeyRainYOffsetRow2", plan.Row2Offset.ToString("0.####"));
            AppendColor(config, "KeyRainColorRow1", ParseColor(cfg.rainColor, new Color(0.3f, 0.76f, 1f, 0.74f)));
            AppendColor(config, "KeyRainColorRow2", Color.white);
            AppendElement(config, "KeyPressAnimationEnabled", "false");

            return doc.OuterXml;
        }

        private static XmlElement BuildLegacyNode(XmlDocument doc, KVConfig cfg, KVKeyConfig k, RowPlan plan)
        {
            var node = doc.CreateElement("KVNode");
            int nodeType = (k.displayMode == 1 || k.useImage) ? 3 : k.nodeType;
            bool isCount = nodeType == 1 || nodeType == 2;

            AppendElement(node, "NodeType", nodeType.ToString());
            AppendElement(node, "KeyBind", isCount ? "None" : (string.IsNullOrEmpty(k.keyCode) ? "D" : k.keyCode));
            AppendElement(node, "CustomText", !string.IsNullOrEmpty(k.label) ? k.label
                                                : nodeType == 1 ? "KPS" : nodeType == 2 ? "TOTAL" : k.keyCode);
            AppendElement(node, "ImagePath", k.imageFile ?? "");
            AppendElement(node, "VideoPath", "");
            AppendElement(node, "VideoLoop", "true");
            AppendElement(node, "VideoContentScale", "1");
            AppendElement(node, "VideoContentOffsetX", "0");
            AppendElement(node, "VideoContentOffsetY", "0");
            AppendElement(node, "IsUnselectable", "false");
            AppendElement(node, "Opacity", (k.imageOpacity <= 0f ? 1f : Mathf.Clamp01(k.imageOpacity)).ToString("0.####"));
            AppendElement(node, "Depth", "0");
            AppendElement(node, "PositionX", k.x.ToString("0.####"));
            AppendElement(node, "PositionY", k.y.ToString("0.####"));
            AppendElement(node, "Width", Mathf.Max(1f, k.w > 0f ? k.w : 70f).ToString("0.####"));
            AppendElement(node, "Height", Mathf.Max(1f, k.h > 0f ? k.h : 70f).ToString("0.####"));
            AppendElement(node, "BorderThickness", (k.borderThickness > 0f ? k.borderThickness : -1f).ToString("0.####"));
            AppendElement(node, "CornerRadius", (k.cornerRadius > 0f ? k.cornerRadius : -1f).ToString("0.####"));
            AppendElement(node, "Scale", "1");
            AppendElement(node, "TextOffsetY", "0");
            AppendElement(node, "TextOffsetX", "0");
            AppendElement(node, "TextScale", "0.85");
            AppendElement(node, "CountOffsetY", "0");
            AppendElement(node, "CountOffsetX", "0");
            AppendElement(node, "CountScale", "0.65");
            // KPS 数值左对齐、Total 数值右对齐（CT 仅对数值生效，标签固定居中）
            AppendElement(node, "CountTextAlignment", isCount ? (nodeType == 1 ? "0" : "2") : "1");
            AppendElement(node, "KeyFontPath", "");
            AppendElement(node, "CountFontPath", "");
            AppendElement(node, "HideCountText", (!cfg.showPerKeyCount).ToString().ToLowerInvariant());
            AppendElement(node, "UseCustomOutline", "false");
            AppendElement(node, "KeyTextOutlineEnabled", "false");
            AppendElement(node, "KeyTextOutlineThickness", "1");
            AppendElement(node, "CountTextOutlineEnabled", "false");
            AppendElement(node, "CountTextOutlineThickness", "1");
            AppendElement(node, "UseCustomShadow", "false");
            AppendElement(node, "KeyTextShadowEnabled", "false");
            AppendElement(node, "CountTextShadowEnabled", "false");

            bool customColor = !string.IsNullOrEmpty(k.idleColor) || !string.IsNullOrEmpty(k.borderColor) || !string.IsNullOrEmpty(k.textColor);
            AppendElement(node, "UseCustomColor", customColor ? "true" : "false");
            AppendColor(node, "ColorBgNormal", ParseColor(Pick(k.idleColor, cfg.keyIdleColor), new Color(0.2f, 0.2f, 0.2f, 0.8f)));
            AppendColor(node, "ColorBgPressed", ParseColor(Pick(k.pressedColor, cfg.keyPressedColor), new Color(0.8f, 0.8f, 0.8f, 0.8f)));
            AppendColor(node, "ColorBorderNormal", ParseColor(Pick(k.borderColor, cfg.keyBorderColor), new Color(0.4f, 0.4f, 0.4f, 1f)));
            AppendColor(node, "ColorBorderPressed", ParseColor(Pick(k.pressedColor, cfg.keyPressedColor), new Color(1f, 1f, 1f, 1f)));
            AppendColor(node, "ColorTextNormal", ParseColor(Pick(k.textColor, cfg.keyTextColor), Color.white));
            AppendColor(node, "ColorTextPressed", ParseColor(Pick(k.textPressedColor, cfg.keyTextPressedColor), Color.black));

            plan.PerKey.TryGetValue(k, out var rain);
            bool customRain = UsesCustomRain(cfg, k, plan);
            int legacyRow = rain != null && plan.TwoRows ? rain.Row : k.rainRow;
            AppendElement(node, "RainRow", legacyRow.ToString());
            AppendElement(node, "EnableKeyRain", cfg.enableRain.ToString().ToLowerInvariant());
            AppendElement(node, "UseCustomRain", customRain ? "true" : "false");
            var rainColor = customRain
                ? (rain?.Color != null ? new Color(rain.Color[0], rain.Color[1], rain.Color[2], rain.Color[3]) : ParseColor(k.rainColor, Color.white))
                : ParseColor(cfg.rainColor, new Color(0.8f, 0.5f, 1f, 0.8f));
            AppendColor(node, "RainColor", rainColor);
            AppendElement(node, "RainGradientEnabled", "false");
            AppendElement(node, "RainHorizontalGradientEnabled", "false");
            AppendElement(node, "RainGradientMode", "0");
            AppendElement(node, "RainFadeHeight", "1");
            AppendElement(node, "RainFadePower", "1");
            AppendElement(node, "RainGradientHeight", "1");
            AppendElement(node, "RainGradientPower", "1");
            AppendElement(node, "RainWidthRatio", (rain != null ? rain.WidthRatio : (k.rainWidthRatio > 0f ? k.rainWidthRatio : 0.72f)).ToString("0.####"));
            AppendElement(node, "RainYOffset", (rain != null ? rain.YOffset : k.rainHeightOffset).ToString("0.####"));
            AppendElement(node, "RainCornerRadius", "0");
            AppendElement(node, "UseCustomRainShadow", "false");
            AppendElement(node, "RainShadowEnabled", "false");
            AppendElement(node, "RainShadowSoftness", "12");
            AppendElement(node, "RainShadowStrength", "1");
            AppendElement(node, "UseCustomKeyPressAnimation", "false");
            AppendElement(node, "KeyPressAnimationEnabled", "false");
            AppendElement(node, "KeyPressAnimationDuration", "0.12");
            AppendElement(node, "KeyPressAnimationEasing", "ease-out-quad");
            AppendElement(node, "KeyPressAnimationAffectColors", "true");
            AppendElement(node, "KeyPressAnimationScale", "1");
            AppendElement(node, "KeyPressAnimationOffsetX", "0");
            AppendElement(node, "KeyPressAnimationOffsetY", "0");
            AppendElement(node, "HitCount", "0");
            return node;
        }

        // ====================================================================
        //  工具
        // ====================================================================

        /// <summary>优先取逐键值，为空则回退全局值</summary>
        private static string Pick(string perKey, string global)
            => !string.IsNullOrEmpty(perKey) ? perKey : global;

        /// <summary>8 位 hex → RGBA float[4]（Unity Color 顺序）</summary>
        public static float[] HexToRgba(string hex, float[] fallback)
        {
            if (!string.IsNullOrEmpty(hex) && ColorUtility.TryParseHtmlString(hex, out var c))
                return new[] { c.r, c.g, c.b, c.a };
            return fallback ?? new[] { 1f, 1f, 1f, 1f };
        }

        private static Color ParseColor(string hex, Color def)
        {
            if (!string.IsNullOrEmpty(hex) && ColorUtility.TryParseHtmlString(hex, out var c)) return c;
            return def;
        }

        private static void AppendElement(XmlElement parent, string name, string value)
        {
            var el = parent.OwnerDocument.CreateElement(name);
            el.InnerText = value ?? "";
            parent.AppendChild(el);
        }

        private static void AppendColor(XmlElement parent, string name, Color c)
        {
            var el = parent.OwnerDocument.CreateElement(name);
            foreach (float v in new[] { c.r, c.g, c.b, c.a })
            {
                var f = parent.OwnerDocument.CreateElement("float");
                f.InnerText = v.ToString("0.####");
                el.AppendChild(f);
            }
            parent.AppendChild(el);
        }
    }
}
