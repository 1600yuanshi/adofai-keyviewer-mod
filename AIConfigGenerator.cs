using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;
using UnityModManagerNet;

namespace ADOFAI.AgentKeyViewer
{
    /// <summary>一条API密钥配置</summary>
    [Serializable]
    public class ApiKeyEntry
    {
        public string name = "";
        public string baseUrl = "https://api.deepseek.com";
        public string apiKey = "";
        public string model = "deepseek-v4-flash";
        /// <summary>关闭思考时注入请求体的 JSON 片段（如 ""thinking"":{""type"":""disabled""}），空串不注入</summary>
        public string thinkingDisableJson = "";
        /// <summary>该供应商允许的最大输出 tokens（如讯飞星火 Lite 上限为 4096）</summary>
        public int maxTokens = 8192;
        public bool isDefault;
    }

    /// <summary>供应商预设：一键填充 Base URL 与模型，用户只需输入 API Key</summary>
    [Serializable]
    public class ProviderPreset
    {
        public string name;
        public string baseUrl;
        /// <summary>单个模型（models 为空时使用）</summary>
        public string model;
        /// <summary>可下拉选择的多个模型（非空时 UI 显示模型下拉）</summary>
        public string[] models;
        public string thinkingDisableJson;
        public int maxTokens = 8192;
    }

    /// <summary>内置供应商预设列表（支持多厂商，可随时扩展）</summary>
    public static class ProviderPresets
    {
        public static readonly List<ProviderPreset> List = new List<ProviderPreset>
        {
            new ProviderPreset { name = "DeepSeek（深度求索）", baseUrl = "https://api.deepseek.com", model = "deepseek-v4-flash", thinkingDisableJson = "\"thinking\":{\"type\":\"disabled\"}", maxTokens = 16384 },
            new ProviderPreset { name = "通义千问 Qwen（阿里百炼）", baseUrl = "https://dashscope.aliyuncs.com/compatible-mode/v1", model = "qwen-plus", thinkingDisableJson = "\"enable_thinking\":false", maxTokens = 8192 },
            new ProviderPreset { name = "TokenRa 网关（多模型）", baseUrl = "https://tokenra.io/v1",
                models = new[] { "artsdance-2-5-pro-260801", "deepseek-v4-flash", "glm-5.2", "glm-5.3", "glm-5.3-flash", "kimi-k3", "MiniMax-M3", "qwen3.8-max", "seedance-2-0-fast", "seedance-2-0-mini", "seedance-2-0-pro" },
                thinkingDisableJson = "\"reasoning\":{\"enabled\":false}", maxTokens = 16384 },
            new ProviderPreset { name = "OpenRouter（通用，自填模型）", baseUrl = "https://openrouter.ai/api/v1", model = "", thinkingDisableJson = "\"reasoning\":{\"enabled\":false}", maxTokens = 8192 },
            new ProviderPreset { name = "自定义（手动填写）", baseUrl = "", model = "", thinkingDisableJson = "", maxTokens = 8192 },
        };
    }

    /// <summary>密钥集合（保存到 config/apikeys.json）</summary>
    [Serializable]
    public class ApiKeyList
    {
        public List<ApiKeyEntry> keys = new List<ApiKeyEntry>();
        public string agentConfigPath = "";
    }

    /// <summary>包装类，用于JsonUtility序列化</summary>
    [Serializable]
    public class ApiKeyListWrapper
    {
        public ApiKeyList data;
    }

    /// <summary>AI日志条目</summary>
    [Serializable]
    public class AiLogEntry
    {
        public string timestamp;
        public string model;
        public string userPrompt;
        public string round1Raw;
        public string round1Extracted;
        public string round2Raw;
        public string round2Extracted;
        public string error;
        public bool success;
    }

    /// <summary>API密钥的磁盘存取与Agent密钥导入</summary>
    public static class ApiKeyStore
    {
        public static string GetPath(UnityModManager.ModEntry modEntry)
        {
            string gameDir = ModPathHelper.GetGameDir(modEntry);
            string configDir = Path.Combine(gameDir, "AgentKeyViewer_config");
            CoreEntry.ModEntry?.Logger.Log($"[AIConfig] GetPath -> gameDir='{gameDir}', configDir='{configDir}'");
            try { if (!Directory.Exists(configDir)) Directory.CreateDirectory(configDir); } catch (Exception ex) { CoreEntry.ModEntry?.Logger.Error($"[AIConfig] 创建目录失败: {ex.Message}"); }
            string path = Path.Combine(configDir, "apikeys.json");
            CoreEntry.ModEntry?.Logger.Log($"[AIConfig] GetPath -> '{path}'");
            return path;
        }

        public static ApiKeyList Load(string path)
        {
            var list = LoadFromFile(path);
            if (list.keys.Count == 0)
            {
                // 旧版本曾把配置写在 Mods 目录下，手动覆盖 Mods 会被清空。
                // 若规范位置（游戏根目录）为空，尝试从历史位置找回并迁移，避免重装 mod 丢失密钥。
                string legacy = FindLegacyApiKeyPath(path);
                if (!string.IsNullOrEmpty(legacy) && File.Exists(legacy))
                {
                    var old = LoadFromFile(legacy);
                    if (old.keys.Count > 0)
                    {
                        list = old;
                        Save(path, list);
                        CoreEntry.ModEntry?.Logger.Log($"[AIConfig] 已从历史位置迁移密钥: {legacy}");
                    }
                }
            }
            return list;
        }

        private static ApiKeyList LoadFromFile(string path)
        {
            try
            {
                if (!string.IsNullOrEmpty(path) && File.Exists(path))
                {
                    string json = File.ReadAllText(path, Encoding.UTF8);
                    var list = ParseApiKeyJson(json);
                    if (list != null) return list;
                }
            }
            catch (Exception ex)
            {
                CoreEntry.ModEntry?.Logger.Error($"[AIConfig] 密钥加载失败: {ex.Message}");
            }
            return new ApiKeyList();
        }

        /// <summary>手写 JSON 解析（避免 Unity JsonUtility 对嵌套 List 序列化不稳定的问题）</summary>
        private static ApiKeyList ParseApiKeyJson(string json)
        {
            if (string.IsNullOrEmpty(json)) return null;
            var result = new ApiKeyList();
            int i = 0;
            SkipWs(json, ref i);
            if (i >= json.Length || json[i] != '{') return null;
            i++;
            while (i < json.Length)
            {
                SkipWs(json, ref i);
                if (i < json.Length && json[i] == '}') break;
                string key = ReadJsonString(json, ref i);
                SkipWs(json, ref i);
                if (i < json.Length && json[i] == ':') i++;
                SkipWs(json, ref i);
                if (key == "keys" && i < json.Length && json[i] == '[')
                {
                    i++;
                    SkipWs(json, ref i);
                    while (i < json.Length && json[i] != ']')
                    {
                        var entry = ParseApiKeyEntry(json, ref i);
                        if (entry != null) result.keys.Add(entry);
                        SkipWs(json, ref i);
                        if (i < json.Length && json[i] == ',') i++;
                        SkipWs(json, ref i);
                    }
                    if (i < json.Length) i++; // 跳过 ]
                }
                else if (key == "agentConfigPath")
                {
                    result.agentConfigPath = ReadJsonString(json, ref i);
                }
                else
                {
                    // 跳过未知值
                    SkipJsonValue(json, ref i);
                }
                SkipWs(json, ref i);
                if (i < json.Length && json[i] == ',') i++;
            }
            return result;
        }

        private static ApiKeyEntry ParseApiKeyEntry(string json, ref int i)
        {
            SkipWs(json, ref i);
            if (i >= json.Length || json[i] != '{') { SkipJsonValue(json, ref i); return null; }
            i++;
            var e = new ApiKeyEntry();
            while (i < json.Length)
            {
                SkipWs(json, ref i);
                if (i < json.Length && json[i] == '}') break;
                string k = ReadJsonString(json, ref i);
                SkipWs(json, ref i);
                if (i < json.Length && json[i] == ':') i++;
                SkipWs(json, ref i);
                switch (k)
                {
                    case "name": e.name = ReadJsonString(json, ref i); break;
                    case "baseUrl": e.baseUrl = ReadJsonString(json, ref i); break;
                    case "apiKey": e.apiKey = ReadJsonString(json, ref i); break;
                    case "model": e.model = ReadJsonString(json, ref i); break;
                    case "thinkingDisableJson": e.thinkingDisableJson = ReadJsonString(json, ref i); break;
                    case "maxTokens": e.maxTokens = ReadJsonInt(json, ref i); break;
                    case "isDefault": e.isDefault = ReadJsonBool(json, ref i); break;
                    default: SkipJsonValue(json, ref i); break;
                }
                SkipWs(json, ref i);
                if (i < json.Length && json[i] == ',') i++;
            }
            if (i < json.Length) i++; // 跳过 }
            return e;
        }

        // ---- 手写 JSON 基础工具 ----
        private static void SkipWs(string s, ref int i) { while (i < s.Length && (s[i] == ' ' || s[i] == '\t' || s[i] == '\r' || s[i] == '\n')) i++; }
        private static string ReadJsonString(string s, ref int i)
        {
            SkipWs(s, ref i);
            if (i >= s.Length || s[i] != '"') { SkipJsonValue(s, ref i); return ""; }
            i++;
            var sb = new StringBuilder();
            while (i < s.Length && s[i] != '"')
            {
                char c = s[i];
                if (c == '\\' && i + 1 < s.Length)
                {
                    char n = s[i + 1];
                    switch (n)
                    {
                        case '"': sb.Append('"'); break;
                        case '\\': sb.Append('\\'); break;
                        case 'n': sb.Append('\n'); break;
                        case 'r': sb.Append('\r'); break;
                        case 't': sb.Append('\t'); break;
                        case 'u':
                            if (i + 5 < s.Length)
                            {
                                string hex = s.Substring(i + 2, 4);
                                if (int.TryParse(hex, System.Globalization.NumberStyles.HexNumber, null, out int cp))
                                    sb.Append((char)cp);
                                i += 4;
                            }
                            break;
                        default: sb.Append(n); break;
                    }
                    i += 2;
                }
                else { sb.Append(c); i++; }
            }
            if (i < s.Length) i++; // 跳过结束引号
            return sb.ToString();
        }
        private static int ReadJsonInt(string s, ref int i)
        {
            SkipWs(s, ref i);
            int start = i;
            while (i < s.Length && (char.IsDigit(s[i]) || s[i] == '-')) i++;
            string t = s.Substring(start, i - start).Trim();
            return int.TryParse(t, out int v) ? v : 0;
        }
        private static bool ReadJsonBool(string s, ref int i)
        {
            SkipWs(s, ref i);
            if (i + 4 <= s.Length && s.Substring(i, 4) == "true") { i += 4; return true; }
            if (i + 5 <= s.Length && s.Substring(i, 5) == "false") { i += 5; return false; }
            SkipJsonValue(s, ref i);
            return false;
        }
        private static void SkipJsonValue(string s, ref int i)
        {
            SkipWs(s, ref i);
            if (i >= s.Length) return;
            char c = s[i];
            if (c == '"') { ReadJsonString(s, ref i); return; }
            if (c == '{') { int d = 1; i++; while (i < s.Length && d > 0) { if (s[i] == '{') d++; else if (s[i] == '}') d--; i++; } return; }
            if (c == '[') { int d = 1; i++; while (i < s.Length && d > 0) { if (s[i] == '[') d++; else if (s[i] == ']') d--; i++; } return; }
            while (i < s.Length && s[i] != ',' && s[i] != '}') i++;
        }

        /// <summary>
        /// 根据规范路径（游戏根目录/AgentKeyViewer_config/apikeys.json）推导历史版本可能存在的密钥位置
        /// </summary>
        private static string FindLegacyApiKeyPath(string primaryPath)
        {
            try
            {
                // primaryPath = <gameRoot>/AgentKeyViewer_config/apikeys.json
                string configDir = Path.GetDirectoryName(primaryPath);   // <gameRoot>/AgentKeyViewer_config
                string gameRoot = Path.GetDirectoryName(configDir);      // <gameRoot>
                if (string.IsNullOrEmpty(gameRoot)) return null;

                var candidates = new List<string>
                {
                    Path.Combine(gameRoot, "Mods", "AgentKeyViewer_config", "apikeys.json"),
                    Path.Combine(gameRoot, "Mods", "ADOFAI.AgentKeyViewer", "config", "apikeys.json"),
                    Path.Combine(gameRoot, "Mods", "ADOFAI.AgentKeyViewer", "apikeys.json"),
                };
                foreach (var c in candidates)
                    if (File.Exists(c)) return c;
            }
            catch { }
            return null;
        }

        public static void Save(string path, ApiKeyList list)
        {
            try
            {
                var dir = Path.GetDirectoryName(path);
                if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir)) Directory.CreateDirectory(dir);
                File.WriteAllText(path, BuildApiKeyJson(list), Encoding.UTF8);
                CoreEntry.ModEntry?.Logger.Log($"[AIConfig] 密钥已保存: {path} ({list.keys.Count}个)");
            }
            catch (Exception ex)
            {
                CoreEntry.ModEntry?.Logger.Error($"[AIConfig] 密钥保存失败: {ex.Message}");
            }
        }

        /// <summary>手写 JSON 序列化（避免 Unity JsonUtility 对嵌套 List 序列化不稳定导致写出 {} 的问题）</summary>
        private static string BuildApiKeyJson(ApiKeyList list)
        {
            var sb = new StringBuilder();
            sb.Append("{\"agentConfigPath\":\"").Append(EscapeJson(list.agentConfigPath)).Append("\",\"keys\":[");
            for (int i = 0; i < list.keys.Count; i++)
            {
                var e = list.keys[i];
                if (i > 0) sb.Append(',');
                sb.Append('{');
                sb.Append("\"name\":\"").Append(EscapeJson(e.name)).Append('"');
                sb.Append(",\"baseUrl\":\"").Append(EscapeJson(e.baseUrl)).Append('"');
                sb.Append(",\"apiKey\":\"").Append(EscapeJson(e.apiKey)).Append('"');
                sb.Append(",\"model\":\"").Append(EscapeJson(e.model)).Append('"');
                sb.Append(",\"thinkingDisableJson\":\"").Append(EscapeJson(e.thinkingDisableJson)).Append('"');
                sb.Append(",\"maxTokens\":").Append(e.maxTokens);
                sb.Append(",\"isDefault\":").Append(e.isDefault ? "true" : "false");
                sb.Append('}');
            }
            sb.Append("]}");
            return sb.ToString();
        }

        private static string EscapeJson(string s)
        {
            if (string.IsNullOrEmpty(s)) return "";
            var sb = new StringBuilder(s.Length);
            foreach (char c in s)
            {
                switch (c)
                {
                    case '"': sb.Append("\\\""); break;
                    case '\\': sb.Append("\\\\"); break;
                    case '\n': sb.Append("\\n"); break;
                    case '\r': sb.Append("\\r"); break;
                    case '\t': sb.Append("\\t"); break;
                    default:
                        if (c < 32) sb.Append("\\u").Append(((int)c).ToString("x4"));
                        else sb.Append(c);
                        break;
                }
            }
            return sb.ToString();
        }

        public static bool TryImportFromAgentConfig(string filePath, out ApiKeyEntry entry)
        {
            entry = null;
            try
            {
                if (string.IsNullOrEmpty(filePath) || !File.Exists(filePath)) return false;

                string apiKey = "", baseUrl = "", model = "";
                foreach (var line in File.ReadAllLines(filePath, Encoding.UTF8))
                {
                    int idx = line.IndexOf('=');
                    if (idx <= 0) continue;
                    string k = line.Substring(0, idx).Trim();
                    string v = line.Substring(idx + 1).Trim();
                    if (k == "API_KEY") apiKey = v;
                    else if (k == "API_BASE") baseUrl = v;
                    else if (k == "MODEL_NAME") model = v;
                }
                if (string.IsNullOrEmpty(apiKey)) return false;

                var dir = Path.GetDirectoryName(Path.GetFullPath(filePath));
                entry = new ApiKeyEntry
                {
                    name = "Agent(" + (string.IsNullOrEmpty(dir) ? "root" : Path.GetFileName(dir)) + ")",
                    baseUrl = string.IsNullOrEmpty(baseUrl) ? "https://api.deepseek.com" : baseUrl.Trim(),
                    apiKey = apiKey.Trim(),
                    model = string.IsNullOrEmpty(model) ? "deepseek-v4-flash" : model.Trim(),
                    isDefault = false,
                };
                return true;
            }
            catch { return false; }
        }
    }

    /// <summary>共享工具方法</summary>
    internal static class ModPathHelper
    {
        private static string _cachedGameDir;

        /// <summary>获取游戏根目录（优先用 modEntry.Path 反推，最可靠）</summary>
        public static string GetGameDir(UnityModManager.ModEntry modEntry)
        {
            if (!string.IsNullOrEmpty(_cachedGameDir)) return _cachedGameDir;

            // 方法 1：从 modEntry.Path 反推（最可靠）
            // modEntry.Path = "游戏根目录/Mods/ModName/"（末尾带分隔符）。
            // 先去掉尾分隔符（否则 GetDirectoryName 只会去掉分隔符返回 ModName 目录），
            // 再向上两级：ModName -> Mods -> 游戏根目录。
            if (modEntry != null && !string.IsNullOrEmpty(modEntry.Path))
            {
                try
                {
                    string modFolder = (modEntry.Path ?? "").TrimEnd('\\', '/');
                    string modsFolder = Path.GetDirectoryName(modFolder);
                    string gameRoot = Path.GetDirectoryName(modsFolder);
                    if (!string.IsNullOrEmpty(gameRoot))
                    {
                        _cachedGameDir = gameRoot;
                        CoreEntry.ModEntry?.Logger.Log($"[Path] 游戏根目录(modEntry): '{_cachedGameDir}'");
                        return _cachedGameDir;
                    }
                }
                catch { }
            }

            // 方法 2：通过 Unity Application
            // Assembly.Location = "游戏根目录/A Dance of Fire and Ice_Data/Managed/UnityModManager.dll"
            // 需向上两级（Managed -> _Data -> 游戏根目录）才能得到游戏根目录
            try
            {
                string loc = typeof(UnityEngine.Application).Assembly.Location;
                if (!string.IsNullOrEmpty(loc))
                {
                    string dir = Path.GetDirectoryName(loc);   // ..._Data/Managed
                    string parent = Path.GetDirectoryName(dir); // ..._Data
                    string gameRoot2 = Path.GetDirectoryName(parent); // 游戏根目录
                    if (!string.IsNullOrEmpty(gameRoot2))
                    {
                        _cachedGameDir = gameRoot2;
                        CoreEntry.ModEntry?.Logger.Log($"[Path] 游戏根目录(Application): '{_cachedGameDir}'");
                        return _cachedGameDir;
                    }
                }
            }
            catch { }

            // 方法 3：通过当前进程
            try
            {
                var proc = System.Diagnostics.Process.GetCurrentProcess();
                if (proc?.MainModule != null)
                {
                    _cachedGameDir = Path.GetDirectoryName(proc.MainModule.FileName);
                    CoreEntry.ModEntry?.Logger.Log($"[Path] 游戏根目录(Process): '{_cachedGameDir}'");
                    return _cachedGameDir;
                }
            }
            catch { }

            // 最后的回退：使用 modEntry 所在目录
            if (modEntry != null && !string.IsNullOrEmpty(modEntry.Path))
            {
                _cachedGameDir = Path.GetDirectoryName(modEntry.Path) ?? ".";
            }
            else
            {
                _cachedGameDir = ".";
            }
            CoreEntry.ModEntry?.Logger.Log($"[Path] 游戏根目录(回退): '{_cachedGameDir}'");
            return _cachedGameDir;
        }

        public static string GetGameDir()
        {
            return GetGameDir(CoreEntry.ModEntry);
        }
    }

    /// <summary>AI配置生成器：两轮对话（生成→自检修复）+ 日志记录</summary>
    public class AIConfigGenerator
    {
        public enum GenState { Idle, Busy, Success, Failed }

        /// <summary>生成模式：Spec=AI 只输出紧凑意图 JSON，由代码构建 XML；DirectXml=AI 直写 XML</summary>
        public enum GenMode { Spec, DirectXml }

        /// <summary>目标格式：Sonnet=CT 重构版新格式；Legacy=旧版格式</summary>
        public enum GenTarget { Sonnet, Legacy }

        /// <summary>生成任务：Kv=按键显示配置；Ov=Overlayer 覆盖物配置</summary>
        public enum GenJob { Kv, Ov }

        public GenState State { get; private set; } = GenState.Idle;
        public string Error { get; private set; }
        public string RawOutput { get; private set; }
        /// <summary>AI 直出的 CT KV XML（纯 .ctkv 生成器模式）</summary>
        public string CtkvXml { get; private set; }
        /// <summary>已保存的 .ctkv 文件完整路径（未保存则为 null）</summary>
        public string SavedCtkvPath { get; private set; }
        public ApiKeyEntry ActiveKey { get; set; }
        public string UserPrompt { get; set; }

        /// <summary>本次生成使用的工作模式</summary>
        public GenMode Mode { get; set; } = GenMode.Spec;
        /// <summary>本次生成的目标格式</summary>
        public GenTarget Target { get; set; } = GenTarget.Sonnet;
        /// <summary>本次生成的任务类型</summary>
        public GenJob Job { get; set; } = GenJob.Kv;

        /// <summary>规格模式下解析出的 KV 意图（可直接再导出为新/旧格式）</summary>
        public KVConfig ParsedConfig { get; private set; }
        /// <summary>Overlayer 生成结果包</summary>
        public OvTransferPackage OvPackage { get; private set; }
        /// <summary>已保存的 .ctov 文件完整路径（未保存则为 null）</summary>
        public string SavedOvPath { get; private set; }

        private Task<string> _task;
        public bool IsBusy => _task != null && !_task.IsCompleted;

        public void Start()
        {
            if (ActiveKey == null || string.IsNullOrWhiteSpace(ActiveKey.apiKey))
            {
                State = GenState.Failed;
                Error = "未选择有效的 API 密钥，请先在「密钥管理」中添加/选择密钥。";
                return;
            }
            if (string.IsNullOrWhiteSpace(UserPrompt))
            {
                State = GenState.Failed;
                Error = Job == GenJob.Ov
                    ? "请先用自然语言描述你想要的 Overlayer 覆盖物。"
                    : "请先用自然语言描述你想要的按键显示配置。";
                return;
            }

            State = GenState.Busy;
            Error = null;
            CtkvXml = null;
            SavedCtkvPath = null;
            SavedOvPath = null;
            RawOutput = null;
            ParsedConfig = null;
            OvPackage = null;

            var key = ActiveKey;
            var prompt = UserPrompt;
            var job = Job;
            var mode = Mode;
            var target = Target;
            _task = Task.Run(() => RunGenerate(key, prompt, job, mode, target));
        }

        /// <summary>按任务类型与模式选择提示词与校验器，执行两轮生成（提示词走 PromptStore，支持热重载）</summary>
        private static string RunGenerate(ApiKeyEntry key, string userPrompt, GenJob job, GenMode mode, GenTarget target)
        {
            if (job == GenJob.Ov)
                return TwoRoundGenerate(key, userPrompt,
                    PromptStore.Get(PromptStore.GenerateOv), PromptStore.Get(PromptStore.SelfCheckOv),
                    "Overlayer 配置 JSON 对象", NormalizeJson, ValidateOvJson);

            if (mode == GenMode.Spec)
                return TwoRoundGenerate(key, userPrompt,
                    PromptStore.Get(PromptStore.GenerateSpec), PromptStore.Get(PromptStore.SelfCheckSpec),
                    "配置意图 JSON 对象", NormalizeJson, ValidateSpecJson);

            if (target == GenTarget.Sonnet)
                return TwoRoundGenerate(key, userPrompt,
                    PromptStore.Get(PromptStore.GenerateSonnetXml), PromptStore.Get(PromptStore.SelfCheckSonnetXml),
                    "<CheryToolsSonnetKeyViewer> XML", NormalizeXml, ValidateSonnetXml);

            return TwoRoundGenerate(key, userPrompt,
                PromptStore.Get(PromptStore.GenerateCtkv), PromptStore.Get(PromptStore.SelfCheckCtkv),
                "<KeyViewerPackage> XML", NormalizeXml, ValidateLegacyXml);
        }

        public void Tick()
        {
            if (_task == null) return;
            if (!_task.IsCompleted) return;

            var job = Job;
            var mode = Mode;
            var target = Target;
            try
            {
                string text = _task.Result;

                // ---- Overlayer：解析意图 JSON 并构建 .ctov 包 ----
                if (job == GenJob.Ov)
                {
                    RawOutput = NormalizeJson(text);
                    CoreEntry.ModEntry?.Logger.Log($"[AIConfig] Tick(OV): RawOutput 长度={RawOutput?.Length ?? 0}");
                    var spec = OvSpec.FromAiJson(RawOutput);
                    if (OvSpecBuilder.Validate(spec, out int ovCount, out string ovError))
                    {
                        OvPackage = OvSpecBuilder.BuildPackage(spec);
                        CtkvXml = CtKvIo.SerializeOvPackage(OvPackage);
                        State = GenState.Success;
                        CoreEntry.ModEntry?.Logger.Log($"[AIConfig] Tick(OV): 解析成功，{ovCount} 个组件");
                    }
                    else
                    {
                        FailWith("Overlayer 配置校验失败: " + ovError,
                                 "AI 返回的内容不是有效的 Overlayer 配置 JSON。请查看 logs/ 目录下的日志文件了解详情。");
                    }
                    return;
                }

                // ---- 规格模式：解析意图 JSON，由代码构建 XML ----
                if (mode == GenMode.Spec)
                {
                    RawOutput = NormalizeJson(text);
                    CoreEntry.ModEntry?.Logger.Log($"[AIConfig] Tick(Spec): RawOutput 长度={RawOutput?.Length ?? 0}");
                    var cfg = KVConfig.FromAiJson(RawOutput);
                    if (cfg != null && cfg.keys != null && cfg.keys.Count > 0)
                    {
                        ParsedConfig = cfg;
                        CtkvXml = target == GenTarget.Sonnet
                            ? KvSpecBuilder.BuildSonnetXml(cfg)
                            : KvSpecBuilder.BuildLegacyXml(cfg);
                        State = GenState.Success;
                        CoreEntry.ModEntry?.Logger.Log($"[AIConfig] Tick(Spec): 构建成功，{cfg.keys.Count} 个键，目标格式={target}");
                    }
                    else
                    {
                        FailWith("配置意图 JSON 解析失败（缺少 keys 数组或格式错误）",
                                 "AI 返回的内容不是有效的配置意图 JSON。请查看 logs/ 目录下的日志文件了解详情。");
                    }
                    return;
                }

                // ---- AI 直写 XML 模式 ----
                RawOutput = NormalizeXml(text);
                CoreEntry.ModEntry?.Logger.Log($"[AIConfig] Tick(DirectXml): RawOutput 长度={RawOutput?.Length ?? 0}");
                if (CtKvIo.ValidateGeneratedXml(RawOutput, out int keyCount, out string configName, out string xmlError))
                {
                    if (RawOutput.IndexOf("<CheryToolsSonnetKeyViewer", StringComparison.Ordinal) >= 0)
                    {
                        // Sonnet 新格式：反序列化为对象后做键雨兜底，再重新序列化
                        var pkg = CtKvIo.ParseSonnetXml(RawOutput, out string parseErr);
                        if (pkg != null)
                        {
                            CtKvIo.FixSonnetRainLayout(pkg);
                            CtkvXml = CtKvIo.SerializeSonnetPackage(pkg);
                        }
                        else
                        {
                            CoreEntry.ModEntry?.Logger.Log($"[AIConfig] Sonnet 反序列化失败，按原文保存: {parseErr}");
                            CtkvXml = RawOutput;
                        }
                    }
                    else
                    {
                        // 旧格式：沿用 XML 打补丁方式做键雨兜底
                        CtkvXml = CtKvIo.FixTwoRowRainLayout(RawOutput);
                    }
                    State = GenState.Success;
                    CoreEntry.ModEntry?.Logger.Log($"[AIConfig] Tick(DirectXml): 校验成功，{keyCount} 个键，配置「{configName}」");
                }
                else
                {
                    FailWith("XML 校验失败: " + xmlError,
                             "AI 返回的内容不是有效的 CT KV XML。请查看 logs/ 目录下的日志文件了解详情。");
                }
            }
            catch (Exception ex)
            {
                State = GenState.Failed;
                Error = ex.Message;
                CtkvXml = null;
                CoreEntry.ModEntry?.Logger.Error($"[AIConfig] Tick 异常: {ex.GetType().Name}: {ex.Message}");
            }
            finally
            {
                _task = null;
            }
        }

        /// <summary>统一记录失败日志并设置错误状态</summary>
        private void FailWith(string logError, string userMessage)
        {
            State = GenState.Failed;
            CtkvXml = null;
            var failLog = new AiLogEntry
            {
                timestamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"),
                userPrompt = UserPrompt ?? "",
                round2Extracted = Truncate(RawOutput, 3000),
                error = logError + "。RawOutput 前500字符: " + Truncate(RawOutput, 500),
                success = false
            };
            WriteLog(failLog);
            Error = userMessage;
        }

        /// <summary>保存生成结果：规格模式按目标格式重新构建，直写模式按原 XML 保存</summary>
        public string SaveAsCtkv(string fileName, out string error)
        {
            error = "";
            if (Job == GenJob.Ov)
            {
                error = "当前是 Overlayer 生成结果，请改用保存 .ctov";
                return null;
            }
            if (Mode == GenMode.Spec && ParsedConfig != null)
            {
                SavedCtkvPath = Target == GenTarget.Sonnet
                    ? CtKvIo.SaveSonnetCtkvFromConfig(ParsedConfig, fileName, CoreEntry.ModEntry, out error)
                    : CtKvIo.SaveLegacyCtkvFromConfig(ParsedConfig, fileName, CoreEntry.ModEntry, out error);
                return SavedCtkvPath;
            }
            if (string.IsNullOrEmpty(CtkvXml))
            {
                error = "当前没有可保存的 AI 生成结果";
                return null;
            }
            SavedCtkvPath = Target == GenTarget.Sonnet
                ? CtKvIo.SaveSonnetCtkvFromXml(CtkvXml, fileName, CoreEntry.ModEntry, out error)
                : CtKvIo.SaveGeneratedCtkv(CtkvXml, fileName, CoreEntry.ModEntry, out error);
            return SavedCtkvPath;
        }

        /// <summary>把当前 Overlayer 生成结果保存为 .ctov，返回文件路径（失败返回 null）</summary>
        public string SaveAsOv(string fileName, out string error)
        {
            error = "";
            if (OvPackage == null)
            {
                error = "当前没有可保存的 Overlayer 生成结果";
                return null;
            }
            SavedOvPath = CtKvIo.SaveOvPackage(OvPackage, fileName, CoreEntry.ModEntry, out error);
            return SavedOvPath;
        }

        /// <summary>
        /// 把当前 Overlayer 生成结果导出为 CT 的 Overlayer 设置文件（绕过 CT 导入 Bug）。
        /// 用户需在关闭游戏后把该文件覆盖到 CT 的 Modules/CheryTools.Overlayer.Preview.xml。
        /// </summary>
        public string SaveAsOvSettings(string fileName, out string error)
        {
            error = "";
            if (OvPackage == null)
            {
                error = "当前没有可保存的 Overlayer 生成结果";
                return null;
            }
            return CtKvIo.SaveOvSettingsFile(OvPackage, fileName, CoreEntry.ModEntry, out error);
        }

        public void Reset()
        {
            _task = null;
            State = GenState.Idle;
            Error = null;
            CtkvXml = null;
            SavedCtkvPath = null;
            SavedOvPath = null;
            RawOutput = null;
            ParsedConfig = null;
            OvPackage = null;
        }

        // ==================== 校验器（返回 null 表示通过，否则返回错误信息）====================

        private static string ValidateLegacyXml(string xml)
        {
            if (CtKvIo.ValidateGeneratedXml(xml, out _, out _, out string err)) return null;
            return err;
        }

        private static string ValidateSonnetXml(string xml)
        {
            if (string.IsNullOrWhiteSpace(xml)) return "内容为空";
            if (CtKvIo.ParseSonnetXml(xml, out string parseErr) == null) return parseErr;
            if (CtKvIo.ValidateGeneratedXml(xml, out _, out _, out string err)) return null;
            return err;
        }

        private static string ValidateSpecJson(string json)
        {
            var cfg = KVConfig.FromAiJson(json);
            if (cfg == null) return "无法解析为配置意图 JSON（JSON 格式错误或缺少 keys 数组）";
            if (cfg.keys == null || cfg.keys.Count == 0) return "keys 数组为空";
            return null;
        }

        private static string ValidateOvJson(string json)
        {
            var spec = OvSpec.FromAiJson(json);
            if (OvSpecBuilder.Validate(spec, out _, out string err)) return null;
            return err;
        }

        // ==================== 两轮对话 ====================

        /// <summary>
        /// 通用两轮对话：第一轮生成 → 校验通过即返回；否则第二轮把具体错误回灌给模型修复。
        /// 通过 normalize/validate 回调适配四种产出（旧格式 XML / Sonnet XML / KV 意图 JSON / OV 意图 JSON）。
        /// </summary>
        private static string TwoRoundGenerate(ApiKeyEntry key, string userPrompt,
            string systemPromptGenerate, string systemPromptSelfCheck, string expectedForm,
            Func<string, string> normalize, Func<string, string> validate)
        {
            ServicePointManager.SecurityProtocol |= SecurityProtocolType.Tls12;
            string url = BuildChatCompletionsUrl(key.baseUrl);
            var log = new AiLogEntry
            {
                timestamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"),
                model = key.model,
                userPrompt = userPrompt
            };

            try
            {
                // 记录游戏目录（方便排查路径问题）
                string gameDir = ModPathHelper.GetGameDir();
                CoreEntry.ModEntry?.Logger.Log($"[AIConfig] 游戏目录: {gameDir}");
                CoreEntry.ModEntry?.Logger.Log($"[AIConfig] API URL: {url}");
                CoreEntry.ModEntry?.Logger.Log($"[AIConfig] 模型: {key.model}  期望产出: {expectedForm}");

                // ---- 第一轮：生成 ----
                // 实时按 baseUrl 匹配预设取参数，预设改动无需重新添加密钥即生效
                ProviderPreset preset = null;
                if (!string.IsNullOrEmpty(key.baseUrl))
                {
                    string kb = key.baseUrl.TrimEnd('/');
                    foreach (var p in PresetStore.All)
                        if (!string.IsNullOrEmpty(p.baseUrl) && p.baseUrl.TrimEnd('/') == kb) { preset = p; break; }
                }
                int maxTokens = preset != null ? preset.maxTokens : key.maxTokens;
                string thinkingDisableJson = (CoreEntry.Settings != null && CoreEntry.Settings.DisableThinking)
                    ? (preset != null ? preset.thinkingDisableJson : key.thinkingDisableJson)
                    : "";
                string body1 = BuildChatBody(key.model, systemPromptGenerate, userPrompt, thinkingDisableJson, maxTokens);
                string raw1 = HttpPostJson(url, body1, key.apiKey);
                log.round1Raw = Truncate(raw1, 2000);
                CoreEntry.ModEntry?.Logger.Log($"[AIConfig] 第一轮响应长度: {raw1?.Length ?? 0}");
                LogCacheMetrics(raw1, key.model);

                string text1 = ExtractContent(raw1);
                log.round1Extracted = Truncate(text1, 2000);

                if (string.IsNullOrWhiteSpace(text1))
                {
                    log.error = "第一轮返回为空。原始响应: " + Truncate(raw1, 500);
                    log.success = false;
                    WriteLog(log);
                    throw new Exception("第一轮生成返回为空，请查看日志");
                }

                // 若第一轮产出已有效，直接采用，避免第二轮重复输出导致的截断与额外开销
                string norm1 = normalize(text1);
                string err1 = validate(norm1);
                if (err1 == null)
                {
                    CoreEntry.ModEntry?.Logger.Log($"[AIConfig] 第一轮产出已有效（{expectedForm}），跳过第二轮");
                    log.success = true;
                    WriteLog(log);
                    return norm1;
                }

                // ---- 第二轮：仅当第一轮无效时，作为"修复器"给出具体错误并让其修正 ----
                string selfCheckPrompt = "用户的原始需求描述：\n" + userPrompt +
                    "\n\n以下AI生成的" + expectedForm + "未能通过校验，错误信息：\n" + err1 +
                    "\n\n请修复并输出完整的" + expectedForm + "。务必：标签/括号全部成对闭合、颜色用单个元素包含 4 个 <float> 子节点（不得用重复标签）、输出必须完整不要截断。\n\n" + norm1;
                string body2 = BuildChatBody(key.model, systemPromptSelfCheck, selfCheckPrompt, thinkingDisableJson, maxTokens);
                string raw2 = HttpPostJson(url, body2, key.apiKey);
                log.round2Raw = Truncate(raw2, 2000);
                CoreEntry.ModEntry?.Logger.Log($"[AIConfig] 第二轮响应长度: {raw2?.Length ?? 0}");

                string text2 = ExtractContent(raw2);
                log.round2Extracted = Truncate(text2, 2000);

                if (string.IsNullOrWhiteSpace(text2))
                {
                    log.error = "第二轮自检返回为空。原始响应: " + Truncate(raw2, 500);
                    log.success = false;
                    WriteLog(log);
                    throw new Exception("自检阶段返回为空，请查看日志");
                }

                log.success = true;
                WriteLog(log);
                return normalize(text2);
            }
            catch (Exception ex)
            {
                // 确保任何异常都记录日志
                if (string.IsNullOrEmpty(log.error))
                    log.error = ex.Message;
                log.success = false;
                WriteLog(log);
                throw;
            }
        }

        /// <summary>从API原始响应中提取 content 或 reasoning_content</summary>
        private static string ExtractContent(string raw)
        {
            string text = ExtractJsonField(raw, "content");
            if (string.IsNullOrWhiteSpace(text))
                text = ExtractJsonField(raw, "reasoning_content");
            return text;
        }

        /// <summary>从 JSON 字符串中手动提取指定字段的值（处理转义引号）</summary>
        private static string ExtractJsonField(string json, string fieldName)
        {
            if (string.IsNullOrEmpty(json)) return null;
            string p1 = "\"" + fieldName + "\":\"";
            string p2 = "\"" + fieldName + "\": \"";
            int idx = json.IndexOf(p1);
            if (idx < 0) idx = json.IndexOf(p2);
            if (idx < 0) return null;

            int start = idx + p1.Length;
            if (json[idx + fieldName.Length + 2] == ' ') start++;

            var sb = new StringBuilder();
            int i = start;
            while (i < json.Length)
            {
                char c = json[i];
                if (c == '\\' && i + 1 < json.Length)
                {
                    char next = json[i + 1];
                    switch (next)
                    {
                        case '"': sb.Append('"'); i += 2; continue;
                        case '\\': sb.Append('\\'); i += 2; continue;
                        case '/': sb.Append('/'); i += 2; continue;
                        case 'n': sb.Append('\n'); i += 2; continue;
                        case 'r': sb.Append('\r'); i += 2; continue;
                        case 't': sb.Append('\t'); i += 2; continue;
                        case 'b': sb.Append('\b'); i += 2; continue;
                        case 'f': sb.Append('\f'); i += 2; continue;
                        case 'u':
                            // 解码 \uXXXX（如 \u003c -> '<'，模型常把 < > 转义成 unicode）
                            if (i + 5 < json.Length &&
                                int.TryParse(json.Substring(i + 2, 4),
                                    System.Globalization.NumberStyles.HexNumber, null, out int code))
                            {
                                sb.Append((char)code);
                                i += 6;
                                continue;
                            }
                            sb.Append(c); i++; continue;
                        default: sb.Append(c); i++; continue;
                    }
                }
                if (c == '"') break;
                sb.Append(c);
                i++;
            }
            return sb.ToString();
        }

        /// <summary>从 AI 输出中提取合法 XML：剥离前置说明文字与 markdown 代码围栏，只保留根元素</summary>
        private static string NormalizeXml(string text)
        {
            if (string.IsNullOrEmpty(text)) return text;

            // 1) 若存在 markdown 围栏，提取围栏内部内容
            int fenceStart = text.IndexOf("```");
            if (fenceStart >= 0)
            {
                int contentStart = text.IndexOf('\n', fenceStart);
                if (contentStart < 0) contentStart = fenceStart + 3;
                int fenceEnd = text.LastIndexOf("```");
                if (fenceEnd > contentStart)
                    text = text.Substring(contentStart + 1, fenceEnd - contentStart - 1);
            }

            // 2) 定位到根元素，去掉前置说明文字（新旧两种根元素都支持）
            int root = text.IndexOf("<CheryToolsSonnetKeyViewer");
            if (root < 0) root = text.IndexOf("<KeyViewerPackage");
            if (root < 0) root = text.IndexOf('<');
            if (root >= 0)
            {
                if (root > 0) text = text.Substring(root);
                // 3) 截断尾部多余文字，保留到最后一个 >
                int lastClose = text.LastIndexOf('>');
                if (lastClose >= 0 && lastClose < text.Length - 1)
                    text = text.Substring(0, lastClose + 1);
            }

            return text.Trim();
        }

        /// <summary>从 AI 输出中提取最外层 JSON 对象（剥离 markdown 围栏与说明文字）</summary>
        private static string NormalizeJson(string text)
        {
            if (string.IsNullOrEmpty(text)) return text;

            int fenceStart = text.IndexOf("```");
            if (fenceStart >= 0)
            {
                int contentStart = text.IndexOf('\n', fenceStart);
                if (contentStart < 0) contentStart = fenceStart + 3;
                int fenceEnd = text.LastIndexOf("```");
                if (fenceEnd > contentStart)
                    text = text.Substring(contentStart + 1, fenceEnd - contentStart - 1);
            }

            int start = text.IndexOf('{');
            int end = text.LastIndexOf('}');
            if (start >= 0 && end > start) text = text.Substring(start, end - start + 1);
            return text.Trim();
        }

        /// <summary>解析并记录供应商返回的 prompt 缓存命中指标（DeepSeek/部分网关会返回 usage.prompt_cache_*）</summary>
        private static void LogCacheMetrics(string raw, string model)
        {
            try
            {
                if (string.IsNullOrEmpty(raw) || raw[0] != '{') return;
                long hit = ExtractJsonNumber(raw, "prompt_cache_hit_tokens");
                long miss = ExtractJsonNumber(raw, "prompt_cache_miss_tokens");
                if (hit + miss > 0)
                {
                    double rate = 100.0 * hit / (hit + miss);
                    CoreEntry.ModEntry?.Logger.Log($"[AIConfig] [{model}] prompt缓存命中={hit} 未命中={miss} 命中率={rate:F1}%");
                }
            }
            catch { }
        }

        /// <summary>提取 JSON 数字字段值（如 usage 里的计数），找不到返回 0</summary>
        private static long ExtractJsonNumber(string json, string fieldName)
        {
            try
            {
                string key = "\"" + fieldName + "\":";
                int idx = json.IndexOf(key);
                if (idx < 0) return 0;
                int start = idx + key.Length;
                int end = start;
                while (end < json.Length && (char.IsDigit(json[end]) || json[end] == '.')) end++;
                long val;
                return long.TryParse(json.Substring(start, end - start), out val) ? val : 0;
            }
            catch { return 0; }
        }

        private static string BuildChatBody(string model, string system, string user, string thinkingDisableJson, int maxTokens)
        {
            var sb = new StringBuilder();
            sb.Append("{\"model\":\"").Append(EscapeJson(model))
              .Append("\",\"temperature\":0.2,\"max_tokens\":").Append(maxTokens).Append(",\"stream\":false");
            // 关闭模型思考：注入供应商对应的禁用参数（如 "reasoning":{"enabled":false} 或 "thinking":{"type":"disabled"}）
            if (!string.IsNullOrEmpty(thinkingDisableJson))
                sb.Append(",").Append(thinkingDisableJson);
            sb.Append(",\"messages\":[")
              .Append("{\"role\":\"system\",\"content\":\"").Append(EscapeJson(system)).Append("\"},")
              .Append("{\"role\":\"user\",\"content\":\"").Append(EscapeJson(user)).Append("\"}")
              .Append("]}");
            return sb.ToString();
        }

        private static string HttpPostJson(string url, string json, string apiKey)
        {
            var req = (HttpWebRequest)WebRequest.Create(url);
            req.Method = "POST";
            req.ContentType = "application/json";
            req.Accept = "application/json";
            req.Headers["Authorization"] = "Bearer " + apiKey;
            req.Timeout = 120000;
            byte[] bytes = Encoding.UTF8.GetBytes(json);
            req.ContentLength = bytes.Length;
            using (var s = req.GetRequestStream()) s.Write(bytes, 0, bytes.Length);
            using (var resp = (HttpWebResponse)req.GetResponse())
            using (var reader = new StreamReader(resp.GetResponseStream(), Encoding.UTF8))
                return reader.ReadToEnd();
        }

        /// <summary>构建 Chat Completions 端点 URL，并对 TokenRa 网关自动补齐 /v1 路径</summary>
        private static string BuildChatCompletionsUrl(string baseUrl)
        {
            string b = (baseUrl ?? "").Trim().TrimEnd('/');
            if (string.IsNullOrEmpty(b)) b = "https://api.deepseek.com";

            // TokenRa 统一网关：必须是 /v1/chat/completions。
            // 若用户填了 https://tokenra.io（缺 /v1），命中网页 SPA 会返回 HTML 而非 JSON。
            if (Uri.TryCreate(b, UriKind.Absolute, out var u) &&
                (u.Host.Equals("tokenra.io", StringComparison.OrdinalIgnoreCase) ||
                 u.Host.EndsWith(".tokenra.io", StringComparison.OrdinalIgnoreCase)) &&
                !b.EndsWith("/v1") && b.IndexOf("/v1/", StringComparison.Ordinal) < 0)
            {
                b = b + "/v1";
            }
            return b + "/chat/completions";
        }

        // ==================== 日志系统 ====================

        private static string GetLogDir()
        {
            string gameDir = ModPathHelper.GetGameDir();
            string dir = Path.Combine(gameDir, "AgentKeyViewer_config", "logs");
            CoreEntry.ModEntry?.Logger.Log($"[AIConfig] GetLogDir -> '{dir}'");
            try
            {
                if (!Directory.Exists(dir))
                {
                    Directory.CreateDirectory(dir);
                    CoreEntry.ModEntry?.Logger.Log($"[AIConfig] 创建日志目录: '{dir}'");
                }
            }
            catch (Exception ex)
            {
                CoreEntry.ModEntry?.Logger.Error($"[AIConfig] 创建日志目录失败: {ex.Message}");
            }
            return dir;
        }

        private static void WriteLog(AiLogEntry log)
        {
            try
            {
                string dir = GetLogDir();
                string file = Path.Combine(dir, "ai_" + DateTime.Now.ToString("yyyyMMdd_HHmmss") + ".json");
                string json = JsonUtility.ToJson(log, true);
                CoreEntry.ModEntry?.Logger.Log($"[AIConfig] 写入日志: '{file}' (内容长度: {json?.Length ?? 0})");
                File.WriteAllText(file, json, Encoding.UTF8);
                CoreEntry.ModEntry?.Logger.Log($"[AIConfig] 日志已保存: {file}");
            }
            catch (Exception ex)
            {
                CoreEntry.ModEntry?.Logger.Error($"[AIConfig] 日志保存失败: {ex.GetType().Name}: {ex.Message}");
                CoreEntry.ModEntry?.Logger.Error($"[AIConfig] 堆栈: {ex.StackTrace}");
            }
        }

        // ==================== 工具方法 ====================

        private static string EscapeJson(string s)
        {
            if (s == null) return "";
            var sb = new StringBuilder(s.Length);
            foreach (char c in s)
            {
                switch (c)
                {
                    case '"': sb.Append("\\\""); break;
                    case '\\': sb.Append("\\\\"); break;
                    case '\n': sb.Append("\\n"); break;
                    case '\r': sb.Append("\\r"); break;
                    case '\t': sb.Append("\\t"); break;
                    case '\b': sb.Append("\\b"); break;
                    case '\f': sb.Append("\\f"); break;
                    default: sb.Append(c); break;
                }
            }
            return sb.ToString();
        }

        private static string Truncate(string s, int n)
        {
            if (string.IsNullOrEmpty(s)) return "";
            return s.Length <= n ? s : s.Substring(0, n);
        }
    }

    // 保留的响应解析类（不再使用，但保留以防引用）
    [Serializable]
    public class ChatResponse { public List<Choice> choices; }
    [Serializable]
    public class Choice { public ChatMessage message; }
    [Serializable]
    public class ChatMessage
    {
        public string content;
        public string reasoning_content;
    }
}
