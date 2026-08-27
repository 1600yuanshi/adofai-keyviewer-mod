using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;
using UnityEngine;
using UnityModManagerNet;

namespace ADOFAI.AgentKeyViewer
{
    /// <summary>
    /// Mod主入口类 - CT风格 KV + FreeMake编辑器 + Agent配置管理
    /// </summary>
    public static class Main
    {
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

        // ====== KV 配置管理 + AI 生成状态 ======
        public static AIConfigGenerator AIGen;
        private static KVConfigList _kvConfigs = new KVConfigList();
        private static string _kvPresetName = "";
        private static Vector2 _kvScroll;
        private static string _kvStatusMsg = "";

        // ====== CherryTools(.ctkv) 导入/导出状态 ======
        private static string[] _ctkvFilesCached = new string[0];
        private static Vector2 _ctkvScroll;
        private static string _ctkvStatusMsg = "";
        private static string _ctkvSaveName = "";

        // API 密钥
        private static ApiKeyList _apiKeyList = new ApiKeyList();
        private static int _selectedKeyIdx = -1;
        private static bool _showKeyManager;
        private static bool _showRawAi;
        private static string _newKeyName = "";
        private static string _newKeyBase = "";
        private static string _newKeyModel = "";
        private static string _newKeyThinkingJson = "";
        private static int _newKeyMaxTokens = 8192;
        private static string[] _currentProviderModels = null;
        private static int _newKeyModelIdx = 0;
        private static GUIContent[] _modelPopupContents = null;
        private static string _newKeySecret = "";
        private static int _providerIdx = -1;
        private static int _lastAppliedProviderIdx = -1;
        private static GUIContent[] _providerPopupContents;
        private static string _agentConfigPath = "";
        private static string _aiPrompt = "";
        private static GUIContent[] _apiKeyPopupContents;

        public static bool Load(UnityModManager.ModEntry modEntry)
        {
            try
            {
                ModEntry = modEntry;
                modEntry.OnToggle = OnToggle;
                modEntry.OnGUI = OnGUI;
                modEntry.OnSaveGUI = OnSaveGUI;

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
                _kvConfigs = KVConfigStore.Load(KVConfigStore.GetPath(modEntry));
                _apiKeyList = ApiKeyStore.Load(ApiKeyStore.GetPath(modEntry));
                _selectedKeyIdx = _apiKeyList.keys.FindIndex(k => k.isDefault);
                if (_selectedKeyIdx < 0 && _apiKeyList.keys.Count > 0) _selectedKeyIdx = 0;
                _agentConfigPath = _apiKeyList.agentConfigPath;

                // 确保LayoutType合法（旧的没有5选项）
                if (Settings.LayoutType < 0 || Settings.LayoutType > 5) Settings.LayoutType = 0;

                // 创建GameLoop
                var gameLoop = GameLoop.Instance;
                modEntry.Logger.Log($"[AgentKeyViewer] Load OK - GameLoop exists: {gameLoop != null}");
                return true;
            }
            catch (Exception ex)
            {
                modEntry.Logger.Error($"Failed to load mod: {ex.Message}");
                modEntry.Logger.Error(ex.StackTrace);
                return false;
            }
        }

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
                GUILayout.Label("<b><size=16>Agent Key Viewer · CherryTools 键位生成器</size></b>", GUILayout.ExpandWidth(true));
                GUILayout.Space(4);
                GUILayout.Label("<i>用 AI 把一句话描述的键位效果，生成为 CherryTools 可导入的 .ctkv 配置包。</i>");
                GUILayout.Space(4);

                if (GUILayout.Button(_showHelp ? "▲ 收起使用说明" : "▶ 使用说明（新手必看）", GUILayout.Height(20)))
                    _showHelp = !_showHelp;
                if (_showHelp)
                {
                    GUILayout.BeginVertical(GUI.skin.box);
                    GUILayout.Label("① <b>选择密钥</b>：在下方下拉框选择 API 密钥；没有就点开「密钥管理」添加（选好供应商只需填 API Key）。");
                    GUILayout.Label("② <b>描述需求</b>：例如「16K 全键盘，紫到天蓝渐变边框，按下白色高亮，开启键雨」。");
                    GUILayout.Label("③ <b>生成并保存</b>：点击「✨ 生成 .ctkv」，成功后起个文件名保存。");
                    GUILayout.Label("④ <b>CT 导入</b>：打开 CherryTools 的 KeyViewer 设置 → 导入配置包，选中保存的 .ctkv 即可显示。");
                    GUILayout.Label("⑤ <b>键雨说明</b>：第一排键雨自动逐键跟随各键边框色，第二排固定白色且更细；上下两排起始高度自动对齐。");
                    GUILayout.EndVertical();
                    GUILayout.Space(4);
                }

                // 选项卡用简单的按钮切换
                int oldTab = _currentTab;
                string[] tabs = { "Agent 生成 .ctkv", ".ctkv 文件管理" };
                _currentTab = GUILayout.Toolbar(_currentTab, tabs);
                if (_currentTab != oldTab) { GUI.FocusControl(null); }
                GUILayout.Space(8);

                switch (_currentTab)
                {
                    case 0: DrawAgentGenerator(); break;
                    case 1: DrawCtkvFiles(); break;
                }
            }
            catch (Exception ex)
            {
                modEntry.Logger.Error($"OnGUI exception: {ex.Message}\n{ex.StackTrace}");
            }
        }

        private static int _currentTab;
        private static bool _showHelp = true;


        // 简易Popup（Unity IMGUI：SelectionGrid在ScrollView内当普通控件用更稳定，这里用简单按钮实现）
        private static bool _popupOpen;
        private static Vector2 _popupScroll;
        private static string _popupFilter = "";
        private static int _popupResultIdx = -1;

        private static int EditorGUILayout_PopupSafe(int currentIdx, GUIContent[] options, int width)
        {
            string currentName = (currentIdx >= 0 && currentIdx < options.Length) ? options[currentIdx].text : "(none)";
            GUILayout.BeginVertical();
            if (GUILayout.Button(currentName + "  ▼", GUILayout.Width(width), GUILayout.Height(20)))
            {
                _popupOpen = !_popupOpen;
                _popupFilter = "";
                _popupScroll = Vector2.zero;
                _popupResultIdx = -1;
            }
            if (_popupOpen)
            {
                _popupFilter = GUILayout.TextField("搜索: " + _popupFilter, GUILayout.Width(width)).Substring("搜索: ".Length);
                _popupScroll = GUILayout.BeginScrollView(_popupScroll, false, true,
                    GUILayout.Width(width), GUILayout.Height(220));
                for (int i = 0; i < options.Length; i++)
                {
                    var label = options[i].text;
                    if (!string.IsNullOrEmpty(_popupFilter))
                    {
                        if (label.IndexOf(_popupFilter, StringComparison.OrdinalIgnoreCase) < 0) continue;
                    }
                    if (GUILayout.Toggle(i == currentIdx, label, GUI.skin.button))
                    {
                        if (i != currentIdx) { _popupResultIdx = i; }
                    }
                }
                GUILayout.EndScrollView();
                if (GUILayout.Button("关闭选择", GUILayout.Width(width)))
                {
                    _popupOpen = false;
                }
            }
            GUILayout.EndVertical();
            if (_popupResultIdx >= 0)
            {
                int result = _popupResultIdx;
                _popupResultIdx = -1;
                _popupOpen = false;
                return result;
            }
            return currentIdx;
        }

        // ====================================================================
        //  Tab 0: Agent 生成 .ctkv（纯生成器）
        // ====================================================================
        private static void DrawAgentGenerator()
        {
            if (AIGen == null) AIGen = new AIConfigGenerator();

            GUILayout.Label("<b>=== AI 生成 .ctkv ===</b>", GUILayout.ExpandWidth(true));
            GUILayout.Space(4);

            // ---- 密钥选择 ----
            GUILayout.BeginHorizontal();
            GUILayout.Label("① API 密钥:", GUILayout.Width(78));
            if (_apiKeyList.keys.Count == 0)
            {
                GUILayout.Label("<color=orange>(暂无密钥，请先添加)</color>", GUILayout.ExpandWidth(true));
            }
            else
            {
                if (_selectedKeyIdx < 0) _selectedKeyIdx = 0;
                if (_selectedKeyIdx >= _apiKeyList.keys.Count) _selectedKeyIdx = _apiKeyList.keys.Count - 1;
                RefreshApiKeyPopupContents();
                _selectedKeyIdx = EditorGUILayout_PopupSafe(_selectedKeyIdx, _apiKeyPopupContents, 340);
            }
            GUILayout.EndHorizontal();

            if (GUILayout.Button(_showKeyManager ? "▼ 密钥管理（点击收起）" : "▶ 密钥管理（点击展开）"))
                _showKeyManager = !_showKeyManager;
            if (_showKeyManager) DrawKeyManager();

            GUILayout.Space(6);
            GUILayout.Label("② 描述需求：", GUILayout.ExpandWidth(true));
            _aiPrompt = GUILayout.TextField(_aiPrompt, GUILayout.Height(24));
            GUILayout.Label("<i>示例：「16K 全键盘，红蓝渐变边框，按下白色高亮，开启键雨」</i>", GUILayout.ExpandWidth(true));

            bool dt = GUILayout.Toggle(Settings.DisableThinking, " 关闭模型思考（推荐）");
            if (dt != Settings.DisableThinking) Settings.DisableThinking = dt;

            GUILayout.BeginHorizontal();
            if (AIGen.IsBusy)
            {
                GUILayout.Label("<color=orange>⏳ 正在生成中（两轮：生成→自检），请稍候...</color>", GUILayout.ExpandWidth(true));
            }
            else
            {
                if (GUILayout.Button("✨ 生成 .ctkv", GUILayout.Height(28))) StartAiGeneration();
            }
            GUILayout.EndHorizontal();

            // 结果状态
            if (AIGen.State == AIConfigGenerator.GenState.Failed)
            {
                GUILayout.Label($"<color=red>生成失败：{AIGen.Error}</color>", GUILayout.ExpandWidth(true));
                if (GUILayout.Button("清除错误状态")) AIGen.Reset();
            }
            else if (AIGen.State == AIConfigGenerator.GenState.Success && AIGen.CtkvXml != null)
            {
                CtKvIo.ValidateGeneratedXml(AIGen.CtkvXml, out int keyCount, out string configName, out _);
                GUILayout.Label($"<color=lime>✓ 已生成 CT 配置「{configName}」（{keyCount} 键）</color>", GUILayout.ExpandWidth(true));

                // 保存为 .ctkv
                GUILayout.BeginHorizontal();
                GUILayout.Label("文件名:", GUILayout.Width(60));
                if (string.IsNullOrEmpty(_ctkvSaveName)) _ctkvSaveName = configName;
                _ctkvSaveName = GUILayout.TextField(_ctkvSaveName, GUILayout.Width(220));
                if (GUILayout.Button("💾 保存为 .ctkv", GUILayout.Height(26)))
                {
                    string path = AIGen.SaveAsCtkv(_ctkvSaveName, out var err);
                    if (path != null)
                    {
                        _ctkvStatusMsg = $"<color=lime>✓ 已保存：{path}</color>";
                        _ctkvFilesCached = CtKvIo.ListFiles(CtKvIo.GetCtkvDir(ModEntry));
                    }
                    else _ctkvStatusMsg = $"<color=red>保存失败：{err}</color>";
                }
                GUILayout.EndHorizontal();

                if (!string.IsNullOrEmpty(AIGen.SavedCtkvPath))
                    GUILayout.Label($"<color=#80C0FF>已保存位置：{AIGen.SavedCtkvPath}</color>", GUILayout.ExpandWidth(true));
                if (!string.IsNullOrEmpty(_ctkvStatusMsg))
                    GUILayout.Label(_ctkvStatusMsg, GUI.skin.box, GUILayout.Height(40));

                if (GUILayout.Button(_showRawAi ? "收起 AI 原始 XML" : "查看 AI 原始 XML", GUILayout.Height(22)))
                    _showRawAi = !_showRawAi;
                if (_showRawAi)
                    GUILayout.Label(AIGen.RawOutput ?? "", GUI.skin.box, GUILayout.Height(120));
            }

            GUILayout.Space(6);
            GUILayout.Label("<i>④ 保存后到 CherryTools → KeyViewer 设置 → 导入配置包即可显示。详见顶部「使用说明」。</i>", GUILayout.ExpandWidth(true));
        }

        // ====================================================================
        //  Tab 1: .ctkv 文件管理
        // ====================================================================
        private static void DrawCtkvFiles()
        {
            GUILayout.Label("<b>=== 已生成的 .ctkv 配置包 ===</b>", GUILayout.ExpandWidth(true));
            GUILayout.Label($"目录：<color=#FFD977>游戏目录/AgentKeyViewer_config/ctkv/</color>（由 CherryTools(CT) 读取）");
            GUILayout.Space(4);

            GUILayout.BeginHorizontal();
            if (GUILayout.Button("📂 打开目录", GUILayout.Height(24)))
            {
                string dir = CtKvIo.GetCtkvDir(ModEntry);
                try { System.Diagnostics.Process.Start(dir); } catch { }
                _ctkvFilesCached = CtKvIo.ListFiles(dir);
            }
            if (GUILayout.Button("🔄 刷新列表", GUILayout.Height(24)))
            {
                _ctkvFilesCached = CtKvIo.ListFiles(CtKvIo.GetCtkvDir(ModEntry));
            }
            GUILayout.EndHorizontal();

            if (!string.IsNullOrEmpty(_ctkvStatusMsg))
                GUILayout.Label(_ctkvStatusMsg, GUI.skin.box, GUILayout.Height(40));

            if (_ctkvFilesCached.Length == 0)
            {
                GUILayout.Label("<color=orange>（ctkv 目录为空。请先用「Agent 生成 .ctkv」生成配置包。）</color>");
            }
            else
            {
                GUILayout.Label($"共 {_ctkvFilesCached.Length} 个 .ctkv 包：");
                _ctkvScroll = GUILayout.BeginScrollView(_ctkvScroll, false, true, GUILayout.Height(200));
                for (int i = 0; i < _ctkvFilesCached.Length; i++)
                {
                    string path = _ctkvFilesCached[i];
                    GUILayout.BeginHorizontal();
                    GUILayout.Label("• " + Path.GetFileName(path), GUILayout.ExpandWidth(true));
                    if (GUILayout.Button("查看内容", GUILayout.Width(80)))
                    {
                        _ctkvStatusMsg = PreviewCtkvXml(path);
                    }
                    if (GUILayout.Button("修正键雨", GUILayout.Width(80)))
                    {
                        if (CtKvIo.ApplyRainFixToFile(path, out var fixErr))
                            _ctkvStatusMsg = $"<color=lime>✓ 键雨已修正（逐键跟随边框色）：{Path.GetFileName(path)}</color>";
                        else
                            _ctkvStatusMsg = $"<color=red>修正失败：{fixErr}</color>";
                    }
                    GUILayout.EndHorizontal();
                }
                GUILayout.EndScrollView();
            }

            GUILayout.Space(6);
            GUILayout.Label("<i>查看内容 = 预览 XML；修正键雨 = 一键修复旧配置的键雨颜色/对齐。本模组为 CherryTools 的附属生成器，游戏内显示由 CT 完成。</i>", GUILayout.ExpandWidth(true));
        }

        /// <summary>预览 .ctkv 包内的 KeyViewer.xml 内容（供快速确认）</summary>
        private static string PreviewCtkvXml(string path)
        {
            try
            {
                using (var ms = new MemoryStream(File.ReadAllBytes(path)))
                using (var zip = new ZipArchive(ms, ZipArchiveMode.Read))
                {
                    var entry = zip.GetEntry("KeyViewer.xml") ?? zip.GetEntry("Settings.xml");
                    if (entry == null) return "压缩包内未找到 KeyViewer.xml / Settings.xml";
                    using (var r = new StreamReader(entry.Open(), Encoding.UTF8))
                    {
                        string xml = r.ReadToEnd();
                        if (CtKvIo.ValidateGeneratedXml(xml, out int kc, out string cn, out _))
                            return $"✓ 「{cn}」（{kc} 键）\n" + (xml.Length > 1500 ? xml.Substring(0, 1500) + "..." : xml);
                        return "配置可解析但结构不完整：" + (xml.Length > 500 ? xml.Substring(0, 500) : xml);
                    }
                }
            }
            catch (Exception ex) { return "读取失败：" + ex.Message; }
        }

        private static void DrawKeyManager()
        {
            GUILayout.BeginVertical(GUI.skin.box);

            // 已有密钥列表
            for (int i = 0; i < _apiKeyList.keys.Count; i++)
            {
                var k = _apiKeyList.keys[i];
                GUILayout.BeginHorizontal();
                GUILayout.Label($"{(i == _selectedKeyIdx ? "▶" : "  ")} {k.name}  ({k.model})", GUILayout.Width(220));
                if (GUILayout.Button(k.isDefault ? "默认✓" : "设为默认", GUILayout.Width(70)))
                {
                    foreach (var x in _apiKeyList.keys) x.isDefault = false;
                    k.isDefault = true;
                    _selectedKeyIdx = i;
                    SaveApiKeys();
                }
                if (GUILayout.Button("✕", GUILayout.Width(28)))
                {
                    _apiKeyList.keys.RemoveAt(i);
                    if (_selectedKeyIdx >= _apiKeyList.keys.Count) _selectedKeyIdx = _apiKeyList.keys.Count - 1;
                    SaveApiKeys();
                }
                GUILayout.EndHorizontal();
            }

            GUILayout.Space(6);
            GUILayout.Label("新增密钥：");
            GUILayout.BeginHorizontal();
            GUILayout.Label("供应商:", GUILayout.Width(48));
            if (_providerPopupContents == null)
            {
                var provNames = new List<GUIContent>();
                foreach (var p in ProviderPresets.List) provNames.Add(new GUIContent(p.name + "  (" + (string.IsNullOrEmpty(p.baseUrl) ? "手动" : p.baseUrl) + ")"));
                _providerPopupContents = provNames.ToArray();
            }
            int newProvider = EditorGUILayout_PopupSafe(_providerIdx, _providerPopupContents, 330);
            if (newProvider != _providerIdx) { _providerIdx = newProvider; _lastAppliedProviderIdx = -1; }
            if (_providerIdx >= 0 && _providerIdx < ProviderPresets.List.Count && _providerIdx != _lastAppliedProviderIdx)
            {
                var p = ProviderPresets.List[_providerIdx];
                _newKeyBase = p.baseUrl;
                _newKeyThinkingJson = p.thinkingDisableJson ?? "";
                _newKeyMaxTokens = p.maxTokens;
                _currentProviderModels = p.models;
                _newKeyModelIdx = 0;
                _modelPopupContents = null;
                if (p.models != null && p.models.Length > 0)
                    _newKeyModel = p.models[0];
                else
                    _newKeyModel = p.model;
                _lastAppliedProviderIdx = _providerIdx;
            }
            GUILayout.EndHorizontal();
            GUILayout.BeginHorizontal();
            GUILayout.Label("名称(可选):", GUILayout.Width(72));
            _newKeyName = GUILayout.TextField(_newKeyName, GUILayout.Width(140));
            GUILayout.Label("Base URL:", GUILayout.Width(66));
            _newKeyBase = GUILayout.TextField(_newKeyBase, GUILayout.Width(240));
            GUILayout.EndHorizontal();
            GUILayout.BeginHorizontal();
            GUILayout.Label("模型:", GUILayout.Width(48));
            if (_currentProviderModels != null && _currentProviderModels.Length > 0)
            {
                if (_modelPopupContents == null)
                {
                    var ms = new List<GUIContent>();
                    foreach (var m in _currentProviderModels) ms.Add(new GUIContent(m));
                    _modelPopupContents = ms.ToArray();
                }
                if (_newKeyModelIdx >= _currentProviderModels.Length) _newKeyModelIdx = 0;
                _newKeyModelIdx = EditorGUILayout_PopupSafe(_newKeyModelIdx, _modelPopupContents, 220);
                if (_newKeyModelIdx >= 0 && _newKeyModelIdx < _currentProviderModels.Length)
                    _newKeyModel = _currentProviderModels[_newKeyModelIdx];
                GUILayout.Label("API Key:", GUILayout.Width(66));
                _newKeySecret = GUILayout.PasswordField(_newKeySecret, '*', GUILayout.Width(240));
            }
            else
            {
                _newKeyModel = GUILayout.TextField(_newKeyModel, GUILayout.Width(140));
                GUILayout.Label("API Key:", GUILayout.Width(66));
                _newKeySecret = GUILayout.PasswordField(_newKeySecret, '*', GUILayout.Width(240));
            }
            GUILayout.EndHorizontal();
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("＋ 添加密钥", GUILayout.Width(120))) AddApiKey();
            GUILayout.Label("Agent路径:", GUILayout.Width(80));
            _agentConfigPath = GUILayout.TextField(_agentConfigPath, GUILayout.Width(240));
            if (GUILayout.Button("导入Agent密钥", GUILayout.Width(120))) ImportAgentKey();
            GUILayout.EndHorizontal();
            GUILayout.Label("<i>提示：选供应商后自动填充 Base URL 与模型，只需填 API Key；「自定义」可手填。名称可留空自动编号；导入 Agent 密钥请选根目录 agent_config.txt。</i>", GUILayout.ExpandWidth(true));

            GUILayout.EndVertical();
        }

        private static void RefreshApiKeyPopupContents()
        {
            var items = new List<GUIContent>();
            foreach (var k in _apiKeyList.keys)
                items.Add(new GUIContent(k.name + (k.isDefault ? "（默认）" : "") + " | " + k.baseUrl));
            _apiKeyPopupContents = items.ToArray();
        }

        private static void StartAiGeneration()
        {
            _kvStatusMsg = "";
            if (_selectedKeyIdx < 0 || _selectedKeyIdx >= _apiKeyList.keys.Count)
            {
                _kvStatusMsg = "请先在「密钥管理」中添加并选择 API 密钥";
                return;
            }
            AIGen.ActiveKey = _apiKeyList.keys[_selectedKeyIdx];
            AIGen.UserPrompt = _aiPrompt;
            AIGen.Start();
        }

        private static void AddApiKey()
        {
            if (string.IsNullOrWhiteSpace(_newKeySecret))
            {
                _kvStatusMsg = "API Key 不能为空";
                return;
            }
            var entry = new ApiKeyEntry
            {
                name = string.IsNullOrWhiteSpace(_newKeyName) ? "密钥" + (_apiKeyList.keys.Count + 1) : _newKeyName.Trim(),
                baseUrl = string.IsNullOrWhiteSpace(_newKeyBase) ? "https://api.deepseek.com" : _newKeyBase.Trim(),
                apiKey = _newKeySecret.Trim(),
                model = string.IsNullOrWhiteSpace(_newKeyModel) ? "deepseek-v4-flash" : _newKeyModel.Trim(),
                thinkingDisableJson = _newKeyThinkingJson ?? "",
                maxTokens = _newKeyMaxTokens,
                isDefault = _apiKeyList.keys.Count == 0,
            };
            _apiKeyList.keys.Add(entry);
            _selectedKeyIdx = _apiKeyList.keys.Count - 1;
            SaveApiKeys();
            _newKeyName = ""; _newKeyBase = ""; _newKeyModel = ""; _newKeySecret = "";
            _kvStatusMsg = "";
        }

        private static void ImportAgentKey()
        {
            string path = _agentConfigPath.Trim();
            if (string.IsNullOrEmpty(path))
            {
                _kvStatusMsg = "请先填写 Agent 配置路径（agent_config.txt）";
                return;
            }
            if (ApiKeyStore.TryImportFromAgentConfig(path, out var entry))
            {
                _apiKeyList.keys.Add(entry);
                _selectedKeyIdx = _apiKeyList.keys.Count - 1;
                _apiKeyList.agentConfigPath = path;
                SaveApiKeys();
                _kvStatusMsg = "已导入 Agent 密钥：" + entry.name;
            }
            else
            {
                _kvStatusMsg = "导入失败：文件不存在或未找到 API_KEY";
            }
        }

        private static void SaveApiKeys() => ApiKeyStore.Save(ApiKeyStore.GetPath(ModEntry), _apiKeyList);


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
