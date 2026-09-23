using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Text;
using System.Xml;
using UnityEngine;
using UnityModManagerNet;

namespace ADOFAI.AgentKeyViewer
{
    /// <summary>
    /// CherryTools（CT）KV 配置包(.ctkv / .cyt)的双向导入导出。
    ///
    /// 格式说明（已逆向分析 CT 默认包 CheryTools_Default_Jipper.cyt）：
    ///   .ctkv/.cyt 实际上是一个 ZIP 压缩包，内含：
    ///     - Settings.xml   ：XML 序列化的 CT 设置，其中 <KeyViewerConfigurations>
    ///                         下包含一个或多个 <KVConfiguration>，每个配置下是一组 <KVNode>。
    ///     - PackageInfo.xml：包信息（FormatVersion / ExportScreenWidth / ExportScreenHeight）。
    ///     - Assets/        ：包内引用的字体/图片等资源（可选）。
    ///   因此 .ctkv 不能用文本编辑器直接阅读（是压缩包）。
    ///
    /// KVNode 关键字段与本模组 KVKeyConfig 的映射：
    ///   NodeType↔nodeType, KeyBind↔keyCode, CustomText↔label,
    ///   PositionX/Y↔x/y, Width/Height↔w/h,
    ///   CornerRadius↔cornerRadius, BorderThickness↔borderThickness,
    ///   ImagePath↔imageFile,
    ///   ColorBgNormal/Pressed↔idleColor/pressedColor,
    ///   ColorBorderNormal↔borderColor, ColorTextNormal/Pressed↔textColor/textPressedColor,
    ///   RainRow/RainColor/RainWidthRatio/RainYOffset↔rainRow/rainColor/rainWidthRatio/rainHeightOffset。
    /// </summary>
    public static class CtKvIo
    {
        /// <summary>CT KV 包默认存放目录：游戏目录/AgentKeyViewer_config/ctkv/</summary>
        public static string GetCtkvDir(UnityModManager.ModEntry modEntry)
        {
            var gameRoot = ModPathHelper.GetGameDir(modEntry);
            var dir = Path.Combine(gameRoot, "AgentKeyViewer_config", "ctkv");
            try { if (!Directory.Exists(dir)) Directory.CreateDirectory(dir); } catch { }
            return dir;
        }

        /// <summary>列出目录下所有 .ctkv 文件（含路径）</summary>
        public static string[] ListFiles(string dir)
        {
            try
            {
                return Directory.Exists(dir) ? Directory.GetFiles(dir, "*.ctkv") : new string[0];
            }
            catch { return new string[0]; }
        }

        // ====================================================================
        //  导入：.ctkv/.cyt → KVConfig
        // ====================================================================
        public static KVConfig Import(string filePath, out string error)
        {
            error = "";
            try
            {
                // 1. 读取压缩包里的配置 XML。
                //    注意：CT 的两种包入口不同——
                //      .cyt（CT 内置默认）→ 入口为 Settings.xml（根 <KeyViewerConfigurations>）
                //      .ctkv（CT 导出的 KV 包）→ 入口为 KeyViewer.xml（根 <KeyViewerPackage>）
                //    这里依次尝试，兼容两种格式。
                string settingsXml = "";
                string[] entryCandidates = { "Settings.xml", "KeyViewer.xml" };
                foreach (var cand in entryCandidates)
                {
                    settingsXml = ReadEntryFromZip(filePath, cand);
                    if (!string.IsNullOrEmpty(settingsXml)) break;
                }
                if (string.IsNullOrEmpty(settingsXml))
                {
                    error = "压缩包内未找到 Settings.xml / KeyViewer.xml";
                    return null;
                }

                var doc = new XmlDocument();
                doc.LoadXml(settingsXml);

                // 2. 定位第一个 KVConfiguration（优先取 IsEnabled 的）
                XmlNode configNode = null;
                var configs = doc.SelectNodes("//KeyViewerConfigurations/KVConfiguration");
                if (configs == null || configs.Count == 0)
                {
                    error = "Settings.xml 中未找到 KVConfiguration";
                    return null;
                }
                for (int i = 0; i < configs.Count; i++)
                {
                    var c = configs[i];
                    if (GetBool(c, "IsEnabled", false)) { configNode = c; break; }
                }
                if (configNode == null) configNode = configs[0];

                var cfg = new KVConfig
                {
                    name = GetString(configNode, "Name", "CT导入配置"),
                    showKpsTotal = true,
                    showPerKeyCount = true,
                    enableRain = true,
                };

                // 3. 解析 KVNode 列表
                var nodes = configNode.SelectNodes("Nodes/KVNode");
                if (nodes == null || nodes.Count == 0)
                {
                    error = "KVConfiguration 中没有按键节点";
                    return null;
                }

                bool first = true;
                foreach (XmlNode n in nodes)
                {
                    var k = new KVKeyConfig
                    {
                        id = GetString(n, "KeyBind", ""),
                        keyCode = GetString(n, "KeyBind", "D"),
                        label = string.IsNullOrEmpty(GetString(n, "CustomText", ""))
                                    ? GetString(n, "KeyBind", "") : GetString(n, "CustomText", ""),
                        nodeType = GetInt(n, "NodeType", 0),
                        x = GetFloat(n, "PositionX", 0f),
                        y = GetFloat(n, "PositionY", 0f),
                        w = GetFloat(n, "Width", 70f),
                        h = GetFloat(n, "Height", 70f),
                        // -1 = 跟随全局
                        cornerRadius = GetFloat(n, "CornerRadius", -1f) >= 0 ? GetFloat(n, "CornerRadius", -1f) : 0f,
                        borderThickness = GetFloat(n, "BorderThickness", -1f) >= 0 ? GetFloat(n, "BorderThickness", -1f) : 0f,
                        imageFile = GetString(n, "ImagePath", ""),
                        rainRow = GetInt(n, "RainRow", 0),
                        useCustomRain = GetBool(n, "UseCustomRain", false),
                        rainColor = ColorToHtml(GetColor(n, "RainColor", new Color(1f, 0.85f, 0.4f, 0.9f))),
                        rainWidthRatio = GetFloat(n, "RainWidthRatio", 0.12f),
                        rainHeightOffset = GetFloat(n, "RainYOffset", 0f),
                    };

                    bool customColor = GetBool(n, "UseCustomColor", false);
                    if (customColor)
                    {
                        k.idleColor = ColorToHtml(GetColor(n, "ColorBgNormal", new Color(0.1f, 0.1f, 0.15f, 0.92f)));
                        k.pressedColor = ColorToHtml(GetColor(n, "ColorBgPressed", new Color(0.95f, 0.75f, 0.15f, 1f)));
                        k.borderColor = ColorToHtml(GetColor(n, "ColorBorderNormal", new Color(0.55f, 0.55f, 0.55f, 1f)));
                        k.textColor = ColorToHtml(GetColor(n, "ColorTextNormal", Color.white));
                        k.textPressedColor = ColorToHtml(GetColor(n, "ColorTextPressed", Color.black));
                    }

                    // 首节点：如果启用自定义颜色/键雨，用其作为全局
                    if (first)
                    {
                        if (customColor)
                        {
                            cfg.keyIdleColor = k.idleColor;
                            cfg.keyPressedColor = k.pressedColor;
                            cfg.keyBorderColor = k.borderColor;
                            cfg.keyTextColor = k.textColor;
                            cfg.keyTextPressedColor = k.textPressedColor;
                        }
                        cfg.enableRain = GetBool(n, "EnableKeyRain", true);
                        first = false;
                    }

                    cfg.keys.Add(k);
                }

                if (cfg.keys.Count == 0)
                {
                    error = "未解析到任何按键";
                    return null;
                }
                return cfg;
            }
            catch (Exception ex)
            {
                error = ex.Message;
                CoreEntry.ModEntry?.Logger.Error($"[CtKvIo] 导入失败 {filePath}: {ex}");
                return null;
            }
        }

        // ====================================================================
        //  导出：KVConfig → .ctkv（ZIP 包：Settings.xml + PackageInfo.xml）
        // ====================================================================
        public static bool Export(KVConfig cfg, string filePath, out string error)
        {
            error = "";
            try
            {
                var settingsDoc = BuildSettingsXml(cfg);
                var infoDoc = BuildPackageInfoXml(cfg);

                var dir = Path.GetDirectoryName(filePath);
                if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir)) Directory.CreateDirectory(dir);
                if (File.Exists(filePath)) File.Delete(filePath);

                using (var zip = ZipFile.Open(filePath, ZipArchiveMode.Create))
                {
                    WriteEntry(zip, "KeyViewer.xml", settingsDoc);
                    WriteEntry(zip, "PackageInfo.xml", infoDoc);
                }
                CoreEntry.ModEntry?.Logger.Log($"[CtKvIo] 已导出 CT KV 包: {filePath}（{cfg.keys.Count} 键）");
                return true;
            }
            catch (Exception ex)
            {
                error = ex.Message;
                CoreEntry.ModEntry?.Logger.Error($"[CtKvIo] 导出失败 {filePath}: {ex}");
                return false;
            }
        }

        // ====================================================================
        //  直出：将 AI 生成的 CT XML 封装为 .ctkv 包（纯生成器模式）
        // ====================================================================

        /// <summary>
        /// 校验 AI 输出的 XML 是否为合法的 CT KV 配置包，并返回 (是否有效, 键数, 配置名, 错误)。
        /// 同时接受两种根元素：
        ///   - 旧格式 &lt;KeyViewerPackage&gt;（KeyViewerConfigurations/KVConfiguration/Nodes/KVNode）
        ///   - Sonnet 新格式 &lt;CheryToolsSonnetKeyViewer&gt;（Profiles/KvProfile/Keys/KvKey）
        /// </summary>
        public static bool ValidateGeneratedXml(string xml, out int keyCount, out string configName, out string error)
        {
            keyCount = 0;
            configName = "";
            error = "";
            try
            {
                if (string.IsNullOrWhiteSpace(xml))
                {
                    error = "AI 返回内容为空";
                    return false;
                }
                var doc = new XmlDocument();
                doc.LoadXml(xml);
                string root = doc.DocumentElement?.Name ?? "";
                if (root == "CheryToolsSonnetKeyViewer")
                    return ValidateSonnetDocument(doc, out keyCount, out configName, out error);
                if (root != "KeyViewerPackage")
                {
                    error = $"根元素不是 <KeyViewerPackage> 或 <CheryToolsSonnetKeyViewer>（实际为 <{root}>）";
                    return false;
                }
                var configs = doc.SelectNodes("//KeyViewerConfigurations/KVConfiguration");
                if (configs == null || configs.Count == 0)
                {
                    error = "未找到 <KVConfiguration>";
                    return false;
                }
                var cfg = configs[0];
                configName = GetString(cfg, "Name", "未命名配置");
                var nodes = cfg.SelectNodes("Nodes/KVNode");
                keyCount = nodes?.Count ?? 0;
                if (keyCount == 0)
                {
                    error = "配置中没有 <KVNode> 按键";
                    return false;
                }
                return true;
            }
            catch (Exception ex)
            {
                error = "XML 解析失败: " + ex.Message;
                return false;
            }
        }

        /// <summary>校验 Sonnet 新格式文档结构（Profiles/KvProfile/Keys/KvKey）</summary>
        private static bool ValidateSonnetDocument(XmlDocument doc, out int keyCount, out string configName, out string error)
        {
            keyCount = 0;
            configName = "";
            error = "";
            var profiles = doc.SelectNodes("//Profiles/KvProfile");
            if (profiles == null || profiles.Count == 0)
            {
                // Kind=node/nodes 的组件包只含 Nodes/KvKey
                var looseNodes = doc.SelectNodes("//Nodes/KvKey");
                keyCount = looseNodes?.Count ?? 0;
                if (keyCount == 0)
                {
                    error = "未找到 <KvProfile> 或 <KvKey>";
                    return false;
                }
                configName = "KV 组件";
                return true;
            }
            var profile = profiles[0];
            configName = GetString(profile, "Name", "未命名配置");
            var keys = profile.SelectNodes("Keys/KvKey");
            keyCount = keys?.Count ?? 0;
            if (keyCount == 0)
            {
                error = "KvProfile 中没有 <KvKey> 按键";
                return false;
            }
            return true;
        }

        /// <summary>
        /// 程序化兜底：检测两排布局并修正键雨对齐参数（AI 经常漏算/漏填）。
        /// 规则：第一排(上排) RainRow=1 用 Row1 参数，第二排(下排) RainRow=0 用 Row2 参数；
        /// KeyRainYOffsetRow2 = 第二排按键高度 − 第一排按键高度；
        /// KeyRainColorRow1 取上排键的 ColorBorderNormal；KeyRainColorRow2 白色；
        /// KeyRainWidthRatio1=1、KeyRainWidthRatio2=0.7（缺失时补默认值）。
        /// 返回修正后的 XML 字符串。
        /// </summary>
        public static string FixTwoRowRainLayout(string xml)
        {
            try
            {
                var doc = new XmlDocument();
                doc.LoadXml(xml);
                var cfg = doc.SelectSingleNode("//KeyViewerConfigurations/KVConfiguration") as XmlElement;
                var nodes = cfg?.SelectNodes("Nodes/KVNode");
                if (cfg == null || nodes == null || nodes.Count == 0) return xml;

                // KPS/Total 数值对齐兜底：KPS(NodeType=1) 左对齐(0)，Total(NodeType=2) 右对齐(2)。
                // CT 渲染时 CountTextAlignment 只影响数值文本（0=左/1=中/2=右），标签固定居中。
                foreach (XmlElement n in nodes)
                {
                    int type = int.TryParse(n["NodeType"]?.InnerText, out var t) ? t : 0;
                    if (type == 1) SetOrReplace(n, "CountTextAlignment", "0");
                    else if (type == 2) SetOrReplace(n, "CountTextAlignment", "2");
                }

                // 按 PositionY 分组
                float ParseF(XmlElement n, string tag, float def)
                {
                    var el = n[tag];
                    if (el == null || !float.TryParse(el.InnerText, System.Globalization.NumberStyles.Float,
                        System.Globalization.CultureInfo.InvariantCulture, out float v)) return def;
                    return v;
                }

                var byY = new SortedDictionary<float, List<XmlElement>>();
                foreach (XmlNode nx in nodes)
                {
                    if (nx is not XmlElement n) continue;
                    float y = ParseF(n, "PositionY", 0f);
                    if (!byY.TryGetValue(y, out var list)) { list = new List<XmlElement>(); byY[y] = list; }
                    list.Add(n);
                }
                if (byY.Count < 2) return xml; // 单排布局无需处理

                var keysArr = new List<float>(byY.Keys);           // 升序：Y 小 = 上排
                var upper = byY[keysArr[0]];                        // 第一排（上）
                var lower = byY[keysArr[1]];                        // 第二排（下）

                // 1) 修正每键 RainRow：上排=1（Row1 参数），下排=0（Row2 参数）
                foreach (var n in upper) SetOrReplace(n, "RainRow", "1");
                foreach (var n in lower) SetOrReplace(n, "RainRow", "0");

                float row1H = ParseF(upper[0], "Height", 70f);
                float row2H = ParseF(lower[0], "Height", 50f);

                // 2) 配置级键雨偏移：几何精确对齐——rain起始Y = 键底Y − 偏移，令两排相等：
                //    Row2偏移 = (下排键底Y − 上排键底Y) + Row1偏移
                float upBottom = ParseF(upper[0], "PositionY", 0f) + row1H;
                float lowBottom = ParseF(lower[0], "PositionY", 0f) + row2H;
                float row1Off = GetCfgFloat(cfg, "KeyRainYOffsetRow1", 0f);
                float row2Off = (lowBottom - upBottom) + row1Off;
                SetCfgFloat(cfg, "KeyRainYOffsetRow1", row1Off);
                SetCfgFloat(cfg, "KeyRainYOffsetRow2", row2Off);

                // 3) 键雨颜色：逐键跟随边框色（用户设想：红键红雨、蓝键蓝雨）。
                //    行级 KeyRainColorRow* 是整排单色，无法实现逐键变色，
                //    因此必须给每个键开启节点级 UseCustomRain 并写各自的 RainColor。
                //    注意：CT 中 UseCustomRain 时节点级 RainYOffset/RainWidthRatio 会覆盖行级配置，
                //    所以偏移和宽度比例也必须同时写入每个节点，否则对齐会失效。
                foreach (var n in upper)
                    ApplyNodeRain(n, ReadFloatArray(n["ColorBorderNormal"]), row1Off, 1f);
                foreach (var n in lower)
                    ApplyNodeRain(n, new[] { 1f, 1f, 1f, 1f }, row2Off, 0.7f);

                // 行级配置同步保留（作为无自定义雨键的回退）
                var border = upper[0]?["ColorBorderNormal"];
                if (border != null && border.SelectNodes("float").Count == 4)
                    ReplaceWithCopy(cfg, "KeyRainColorRow1", border);
                SetCfgFloatArray(cfg, "KeyRainColorRow2", new[] { 1f, 1f, 1f, 1f });
                SetCfgFloat(cfg, "KeyRainWidthRatio1", 1f);
                SetCfgFloat(cfg, "KeyRainWidthRatio2", 0.7f);

                CoreEntry.ModEntry?.Logger.Log($"[AIConfig] 两排键雨兜底修正: YOffsetRow1={row1Off}, YOffsetRow2={row2Off} (upBottom={upBottom}, lowBottom={lowBottom}), 逐键自定义雨已应用(上排=各自边框色, 下排=白色)");
                return doc.OuterXml;
            }
            catch (Exception ex)
            {
                CoreEntry.ModEntry?.Logger.Error($"[AIConfig] FixTwoRowRainLayout 失败: {ex.Message}（保留原始 XML）");
                return xml;
            }
        }

        /// <summary>给单个 KVNode 应用节点级自定义雨参数（覆盖行级配置）；color 为 null 时保持 AI 已写的值</summary>
        private static void ApplyNodeRain(XmlElement node, float[] color, float yOffset, float widthRatio)
        {
            SetOrReplace(node, "EnableKeyRain", "true");
            SetOrReplace(node, "UseCustomRain", "true");
            if (color != null) SetCfgFloatArray(node, "RainColor", color);
            else if (node["RainColor"] == null || node["RainColor"].SelectNodes("float").Count != 4)
                SetCfgFloatArray(node, "RainColor", new[] { 1f, 1f, 1f, 1f });
            SetCfgFloat(node, "RainYOffset", yOffset);
            SetCfgFloat(node, "RainWidthRatio", widthRatio);
        }

        /// <summary>读取元素中的 <float>×4 数组，格式非法返回 null</summary>
        private static float[] ReadFloatArray(XmlElement el)
        {
            if (el == null) return null;
            var fs = el.SelectNodes("float");
            if (fs == null || fs.Count != 4) return null;
            try
            {
                return new[]
                {
                    float.Parse(fs[0].InnerText, System.Globalization.CultureInfo.InvariantCulture),
                    float.Parse(fs[1].InnerText, System.Globalization.CultureInfo.InvariantCulture),
                    float.Parse(fs[2].InnerText, System.Globalization.CultureInfo.InvariantCulture),
                    float.Parse(fs[3].InnerText, System.Globalization.CultureInfo.InvariantCulture),
                };
            }
            catch { return null; }
        }

        private static void SetOrReplace(XmlElement parent, string tag, string text)
        {
            var el = parent[tag];
            if (el == null) { el = parent.OwnerDocument.CreateElement(tag); parent.AppendChild(el); }
            el.InnerText = text;
        }

        private static float GetCfgFloat(XmlElement cfg, string tag, float def)
        {
            var el = cfg[tag];
            if (el == null || !float.TryParse(el.InnerText, System.Globalization.NumberStyles.Float,
                System.Globalization.CultureInfo.InvariantCulture, out float v)) return def;
            return v;
        }

        private static void SetCfgFloat(XmlElement cfg, string tag, float value)
        {
            SetOrReplace(cfg, tag, value.ToString(System.Globalization.CultureInfo.InvariantCulture));
        }

        private static void SetCfgFloatArray(XmlElement cfg, string tag, float[] values)
        {
            var doc = cfg.OwnerDocument;
            var old = cfg[tag];
            if (old != null) cfg.RemoveChild(old);
            var wrap = doc.CreateElement(tag);
            foreach (float v in values)
            {
                var f = doc.CreateElement("float");
                f.InnerText = v.ToString(System.Globalization.CultureInfo.InvariantCulture);
                wrap.AppendChild(f);
            }
            cfg.AppendChild(wrap);
        }

        private static void ReplaceWithCopy(XmlElement cfg, string tag, XmlElement source)
        {
            var doc = cfg.OwnerDocument;
            var old = cfg[tag];
            if (old != null) cfg.RemoveChild(old);
            var wrap = doc.CreateElement(tag);
            foreach (XmlNode f in source.SelectNodes("float"))
                wrap.AppendChild(doc.ImportNode(f, true));
            cfg.AppendChild(wrap);
        }

        /// <summary>把 AI 生成的 CT XML 写入 .ctkv（ZIP：KeyViewer.xml + PackageInfo.xml）。返回文件完整路径，失败返回 null。</summary>
        public static string SaveGeneratedCtkv(string xml, string fileName, UnityModManager.ModEntry modEntry, out string error)
        {
            error = "";
            try
            {
                if (!ValidateGeneratedXml(xml, out int keys, out string name, out error)) return null;

                // 规范化：确保根元素存在，且补充 PackageInfo 需要的信息
                var doc = new XmlDocument();
                doc.LoadXml(xml);

                var dir = GetCtkvDir(modEntry);
                if (string.IsNullOrEmpty(fileName)) fileName = name;
                if (string.IsNullOrEmpty(fileName)) fileName = "AI生成配置";
                if (!fileName.EndsWith(".ctkv", StringComparison.OrdinalIgnoreCase)) fileName += ".ctkv";
                // 去掉非法文件名字符
                foreach (char c in Path.GetInvalidFileNameChars()) fileName = fileName.Replace(c, '_');

                string filePath = Path.Combine(dir, fileName);
                if (File.Exists(filePath)) File.Delete(filePath);

                using (var zip = ZipFile.Open(filePath, ZipArchiveMode.Create))
                {
                    WriteEntry(zip, "KeyViewer.xml", doc.OuterXml);
                    WriteEntry(zip, "PackageInfo.xml", BuildMinimalPackageInfoXml());
                }
                CoreEntry.ModEntry?.Logger.Log($"[CtKvIo] 已直出 CT KV 包: {filePath}（{keys} 键）");
                return filePath;
            }
            catch (Exception ex)
            {
                error = ex.Message;
                CoreEntry.ModEntry?.Logger.Error($"[CtKvIo] 直出 .ctkv 失败: {ex}");
                return null;
            }
        }

        // ====================================================================
        //  Sonnet 新格式：解析 / 兜底修正 / 导出
        // ====================================================================

        /// <summary>把 Sonnet 新格式 XML 反序列化为强类型包对象（同时起到结构校验作用）</summary>
        public static KvTransferPackage ParseSonnetXml(string xml, out string error)
        {
            error = "";
            try
            {
                var serializer = new System.Xml.Serialization.XmlSerializer(typeof(KvTransferPackage));
                using (var reader = new System.IO.StringReader(xml))
                    return serializer.Deserialize(reader) as KvTransferPackage;
            }
            catch (Exception ex)
            {
                error = "Sonnet XML 反序列化失败: " + ex.Message;
                return null;
            }
        }

        /// <summary>把 Sonnet 包对象序列化为 XML 字符串（用于预览/展示）</summary>
        public static string SerializeSonnetPackage(KvTransferPackage pkg)
        {
            var serializer = new System.Xml.Serialization.XmlSerializer(typeof(KvTransferPackage));
            using (var ms = new MemoryStream())
            {
                serializer.Serialize(ms, pkg);
                return new UTF8Encoding(false).GetString(ms.ToArray()).TrimStart('\uFEFF');
            }
        }

        /// <summary>把 Overlayer 包对象序列化为 XML 字符串（用于预览/展示）</summary>
        public static string SerializeOvPackage(OvTransferPackage pkg)
        {
            var serializer = new System.Xml.Serialization.XmlSerializer(typeof(OvTransferPackage));
            using (var ms = new MemoryStream())
            {
                serializer.Serialize(ms, pkg);
                return new UTF8Encoding(false).GetString(ms.ToArray()).TrimStart('\uFEFF');
            }
        }

        /// <summary>
        /// 程序化兜底（对象级）：检测两排布局并修正键雨对齐。
        /// 新格式坐标约定与旧格式不同，注意：
        ///   - KvKey.PositionX/Y 是按键<b>中心</b>且 <b>Y 轴取反</b>，因此 PositionY 越大越靠上；
        ///   - 键底Y（新坐标）= PositionY − Height/2；
        ///   - RainRow 语义为 <b>1=上排 / 2=下排</b>；
        ///   - 偏移量与旧格式同值直传（与 CT 的 ConvertLegacy 一致，不取反）：
        ///       RainYOffset2 = (上排底Y − 下排底Y) + RainYOffset
        /// </summary>
        public static void FixSonnetRainLayout(KvTransferPackage pkg)
        {
            if (pkg?.Profiles == null) return;
            foreach (var profile in pkg.Profiles)
            {
                if (profile?.Keys == null || profile.Keys.Count == 0) continue;

                // KPS 数值左对齐(0)、Total 数值右对齐(2)
                foreach (var k in profile.Keys)
                {
                    if (k.NodeType == 1) k.CountAlignment = 0;
                    else if (k.NodeType == 2) k.CountAlignment = 2;
                }

                // 按 PositionY 分组：新格式 Y 越大越靠上
                var byY = new SortedDictionary<float, List<KvKey>>(
                    Comparer<float>.Create((a, b) => b.CompareTo(a))); // 降序：上排在前
                foreach (var k in profile.Keys)
                {
                    if (!byY.TryGetValue(k.PositionY, out var list)) { list = new List<KvKey>(); byY[k.PositionY] = list; }
                    list.Add(k);
                }
                if (byY.Count < 2) continue;

                var ys = new List<float>(byY.Keys);
                var upper = byY[ys[0]];
                var lower = byY[ys[1]];

                float BottomOf(KvKey k) => k.PositionY - (k.Height > 0f ? k.Height : profile.KeyHeight) * 0.5f;

                float row1Off = profile.RainYOffset;
                float row2Off = (BottomOf(upper[0]) - BottomOf(lower[0])) + row1Off;
                profile.RainYOffset = row1Off;
                profile.RainYOffset2 = row2Off;
                if (profile.RainWidthRatio <= 0f) profile.RainWidthRatio = 1f;
                if (profile.RainWidthRatio2 <= 0f) profile.RainWidthRatio2 = 0.7f;

                // 逐键雨：上排跟随各自边框色，下排固定白色且更细
                foreach (var k in upper)
                {
                    k.RainEnabled = profile.RainEnabled;
                    k.RainRow = 1;
                    k.UseCustomRain = true;
                    k.RainColor = Clone4(k.BorderNormal, new[] { 1f, 1f, 1f, 1f });
                    k.RainEndColor = new[] { k.RainColor[0], k.RainColor[1], k.RainColor[2], 0.08f };
                    k.RainRightColor = Clone4(k.RainColor, new[] { 1f, 1f, 1f, 1f });
                    k.RainYOffset = row1Off;
                    k.RainWidthRatio = profile.RainWidthRatio;
                }
                foreach (var k in lower)
                {
                    k.RainEnabled = profile.RainEnabled;
                    k.RainRow = 2;
                    k.UseCustomRain = true;
                    k.RainColor = new[] { 1f, 1f, 1f, 1f };
                    k.RainEndColor = new[] { 1f, 1f, 1f, 0.08f };
                    k.RainRightColor = new[] { 1f, 1f, 1f, 1f };
                    k.RainYOffset = row2Off;
                    k.RainWidthRatio = profile.RainWidthRatio2;
                }

                // 行级配置同步保留（无自定义雨的键的回退）
                if (upper[0].BorderNormal != null) profile.RainColor = Clone4(upper[0].BorderNormal, profile.RainColor);
                profile.RainColor2 = new[] { 1f, 1f, 1f, 1f };

                CoreEntry.ModEntry?.Logger.Log($"[CtKvIo] Sonnet 两排键雨兜底: RainYOffset={row1Off}, RainYOffset2={row2Off}, 逐键自定义雨已应用");
            }
        }

        private static float[] Clone4(float[] src, float[] fallback)
            => src != null && src.Length >= 4 ? new[] { src[0], src[1], src[2], src[3] } : fallback;

        /// <summary>把意图规格构建为 Sonnet 新格式 .ctkv 并保存（条目 KeyViewer.Sonnet.xml）</summary>
        public static string SaveSonnetCtkvFromConfig(KVConfig cfg, string fileName, UnityModManager.ModEntry modEntry, out string error)
        {
            byte[] bytes;
            try
            {
                bytes = KvSpecBuilder.BuildSonnetXmlBytes(cfg);
            }
            catch (Exception ex)
            {
                error = "构建 Sonnet 包失败: " + ex.Message;
                CoreEntry.ModEntry?.Logger.Error($"[CtKvIo] {error}");
                return null;
            }
            return SavePackage(fileName, ".ctkv", "KeyViewer.Sonnet.xml", bytes, cfg?.name, modEntry, out error);
        }

        /// <summary>把 AI 直写的 Sonnet XML 保存为 .ctkv（会先做键雨兜底修正）</summary>
        public static string SaveSonnetCtkvFromXml(string xml, string fileName, UnityModManager.ModEntry modEntry, out string error)
        {
            error = "";
            var pkg = ParseSonnetXml(xml, out error);
            if (pkg == null) return null;
            FixSonnetRainLayout(pkg);

            byte[] bytes;
            try
            {
                var serializer = new System.Xml.Serialization.XmlSerializer(typeof(KvTransferPackage));
                using (var ms = new MemoryStream())
                {
                    serializer.Serialize(ms, pkg);
                    bytes = ms.ToArray();
                }
            }
            catch (Exception ex)
            {
                error = "序列化 Sonnet 包失败: " + ex.Message;
                return null;
            }
            string name = pkg.Profiles != null && pkg.Profiles.Count > 0 ? pkg.Profiles[0].Name : null;
            return SavePackage(fileName, ".ctkv", "KeyViewer.Sonnet.xml", bytes, name, modEntry, out error);
        }

        /// <summary>把意图规格构建为旧格式 .ctkv 并保存（条目 KeyViewer.xml + PackageInfo.xml）</summary>
        public static string SaveLegacyCtkvFromConfig(KVConfig cfg, string fileName, UnityModManager.ModEntry modEntry, out string error)
        {
            error = "";
            try
            {
                string xml = KvSpecBuilder.BuildLegacyXml(cfg);
                var dir = GetCtkvDir(modEntry);
                string filePath = ResolvePackagePath(dir, fileName, ".ctkv", cfg?.name);
                if (File.Exists(filePath)) File.Delete(filePath);
                using (var zip = ZipFile.Open(filePath, ZipArchiveMode.Create))
                {
                    WriteEntry(zip, "KeyViewer.xml", xml);
                    WriteEntry(zip, "PackageInfo.xml", BuildMinimalPackageInfoXml());
                }
                CoreEntry.ModEntry?.Logger.Log($"[CtKvIo] 已导出旧格式 KV 包: {filePath}（{cfg?.keys?.Count ?? 0} 键）");
                return filePath;
            }
            catch (Exception ex)
            {
                error = ex.Message;
                CoreEntry.ModEntry?.Logger.Error($"[CtKvIo] 旧格式导出失败: {ex}");
                return null;
            }
        }

        /// <summary>保存 Overlayer(.ctov) 包（条目 Overlayer.Sonnet.xml）</summary>
        public static string SaveOvPackage(OvTransferPackage pkg, string fileName, UnityModManager.ModEntry modEntry, out string error)
        {
            error = "";
            byte[] bytes;
            try
            {
                var serializer = new System.Xml.Serialization.XmlSerializer(typeof(OvTransferPackage));
                using (var ms = new MemoryStream())
                {
                    serializer.Serialize(ms, pkg);
                    bytes = ms.ToArray();
                }
            }
            catch (Exception ex)
            {
                error = "序列化 Overlayer 包失败: " + ex.Message;
                CoreEntry.ModEntry?.Logger.Error($"[CtKvIo] {error}");
                return null;
            }
            return SavePackage(fileName, ".ctov", "Overlayer.Sonnet.xml", bytes, null, modEntry, out error);
        }

        /// <summary>
        /// CT Overlayer 模块的设置文件路径：
        /// &lt;游戏目录&gt;/Mods/CheryTools/Modules/CheryTools.Overlayer.Preview.xml
        /// </summary>
        public static string GetCtOverlayerSettingsPath(UnityModManager.ModEntry modEntry)
        {
            var gameRoot = ModPathHelper.GetGameDir(modEntry);
            return Path.Combine(gameRoot, "Mods", "CheryTools", "Modules", "CheryTools.Overlayer.Preview.xml");
        }

        /// <summary>
        /// 把 Overlayer 生成结果导出为 CT 的 Overlayer <b>设置文件</b>（而非 .ctov 包）。
        ///
        /// 用于绕过 CT 的导入 Bug：CT 的「导入 .ctov」会因 XmlReaderSettings.DtdProcessing
        /// 在当前 Unity 运行时无法解析而必挂，但它的设置文件读取走 XmlSerializer Stream 重载，不受影响。
        /// 用户需在<b>关闭游戏</b>后把导出的文件覆盖到 <see cref="GetCtOverlayerSettingsPath"/> 再启动游戏。
        /// </summary>
        public static string SaveOvSettingsFile(OvTransferPackage pkg, string fileName,
            UnityModManager.ModEntry modEntry, out string error)
        {
            error = "";
            if (pkg == null)
            {
                error = "没有可导出的 Overlayer 结果";
                return null;
            }
            try
            {
                // 以 CT 现有设置为基底做「追加合并」，避免整体替换把用户已有的覆盖物清掉。
                // 与 CT 自身 MergeIntoSettings 的行为一致：同名组件自动改名后追加。
                var settings = LoadExistingOvSettings() ?? new OvSettingsFile();

                var used = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                foreach (var t in settings.Texts) if (!string.IsNullOrEmpty(t?.Name)) used.Add(t.Name);
                foreach (var i in settings.Images) if (!string.IsNullOrEmpty(i?.Name)) used.Add(i.Name);
                foreach (var v in settings.Videos) if (!string.IsNullOrEmpty(v?.Name)) used.Add(v.Name);
                foreach (var b in settings.ProgressBars) if (!string.IsNullOrEmpty(b?.Name)) used.Add(b.Name);

                int added = 0;
                if (pkg.Texts != null)
                    foreach (var t in pkg.Texts) { if (t == null) continue; t.Name = UniqueName(t.Name, used); settings.Texts.Add(t); added++; }
                if (pkg.Images != null)
                    foreach (var i in pkg.Images) { if (i == null) continue; i.Name = UniqueName(i.Name, used); settings.Images.Add(i); added++; }
                if (pkg.Videos != null)
                    foreach (var v in pkg.Videos) { if (v == null) continue; v.Name = UniqueName(v.Name, used); settings.Videos.Add(v); added++; }
                if (pkg.ProgressBars != null)
                    foreach (var b in pkg.ProgressBars) { if (b == null) continue; b.Name = UniqueName(b.Name, used); settings.ProgressBars.Add(b); added++; }

                settings.Enabled = true;
                // CT 会把该值夹在 15~360 之间
                if (settings.DataUpdateRate < 15f || settings.DataUpdateRate > 360f) settings.DataUpdateRate = 60f;

                var serializer = new System.Xml.Serialization.XmlSerializer(typeof(OvSettingsFile));
                byte[] bytes;
                using (var ms = new MemoryStream())
                {
                    serializer.Serialize(ms, settings);
                    bytes = ms.ToArray();
                }

                var dir = GetCtkvDir(modEntry);
                string filePath = ResolvePackagePath(dir, fileName, ".OvSettings.xml", "Overlayer设置");
                File.WriteAllBytes(filePath, bytes);

                CoreEntry.ModEntry?.Logger.Log($"[CtKvIo] 已导出 CT Overlayer 设置文件: {filePath}（新增 {added} 个组件，合并后共 {settings.Texts.Count + settings.Images.Count + settings.Videos.Count + settings.ProgressBars.Count} 个）");
                return filePath;
            }
            catch (Exception ex)
            {
                error = ex.Message;
                CoreEntry.ModEntry?.Logger.Error($"[CtKvIo] 导出 CT Overlayer 设置文件失败: {ex}");
                return null;
            }
        }

        /// <summary>读取 CT 现有的 Overlayer 设置文件（不存在或解析失败返回 null）</summary>
        private static OvSettingsFile LoadExistingOvSettings()
        {
            try
            {
                string path = GetCtOverlayerSettingsPath(CoreEntry.ModEntry);
                if (!File.Exists(path)) return null;
                var serializer = new System.Xml.Serialization.XmlSerializer(typeof(OvSettingsFile));
                using (var fs = File.OpenRead(path))
                    return serializer.Deserialize(fs) as OvSettingsFile;
            }
            catch (Exception ex)
            {
                CoreEntry.ModEntry?.Logger.Log($"[CtKvIo] 读取现有 CT Overlayer 设置失败（将作为新建处理）: {ex.Message}");
                return null;
            }
        }

        /// <summary>名称去重：同名时依次尝试「名称 (2)」「名称 (3)」…（对齐 CT 的 UniqueName）</summary>
        private static string UniqueName(string name, HashSet<string> used)
        {
            if (string.IsNullOrWhiteSpace(name)) name = "组件";
            name = name.Trim();
            if (used.Add(name)) return name;
            for (int i = 2; i < 1000; i++)
            {
                string candidate = $"{name} ({i})";
                if (used.Add(candidate)) return candidate;
            }
            string fallback = name + " " + Guid.NewGuid().ToString("N").Substring(0, 4);
            used.Add(fallback);
            return fallback;
        }

        /// <summary>通用包保存：写入指定清单条目名的 ZIP</summary>
        private static string SavePackage(string fileName, string extension, string manifestEntry,
            byte[] content, string fallbackName, UnityModManager.ModEntry modEntry, out string error)
        {
            error = "";
            try
            {
                var dir = GetCtkvDir(modEntry);
                string filePath = ResolvePackagePath(dir, fileName, extension, fallbackName);
                if (File.Exists(filePath)) File.Delete(filePath);
                using (var zip = ZipFile.Open(filePath, ZipArchiveMode.Create))
                    WriteEntryBytes(zip, manifestEntry, content);
                CoreEntry.ModEntry?.Logger.Log($"[CtKvIo] 已导出包: {filePath}（条目 {manifestEntry}）");
                return filePath;
            }
            catch (Exception ex)
            {
                error = ex.Message;
                CoreEntry.ModEntry?.Logger.Error($"[CtKvIo] 导出包失败: {ex}");
                return null;
            }
        }

        /// <summary>补扩展名、清洗非法文件名字符，返回完整路径</summary>
        private static string ResolvePackagePath(string dir, string fileName, string extension, string fallbackName)
        {
            if (string.IsNullOrWhiteSpace(fileName)) fileName = fallbackName;
            if (string.IsNullOrWhiteSpace(fileName)) fileName = "AI生成配置";
            if (!fileName.EndsWith(extension, StringComparison.OrdinalIgnoreCase)) fileName += extension;
            foreach (char c in Path.GetInvalidFileNameChars()) fileName = fileName.Replace(c, '_');
            return Path.Combine(dir, fileName);
        }

        private static void WriteEntryBytes(ZipArchive zip, string name, byte[] content)
        {
            var entry = zip.CreateEntry(name, System.IO.Compression.CompressionLevel.Optimal);
            using (var s = entry.Open())
                s.Write(content, 0, content.Length);
        }

        /// <summary>
        /// 就地修正已有 .ctkv 文件的键雨参数（重写包内 KeyViewer.xml，
        /// 其余条目原样复制）。用于修复旧版本生成的配置：节点级
        /// UseCustomRain/RainColor 缺失导致整排雨为单色的问题。
        /// </summary>
        public static bool ApplyRainFixToFile(string filePath, out string error)
        {
            error = "";
            try
            {
                // 优先处理 Sonnet 新格式包
                string sonnetXml = ReadEntryFromZip(filePath, "KeyViewer.Sonnet.xml");
                if (!string.IsNullOrEmpty(sonnetXml))
                {
                    var pkg = ParseSonnetXml(sonnetXml, out error);
                    if (pkg == null) return false;
                    FixSonnetRainLayout(pkg);
                    var sonnetSerializer = new System.Xml.Serialization.XmlSerializer(typeof(KvTransferPackage));
                    byte[] sonnetBytes;
                    using (var ms = new MemoryStream())
                    {
                        sonnetSerializer.Serialize(ms, pkg);
                        sonnetBytes = ms.ToArray();
                    }
                    return RewriteZipEntry(filePath, "KeyViewer.Sonnet.xml", sonnetBytes, out error);
                }

                string xml = ReadEntryFromZip(filePath, "KeyViewer.xml")
                          ?? ReadEntryFromZip(filePath, "Settings.xml");
                if (string.IsNullOrEmpty(xml))
                {
                    error = "压缩包内未找到 KeyViewer.Sonnet.xml / KeyViewer.xml / Settings.xml";
                    return false;
                }
                if (!ValidateGeneratedXml(xml, out _, out _, out error)) return false;

                string fixedXml = FixTwoRowRainLayout(xml);

                // 重写 ZIP：新内容覆盖原文件（先写临时文件再替换）
                string tmp = filePath + ".tmp";
                using (var src = ZipFile.OpenRead(filePath))
                using (var dst = ZipFile.Open(tmp, ZipArchiveMode.Create))
                {
                    bool mainWritten = false;
                    foreach (var entry in src.Entries)
                    {
                        bool isMain = entry.FullName == "KeyViewer.xml" || entry.FullName == "Settings.xml";
                        if (isMain && !mainWritten)
                        {
                            WriteEntry(dst, entry.FullName, fixedXml);
                            mainWritten = true;
                            continue;
                        }
                        var ne = dst.CreateEntry(entry.FullName);
                        using (var s = entry.Open())
                        using (var d = ne.Open())
                            s.CopyTo(d);
                    }
                    if (!mainWritten) WriteEntry(dst, "KeyViewer.xml", fixedXml);
                }
                File.Delete(filePath);
                File.Move(tmp, filePath);

                CoreEntry.ModEntry?.Logger.Log($"[CtKvIo] 键雨就地修正完成: {filePath}");
                return true;
            }
            catch (Exception ex)
            {
                error = ex.Message;
                CoreEntry.ModEntry?.Logger.Error($"[CtKvIo] 键雨就地修正失败: {ex}");
                return false;
            }
        }

        /// <summary>用新内容替换 ZIP 内指定条目，其余条目原样复制（先写临时文件再替换）</summary>
        private static bool RewriteZipEntry(string filePath, string entryName, byte[] content, out string error)
        {
            error = "";
            try
            {
                string tmp = filePath + ".tmp";
                using (var src = ZipFile.OpenRead(filePath))
                using (var dst = ZipFile.Open(tmp, ZipArchiveMode.Create))
                {
                    bool written = false;
                    foreach (var entry in src.Entries)
                    {
                        if (entry.FullName == entryName && !written)
                        {
                            WriteEntryBytes(dst, entryName, content);
                            written = true;
                            continue;
                        }
                        var ne = dst.CreateEntry(entry.FullName);
                        using (var s = entry.Open())
                        using (var d = ne.Open())
                            s.CopyTo(d);
                    }
                    if (!written) WriteEntryBytes(dst, entryName, content);
                }
                File.Delete(filePath);
                File.Move(tmp, filePath);

                CoreEntry.ModEntry?.Logger.Log($"[CtKvIo] 键雨就地修正完成: {filePath}");
                return true;
            }
            catch (Exception ex)
            {
                error = ex.Message;
                CoreEntry.ModEntry?.Logger.Error($"[CtKvIo] 键雨就地修正失败: {ex}");
                return false;
            }
        }

        private static string BuildMinimalPackageInfoXml()
        {
            var doc = new XmlDocument();
            doc.AppendChild(doc.CreateXmlDeclaration("1.0", "utf-8", null));
            var root = doc.CreateElement("CytPackageInfo");
            root.SetAttribute("xmlns:xsd", "http://www.w3.org/2001/XMLSchema");
            root.SetAttribute("xmlns:xsi", "http://www.w3.org/2001/XMLSchema-instance");
            doc.AppendChild(root);
            AppendElement(root, "FormatVersion", "1");
            AppendElement(root, "ExportScreenWidth", "1920");
            AppendElement(root, "ExportScreenHeight", "1080");
            return doc.OuterXml;
        }

        /// <summary>构建符合 CT .ctkv 格式的 KeyViewer.xml（根元素 &lt;KeyViewerPackage&gt;，含 KVConfiguration 及其 KVNode 列表）</summary>
        private static string BuildSettingsXml(KVConfig cfg)
        {
            var doc = new XmlDocument();
            doc.AppendChild(doc.CreateXmlDeclaration("1.0", "utf-8", null));
            var root = doc.CreateElement("KeyViewerPackage");
            root.SetAttribute("xmlns:xsd", "http://www.w3.org/2001/XMLSchema");
            root.SetAttribute("xmlns:xsi", "http://www.w3.org/2001/XMLSchema-instance");
            doc.AppendChild(root);

            AppendElement(root, "FormatVersion", "3");
            AppendElement(root, "ExportedAt", System.DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"));
            AppendElement(root, "ExportScreenWidth", "1920");
            AppendElement(root, "ExportScreenHeight", "1080");

            var configs = doc.CreateElement("KeyViewerConfigurations");
            root.AppendChild(configs);
            var config = doc.CreateElement("KVConfiguration");
            configs.AppendChild(config);

            AppendElement(config, "Name", string.IsNullOrEmpty(cfg.name) ? "未命名配置" : cfg.name);
            AppendElement(config, "IsEnabled", "true");
            AppendElement(config, "ShowInGame", "true");
            AppendElement(config, "OnlyShowPlaying", "false");
            AppendElement(config, "TotalHits", "0");

            var nodes = doc.CreateElement("Nodes");
            config.AppendChild(nodes);

            foreach (var k in cfg.keys)
            {
                var node = doc.CreateElement("KVNode");
                nodes.AppendChild(node);

                AppendElement(node, "NodeType", k.nodeType.ToString());
                AppendElement(node, "KeyBind", string.IsNullOrEmpty(k.keyCode) ? "D" : k.keyCode);
                AppendElement(node, "CustomText", string.IsNullOrEmpty(k.label) ? k.keyCode : k.label);
                AppendElement(node, "ImagePath", k.imageFile ?? "");
                AppendElement(node, "VideoPath", "");
                AppendElement(node, "VideoLoop", "true");
                AppendElement(node, "VideoContentScale", "1");
                AppendElement(node, "VideoContentOffsetX", "0");
                AppendElement(node, "VideoContentOffsetY", "0");
                AppendElement(node, "IsUnselectable", "false");
                AppendElement(node, "Opacity", "1");
                AppendElement(node, "Depth", "0");
                AppendElement(node, "PositionX", k.x.ToString("0.####"));
                AppendElement(node, "PositionY", k.y.ToString("0.####"));
                AppendElement(node, "Width", Mathf.Max(1, k.w).ToString("0.####"));
                AppendElement(node, "Height", Mathf.Max(1, k.h).ToString("0.####"));
                AppendElement(node, "BorderThickness", (k.borderThickness > 0f ? k.borderThickness : -1f).ToString("0.####"));
                AppendElement(node, "CornerRadius", (k.cornerRadius > 0f ? k.cornerRadius : -1f).ToString("0.####"));
                AppendElement(node, "Scale", "1");
                AppendElement(node, "TextOffsetY", "0");
                AppendElement(node, "TextOffsetX", "0");
                AppendElement(node, "TextScale", "0.85");
                AppendElement(node, "CountOffsetY", "0");
                AppendElement(node, "CountOffsetX", "0");
                AppendElement(node, "CountScale", "0.65");
                AppendElement(node, "CountTextAlignment", "1");
                AppendElement(node, "KeyFontPath", "");
                AppendElement(node, "CountFontPath", "");
                AppendElement(node, "HideCountText", "false");
                AppendElement(node, "UseCustomOutline", "false");
                AppendElement(node, "KeyTextOutlineEnabled", "false");
                AppendElement(node, "KeyTextOutlineThickness", "1");
                AppendElement(node, "CountTextOutlineEnabled", "false");
                AppendElement(node, "CountTextOutlineThickness", "1");
                AppendElement(node, "UseCustomShadow", "false");
                AppendElement(node, "KeyTextShadowEnabled", "false");
                AppendElement(node, "CountTextShadowEnabled", "false");

                // 颜色（自定义时写入，否则跟随全局/默认）
                bool customColor = !string.IsNullOrEmpty(k.idleColor);
                AppendElement(node, "UseCustomColor", customColor ? "true" : "false");
                AppendColor(node, "ColorBgNormal", ParseColor(k.idleColor, new Color(0.2f, 0.2f, 0.2f, 0.8f)));
                AppendColor(node, "ColorBgPressed", ParseColor(k.pressedColor, new Color(0.8f, 0.8f, 0.8f, 0.8f)));
                AppendColor(node, "ColorBorderNormal", ParseColor(k.borderColor, new Color(0.4f, 0.4f, 0.4f, 1f)));
                AppendColor(node, "ColorBorderPressed", ParseColor(k.pressedColor, new Color(1f, 1f, 1f, 1f)));
                AppendColor(node, "ColorTextNormal", ParseColor(k.textColor, Color.white));
                AppendColor(node, "ColorTextPressed", ParseColor(k.textPressedColor, Color.black));

                // 键雨
                AppendElement(node, "RainRow", k.rainRow.ToString());
                AppendElement(node, "EnableKeyRain", (cfg.enableRain).ToString().ToLowerInvariant());
                AppendElement(node, "UseCustomRain", k.useCustomRain ? "true" : "false");
                AppendColor(node, "RainColor", ParseColor(k.rainColor, new Color(0.8f, 0.5f, 1f, 0.8f)));
                AppendElement(node, "RainGradientEnabled", "false");
                AppendElement(node, "RainHorizontalGradientEnabled", "false");
                AppendElement(node, "RainGradientMode", "0");
                AppendElement(node, "RainFadeHeight", "1");
                AppendElement(node, "RainFadePower", "1");
                AppendElement(node, "RainGradientHeight", "1");
                AppendElement(node, "RainGradientPower", "1");
                AppendElement(node, "RainWidthRatio", k.rainWidthRatio.ToString("0.####"));
                AppendElement(node, "RainYOffset", k.rainHeightOffset.ToString("0.####"));
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
            }

            // ---- KVConfiguration 全局设置（CT 读取）----
            AppendElement(config, "FontPath", "Assets/Fonts/adofai.otf");
            AppendElement(config, "Scale", cfg.scale.ToString("0.####"));
            AppendElement(config, "BorderThickness", "2");
            AppendElement(config, "CornerRadius", "0");
            AppendElement(config, "HideCountText", "false");
            AppendColor(config, "ColorBgNormal", ParseColor(cfg.keyIdleColor, new Color(0.35f, 0f, 1f, 0.15f)));
            AppendColor(config, "ColorBgPressed", ParseColor(cfg.keyPressedColor, Color.white));
            AppendColor(config, "ColorBorderNormal", ParseColor(cfg.keyBorderColor, new Color(0.26f, 0.14f, 1f, 1f)));
            AppendColor(config, "ColorBorderPressed", ParseColor(cfg.keyPressedColor, new Color(1f, 1f, 1f, 1f)));
            AppendColor(config, "ColorTextNormal", ParseColor(cfg.keyTextColor, Color.white));
            AppendColor(config, "ColorTextPressed", ParseColor(cfg.keyTextPressedColor, Color.black));
            AppendColor(config, "ColorKps", Color.white);
            AppendColor(config, "ColorTotal", Color.white);
            AppendElement(config, "EnableKeyRain", cfg.enableRain.ToString().ToLowerInvariant());
            AppendElement(config, "KeyRainSpeed", cfg.rainSpeed.ToString("0.####"));
            AppendElement(config, "KeyRainMaxHeight", cfg.rainDistance.ToString("0.####"));
            AppendElement(config, "KeyRainFadeMode", cfg.rainFadeMode.ToString());
            AppendElement(config, "KeyRainWidthRatio1", cfg.rainWidthRatio.ToString("0.####"));
            AppendElement(config, "KeyRainWidthRatio2", cfg.rainWidthRatio.ToString("0.####"));
            AppendElement(config, "KeyRainYOffsetRow1", cfg.rainHeightOffset.ToString("0.####"));
            AppendElement(config, "KeyRainYOffsetRow2", cfg.rainHeightOffset.ToString("0.####"));
            AppendColor(config, "KeyRainColorRow1", ParseColor(cfg.rainColor, new Color(1f, 1f, 1f, 0.8f)));
            AppendColor(config, "KeyRainColorRow2", ParseColor(cfg.rainColor, new Color(0f, 0f, 0f, 0.8f)));
            AppendElement(config, "KeyPressAnimationEnabled", "false");

            return doc.OuterXml;
        }

        private static string BuildPackageInfoXml(KVConfig cfg)
        {
            var doc = new XmlDocument();
            doc.AppendChild(doc.CreateXmlDeclaration("1.0", "utf-8", null));
            var root = doc.CreateElement("CytPackageInfo");
            root.SetAttribute("xmlns:xsd", "http://www.w3.org/2001/XMLSchema");
            root.SetAttribute("xmlns:xsi", "http://www.w3.org/2001/XMLSchema-instance");
            doc.AppendChild(root);
            AppendElement(root, "FormatVersion", "1");
            AppendElement(root, "ExportScreenWidth", Screen.width.ToString());
            AppendElement(root, "ExportScreenHeight", Screen.height.ToString());
            return doc.OuterXml;
        }

        // ====================================================================
        //  工具方法
        // ====================================================================

        /// <summary>读取 ZIP 内指定条目的文本内容（供 WebUI 预览用；条目不存在返回 null）</summary>
        public static string ReadEntryText(string filePath, string entryName)
        {
            try
            {
                return File.Exists(filePath) ? ReadEntryFromZip(filePath, entryName) : null;
            }
            catch (Exception ex)
            {
                CoreEntry.ModEntry?.Logger.Error($"[CtKvIo] 读取条目失败 {filePath}/{entryName}: {ex.Message}");
                return null;
            }
        }

        private static string ReadEntryFromZip(string filePath, string entryName)
        {
            using (var zip = ZipFile.OpenRead(filePath))
            {
                var entry = zip.GetEntry(entryName);
                if (entry == null) return null;
                using (var reader = new StreamReader(entry.Open(), Encoding.UTF8))
                    return reader.ReadToEnd();
            }
        }

        private static void WriteEntry(ZipArchive zip, string name, string content)
        {
            var entry = zip.CreateEntry(name, System.IO.Compression.CompressionLevel.Optimal);
            using (var writer = new StreamWriter(entry.Open(), new UTF8Encoding(false)))
                writer.Write(content);
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

        private static string GetString(XmlNode n, string name, string def)
        {
            var c = n.SelectSingleNode(name);
            if (c == null) return def;
            return string.IsNullOrEmpty(c.InnerText) ? def : c.InnerText;
        }

        private static float GetFloat(XmlNode n, string name, float def)
        {
            var c = n.SelectSingleNode(name);
            if (c == null) return def;
            return float.TryParse(c.InnerText, System.Globalization.NumberStyles.Float,
                                  System.Globalization.CultureInfo.InvariantCulture, out var v) ? v : def;
        }

        private static int GetInt(XmlNode n, string name, int def)
        {
            var c = n.SelectSingleNode(name);
            if (c == null) return def;
            return int.TryParse(c.InnerText, out var v) ? v : def;
        }

        private static bool GetBool(XmlNode n, string name, bool def)
        {
            var c = n.SelectSingleNode(name);
            if (c == null) return def;
            return bool.TryParse(c.InnerText, out var v) ? v : def;
        }

        /// <summary>读取 CT 颜色节点：<name><float>r</float>...</name> → Color</summary>
        private static Color GetColor(XmlNode n, string name, Color def)
        {
            var c = n.SelectSingleNode(name);
            if (c == null) return def;
            var floats = c.SelectNodes("float");
            if (floats == null || floats.Count < 4) return def;
            float r = GetFloat(c, "float", def.r); // 仅取第一个
            float g = def.g, b = def.b, a = def.a;
            var arr = new List<float>();
            foreach (XmlNode f in floats)
                if (float.TryParse(f.InnerText, System.Globalization.NumberStyles.Float,
                                    System.Globalization.CultureInfo.InvariantCulture, out var v)) arr.Add(v);
            if (arr.Count >= 4) { r = arr[0]; g = arr[1]; b = arr[2]; a = arr[3]; }
            else if (arr.Count >= 1) r = arr[0];
            return new Color(r, g, b, a);
        }

        private static Color ParseColor(string html, Color def)
        {
            if (string.IsNullOrEmpty(html)) return def;
            if (ColorUtility.TryParseHtmlString(html, out var c)) return c;
            return def;
        }

        private static string ColorToHtml(Color c)
        {
            return $"#{ColorUtility.ToHtmlStringRGBA(c)}";
        }
    }
}
