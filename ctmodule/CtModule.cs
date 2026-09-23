using System;
using System.Collections.Generic;
using CheryTools.Sonnet.Contracts;
using ImGuiNET;
using ADOFAI.AgentKeyViewer.Bootstrap;

namespace ADOFAI.AgentKeyViewer.CtModule
{
    /// <summary>
    /// CherryTools Sonnet 模块：把 Agent Key Viewer 挂进 CT 的 ImGui 面板。
    ///
    /// 这是一个「薄入口」：配置界面、生成流程、提示词热重载、检查更新都在本模组的本地 WebUI 里，
    /// 这里只做状态速览与一键打开 WebUI。
    ///
    /// 重要：本模块只引用<b>引导器</b>（BootstrapMain），不直接引用核心程序集。
    /// 因为核心是可热替换的，直接绑定核心类型会在热更新后指向旧实例。
    /// </summary>
    public sealed class AgentKeyViewerModule : ICheryModule
    {
        private ModuleContext _context;

        public ModuleMetadata Metadata { get; } = new ModuleMetadata(
            "agentkeyviewer",
            "Agent Key Viewer",
            "2.0.0",
            "CheryTools",
            new System.Numerics.Vector4(0.29f, 0.66f, 1f, 1f),
            1)
        {
            NavigationTitle = "键位生成器"
        };

        private static readonly IReadOnlyList<ModuleTab> ModuleTabs = new List<ModuleTab>
        {
            new ModuleTab("main", "生成器"),
        };

        public IReadOnlyList<ModuleTab> Tabs => ModuleTabs;

        public void Initialize(ModuleContext context)
        {
            _context = context;
            try { _context?.Logger?.Info("Agent Key Viewer 模块已加载（界面在本地 WebUI 中）"); }
            catch { }
        }

        public void Update() { }

        public void Shutdown()
        {
            try { _context?.Logger?.Info("Agent Key Viewer 模块已卸载"); }
            catch { }
        }

        public void Draw(ModuleUiContext context)
        {
            ImGui.TextWrapped("用一句自然语言描述，即可生成 CherryTools 可导入的按键显示配置（.ctkv）与 Overlayer 覆盖物（.ctov）。");
            ImGui.Separator();

            // 通过引导器拿当前核心：核心被热替换后这里自动指向新实例
            IAgentKeyViewerCore core = BootstrapMain.Core;
            if (core == null)
            {
                ImGui.TextColored(new System.Numerics.Vector4(1f, 0.42f, 0.42f, 1f),
                    "未检测到 Agent Key Viewer 核心，请先在 UMM 中安装并启用它。");
                if (!string.IsNullOrEmpty(BootstrapMain.LastError))
                    ImGui.TextWrapped("原因：" + BootstrapMain.LastError);
                return;
            }

            if (core.WebUiRunning)
                ImGui.TextColored(new System.Numerics.Vector4(0.25f, 0.73f, 0.31f, 1f), "● " + core.StatusSummary);
            else
                ImGui.TextColored(new System.Numerics.Vector4(1f, 0.71f, 0.33f, 1f), "● " + core.StatusSummary);

            if (ImGui.Button("在浏览器中打开 WebUI"))
            {
                try { core.OpenWebUi(); }
                catch (Exception ex) { _context?.Logger?.Error("打开 WebUI 失败：" + ex.Message); }
            }

            ImGui.Separator();
            ImGui.TextUnformatted("核心版本：" + core.Version + "（API " + core.ApiVersion + "）");
            if (BootstrapMain.ReloadCount > 0)
                ImGui.TextUnformatted($"已热更新 {BootstrapMain.ReloadCount} 次");

            ImGui.Separator();
            ImGui.TextWrapped("提示：生成 .ctkv 后在 CT 的 KeyViewer 模块「导入 .ctkv」；生成 .ctov 后在 Overlayer 模块导入。");
            ImGui.TextWrapped("密钥管理、生成、文件管理、提示词热重载、检查更新都在 WebUI 中。");
        }
    }
}
