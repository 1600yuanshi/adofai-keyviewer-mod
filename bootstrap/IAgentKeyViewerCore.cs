using UnityModManagerNet;

namespace ADOFAI.AgentKeyViewer.Bootstrap
{
    /// <summary>
    /// 核心程序集契约。
    ///
    /// 本接口属于<b>引导器</b>，因此必须保持稳定：一旦修改它（增删成员或改签名），
    /// 引导器本身就需要重新编译并重启游戏，热更新才会重新生效。
    /// 核心实现应通过 <see cref="ApiVersion"/> 声明自己兼容的版本。
    /// </summary>
    public interface IAgentKeyViewerCore
    {
        /// <summary>核心实现所遵循的契约版本，必须等于 BootstrapMain.RequiredApiVersion</summary>
        int ApiVersion { get; }

        /// <summary>核心版本号（用于界面展示与更新比对）</summary>
        string Version { get; }

        /// <summary>初始化（等价于原 UMM Load）。返回 false 表示加载失败。</summary>
        bool Initialize(UnityModManager.ModEntry modEntry);

        /// <summary>卸载/热替换前的收尾：停止后台服务、保存状态</summary>
        void Shutdown();

        /// <summary>UMM 启用/停用回调</summary>
        bool Toggle(bool enabled);

        /// <summary>UMM 设置面板绘制</summary>
        void DrawGui(UnityModManager.ModEntry modEntry);

        /// <summary>UMM 保存设置回调</summary>
        void SaveGui(UnityModManager.ModEntry modEntry);

        /// <summary>每帧驱动（由引导器的 MonoBehaviour 调用）</summary>
        void Update();

        /// <summary>游戏内 GUI 绘制（由引导器的 MonoBehaviour 调用）</summary>
        void DrawInGameGui();

        // ---- 供外部面板（如 CherryTools 模块）读取的最小状态接口 ----
        // 刻意只暴露这几个成员：外部组件通过引导器持有核心，热替换后自动指向新核心。

        /// <summary>WebUI 是否正在运行</summary>
        bool WebUiRunning { get; }

        /// <summary>一行状态摘要（含端口/令牌或错误原因），供外部面板直接显示</summary>
        string StatusSummary { get; }

        /// <summary>打开 WebUI（未运行则先启动）</summary>
        void OpenWebUi();
    }
}
