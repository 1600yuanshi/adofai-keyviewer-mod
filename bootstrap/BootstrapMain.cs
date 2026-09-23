using System;
using System.IO;
using System.Reflection;
using UnityModManagerNet;

namespace ADOFAI.AgentKeyViewer.Bootstrap
{
    /// <summary>
    /// UMM 入口与核心热加载器。
    ///
    /// 热替换原理：UMM/Mono 无法卸载已载入的程序集，所以把「会频繁改动」的全部逻辑
    /// 放到独立的 AgentKeyViewer.Core.dll 里，用 <see cref="Assembly.Load(byte[])"/> 加载。
    /// 字节加载不走路径缓存，因此替换磁盘上的同名 DLL 后可以再次加载出新版本并切换过去，
    /// 无需重启游戏。旧程序集仍驻留内存（每次热更新泄漏一份，可接受）。
    /// </summary>
    public static class BootstrapMain
    {
        /// <summary>当前契约版本</summary>
        public const int RequiredApiVersion = 1;

        /// <summary>核心程序集文件名</summary>
        public const string CoreAssemblyName = "AgentKeyViewer.Core.dll";

        public static UnityModManager.ModEntry ModEntry { get; private set; }
        public static IAgentKeyViewerCore Core { get; private set; }
        public static bool Enabled { get; private set; }

        /// <summary>最近一次加载/热替换的错误信息</summary>
        public static string LastError { get; private set; } = "";

        /// <summary>已完成的成功热替换次数</summary>
        public static int ReloadCount { get; private set; }

        private static bool _reloadRequested;

        /// <summary>核心程序集完整路径（与引导器同目录）</summary>
        public static string CorePath
        {
            get
            {
                string dir = ModEntry != null ? Path.GetDirectoryName(ModEntry.Path) : null;
                if (string.IsNullOrEmpty(dir)) dir = ".";
                return Path.Combine(dir, CoreAssemblyName);
            }
        }

        public static bool Load(UnityModManager.ModEntry modEntry)
        {
            ModEntry = modEntry;
            LastError = "";

            // 1) 先加载并校验核心（校验通过前不动旧核心）
            var core = LoadCore(out string error);
            if (core == null)
            {
                LastError = error;
                LogError($"核心加载失败：{error}");
                return false;
            }

            Core = core;
            if (!core.Initialize(modEntry))
            {
                LastError = "核心初始化失败，详见日志";
                LogError(LastError);
                Core = null;
                return false;
            }

            // 2) 注册 UMM 回调（回调始终指向引导器，因此热替换核心不会丢失回调）
            modEntry.OnToggle = OnToggle;
            modEntry.OnGUI = OnGUI;
            modEntry.OnSaveGUI = OnSaveGUI;

            // 3) 创建每帧驱动器
            BootstrapLoop.EnsureExists();

            Enabled = true;
            Log($"引导器已加载，核心版本 {core.Version}（API {core.ApiVersion}）");
            return true;
        }

        // ====================================================================
        //  核心加载 / 热替换
        // ====================================================================

        private static IAgentKeyViewerCore LoadCore(out string error)
        {
            error = "";
            try
            {
                string path = CorePath;
                if (!File.Exists(path))
                {
                    error = $"未找到核心程序集：{path}";
                    return null;
                }

                // 字节加载：不走路径缓存，同名新版本也能再次加载
                byte[] bytes = File.ReadAllBytes(path);
                Assembly assembly = Assembly.Load(bytes);

                foreach (Type type in assembly.GetTypes())
                {
                    if (type.IsAbstract || type.IsInterface) continue;
                    if (!typeof(IAgentKeyViewerCore).IsAssignableFrom(type)) continue;

                    var instance = Activator.CreateInstance(type) as IAgentKeyViewerCore;
                    if (instance == null) continue;
                    if (instance.ApiVersion != RequiredApiVersion)
                    {
                        error = $"核心契约版本不匹配：需要 {RequiredApiVersion}，实际 {instance.ApiVersion}（请更新整个模组）";
                        return null;
                    }
                    return instance;
                }

                error = "核心程序集中未找到 IAgentKeyViewerCore 的实现";
                return null;
            }
            catch (Exception ex)
            {
                error = ex.GetType().Name + ": " + ex.Message;
                return null;
            }
        }

        /// <summary>请求在下一帧热替换核心（由核心自身在下载完新版本后调用）</summary>
        public static void RequestCoreReload()
        {
            _reloadRequested = true;
        }

        /// <summary>立即热替换核心：先校验新核心，再停旧核心、起新核心</summary>
        public static bool ReloadCore(out string error)
        {
            error = "";
            try
            {
                var fresh = LoadCore(out error);
                if (fresh == null) return false;

                var old = Core;
                if (old != null)
                {
                    try { old.Shutdown(); }
                    catch (Exception ex) { LogError("旧核心 Shutdown 异常：" + ex.Message); }
                }

                Core = fresh;
                if (!fresh.Initialize(ModEntry))
                {
                    error = "新核心初始化失败，详见日志";
                    LogError(error);
                    return false;
                }

                ReloadCount++;
                LastError = "";
                Log($"核心已热替换为 {fresh.Version}（第 {ReloadCount} 次）");
                return true;
            }
            catch (Exception ex)
            {
                error = ex.GetType().Name + ": " + ex.Message;
                LogError("热替换失败：" + error);
                return false;
            }
        }

        // ====================================================================
        //  每帧驱动
        // ====================================================================

        /// <summary>由 BootstrapLoop 每帧调用</summary>
        internal static void Tick()
        {
            if (_reloadRequested)
            {
                _reloadRequested = false;
                ReloadCore(out string err);
                if (!string.IsNullOrEmpty(err)) LastError = err;
            }

            try { Core?.Update(); }
            catch (Exception ex) { LogError("核心 Update 异常：" + ex.Message); }
        }

        internal static void DrawInGame()
        {
            if (!Enabled) return;
            try { Core?.DrawInGameGui(); }
            catch (Exception ex) { LogError("核心游戏内 GUI 异常：" + ex.Message); }
        }

        // ====================================================================
        //  UMM 回调
        // ====================================================================

        private static bool OnToggle(UnityModManager.ModEntry modEntry, bool value)
        {
            Enabled = value;
            try
            {
                return Core?.Toggle(value) ?? true;
            }
            catch (Exception ex)
            {
                LogError("核心 Toggle 异常：" + ex.Message);
                return false;
            }
        }

        private static void OnGUI(UnityModManager.ModEntry modEntry)
        {
            try { Core?.DrawGui(modEntry); }
            catch (Exception ex) { LogError("核心 OnGUI 异常：" + ex.Message); }
        }

        private static void OnSaveGUI(UnityModManager.ModEntry modEntry)
        {
            try { Core?.SaveGui(modEntry); }
            catch (Exception ex) { LogError("核心 OnSaveGUI 异常：" + ex.Message); }
        }

        // ====================================================================
        //  日志
        // ====================================================================

        public static void Log(string message) => ModEntry?.Logger.Log("[Bootstrap] " + message);

        public static void LogError(string message) => ModEntry?.Logger.Error("[Bootstrap] " + message);
    }
}
