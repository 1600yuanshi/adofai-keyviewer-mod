using System;
using System.IO;
using System.Text;
using UnityEngine;
using UnityModManagerNet;

namespace ADOFAI.AgentKeyViewer
{
    /// <summary>
    /// Agent配置管理器，负责与外部Agent系统进行配置同步
    /// </summary>
    public class AgentConfigManager
    {
        private readonly UnityModManager.ModEntry _modEntry;
        private AgentConfig _config;

        public string ConfigPath { get; private set; }
        public bool IsConnected { get; private set; }

        public AgentConfigManager(UnityModManager.ModEntry modEntry)
        {
            _modEntry = modEntry;
            ConfigPath = Path.Combine(
                Path.GetDirectoryName(modEntry.Path) ?? ".",
                "config",
                "agent.config.json"
            );
            _config = new AgentConfig();
        }

        /// <summary>
        /// 加载Agent配置
        /// </summary>
        public void Load()
        {
            try
            {
                // 确保配置目录存在
                string configDir = Path.GetDirectoryName(ConfigPath);
                if (!string.IsNullOrEmpty(configDir) && !Directory.Exists(configDir))
                {
                    Directory.CreateDirectory(configDir);
                }

                // 尝试加载配置文件
                if (File.Exists(ConfigPath))
                {
                    string json = File.ReadAllText(ConfigPath, Encoding.UTF8);
                    _config = JsonUtility.FromJson<AgentConfig>(json);
                    _modEntry.Logger.Log($"[AgentConfig] Loaded config from {ConfigPath}");
                }
                else
                {
                    // 创建默认配置
                    _config = CreateDefaultConfig();
                    Save();
                    _modEntry.Logger.Log($"[AgentConfig] Created default config at {ConfigPath}");
                }

                IsConnected = true;
            }
            catch (Exception ex)
            {
                _modEntry.Logger.Error($"[AgentConfig] Failed to load config: {ex.Message}");
                _config = CreateDefaultConfig();
                IsConnected = false;
            }
        }

        /// <summary>
        /// 保存Agent配置
        /// </summary>
        public void Save()
        {
            try
            {
                string json = JsonUtility.ToJson(_config, true);
                File.WriteAllText(ConfigPath, json, Encoding.UTF8);
                _modEntry.Logger.Log($"[AgentConfig] Saved config to {ConfigPath}");
            }
            catch (Exception ex)
            {
                _modEntry.Logger.Error($"[AgentConfig] Failed to save config: {ex.Message}");
            }
        }

        /// <summary>
        /// 获取当前配置
        /// </summary>
        public AgentConfig GetConfig()
        {
            return _config;
        }

        /// <summary>
        /// 更新配置
        /// </summary>
        public void UpdateConfig(Action<AgentConfig> updater)
        {
            updater?.Invoke(_config);
            Save();
        }

        /// <summary>
        /// 同步Mod设置到Agent配置
        /// </summary>
        public void SyncFromModSettings()
        {
            if (CoreEntry.Settings == null) return;

            _config.displaySettings.showKeyDisplay = CoreEntry.Settings.ShowKeyDisplay;
            _config.displaySettings.displayX = CoreEntry.Settings.DisplayX;
            _config.displaySettings.displayY = CoreEntry.Settings.DisplayY;
            _config.displaySettings.opacity = CoreEntry.Settings.Opacity;
            _config.displaySettings.showKpsTotal = CoreEntry.Settings.ShowKpsTotal;

            _config.keyCapture.layoutType = CoreEntry.Settings.LayoutType;
            _config.keyCapture.includeMouse = CoreEntry.Settings.IncludeMouse;
            _config.keyCapture.captureLeftClick = CoreEntry.Settings.IncludeMouse;
            _config.keyCapture.captureRightClick = CoreEntry.Settings.IncludeMouse;

            Save();
        }

        /// <summary>
        /// 从Agent配置同步到Mod设置
        /// </summary>
        public void SyncToModSettings()
        {
            if (CoreEntry.Settings == null) return;

            CoreEntry.Settings.ShowKeyDisplay = _config.displaySettings.showKeyDisplay;
            CoreEntry.Settings.DisplayX = _config.displaySettings.displayX;
            CoreEntry.Settings.DisplayY = _config.displaySettings.displayY;
            CoreEntry.Settings.Opacity = _config.displaySettings.opacity;
            CoreEntry.Settings.ShowKpsTotal = _config.displaySettings.showKpsTotal;

            CoreEntry.Settings.LayoutType = _config.keyCapture.layoutType;
            CoreEntry.Settings.IncludeMouse = _config.keyCapture.includeMouse;
        }

        /// <summary>
        /// 记录按键事件到Agent日志（使用KeyDefinition参数替代KeyEvent）
        /// </summary>
        public void LogKeyEvent(string keyCodeStr, string displayName, bool isPressed)
        {
            try
            {
                string logPath = Path.Combine(
                    Path.GetDirectoryName(ConfigPath) ?? ".",
                    "logs",
                    $"keylog_{DateTime.Now:yyyyMMdd}.log"
                );

                string logDir = Path.GetDirectoryName(logPath);
                if (!string.IsNullOrEmpty(logDir) && !Directory.Exists(logDir))
                {
                    Directory.CreateDirectory(logDir);
                }

                string logEntry = $"[{DateTime.Now:HH:mm:ss.fff}] {keyCodeStr} ({displayName}) - {(isPressed ? "Pressed" : "Released")}\n";
                File.AppendAllText(logPath, logEntry, Encoding.UTF8);
            }
            catch (Exception ex)
            {
                _modEntry.Logger.Error($"[AgentConfig] Failed to log key event: {ex.Message}");
            }
        }

        private AgentConfig CreateDefaultConfig()
        {
            return new AgentConfig
            {
                version = "1.0.0",
                agentName = "ADOFAI Agent",
                displaySettings = new DisplaySettings
                {
                    showKeyDisplay = true,
                    showKeyNames = true,
                    showAnimations = true,
                    showKpsTotal = true,
                    displayX = 100,
                    displayY = 100,
                    fontSize = 24,
                    scale = 1.0f,
                    opacity = 0.85f
                },
                keyCapture = new KeyCaptureSettings
                {
                    layoutType = 0,
                    includeMouse = true,
                    captureLeftClick = true,
                    captureRightClick = true,
                    captureSpace = true,
                    captureArrowKeys = true
                },
                logging = new LoggingSettings
                {
                    enabled = true,
                    logToFile = true,
                    logLevel = "info"
                }
            };
        }
    }

    /// <summary>
    /// Agent配置数据类
    /// </summary>
    [Serializable]
    public class AgentConfig
    {
        public string version;
        public string agentName;
        public DisplaySettings displaySettings;
        public KeyCaptureSettings keyCapture;
        public LoggingSettings logging;
    }

    [Serializable]
    public class DisplaySettings
    {
        public bool showKeyDisplay;
        public bool showKeyNames;
        public bool showAnimations;
        public bool showKpsTotal;
        public float displayX;
        public float displayY;
        public float fontSize;
        public float scale;
        public float opacity;
    }

    [Serializable]
    public class KeyCaptureSettings
    {
        public int layoutType;
        public bool includeMouse;
        public bool captureLeftClick;
        public bool captureRightClick;
        public bool captureSpace;
        public bool captureArrowKeys;
    }

    [Serializable]
    public class LoggingSettings
    {
        public bool enabled;
        public bool logToFile;
        public string logLevel;
    }
}
