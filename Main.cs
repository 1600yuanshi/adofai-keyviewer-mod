using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;
using UnityEngine;
using UnityModManagerNet;
using ADOFAI.AgentKeyViewer.Bootstrap;

namespace ADOFAI.AgentKeyViewer
{
    /// <summary>
    /// 核心实现（原 Mod 主入口 Main）。
    ///
    /// 由引导器 <see cref="BootstrapMain"/> 实例化并负责热替换：内部状态一律保持静态，
    /// 这样热替换出的新实例会直接接管同一份状态（配置、密钥、WebUI 端口等）。
    /// 每帧驱动与 UMM 回调都由引导器转发进来。
    /// </summary>
    public sealed class CoreEntry : IAgentKeyViewerCore
    {
        public int ApiVersion => BootstrapMain.RequiredApiVersion;

        /// <summary>版本号（实例成员，供引导器/外部面板读取）</summary>
        public string Version => CoreVersionText;

        /// <summary>
        /// 版本号：优先读 Info.json（更新器以此为准），读取失败回退内置常量。
        /// 静态实现，便于更新器在任意位置读取。
        /// </summary>
        public static string CoreVersionText
        {
            get
            {
                if (!string.IsNullOrEmpty(_version)) return _version;
                try
                {
                    string dir = ModEntry != null ? Path.GetDirectoryName(ModEntry.Path) : null;
                    string info = Path.Combine(dir ?? ".", "Info.json");
                    if (File.Exists(info))
                    {
                        string text = File.ReadAllText(info, Encoding.UTF8);
                        int i = text.IndexOf("\"Version\"", StringComparison.Ordinal);
                        if (i >= 0)
                        {
                            int q1 = text.IndexOf('"', text.IndexOf(':', i) + 1);
                            int q2 = text.IndexOf('"', q1 + 1);
                            if (q1 > 0 && q2 > q1) _version = text.Substring(q1 + 1, q2 - q1 - 1);
                        }
                    }
                }
                catch { }
                return string.IsNullOrEmpty(_version) ? FallbackVersion : _version;
            }
        }

        private const string FallbackVersion = "2.0.0";
        private static string _version = "";

        public static UnityModManager.ModEntry ModEntry { get; private set; }
        public static Settings Settings { get; private set; }
        public static bool Enabled { get; private set; }
        /// <summary>纯 .ctkv 生成器模式：关闭本模组自己的游戏内按键渲染（只保留 agent 生成 .ctkv）</summary>
        public static bool RenderInGame = false;

        // 核心组件
        public static KeyInputCapture InputCapture { get; private set; }
        public static KeyDisplayRenderer DisplayRenderer { get; private set; }
        public static AgentConfigManager AgentConfig { get; private set; }

        // ====== FreeMake编辑器状态 ======
        private static int _freemakeSelectedIdx = -1;
        private static Vector2 _freemakeListScroll;
        private static string _freemakeSearch = "";
        // 用于让Color字段每次编辑都能刷新
        private static bool _colorsDirty;
        private static readonly GUIContent[] _noKeyCodeContents = new GUIContent[0];
        private static GUIContent[] _allKeyCodeContentsCached;
        private static int[] _allKeyCodeValuesCached;

        // ====== 按键录制状态（用户建议：打开开关后自己按键由系统识别）======
        private static bool _isRecordingKey;
        private static KeyDefinition _recordingTargetKey;
        private static readonly KeyCode[] _quickKeys = new KeyCode[]
        {
            // 符号键（FreeMake 之前无法添加的）
            KeyCode.Semicolon, KeyCode.Comma, KeyCode.Period, KeyCode.Slash, KeyCode.Backslash,
            KeyCode.LeftBracket, KeyCode.RightBracket, KeyCode.Minus, KeyCode.Equals,
            KeyCode.BackQuote, KeyCode.Quote,
            // 游戏常用键
            KeyCode.D, KeyCode.F, KeyCode.J, KeyCode.K, KeyCode.L, KeyCode.Space,
            KeyCode.LeftArrow, KeyCode.RightArrow, KeyCode.UpArrow, KeyCode.DownArrow,
            KeyCode.Mouse0, KeyCode.Mouse1,
        };

        // ====== AI 生成状态 ======
        public static AIConfigGenerator AIGen;

        // API 密钥
        private static ApiKeyList _apiKeyList = new ApiKeyList();
        private static int _selectedKeyIdx = -1;

        // ====== WebUI 对外访问入口（供 WebUiServer 在主线程读写）======
        /// <summary>当前密钥列表</summary>
        public static ApiKeyList ApiKeys => _apiKeyList;
        /// <summary>当前选中的密钥下标</summary>
        public static int SelectedKeyIndex
        {
            get => _selectedKeyIdx;
            set => _selectedKeyIdx = value;
        }

        public bool Initialize(UnityModManager.ModEntry modEntry)
        {
            try
            {
                ModEntry = modEntry;
                _version = "";
                // 核心被加载即代表模组处于启用状态（UMM 不会加载已停用的模组）。
                // 这样即便 UMM 在 Load 之前/之后未回调 OnToggle，也不会误判为「已停用」。
                Enabled = true;
                // 注意：UMM 回调由引导器注册并转发，核心不直接注册，热替换时才不会丢回调

                // 加载配置
                Settings = UnityModManager.ModSettings.Load<Settings>(modEntry);

                // 初始化Agent配置管理器
                AgentConfig = new AgentConfigManager(modEntry);
                AgentConfig.Load();

                // 初始化核心组件
                InputCapture = new KeyInputCapture();
                DisplayRenderer = new KeyDisplayRenderer();

                // 初始化 KV 配置管理 + AI 生成
                AIGen = new AIConfigGenerator();
                _apiKeyList = ApiKeyStore.Load(ApiKeyStore.GetPath(modEntry));
                _selectedKeyIdx = _apiKeyList.keys.FindIndex(k => k.isDefault);
                if (_selectedKeyIdx < 0 && _apiKeyList.keys.Count > 0) _selectedKeyIdx = 0;

                // 提示词与供应商预设外置到磁盘，改文件即生效（无需重启游戏）
                PromptStore.EnsureSeeded();
                PresetStore.EnsureSeeded();

                // 确保LayoutType合法（旧的没有5选项）
                if (Settings.LayoutType < 0 || Settings.LayoutType > 5) Settings.LayoutType = 0;

                // 每帧驱动器由引导器持有，核心不需要创建 GameObject

                // 启动本地 WebUI（作为主要操作界面，取代原先的 UMM IMGUI 面板）
                StartWebUi();
                modEntry.Logger.Log($"[AgentKeyViewer] 核心初始化完成，版本 {Version}");
                return true;
            }
            catch (Exception ex)
            {
                modEntry.Logger.Error($"Failed to load mod: {ex.Message}");
                modEntry.Logger.Error(ex.StackTrace);
                return false;
            }
        }

        // ====================================================================
        //  IAgentKeyViewerCore 实现（由引导器调用）
        // ====================================================================

        /// <summary>热替换前的收尾：停止 WebUI 服务并保存设置</summary>
        public void Shutdown()
        {
            try
            {
                WebUiServer.Stop();
                OnSaveGUI(ModEntry);
                ModEntry?.Logger.Log("[AgentKeyViewer] 核心已收尾（准备热替换）");
            }
            catch (Exception ex)
            {
                ModEntry?.Logger.Error($"[AgentKeyViewer] Shutdown 异常: {ex.Message}");
            }
        }

        public bool Toggle(bool enabled) => OnToggle(ModEntry, enabled);

        public void DrawGui(UnityModManager.ModEntry modEntry) => OnGUI(modEntry);

        public void SaveGui(UnityModManager.ModEntry modEntry) => OnSaveGUI(modEntry);

        /// <summary>每帧驱动（原 GameLoop.Update）</summary>
        public void Update()
        {
            // WebUI 请求队列始终排空：即使模组被停用也要给出明确响应，避免前端一直等待
            WebUiServer.PumpMainThread();

            if (!Enabled) return;

            // 纯 .ctkv 生成器模式：停用本模组的游戏内按键渲染（仅保留 agent 生成）
            if (RenderInGame)
            {
                // 按键录制模式：识别用户按下的键并绑定
                HandleKeyRecording();

                // 传入DisplayRenderer的布局给InputCapture
                var layout = DisplayRenderer?.CurrentLayout;
                InputCapture?.Update(layout);

                // 更新显示渲染器
                DisplayRenderer?.Update();
            }

            // 驱动 AI 配置生成（后台任务完成时解析结果）
            AIGen?.Tick();

            // 处理更新器的延迟热重载请求
            ModUpdater.Tick();
        }

        /// <summary>游戏内 GUI 绘制（原 GameLoop.OnGUI）</summary>
        public void DrawInGameGui()
        {
            if (!Enabled) return;
            if (RenderInGame) DisplayRenderer?.OnGUI();
        }

        // ---- 供外部面板（如 CherryTools 模块）读取的最小状态 ----

        public bool WebUiRunning => WebUiServer.IsRunning;

        public string StatusSummary
        {
            get
            {
                if (WebUiServer.IsRunning)
                    return $"WebUI 运行中：127.0.0.1:{WebUiServer.Port}  令牌 {WebUiServer.Token}";

                string reason = string.IsNullOrEmpty(WebUiServer.LastError)
                    ? "" : $"（原因：{WebUiServer.LastError}）";
                return "WebUI 未运行" + reason;
            }
        }

        public void OpenWebUi() => OpenWebUiWindow();

        public static bool OnToggle(UnityModManager.ModEntry modEntry, bool value)
        {
            try
            {
                Enabled = value;
                // 纯 .ctkv 生成器模式：不启动本模组的游戏内按键渲染。
                // 模组只负责通过 agent 生成 CT 的 .ctkv 配置包，由 CherryTools(CT) 在游戏内读取。
                modEntry.Logger.Log(value
                    ? "[AgentKeyViewer] Mod enabled（纯 .ctkv 生成器模式，游戏内渲染已停用）"
                    : "[AgentKeyViewer] Mod disabled");
                return true;
            }
            catch (Exception ex)
            {
                modEntry.Logger.Error($"Failed to toggle mod: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// 按键录制模式：每帧调用，检测用户按下的键并绑定到目标键（需在Update中调用）
        /// </summary>
        public static void HandleKeyRecording()
        {
            if (!_isRecordingKey) return;

            if (KeyScan.TryGetPressedKey(out var pressed))
            {
                if (pressed == KeyCode.Escape)
                {
                    _isRecordingKey = false;
                    _recordingTargetKey = null;
                    ModEntry?.Logger.Log("[AgentKeyViewer] 按键录制已取消");
                    return;
                }

                if (_recordingTargetKey != null)
                {
                    _recordingTargetKey.KeyCode = pressed;
                    if (string.IsNullOrWhiteSpace(_recordingTargetKey.Label))
                        _recordingTargetKey.Label = KeyScan.ToFriendlyName(pressed);
                    DisplayRenderer?.SyncCustomKeysToLayout();
                    ModEntry?.Logger.Log($"[AgentKeyViewer] 按键已绑定: {KeyScan.ToFriendlyName(pressed)}");
                }
                _isRecordingKey = false;
                _recordingTargetKey = null;
            }
        }

        public static void OnGUI(UnityModManager.ModEntry modEntry)
        {
            try
            {
                GUILayout.Label("<b><size=16>Agent Key Viewer · CherryTools 配置生成器</size></b>", GUILayout.ExpandWidth(true));
                GUILayout.Space(4);
                GUILayout.Label("<i>主要操作界面已迁移到浏览器（本地 WebUI）：密钥管理、生成 .ctkv / .ctov、文件管理都在那里。</i>");
                GUILayout.Space(6);

                GUILayout.BeginVertical(GUI.skin.box);
                if (WebUiServer.IsRunning)
                {
                    GUILayout.Label($"● WebUI 运行中：<b>127.0.0.1:{WebUiServer.Port}</b>");
                    GUILayout.Label($"<i>访问令牌：{WebUiServer.Token}</i>");
                    GUILayout.Space(4);
                    if (GUILayout.Button("🌐 在浏览器中打开 WebUI", GUILayout.Height(26)))
                        OpenWebUiWindow();
                    GUILayout.Label("<i>若浏览器未自动带令牌，请把上面的令牌填到页面提示处。</i>");
                }
                else
                {
                    GUILayout.Label("<color=#ffb454>● WebUI 未运行</color>");
                    if (!string.IsNullOrEmpty(WebUiServer.LastError))
                        GUILayout.Label($"<i>原因：{WebUiServer.LastError}</i>");
                    if (GUILayout.Button("启动 WebUI", GUILayout.Height(24)))
                        StartWebUi();
                }
                GUILayout.EndVertical();
                GUILayout.Space(6);

                // 关键状态速览
                var gen = AIGen;
                string stateText = gen == null ? "未初始化"
                    : gen.IsBusy ? "生成中…"
                    : gen.State == AIConfigGenerator.GenState.Success ? "上次生成成功"
                    : gen.State == AIConfigGenerator.GenState.Failed ? "上次生成失败" : "空闲";
                GUILayout.Label($"生成状态：<b>{stateText}</b>");
                if (gen != null && gen.State == AIConfigGenerator.GenState.Failed && !string.IsNullOrEmpty(gen.Error))
                    GUILayout.Label($"<color=#ff6b6b>{gen.Error}</color>");
                if (gen != null && !string.IsNullOrEmpty(gen.SavedCtkvPath))
                    GUILayout.Label($"<i>最近保存：{gen.SavedCtkvPath}</i>");
                if (gen != null && !string.IsNullOrEmpty(gen.SavedOvPath))
                    GUILayout.Label($"<i>最近保存：{gen.SavedOvPath}</i>");

                GUILayout.Space(6);
                GUILayout.Label("<i>提示：旧版 IMGUI 面板已停用；如需生成配置请在 WebUI 中操作。</i>");
            }
            catch (Exception ex)
            {
                modEntry.Logger.Error($"OnGUI exception: {ex.Message}\n{ex.StackTrace}");
            }
        }

        /// <summary>启动本地 WebUI 服务（已运行则忽略）</summary>
        public static void StartWebUi()
        {
            if (Settings == null || !Settings.WebUiEnabled) return;
            if (WebUiServer.IsRunning) return;
            if (WebUiServer.Start(Settings.WebUiPort, out string err))
            {
                ModEntry?.Logger.Log($"[AgentKeyViewer] WebUI: {WebUiServer.Url}");
                if (Settings.AutoOpenWebUi) OpenWebUiWindow();
            }
            else
            {
                ModEntry?.Logger.Error($"[AgentKeyViewer] WebUI 启动失败: {err}");
            }
        }

        /// <summary>在系统默认浏览器中打开 WebUI</summary>
        public static void OpenWebUiWindow()
        {
            try
            {
                if (!WebUiServer.IsRunning) StartWebUi();
                if (!WebUiServer.IsRunning) return;
                Application.OpenURL(WebUiServer.Url);
            }
            catch (Exception ex)
            {
                ModEntry?.Logger.Error($"[AgentKeyViewer] 打开 WebUI 失败: {ex.Message}");
            }
        }

        public static void SaveApiKeys() => ApiKeyStore.Save(ApiKeyStore.GetPath(ModEntry), _apiKeyList);


        public static void OnSaveGUI(UnityModManager.ModEntry modEntry)
        {
            try
            {
                // 保存前把FreeMake当前CustomKeys同步回布局（防止用户忘记点应用）
                if (Settings.LayoutType == 5)
                    DisplayRenderer?.SyncCustomKeysToLayout();
                Settings.Save(modEntry);
                AgentConfig?.Save();
            }
            catch (Exception ex)
            {
                modEntry.Logger.Error($"Failed to save settings: {ex.Message}");
            }
        }
    }
}
