using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEngine;

namespace ADOFAI.AgentKeyViewer
{
    /// <summary>
    /// 提示词与供应商预设的磁盘化存储 + 热重载。
    ///
    /// 目的：改提示词/预设不需要重新编译模组、也不需要重启游戏。
    ///   - 提示词：<c>&lt;游戏目录&gt;/AgentKeyViewer_config/prompts/&lt;键名&gt;.txt</c>，首次运行自动从内置常量播种，
    ///     之后可直接用任意编辑器修改；<see cref="Get"/> 会按文件时间戳自动重读（改完即生效）。
    ///   - 供应商预设：<c>&lt;游戏目录&gt;/AgentKeyViewer_config/presets.json</c>，同样首次播种。
    /// </summary>
    public static class PromptStore
    {
        public const string GenerateCtkv = "GenerateCtkv";
        public const string SelfCheckCtkv = "SelfCheckCtkv";
        public const string GenerateSonnetXml = "GenerateSonnetXml";
        public const string SelfCheckSonnetXml = "SelfCheckSonnetXml";
        public const string GenerateSpec = "GenerateSpec";
        public const string SelfCheckSpec = "SelfCheckSpec";
        public const string GenerateOv = "GenerateOv";
        public const string SelfCheckOv = "SelfCheckOv";

        /// <summary>键名 → (显示名, 内置默认内容)</summary>
        private static readonly List<KeyValuePair<string, KeyValuePair<string, string>>> Entries =
            new List<KeyValuePair<string, KeyValuePair<string, string>>>
        {
            new KeyValuePair<string, KeyValuePair<string, string>>(GenerateCtkv,
                new KeyValuePair<string, string>("生成旧格式 XML", PromptTemplate.SystemPrompt_GenerateCtkv)),
            new KeyValuePair<string, KeyValuePair<string, string>>(SelfCheckCtkv,
                new KeyValuePair<string, string>("旧格式 XML 自检", PromptTemplate.SystemPrompt_SelfCheckCtkv)),
            new KeyValuePair<string, KeyValuePair<string, string>>(GenerateSonnetXml,
                new KeyValuePair<string, string>("生成 Sonnet 新格式 XML", PromptTemplate.SystemPrompt_GenerateSonnetXml)),
            new KeyValuePair<string, KeyValuePair<string, string>>(SelfCheckSonnetXml,
                new KeyValuePair<string, string>("Sonnet XML 自检", PromptTemplate.SystemPrompt_SelfCheckSonnetXml)),
            new KeyValuePair<string, KeyValuePair<string, string>>(GenerateSpec,
                new KeyValuePair<string, string>("生成 KV 意图 JSON", PromptTemplate.SystemPrompt_GenerateSpec)),
            new KeyValuePair<string, KeyValuePair<string, string>>(SelfCheckSpec,
                new KeyValuePair<string, string>("KV 意图 JSON 自检", PromptTemplate.SystemPrompt_SelfCheckSpec)),
            new KeyValuePair<string, KeyValuePair<string, string>>(GenerateOv,
                new KeyValuePair<string, string>("生成 Overlayer 意图 JSON", PromptTemplate.SystemPrompt_GenerateOv)),
            new KeyValuePair<string, KeyValuePair<string, string>>(SelfCheckOv,
                new KeyValuePair<string, string>("Overlayer JSON 自检", PromptTemplate.SystemPrompt_SelfCheckOv)),
        };

        private static readonly Dictionary<string, string> Defaults = BuildDefaults();
        private static readonly Dictionary<string, string> Cache = new Dictionary<string, string>();
        private static readonly Dictionary<string, DateTime> Stamps = new Dictionary<string, DateTime>();

        private static Dictionary<string, string> BuildDefaults()
        {
            var d = new Dictionary<string, string>();
            foreach (var e in Entries) d[e.Key] = e.Value.Value;
            return d;
        }

        /// <summary>提示词目录</summary>
        public static string Dir
        {
            get
            {
                var gameRoot = ModPathHelper.GetGameDir(CoreEntry.ModEntry);
                return Path.Combine(gameRoot, "AgentKeyViewer_config", "prompts");
            }
        }

        public static string FilePath(string key) => Path.Combine(Dir, key + ".txt");

        public static IReadOnlyList<string> Keys
        {
            get
            {
                var list = new List<string>(Entries.Count);
                foreach (var e in Entries) list.Add(e.Key);
                return list;
            }
        }

        public static string DisplayName(string key)
        {
            foreach (var e in Entries) if (e.Key == key) return e.Value.Key;
            return key;
        }

        public static string GetDefault(string key) => Defaults.TryGetValue(key, out var v) ? v : "";

        /// <summary>是否已被磁盘文件覆盖</summary>
        public static bool IsOverridden(string key)
        {
            try { return File.Exists(FilePath(key)); } catch { return false; }
        }

        /// <summary>首次运行时把内置提示词写盘，方便用户直接用编辑器修改</summary>
        public static void EnsureSeeded()
        {
            try
            {
                Directory.CreateDirectory(Dir);
                int created = 0;
                foreach (var kv in Defaults)
                {
                    string p = FilePath(kv.Key);
                    if (!File.Exists(p))
                    {
                        File.WriteAllText(p, kv.Value, new UTF8Encoding(false));
                        created++;
                    }
                }
                if (created > 0)
                    CoreEntry.ModEntry?.Logger.Log($"[PromptStore] 已播种 {created} 个提示词文件到 {Dir}");
            }
            catch (Exception ex)
            {
                CoreEntry.ModEntry?.Logger.Error($"[PromptStore] 播种提示词失败: {ex.Message}");
            }
        }

        /// <summary>
        /// 取提示词。文件存在且非空时用文件内容（按时间戳自动热重载），否则回退内置默认。
        /// </summary>
        public static string Get(string key)
        {
            try
            {
                string p = FilePath(key);
                if (File.Exists(p))
                {
                    var stamp = File.GetLastWriteTimeUtc(p);
                    if (Cache.TryGetValue(key, out var cached) && Stamps.TryGetValue(key, out var s) && s == stamp)
                        return cached;

                    string text = File.ReadAllText(p, Encoding.UTF8);
                    if (!string.IsNullOrWhiteSpace(text))
                    {
                        Cache[key] = text;
                        Stamps[key] = stamp;
                        return text;
                    }
                }
            }
            catch (Exception ex)
            {
                CoreEntry.ModEntry?.Logger.Error($"[PromptStore] 读取提示词 {key} 失败: {ex.Message}");
            }
            return GetDefault(key);
        }

        /// <summary>写入提示词文件（立即生效，无需重启）</summary>
        public static void Save(string key, string text)
        {
            Directory.CreateDirectory(Dir);
            File.WriteAllText(FilePath(key), text ?? "", new UTF8Encoding(false));
            Cache.Remove(key);
            Stamps.Remove(key);
            CoreEntry.ModEntry?.Logger.Log($"[PromptStore] 已保存提示词 {key}");
        }

        /// <summary>删除磁盘文件，恢复内置默认</summary>
        public static void ResetToDefault(string key)
        {
            try
            {
                string p = FilePath(key);
                if (File.Exists(p)) File.Delete(p);
            }
            catch (Exception ex) { CoreEntry.ModEntry?.Logger.Error($"[PromptStore] 恢复默认失败: {ex.Message}"); }
            Cache.Remove(key);
            Stamps.Remove(key);
        }

        public static void ResetAll()
        {
            foreach (var key in Keys) ResetToDefault(key);
        }

        /// <summary>清空缓存，强制下次读取时重读磁盘</summary>
        public static void ReloadAll()
        {
            Cache.Clear();
            Stamps.Clear();
            CoreEntry.ModEntry?.Logger.Log("[PromptStore] 提示词缓存已清空，将从磁盘重读");
        }
    }

    /// <summary>供应商预设的磁盘化存储 + 热重载（presets.json，首次运行从内置列表播种）</summary>
    public static class PresetStore
    {
        private static List<ProviderPreset> _cache;
        private static DateTime _stamp = DateTime.MinValue;

        public static string FilePath
        {
            get
            {
                var gameRoot = ModPathHelper.GetGameDir(CoreEntry.ModEntry);
                return Path.Combine(gameRoot, "AgentKeyViewer_config", "presets.json");
            }
        }

        /// <summary>当前生效的供应商列表（文件存在则按文件，按时间戳热重载）</summary>
        public static List<ProviderPreset> All
        {
            get
            {
                try
                {
                    string p = FilePath;
                    if (File.Exists(p))
                    {
                        var stamp = File.GetLastWriteTimeUtc(p);
                        if (_cache != null && stamp == _stamp) return _cache;

                        var parsed = Parse(File.ReadAllText(p, Encoding.UTF8));
                        if (parsed != null && parsed.Count > 0)
                        {
                            _cache = parsed;
                            _stamp = stamp;
                            return _cache;
                        }
                    }
                }
                catch (Exception ex)
                {
                    CoreEntry.ModEntry?.Logger.Error($"[PresetStore] 读取预设失败: {ex.Message}");
                }
                return ProviderPresets.List;
            }
        }

        public static void EnsureSeeded()
        {
            try
            {
                string p = FilePath;
                if (File.Exists(p)) return;
                var dir = Path.GetDirectoryName(p);
                if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);
                File.WriteAllText(p, Serialize(ProviderPresets.List), new UTF8Encoding(false));
                CoreEntry.ModEntry?.Logger.Log($"[PresetStore] 已播种供应商预设到 {p}");
            }
            catch (Exception ex)
            {
                CoreEntry.ModEntry?.Logger.Error($"[PresetStore] 播种预设失败: {ex.Message}");
            }
        }

        public static void Save(List<ProviderPreset> presets)
        {
            string p = FilePath;
            var dir = Path.GetDirectoryName(p);
            if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);
            File.WriteAllText(p, Serialize(presets), new UTF8Encoding(false));
            _cache = null;
            _stamp = DateTime.MinValue;
            CoreEntry.ModEntry?.Logger.Log("[PresetStore] 已保存供应商预设");
        }

        public static void ResetToDefault()
        {
            try { string p = FilePath; if (File.Exists(p)) File.Delete(p); } catch { }
            _cache = null;
            _stamp = DateTime.MinValue;
        }

        public static void Reload()
        {
            _cache = null;
            _stamp = DateTime.MinValue;
        }

        /// <summary>只解析不写盘，供界面保存前校验格式</summary>
        public static List<ProviderPreset> DeserializeForValidation(string json)
        {
            try { return Parse(json); }
            catch (Exception ex)
            {
                CoreEntry.ModEntry?.Logger.Error($"[PresetStore] 预设格式校验失败: {ex.Message}");
                return null;
            }
        }

        // ---------------- JSON 读写（手写，避免 JsonUtility 对嵌套结构/数组的限制）----------------

        public static string Serialize(List<ProviderPreset> presets)
        {
            var sb = new StringBuilder();
            sb.Append("[\n");
            for (int i = 0; i < presets.Count; i++)
            {
                var p = presets[i];
                if (i > 0) sb.Append(",\n");
                sb.Append("  {\n");
                sb.Append("    \"name\": \"").Append(Json.Escape(p.name ?? "")).Append("\",\n");
                sb.Append("    \"baseUrl\": \"").Append(Json.Escape(p.baseUrl ?? "")).Append("\",\n");
                sb.Append("    \"model\": \"").Append(Json.Escape(p.model ?? "")).Append("\",\n");
                sb.Append("    \"thinkingDisableJson\": \"").Append(Json.Escape(p.thinkingDisableJson ?? "")).Append("\",\n");
                sb.Append("    \"maxTokens\": ").Append(p.maxTokens).Append(",\n");
                sb.Append("    \"models\": [");
                if (p.models != null)
                    for (int j = 0; j < p.models.Length; j++)
                    {
                        if (j > 0) sb.Append(", ");
                        sb.Append('"').Append(Json.Escape(p.models[j])).Append('"');
                    }
                sb.Append("]\n  }");
            }
            sb.Append("\n]\n");
            return sb.ToString();
        }

        private static List<ProviderPreset> Parse(string json)
        {
            var list = new List<ProviderPreset>();
            if (string.IsNullOrWhiteSpace(json)) return list;

            int arrStart = KVConfig.FindArrayStart(json, 0);
            if (arrStart < 0) return list;
            int arrEnd = KVConfig.FindMatchingBracket(json, arrStart, '[', ']');
            if (arrEnd < 0) arrEnd = json.Length - 1;

            int i = arrStart + 1;
            while (i < arrEnd)
            {
                int objStart = KVConfig.FindObjectStart(json, i, arrEnd);
                if (objStart < 0) break;
                int objEnd = KVConfig.FindMatchingBracket(json, objStart, '{', '}');
                if (objEnd < 0 || objEnd > arrEnd) break;

                string obj = json.Substring(objStart, objEnd - objStart + 1);
                var preset = new ProviderPreset
                {
                    name = Json.GetString(obj, "name"),
                    baseUrl = Json.GetString(obj, "baseUrl"),
                    model = Json.GetString(obj, "model"),
                    thinkingDisableJson = Json.GetString(obj, "thinkingDisableJson"),
                    maxTokens = Json.GetInt(obj, "maxTokens", 8192),
                };
                preset.models = ParseStringArray(obj, "models");
                if (!string.IsNullOrWhiteSpace(preset.name)) list.Add(preset);

                i = objEnd + 1;
            }
            return list;
        }

        private static string[] ParseStringArray(string json, string field)
        {
            int keyPos = json.IndexOf("\"" + field + "\"", StringComparison.Ordinal);
            if (keyPos < 0) return null;
            int arrStart = KVConfig.FindArrayStart(json, keyPos);
            if (arrStart < 0) return null;
            int arrEnd = KVConfig.FindMatchingBracket(json, arrStart, '[', ']');
            if (arrEnd < 0) return null;

            var items = new List<string>();
            string body = json.Substring(arrStart + 1, arrEnd - arrStart - 1);
            int pos = 0;
            while (pos < body.Length)
            {
                int q1 = body.IndexOf('"', pos);
                if (q1 < 0) break;
                var sb = new StringBuilder();
                int k = q1 + 1;
                while (k < body.Length)
                {
                    char c = body[k];
                    if (c == '\\' && k + 1 < body.Length) { sb.Append(body[k + 1]); k += 2; continue; }
                    if (c == '"') break;
                    sb.Append(c);
                    k++;
                }
                items.Add(sb.ToString());
                pos = k + 1;
            }
            return items.Count > 0 ? items.ToArray() : null;
        }
    }
}
