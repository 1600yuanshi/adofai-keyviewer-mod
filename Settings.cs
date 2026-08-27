using System.Collections.Generic;
using UnityEngine;
using UnityModManagerNet;

namespace ADOFAI.AgentKeyViewer
{
    /// <summary>
    /// Mod设置类（支持FreeMake自定义、全局颜色、拖拽移动）
    /// </summary>
    public class Settings : UnityModManager.ModSettings
    {
        // ===== 基础开关 =====
        public bool ShowKeyDisplay = true;
        public bool ShowKpsTotal = true;
        public bool ShowPerKeyCount = true;
        /// <summary>允许在显示面板上按住拖动来改变位置</summary>
        public bool EnablePanelDrag = true;

        // ===== 显示位置 =====
        public float DisplayX = 60;
        public float DisplayY = 60;
        public float Opacity = 0.85f;
        public float Scale = 1.0f;

        // ===== 布局设置 =====
        /// <summary>0=4K, 1=6K, 2=方向键, 3=2K, 4=Full, 5=FreeMake自定义</summary>
        public int LayoutType = 0;
        public bool IncludeMouse = true;

        // ===== 全局颜色 =====
        public SerializableColor PanelBgColor = new Color(0.05f, 0.05f, 0.1f, 0.6f);
        public SerializableColor KpsTextColor = new Color(1f, 0.5f, 0.5f, 1f);
        public SerializableColor TotalTextColor = new Color(0.5f, 0.75f, 1f, 1f);
        public SerializableColor PerKeyCountColor = new Color(1f, 1f, 0.6f, 1f);

        // ===== 按键默认颜色（预设布局/批量应用使用，持久化保存）=====
        public SerializableColor KeyIdleColor = new Color(0.1f, 0.1f, 0.15f, 0.92f);
        public SerializableColor KeyPressedColor = new Color(0.95f, 0.75f, 0.15f, 1f);
        public SerializableColor KeyBorderColor = new Color(0.55f, 0.55f, 0.55f, 1f);
        public SerializableColor KeyTextColor = Color.white;
        public SerializableColor KeyPressedTextColor = Color.black;

        // ===== 动画与显示细节（可配置）=====
        /// <summary>按下动画时长（秒）</summary>
        public float AnimDuration = 0.08f;
        /// <summary>按下视觉保持窗口（秒），应对丢帧</summary>
        public float VisualHoldTime = 0.05f;
        /// <summary>按下时按键外扩的像素</summary>
        public float PressExpandSize = 6f;
        /// <summary>按键边框粗细（像素）</summary>
        public float BorderThickness = 2f;
        /// <summary>面板内边距（像素）</summary>
        public float PanelPadding = 6f;
        /// <summary>按键标签字号相对边长比例</summary>
        public float KeyFontRatio = 0.42f;
        /// <summary>单键计数字号相对按键字号比例</summary>
        public float HitCountFontRatio = 0.45f;
        /// <summary>KPS/Total 框宽</summary>
        public float StatsBoxWidth = 80f;
        /// <summary>KPS/Total 框高</summary>
        public float StatsBoxHeight = 50f;
        /// <summary>统计标签字号比例（相对框高）</summary>
        public float StatLabelFontRatio = 0.3f;
        /// <summary>统计数值字号比例（相对框高）</summary>
        public float StatValueFontRatio = 0.5f;
        /// <summary>是否显示面板拖动提示文字</summary>
        public bool ShowDragHint = true;

        // ===== 圆角（Rounded Corner，现代 UI 风格，参考 CT 圆角支持）=====
        /// <summary>是否启用圆角</summary>
        public bool EnableRoundedCorners = true;
        /// <summary>按键圆角半径（px）</summary>
        public float KeyCornerRadius = 12f;
        /// <summary>KPS/Total 统计框圆角半径（px）</summary>
        public float StatsCornerRadius = 10f;
        /// <summary>面板背景圆角半径（px）</summary>
        public float PanelCornerRadius = 14f;

        // ===== 键背景图片（参考 CT "背景贴图" 节点）=====
        /// <summary>是否允许使用键背景图片</summary>
        public bool EnableKeyImages = true;
        /// <summary>图片缩放模式：0=拉伸，1=适应(留边)，2=填充(裁剪)</summary>
        public int KeyImageFit = 0;

        // ===== Rain 键雨（参考 CheryTools）=====
        /// <summary>是否启用键雨</summary>
        public bool EnableRain = true;
        /// <summary>键雨消失模式：0=高度裁剪，1=羽化透明</summary>
        public int RainFadeMode = 1;
        /// <summary>键雨速度（px/s，第0排）</summary>
        public float RainSpeed = 700f;
        /// <summary>键雨消失距离（px，第0排）</summary>
        public float RainDistance = 260f;
        /// <summary>键雨颜色（第0排默认）</summary>
        public SerializableColor RainColor = new Color(1f, 0.85f, 0.4f, 0.9f);
        /// <summary>键雨宽度比例（相对键宽，第0排）</summary>
        public float RainWidthRatio = 0.12f;
        /// <summary>键雨高度偏移（相对键顶，px，第0排）</summary>
        public float RainHeightOffset = 0f;
        /// <summary>键雨增长速度（px/s，按住按键时长度持续增加）</summary>
        public float RainGrowSpeed = 600f;
        /// <summary>键雨绝对宽度（px，0=按宽度比例自动）</summary>
        public float RainWidthPx = 0f;
        /// <summary>最大键雨条数量（性能上限）</summary>
        public int RainMaxDrops = 200;

        // ---- 两排键雨分别设置 ----
        /// <summary>是否启用两排键雨分别设置（按键按RainRow分组：0排/1排）</summary>
        public bool EnableTwoRowRain = false;
        public float RainRow1Speed = 700f;
        public float RainRow1Distance = 260f;
        public SerializableColor RainRow1Color = new Color(0.55f, 0.8f, 1f, 0.9f);
        public float RainRow1WidthRatio = 0.12f;
        public float RainRow1HeightOffset = 0f;
        public float RainRow1GrowSpeed = 600f;
        public float RainRow1WidthPx = 0f;

        // ===== FreeMake自定义布局（LayoutType=5时生效）=====
        public List<KeyDefinition> CustomKeys = new List<KeyDefinition>();

        // ===== Agent配置 =====
        public string AgentConfigPath = "config/agent.config.json";
        public bool AutoSyncAgent = true;
        /// <summary>生成配置时关闭模型思考（对推理模型有效，避免输出思维链）</summary>
        public bool DisableThinking = true;

        public override void Save(UnityModManager.ModEntry modEntry)
        {
            Save(this, modEntry);
        }
    }
}
