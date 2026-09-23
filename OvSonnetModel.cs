using System;
using System.Collections.Generic;
using System.Xml.Serialization;

namespace ADOFAI.AgentKeyViewer
{
    /// <summary>
    /// CheryTools Sonnet 新版 Overlayer(.ctov) 包模型。
    /// 字段名与声明顺序镜像自 CheryTools.Overlayer.Preview.dll 的反编译结果。
    ///
    /// 有意省略的字段：OvTextComponent 的 TokenSourceSnapshot / TokenBindings / TokenAnimation
    /// 与 OvImageComponent 的 NodeAnimation。这些是编辑器侧的动画图缓存，CT 侧字段带内联默认值，
    /// XmlSerializer 构造实例时会先执行字段初始化，因此不输出这些元素时 CT 会沿用自身默认值
    /// （空绑定 + 默认动画图），既避免我们猜错深层结构，也保证能正常导入。
    /// </summary>
    [Serializable]
    [XmlRoot("CheryToolsSonnetOverlayer")]
    public sealed class OvTransferPackage
    {
        public int FormatVersion = 1;

        public string Kind = "Overlayer";

        public string ExportedAt = string.Empty;

        public int ScreenWidth;

        public int ScreenHeight;

        public List<OvTextComponent> Texts = new List<OvTextComponent>();

        public List<OvImageComponent> Images = new List<OvImageComponent>();

        public List<OvVideoComponent> Videos = new List<OvVideoComponent>();

        public List<OvProgressComponent> ProgressBars = new List<OvProgressComponent>();
    }

    [Serializable]
    public abstract class OvComponent
    {
        public string Id = Guid.NewGuid().ToString("N");

        public string Name = "新组件";

        public bool Enabled = true;

        public bool ShowInGame = true;

        public bool OnlyShowPlaying;

        public float PositionX = 50f;

        public float PositionY = 50f;

        public float Opacity = 1f;

        public int Depth;

        public float PivotX;

        public float PivotY;
    }

    [Serializable]
    public sealed class OvTextComponent : OvComponent
    {
        public string TextFormat = "FPS {fps}";

        public float FontSize = 32f;

        public string FontPath = string.Empty;

        /// <summary>0=左 1=中 2=右</summary>
        public int Alignment;

        public float[] TextColor = new float[4] { 1f, 1f, 1f, 1f };

        public bool OutlineEnabled;

        public float[] OutlineColor = new float[4] { 0f, 0f, 0f, 1f };

        public float OutlineThickness = 1f;

        public bool ShadowEnabled;

        public float[] ShadowColor = new float[4] { 0f, 0f, 0f, 0.7f };

        public float[] ShadowOffset = new float[2] { 2f, 2f };

        public float ShadowSoftness;

        public float LetterSpacing;

        public float LineHeightOffset;
    }

    [Serializable]
    public sealed class OvImageComponent : OvComponent
    {
        public string ImagePath = string.Empty;

        public float Scale = 1f;

        public float Rotation;
    }

    [Serializable]
    public sealed class OvVideoComponent : OvComponent
    {
        public string VideoPath = string.Empty;

        public bool Loop = true;

        public float Width = 320f;

        public float Height = 180f;

        public float ContentScale = 1f;

        public float ContentOffsetX;

        public float ContentOffsetY;

        public float Rotation;
    }

    public enum OvProgressFillDirection
    {
        LeftToRight,
        RightToLeft,
        BottomToTop,
        TopToBottom
    }

    [Serializable]
    public sealed class OvProgressComponent : OvComponent
    {
        public string ValueTag = "{progress}";

        public bool ValueIsConstant;

        public double ValueConstant;

        public string MinimumTag = string.Empty;

        public string MaximumTag = string.Empty;

        public double Minimum;

        public double Maximum = 100.0;

        public float Width = 300f;

        public float Height = 20f;

        public OvProgressFillDirection FillDirection;

        public float[] BackgroundColor = new float[4] { 0f, 0f, 0f, 0.45f };

        public float[] FillColor = new float[4] { 0.2f, 0.75f, 1f, 0.95f };

        public float[] BorderColor = new float[4] { 1f, 1f, 1f, 0.8f };

        public float BorderThickness = 1f;

        public float CornerRadius;

        public bool Reverse;

        public bool ClampValue = true;

        public bool EnableFillGradient;

        public float[] FillGradientStartColor = new float[4] { 1f, 0.25f, 0.25f, 0.95f };

        public float[] FillGradientEndColor = new float[4] { 0.25f, 1f, 0.35f, 0.95f };

        public bool EnableShadow;

        public float[] ShadowColor = new float[4] { 0f, 0f, 0f, 0.45f };

        public float[] ShadowOffset = new float[2] { 2f, 2f };

        public float ShadowSoftness;
    }

    /// <summary>
    /// CT Overlayer 模块的<b>设置文件</b>结构（对应 CheryTools.Overlayer.Preview.xml）。
    ///
    /// 用途：CT 的「导入 .ctov」在当前 Unity 运行时会因 XmlReaderSettings.DtdProcessing
    /// 类型解析失败而必挂（CT 侧 Bug，导出/导入路径不对称），且改包内容无法绕过。
    /// 因此提供一个绕过路径：直接生成一份符合该设置格式的文件，让用户关闭游戏后
    /// 覆盖到 &lt;游戏目录&gt;/Mods/CheryTools/Modules/CheryTools.Overlayer.Preview.xml，
    /// 下次启动游戏即可看到覆盖物（CT 的 OvSettings.Load 走 XmlSerializer Stream 重载，不受该 Bug 影响）。
    ///
    /// 注意：CT 把设置常驻内存并在关闭游戏时回写，因此必须在游戏关闭状态下替换该文件。
    /// 字段与顺序镜像自反编译的 OvSettings（6847-6867）；无版本号字段。
    /// </summary>
    [Serializable]
    [XmlRoot("CheryToolsSonnetOverlayer")]
    public sealed class OvSettingsFile
    {
        public bool Enabled = true;

        public bool OnlyShowPlaying;

        public bool EditMode;

        /// <summary>CT 会把它夹在 15~360 之间</summary>
        public float DataUpdateRate = 60f;

        public List<OvTextComponent> Texts = new List<OvTextComponent>();

        public List<OvImageComponent> Images = new List<OvImageComponent>();

        public List<OvVideoComponent> Videos = new List<OvVideoComponent>();

        public List<OvProgressComponent> ProgressBars = new List<OvProgressComponent>();
    }
}
