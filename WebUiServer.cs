using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;
using ADOFAI.AgentKeyViewer.Bootstrap;

namespace ADOFAI.AgentKeyViewer
{
    /// <summary>
    /// 本地 WebUI 服务：仅绑定回环地址，用随机令牌鉴权，取代 UMM IMGUI 面板作为主要操作界面。
    ///
    /// 线程模型：
    ///   - 后台线程跑 HttpListener 接受循环，解析请求后把需要访问游戏/模组状态的请求
    ///     投递到 <see cref="MainThreadQueue"/>，并等待主线程回填响应（带超时）；
    ///   - 主线程在 <see cref="GameLoop"/>.Update 里调用 <see cref="PumpMainThread"/> 排空队列，
    ///     因此所有生成、文件、密钥操作都在 Unity 主线程执行，无并发风险。
    /// </summary>
    public static class WebUiServer
    {
        private sealed class PendingRequest
        {
            public string Method = "GET";
            public string Path = "/";
            public Dictionary<string, string> Query = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            public string Body = "";
            public readonly TaskCompletionSource<WebUiResponse> Completion =
                new TaskCompletionSource<WebUiResponse>(TaskCreationOptions.RunContinuationsAsynchronously);
        }

        private static readonly ConcurrentQueue<PendingRequest> MainThreadQueue = new ConcurrentQueue<PendingRequest>();

        private static HttpListener _listener;
        private static Thread _acceptThread;
        private static volatile bool _running;
        private static int _pendingCount;

        public static bool IsRunning => _running;
        public static int Port { get; private set; }
        /// <summary>本次启动随机生成的访问令牌（防止本机其他程序调用接口）</summary>
        public static string Token { get; private set; } = "";
        public static string LastError { get; private set; } = "";

        /// <summary>带令牌的完整访问地址</summary>
        public static string Url => IsRunning ? $"http://127.0.0.1:{Port}/?t={Token}" : "";

        // ====================================================================
        //  启停
        // ====================================================================

        public static bool Start(int preferredPort, out string error)
        {
            error = "";
            if (_running) { error = "WebUI 已在运行"; return true; }

            LastError = "";
            Token = Guid.NewGuid().ToString("N").Substring(0, 16);

            // 端口占用时顺延最多 20 个端口
            int basePort = preferredPort > 0 && preferredPort < 65000 ? preferredPort : 8765;
            for (int i = 0; i < 20; i++)
            {
                int port = basePort + i;
                // 先试回环地址，失败再试 localhost（个别系统上 127.0.0.1 前缀注册会失败）
                foreach (var host in new[] { "127.0.0.1", "localhost" })
                {
                    var listener = new HttpListener();
                    string prefix = $"http://{host}:{port}/";
                    try
                    {
                        listener.Prefixes.Add(prefix);
                        listener.Start();
                    }
                    catch (Exception ex)
                    {
                        try { listener.Close(); } catch { }
                        LastError = ex.Message;
                        continue;
                    }
                    _listener = listener;
                    Port = port;
                    _running = true;
                    _acceptThread = new Thread(AcceptLoop) { IsBackground = true, Name = "AgentKeyViewer-WebUI" };
                    _acceptThread.Start();
                    CoreEntry.ModEntry?.Logger.Log($"[WebUI] 已启动: {prefix}（令牌 {Token}）");
                    return true;
                }
            }

            error = $"无法监听端口 {basePort}~{basePort + 19}：{LastError}";
            CoreEntry.ModEntry?.Logger.Error($"[WebUI] {error}");
            return false;
        }

        public static void Stop()
        {
            if (!_running) return;
            _running = false;
            try { _listener?.Stop(); } catch { }
            try { _listener?.Close(); } catch { }
            _listener = null;
            CoreEntry.ModEntry?.Logger.Log("[WebUI] 已停止");
        }

        // ====================================================================
        //  后台接受循环
        // ====================================================================

        private static void AcceptLoop()
        {
            while (_running)
            {
                HttpListenerContext ctx;
                try { ctx = _listener.GetContext(); }
                catch { break; }
                ThreadPool.QueueUserWorkItem(_ => HandleContext(ctx));
            }
        }

        private static void HandleContext(HttpListenerContext ctx)
        {
            try
            {
                var request = ctx.Request;
                string path = request.Url?.AbsolutePath ?? "/";

                // 鉴权：令牌可放在 ?t= 或 X-Token 头
                string token = request.QueryString["t"] ?? request.Headers["X-Token"];
                if (!string.Equals(token, Token, StringComparison.Ordinal))
                {
                    Write(ctx, new WebUiResponse { Status = 403, Body = Encoding.UTF8.GetBytes("{\"ok\":false,\"error\":\"令牌无效\"}") });
                    return;
                }

                // 静态页面不经过主线程
                if (path == "/" || path == "/index.html")
                {
                    Write(ctx, new WebUiResponse
                    {
                        ContentType = "text/html; charset=utf-8",
                        Body = Encoding.UTF8.GetBytes(WebUiPage.Html)
                    });
                    return;
                }

                var pending = new PendingRequest { Method = request.HttpMethod.ToUpperInvariant(), Path = path };
                foreach (string k in request.QueryString.AllKeys)
                    if (k != null) pending.Query[k] = request.QueryString[k];

                if (request.HasEntityBody)
                {
                    using (var reader = new StreamReader(request.InputStream, request.ContentEncoding ?? Encoding.UTF8))
                        pending.Body = reader.ReadToEnd();
                }

                if (Interlocked.Increment(ref _pendingCount) > 32)
                {
                    Interlocked.Decrement(ref _pendingCount);
                    Write(ctx, new WebUiResponse { Status = 503, Body = Encoding.UTF8.GetBytes("{\"ok\":false,\"error\":\"请求过多\"}") });
                    return;
                }

                MainThreadQueue.Enqueue(pending);
                if (!pending.Completion.Task.Wait(30000))
                {
                    Write(ctx, new WebUiResponse { Status = 504, Body = Encoding.UTF8.GetBytes("{\"ok\":false,\"error\":\"主线程处理超时\"}") });
                }
                else
                {
                    Write(ctx, pending.Completion.Task.Result);
                }
                Interlocked.Decrement(ref _pendingCount);
            }
            catch (Exception ex)
            {
                try
                {
                    Write(ctx, new WebUiResponse { Status = 500, Body = Encoding.UTF8.GetBytes(Json.Escape(ex.Message)) });
                }
                catch { }
            }
        }

        private static void Write(HttpListenerContext ctx, WebUiResponse res)
        {
            try
            {
                ctx.Response.StatusCode = res.Status;
                ctx.Response.ContentType = res.ContentType;
                ctx.Response.ContentLength64 = res.Body.Length;
                ctx.Response.OutputStream.Write(res.Body, 0, res.Body.Length);
            }
            catch { }
            finally
            {
                try { ctx.Response.Close(); } catch { }
            }
        }

        // ====================================================================
        //  主线程：排空队列
        // ====================================================================

        /// <summary>在 GameLoop.Update 中调用，处理来自 WebUI 的请求（保证在 Unity 主线程执行）</summary>
        public static void PumpMainThread()
        {
            int budget = 8; // 每帧最多处理 8 个，避免卡帧
            while (budget-- > 0 && MainThreadQueue.TryDequeue(out var req))
            {
                WebUiResponse res;
                try { res = Route(req); }
                catch (Exception ex)
                {
                    CoreEntry.ModEntry?.Logger.Error($"[WebUI] 处理 {req.Path} 失败: {ex}");
                    res = Json.Error("处理失败: " + ex.Message);
                }
                req.Completion.TrySetResult(res);
            }
        }

        // ====================================================================
        //  路由（主线程）
        // ====================================================================

        private static WebUiResponse Route(PendingRequest req)
        {
            // 模组被停用时只允许查询状态，其余操作给出明确提示
            if (!CoreEntry.Enabled && req.Path != "/api/state")
                return Json.Error("模组已停用，请在 UMM 中启用 Agent Key Viewer");

            switch (req.Path)
            {
                case "/api/state": return ApiState();
                case "/api/keys": return req.Method == "POST" ? ApiKeysPost(req.Body) : ApiKeysGet();
                case "/api/importagent": return ApiImportAgent(req.Body);
                case "/api/generate": return ApiGenerate(req.Body);
                case "/api/save": return ApiSave(req.Body);
                case "/api/settings": return ApiSettings(req.Body);
                case "/api/prompts": return ApiPrompts();
                case "/api/prompt": return req.Method == "POST" ? ApiPromptPost(req.Body) : ApiPromptGet(req.Query);
                case "/api/prompts/reload": return ApiPromptsReload();
                case "/api/presets": return req.Method == "POST" ? ApiPresetsPost(req.Body) : ApiPresetsGet();
                case "/api/update": return ApiUpdate();
                case "/api/update/check": return ApiUpdateCheck();
                case "/api/update/download": return ApiUpdateDownload();
                case "/api/update/apply": return ApiUpdateApply();
                case "/api/reloadcore": return ApiReloadCore();
                case "/api/files": return ApiFiles();
                case "/api/file": return ApiFile(req.Query);
                case "/api/download": return ApiDownload(req.Query);
                case "/api/fixrain": return ApiFixRain(req.Body);
                case "/api/delete": return ApiDelete(req.Body);
                case "/api/openfolder": return ApiOpenFolder();
                default: return Json.Error("未知接口: " + req.Path, 404);
            }
        }

        private static WebUiResponse ApiState()
        {
            var gen = CoreEntry.AIGen;
            var sb = new StringBuilder();
            sb.Append("{\"ok\":true");
            sb.Append(",\"port\":").Append(Port);
            sb.Append(",\"token\":\"").Append(Json.Escape(Token)).Append('"');
            sb.Append(",\"modEnabled\":").Append(CoreEntry.Enabled ? "true" : "false");

            // 生成状态
            sb.Append(",\"gen\":{");
            sb.Append("\"busy\":").Append(gen != null && gen.IsBusy ? "true" : "false");
            sb.Append(",\"state\":\"").Append(gen?.State.ToString() ?? "Idle").Append('"');
            sb.Append(",\"error\":\"").Append(Json.Escape(gen?.Error ?? "")).Append('"');
            sb.Append(",\"job\":\"").Append(gen?.Job.ToString() ?? "Kv").Append('"');
            sb.Append(",\"savedCtkv\":\"").Append(Json.Escape(gen?.SavedCtkvPath ?? "")).Append('"');
            sb.Append(",\"savedOv\":\"").Append(Json.Escape(gen?.SavedOvPath ?? "")).Append('"');
            sb.Append(",\"rawLength\":").Append(gen?.RawOutput?.Length ?? 0);
            sb.Append(",\"rawPreview\":\"").Append(Json.Escape(Truncate(gen?.CtkvXml, 4000))).Append('"');
            sb.Append('}');

            // 设置
            var s = CoreEntry.Settings;
            sb.Append(",\"settings\":{");
            sb.Append("\"genMode\":").Append(s?.GenMode ?? 0);
            sb.Append(",\"targetFormat\":").Append(s?.TargetFormat ?? 0);
            sb.Append(",\"disableThinking\":").Append(s != null && s.DisableThinking ? "true" : "false");
            sb.Append(",\"autoOpen\":").Append(s != null && s.AutoOpenWebUi ? "true" : "false");
            sb.Append('}');

            // 供应商预设（支持热重载）
            var presets = PresetStore.All;
            sb.Append(",\"providers\":[");
            for (int i = 0; i < presets.Count; i++)
            {
                var p = presets[i];
                if (i > 0) sb.Append(',');
                sb.Append("{\"name\":\"").Append(Json.Escape(p.name)).Append('"');
                sb.Append(",\"baseUrl\":\"").Append(Json.Escape(p.baseUrl)).Append('"');
                sb.Append(",\"model\":\"").Append(Json.Escape(p.model ?? "")).Append('"');
                sb.Append(",\"models\":[");
                if (p.models != null)
                    for (int j = 0; j < p.models.Length; j++)
                    {
                        if (j > 0) sb.Append(',');
                        sb.Append('"').Append(Json.Escape(p.models[j])).Append('"');
                    }
                sb.Append("]}");
            }
            sb.Append(']');

            // 密钥（隐藏明文，只给掩码）
            sb.Append(",\"keys\":[");
            var list = CoreEntry.ApiKeys;
            if (list?.keys != null)
                for (int i = 0; i < list.keys.Count; i++)
                {
                    var k = list.keys[i];
                    if (i > 0) sb.Append(',');
                    sb.Append("{\"index\":").Append(i);
                    sb.Append(",\"name\":\"").Append(Json.Escape(k.name)).Append('"');
                    sb.Append(",\"baseUrl\":\"").Append(Json.Escape(k.baseUrl)).Append('"');
                    sb.Append(",\"model\":\"").Append(Json.Escape(k.model)).Append('"');
                    sb.Append(",\"masked\":\"").Append(Json.Escape(MaskKey(k.apiKey))).Append('"');
                    sb.Append(",\"isDefault\":").Append(k.isDefault ? "true" : "false");
                    sb.Append('}');
                }
            sb.Append(']');
            sb.Append(",\"selectedKeyIndex\":").Append(CoreEntry.SelectedKeyIndex);
            // CT Overlayer 设置文件路径（导出绕过文件时要覆盖到此处）
            sb.Append(",\"ctOvSettingsPath\":\"").Append(Json.Escape(CtKvIo.GetCtOverlayerSettingsPath(CoreEntry.ModEntry))).Append('"');
            sb.Append('}');
            return Json.Raw(sb.ToString());
        }

        private static WebUiResponse ApiKeysGet() => ApiState();

        /// <summary>密钥增删改：{action:"add"|"delete"|"default", index, name, baseUrl, apiKey, model}</summary>
        private static WebUiResponse ApiKeysPost(string body)
        {
            var list = CoreEntry.ApiKeys;
            if (list == null) return Json.Error("密钥列表未初始化");

            string action = Json.GetString(body, "action");
            int index = Json.GetInt(body, "index", -1);

            if (action == "delete")
            {
                if (index < 0 || index >= list.keys.Count) return Json.Error("索引无效");
                list.keys.RemoveAt(index);
            }
            else if (action == "default")
            {
                if (index < 0 || index >= list.keys.Count) return Json.Error("索引无效");
                for (int i = 0; i < list.keys.Count; i++) list.keys[i].isDefault = i == index;
                CoreEntry.SelectedKeyIndex = index;
            }
            else if (action == "add" || action == "update")
            {
                string apiKey = Json.GetString(body, "apiKey");
                var entry = index >= 0 && index < list.keys.Count ? list.keys[index] : new ApiKeyEntry();
                entry.name = Json.GetString(body, "name");
                entry.baseUrl = Json.GetString(body, "baseUrl");
                entry.model = Json.GetString(body, "model");
                if (!string.IsNullOrWhiteSpace(apiKey)) entry.apiKey = apiKey;
                if (string.IsNullOrWhiteSpace(entry.name)) entry.name = "未命名密钥";
                if (index < 0)
                {
                    entry.isDefault = list.keys.Count == 0;
                    list.keys.Add(entry);
                    index = list.keys.Count - 1;
                }
                if (CoreEntry.SelectedKeyIndex < 0) CoreEntry.SelectedKeyIndex = index;
            }
            else
            {
                return Json.Error("未知操作: " + action);
            }

            CoreEntry.SaveApiKeys();
            return ApiKeysGet();
        }

        /// <summary>启动生成：{prompt, job:"kv"|"ov", mode:0|1, format:0|1, keyIndex}</summary>
        private static WebUiResponse ApiGenerate(string body)
        {
            var gen = CoreEntry.AIGen;
            if (gen == null) return Json.Error("生成器未初始化");
            if (gen.IsBusy) return Json.Error("已有生成任务正在进行，请稍候");

            string prompt = Json.GetString(body, "prompt");
            if (string.IsNullOrWhiteSpace(prompt)) return Json.Error("请填写需求描述");

            var list = CoreEntry.ApiKeys;
            int keyIndex = Json.GetInt(body, "keyIndex", CoreEntry.SelectedKeyIndex);
            if (list?.keys == null || list.keys.Count == 0) return Json.Error("请先添加 API 密钥");
            if (keyIndex < 0 || keyIndex >= list.keys.Count) keyIndex = 0;
            var key = list.keys[keyIndex];
            if (string.IsNullOrWhiteSpace(key.apiKey)) return Json.Error("该密钥没有填写 API Key");

            string job = Json.GetString(body, "job");
            gen.Job = job == "ov" ? AIConfigGenerator.GenJob.Ov : AIConfigGenerator.GenJob.Kv;
            gen.Mode = Json.GetInt(body, "mode", CoreEntry.Settings?.GenMode ?? 0) == 1
                ? AIConfigGenerator.GenMode.DirectXml : AIConfigGenerator.GenMode.Spec;
            gen.Target = Json.GetInt(body, "format", CoreEntry.Settings?.TargetFormat ?? 0) == 1
                ? AIConfigGenerator.GenTarget.Legacy : AIConfigGenerator.GenTarget.Sonnet;

            // 同步回设置，便于下次打开时保持选择
            if (CoreEntry.Settings != null)
            {
                CoreEntry.Settings.GenMode = gen.Mode == AIConfigGenerator.GenMode.Spec ? 0 : 1;
                CoreEntry.Settings.TargetFormat = gen.Target == AIConfigGenerator.GenTarget.Sonnet ? 0 : 1;
            }

            CoreEntry.SelectedKeyIndex = keyIndex;
            gen.ActiveKey = key;
            gen.UserPrompt = prompt;
            gen.Start();
            CoreEntry.ModEntry?.Logger.Log($"[WebUI] 已提交生成任务 job={gen.Job} mode={gen.Mode} format={gen.Target}");

            return Json.Raw("{\"ok\":true,\"job\":\"" + gen.Job + "\"}");
        }

        /// <summary>保存生成结果：{kind:"kv"|"ov", fileName}</summary>
        private static WebUiResponse ApiSave(string body)
        {
            var gen = CoreEntry.AIGen;
            if (gen == null) return Json.Error("生成器未初始化");
            string kind = Json.GetString(body, "kind");
            string fileName = Json.GetString(body, "fileName");

            string path;
            string error;
            if (kind == "ov")
                path = gen.SaveAsOv(fileName, out error);
            else if (kind == "ovsettings")
                path = gen.SaveAsOvSettings(fileName, out error);
            else
                path = gen.SaveAsCtkv(fileName, out error);

            if (string.IsNullOrEmpty(path)) return Json.Error(string.IsNullOrEmpty(error) ? "保存失败" : error);
            return Json.Raw("{\"ok\":true,\"path\":\"" + Json.Escape(path) + "\"}");
        }

        /// <summary>从 agent 配置文件导入密钥：{path}</summary>
        private static WebUiResponse ApiImportAgent(string body)
        {
            string path = Json.GetString(body, "path");
            if (string.IsNullOrWhiteSpace(path)) return Json.Error("请填写 agent 配置文件路径");
            if (!File.Exists(path)) return Json.Error("文件不存在：" + path);

            if (!ApiKeyStore.TryImportFromAgentConfig(path, out var entry) || entry == null)
                return Json.Error("导入失败：文件中未找到 API_KEY");

            var list = CoreEntry.ApiKeys;
            if (list == null) return Json.Error("密钥列表未初始化");
            list.keys.Add(entry);
            list.agentConfigPath = path;
            CoreEntry.SelectedKeyIndex = list.keys.Count - 1;
            CoreEntry.SaveApiKeys();

            CoreEntry.ModEntry?.Logger.Log($"[WebUI] 已从 agent 配置导入密钥：{entry.name}");
            return Json.Raw("{\"ok\":true,\"name\":\"" + Json.Escape(entry.name) + "\"}");
        }

        /// <summary>保存界面设置：{genMode, targetFormat, autoOpen}</summary>
        private static WebUiResponse ApiSettings(string body)
        {
            var s = CoreEntry.Settings;
            if (s == null) return Json.Error("设置未初始化");
            int genMode = Json.GetInt(body, "genMode", s.GenMode);
            int targetFormat = Json.GetInt(body, "targetFormat", s.TargetFormat);
            s.GenMode = genMode == 1 ? 1 : 0;
            s.TargetFormat = targetFormat == 1 ? 1 : 0;
            s.AutoOpenWebUi = Json.GetBool(body, "autoOpen", s.AutoOpenWebUi);
            return Json.Raw("{\"ok\":true}");
        }

        /// <summary>列出 ctkv 目录下的 .ctkv / .ctov 文件</summary>
        private static WebUiResponse ApiFiles()
        {
            var dir = CtKvIo.GetCtkvDir(CoreEntry.ModEntry);
            var sb = new StringBuilder("{\"ok\":true,\"dir\":\"" + Json.Escape(dir) + "\",\"files\":[");
            bool first = true;
            try
            {
                if (Directory.Exists(dir))
                {
                    var files = new List<string>();
                    files.AddRange(Directory.GetFiles(dir, "*.ctkv"));
                    files.AddRange(Directory.GetFiles(dir, "*.ctov"));
                    files.Sort(StringComparer.OrdinalIgnoreCase);
                    foreach (var f in files)
                    {
                        var info = new FileInfo(f);
                        if (!first) sb.Append(',');
                        first = false;
                        sb.Append("{\"name\":\"").Append(Json.Escape(info.Name)).Append('"');
                        sb.Append(",\"size\":").Append(info.Length);
                        sb.Append(",\"modified\":\"").Append(info.LastWriteTime.ToString("yyyy-MM-dd HH:mm:ss")).Append('"');
                        sb.Append(",\"kind\":\"").Append(info.Extension.TrimStart('.').ToLowerInvariant()).Append('"');
                        sb.Append('}');
                    }
                }
            }
            catch (Exception ex) { sb.Append("],\"error\":\"").Append(Json.Escape(ex.Message)).Append("\"}"); return Json.Raw(sb.ToString()); }
            sb.Append("]}");
            return Json.Raw(sb.ToString());
        }

        /// <summary>预览包内 XML 文本</summary>
        private static WebUiResponse ApiFile(Dictionary<string, string> query)
        {
            query.TryGetValue("name", out string name);
            if (string.IsNullOrWhiteSpace(name)) return Json.Error("缺少 name");
            var dir = CtKvIo.GetCtkvDir(CoreEntry.ModEntry);
            string path = Path.Combine(dir, Path.GetFileName(name));
            if (!File.Exists(path)) return Json.Error("文件不存在", 404);

            string content;
            if (path.EndsWith(".ctov", StringComparison.OrdinalIgnoreCase))
                content = CtKvIo.ReadEntryText(path, "Overlayer.Sonnet.xml");
            else
                content = CtKvIo.ReadEntryText(path, "KeyViewer.Sonnet.xml")
                       ?? CtKvIo.ReadEntryText(path, "KeyViewer.xml")
                       ?? CtKvIo.ReadEntryText(path, "Settings.xml");

            return Json.Raw("{\"ok\":true,\"name\":\"" + Json.Escape(Path.GetFileName(name))
                + "\",\"content\":\"" + Json.Escape(Truncate(content, 200000)) + "\"}");
        }

        /// <summary>下载原始包文件</summary>
        private static WebUiResponse ApiDownload(Dictionary<string, string> query)
        {
            query.TryGetValue("name", out string name);
            if (string.IsNullOrWhiteSpace(name)) return Json.Error("缺少 name");
            var dir = CtKvIo.GetCtkvDir(CoreEntry.ModEntry);
            string path = Path.Combine(dir, Path.GetFileName(name));
            if (!File.Exists(path)) return Json.Error("文件不存在", 404);
            return new WebUiResponse
            {
                ContentType = "application/octet-stream",
                Body = File.ReadAllBytes(path)
            };
        }

        /// <summary>就地修正键雨：{name}</summary>
        private static WebUiResponse ApiFixRain(string body)
        {
            string name = Json.GetString(body, "name");
            if (string.IsNullOrWhiteSpace(name)) return Json.Error("缺少 name");
            var dir = CtKvIo.GetCtkvDir(CoreEntry.ModEntry);
            string path = Path.Combine(dir, Path.GetFileName(name));
            if (!File.Exists(path)) return Json.Error("文件不存在", 404);
            if (CtKvIo.ApplyRainFixToFile(path, out string err))
                return Json.Raw("{\"ok\":true,\"message\":\"键雨已修正\"}");
            return Json.Error("修正失败: " + err);
        }

        /// <summary>删除包文件：{name}</summary>
        private static WebUiResponse ApiDelete(string body)
        {
            string name = Json.GetString(body, "name");
            if (string.IsNullOrWhiteSpace(name)) return Json.Error("缺少 name");
            var dir = CtKvIo.GetCtkvDir(CoreEntry.ModEntry);
            string path = Path.Combine(dir, Path.GetFileName(name));
            if (!File.Exists(path)) return Json.Error("文件不存在", 404);
            try
            {
                File.Delete(path);
                return Json.Raw("{\"ok\":true}");
            }
            catch (Exception ex) { return Json.Error("删除失败: " + ex.Message); }
        }

        private static WebUiResponse ApiOpenFolder()
        {
            try
            {
                var dir = CtKvIo.GetCtkvDir(CoreEntry.ModEntry);
                Application.OpenURL("file:///" + dir.Replace('\\', '/'));
                return Json.Raw("{\"ok\":true}");
            }
            catch (Exception ex) { return Json.Error(ex.Message); }
        }

        // ====================================================================
        //  提示词 / 预设（支持热重载，改完即生效，无需重启游戏）
        // ====================================================================

        private static WebUiResponse ApiPrompts()
        {
            var sb = new StringBuilder("{\"ok\":true,\"dir\":\"" + Json.Escape(PromptStore.Dir) + "\",\"keys\":[");
            var keys = PromptStore.Keys;
            for (int i = 0; i < keys.Count; i++)
            {
                string key = keys[i];
                if (i > 0) sb.Append(',');
                sb.Append("{\"key\":\"").Append(Json.Escape(key)).Append('"');
                sb.Append(",\"display\":\"").Append(Json.Escape(PromptStore.DisplayName(key))).Append('"');
                sb.Append(",\"overridden\":").Append(PromptStore.IsOverridden(key) ? "true" : "false");
                sb.Append(",\"length\":").Append(PromptStore.Get(key)?.Length ?? 0);
                sb.Append('}');
            }
            sb.Append("],\"presetPath\":\"").Append(Json.Escape(PresetStore.FilePath)).Append("\"}");
            return Json.Raw(sb.ToString());
        }

        private static WebUiResponse ApiPromptGet(Dictionary<string, string> query)
        {
            query.TryGetValue("key", out string key);
            if (string.IsNullOrWhiteSpace(key)) return Json.Error("缺少 key");
            return Json.Raw("{\"ok\":true,\"key\":\"" + Json.Escape(key)
                + "\",\"display\":\"" + Json.Escape(PromptStore.DisplayName(key))
                + "\",\"overridden\":" + (PromptStore.IsOverridden(key) ? "true" : "false")
                + ",\"text\":\"" + Json.Escape(PromptStore.Get(key)) + "\"}");
        }

        /// <summary>保存或恢复默认：{key, text} 或 {key, reset:true}</summary>
        private static WebUiResponse ApiPromptPost(string body)
        {
            string key = Json.GetString(body, "key");
            if (string.IsNullOrWhiteSpace(key)) return Json.Error("缺少 key");

            if (Json.GetBool(body, "reset", false))
            {
                PromptStore.ResetToDefault(key);
                return Json.Raw("{\"ok\":true,\"text\":\"" + Json.Escape(PromptStore.Get(key)) + "\"}");
            }

            string text = Json.GetString(body, "text");
            if (string.IsNullOrWhiteSpace(text)) return Json.Error("提示词内容不能为空");
            PromptStore.Save(key, text);
            return Json.Raw("{\"ok\":true}");
        }

        private static WebUiResponse ApiPromptsReload()
        {
            PromptStore.ReloadAll();
            PresetStore.Reload();
            return Json.Raw("{\"ok\":true}");
        }

        private static WebUiResponse ApiPresetsGet()
        {
            return Json.Raw("{\"ok\":true,\"path\":\"" + Json.Escape(PresetStore.FilePath)
                + "\",\"text\":\"" + Json.Escape(PresetStore.Serialize(PresetStore.All)) + "\"}");
        }

        /// <summary>保存预设：{text} 或 {reset:true}</summary>
        private static WebUiResponse ApiPresetsPost(string body)
        {
            if (Json.GetBool(body, "reset", false))
            {
                PresetStore.ResetToDefault();
                return ApiPresetsGet();
            }

            string text = Json.GetString(body, "text");
            if (string.IsNullOrWhiteSpace(text)) return Json.Error("预设内容不能为空");

            // 先解析校验，避免写入坏文件导致预设列表变空
            var parsed = PresetStore.DeserializeForValidation(text);
            if (parsed == null || parsed.Count == 0)
                return Json.Error("JSON 解析失败或没有任何供应商条目，请检查格式");

            PresetStore.Save(parsed);
            return ApiPresetsGet();
        }

        // ====================================================================
        //  更新与热重载
        // ====================================================================

        private static WebUiResponse ApiUpdate()
        {
            var sb = new StringBuilder("{\"ok\":true");
            sb.Append(",\"currentVersion\":\"").Append(Json.Escape(ModUpdater.CurrentVersion)).Append('"');
            sb.Append(",\"latestVersion\":\"").Append(Json.Escape(ModUpdater.LatestVersion)).Append('"');
            sb.Append(",\"state\":\"").Append(ModUpdater.State).Append('"');
            sb.Append(",\"busy\":").Append(ModUpdater.IsBusy ? "true" : "false");
            sb.Append(",\"error\":\"").Append(Json.Escape(ModUpdater.Error)).Append('"');
            sb.Append(",\"notes\":\"").Append(Json.Escape(ModUpdater.ReleaseNotes)).Append('"');
            sb.Append(",\"bootstrapChanged\":").Append(ModUpdater.BootstrapChanged ? "true" : "false");
            sb.Append(",\"reloadCount\":").Append(BootstrapMain.ReloadCount);
            sb.Append(",\"coreFile\":\"").Append(Json.Escape(BootstrapMain.CoreAssemblyName)).Append('"');
            sb.Append('}');
            return Json.Raw(sb.ToString());
        }

        private static WebUiResponse ApiUpdateCheck()
        {
            if (ModUpdater.IsBusy) return Json.Error("已有更新任务在进行");
            ModUpdater.Check();
            return Json.Raw("{\"ok\":true}");
        }

        private static WebUiResponse ApiUpdateDownload()
        {
            if (ModUpdater.IsBusy) return Json.Error("已有更新任务在进行");
            if (!ModUpdater.Download()) return Json.Error(string.IsNullOrEmpty(ModUpdater.Error) ? "无法开始下载" : ModUpdater.Error);
            return Json.Raw("{\"ok\":true}");
        }

        private static WebUiResponse ApiUpdateApply()
        {
            if (!ModUpdater.ApplyStaged(out string message))
                return Json.Error(message);
            return Json.Raw("{\"ok\":true,\"message\":\"" + Json.Escape(message) + "\"}");
        }

        /// <summary>仅重载核心（本地开发：重新编译后点一下即生效）</summary>
        private static WebUiResponse ApiReloadCore()
        {
            ModUpdater.RequestCoreReload();
            return Json.Raw("{\"ok\":true,\"message\":\"已请求热重载核心，约 0.5 秒后生效（WebUI 会换新令牌，请用 UMM 面板里的新地址）\"}");
        }

        // ====================================================================
        //  小工具
        // ====================================================================

        private static string MaskKey(string key)
        {
            if (string.IsNullOrEmpty(key)) return "";
            if (key.Length <= 8) return new string('*', key.Length);
            return key.Substring(0, 4) + "****" + key.Substring(key.Length - 4);
        }

        private static string Truncate(string s, int max)
            => string.IsNullOrEmpty(s) ? "" : (s.Length <= max ? s : s.Substring(0, max) + "\n…（已截断）");
    }

    /// <summary>极简 JSON 构造/解析工具（避免 JsonUtility 对嵌套结构的限制）</summary>
    internal static class Json
    {
        public static string Escape(string s)
        {
            if (string.IsNullOrEmpty(s)) return "";
            var sb = new StringBuilder(s.Length + 16);
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
                        if (c < ' ') sb.Append("\\u").Append(((int)c).ToString("x4"));
                        else sb.Append(c);
                        break;
                }
            }
            return sb.ToString();
        }

        public static WebUiResponse Raw(string json) => new WebUiResponse(json);

        public static WebUiResponse Error(string message, int status = 400)
            => new WebUiResponse("{\"ok\":false,\"error\":\"" + Escape(message) + "\"}", status);

        /// <summary>从 JSON 文本中读取字符串字段（支持 \" 转义），找不到返回空串</summary>
        public static string GetString(string json, string field)
        {
            if (string.IsNullOrEmpty(json)) return "";
            string key = "\"" + field + "\"";
            int idx = json.IndexOf(key, StringComparison.Ordinal);
            if (idx < 0) return "";
            int colon = json.IndexOf(':', idx + key.Length);
            if (colon < 0) return "";
            int i = colon + 1;
            while (i < json.Length && char.IsWhiteSpace(json[i])) i++;
            if (i >= json.Length || json[i] != '"') return "";
            i++;
            var sb = new StringBuilder();
            while (i < json.Length)
            {
                char c = json[i];
                if (c == '\\' && i + 1 < json.Length)
                {
                    char n = json[i + 1];
                    switch (n)
                    {
                        case 'n': sb.Append('\n'); break;
                        case 'r': sb.Append('\r'); break;
                        case 't': sb.Append('\t'); break;
                        case '"': sb.Append('"'); break;
                        case '\\': sb.Append('\\'); break;
                        case '/': sb.Append('/'); break;
                        case 'u':
                            if (i + 5 < json.Length && int.TryParse(json.Substring(i + 2, 4),
                                    System.Globalization.NumberStyles.HexNumber, null, out int code))
                            {
                                sb.Append((char)code);
                                i += 6;
                                continue;
                            }
                            break;
                        default: sb.Append(n); break;
                    }
                    i += 2;
                    continue;
                }
                if (c == '"') break;
                sb.Append(c);
                i++;
            }
            return sb.ToString();
        }

        public static int GetInt(string json, string field, int fallback)
        {
            string raw = GetRawNumber(json, field);
            return int.TryParse(raw, out int v) ? v : fallback;
        }

        public static bool GetBool(string json, string field, bool fallback)
        {
            string raw = GetRawNumber(json, field);
            if (raw == "true") return true;
            if (raw == "false") return false;
            return fallback;
        }

        private static string GetRawNumber(string json, string field)
        {
            if (string.IsNullOrEmpty(json)) return "";
            string key = "\"" + field + "\"";
            int idx = json.IndexOf(key, StringComparison.Ordinal);
            if (idx < 0) return "";
            int colon = json.IndexOf(':', idx + key.Length);
            if (colon < 0) return "";
            int i = colon + 1;
            while (i < json.Length && char.IsWhiteSpace(json[i])) i++;
            int start = i;
            while (i < json.Length && (char.IsLetterOrDigit(json[i]) || json[i] == '-' || json[i] == '.' || json[i] == '+')) i++;
            return json.Substring(start, i - start);
        }
    }

    /// <summary>WebUI 响应（JSON 文本或二进制附件）</summary>
    internal sealed class WebUiResponse
    {
        public int Status = 200;
        public string ContentType = "application/json; charset=utf-8";
        public byte[] Body = new byte[0];

        public WebUiResponse() { }

        public WebUiResponse(string json, int status = 200)
        {
            Status = status;
            Body = Encoding.UTF8.GetBytes(json ?? "");
        }
    }
}
