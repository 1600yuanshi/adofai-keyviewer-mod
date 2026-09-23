using System;
using System.Collections.Generic;
using System.Xml.Serialization;
using UnityEngine;

namespace ADOFAI.AgentKeyViewer
{
    /// <summary>
    /// CheryTools Sonnet 新版 KV 包模型。
    /// 字段名、类型、声明顺序均逐字段镜像自 CheryTools.KeyViewer.Preview.dll 的反编译结果，
    /// 以保证 XmlSerializer 产出与 CT 自身导出完全一致的结构。
    /// 注意：不要添加额外的 XmlArray/XmlArrayItem 特性——CT 使用裸 XmlSerializer，
    /// 默认容器命名为 &lt;Keys&gt;&lt;KvKey&gt;，多加特性会导致 CT 导入失败。
    /// </summary>
    [Serializable]
    [XmlRoot("CheryToolsSonnetKeyViewer")]
    public sealed class KvTransferPackage
    {
        public int FormatVersion = 1;

        /// <summary>profile / all / node / nodes</summary>
        public string Kind = "profile";

        public string ExportedAt = string.Empty;

        public int ScreenWidth;

        public int ScreenHeight;

        public List<KvProfile> Profiles = new List<KvProfile>();

        public List<KvKey> Nodes = new List<KvKey>();
    }

    [Serializable]
    public sealed class KvProfile
    {
        public string Id = Guid.NewGuid().ToString("N");

        public string Name = "新配置";

        public bool Enabled = true;

        public bool ShowInGame = true;

        public bool OnlyShowPlaying;

        public int TotalHits;

        public List<KvKey> Keys = new List<KvKey>();

        public int LayoutVersion;

        public float OffsetX;

        public float OffsetY = -330f;

        public float Scale = 1f;

        public float KeyWidth = 72f;

        public float KeyHeight = 72f;

        public float Gap = 8f;

        public float CornerRadius = 10f;

        public float BorderThickness = 2f;

        public float LabelSize = 22f;

        public float CountSize = 13f;

        public bool HideCount;

        public bool ShowKps = true;

        public bool ShowTotal = true;

        public float[] BackgroundNormal = new float[4] { 0.075f, 0.085f, 0.11f, 0.88f };

        public float[] BackgroundPressed = new float[4] { 0.3f, 0.76f, 1f, 0.96f };

        public float[] BorderNormal = new float[4] { 0.42f, 0.48f, 0.58f, 0.7f };

        public float[] BorderPressed = new float[4] { 0.72f, 0.92f, 1f, 1f };

        public float[] TextNormal = new float[4] { 0.94f, 0.96f, 1f, 1f };

        public float[] TextPressed = new float[4] { 0.015f, 0.025f, 0.04f, 1f };

        public string FontPath = string.Empty;

        public bool LabelOutlineEnabled;

        public float[] LabelOutlineColor = new float[4] { 0f, 0f, 0f, 1f };

        public float LabelOutlineThickness = 1f;

        public bool CountOutlineEnabled;

        public float[] CountOutlineColor = new float[4] { 0f, 0f, 0f, 1f };

        public float CountOutlineThickness = 1f;

        public bool LabelShadowEnabled;

        public float[] LabelShadowColor = new float[4] { 0f, 0f, 0f, 0.7f };

        public float[] LabelShadowOffset = new float[2] { 2f, -2f };

        public float LabelShadowSoftness;

        public bool CountShadowEnabled;

        public float[] CountShadowColor = new float[4] { 0f, 0f, 0f, 0.7f };

        public float[] CountShadowOffset = new float[2] { 2f, -2f };

        public float CountShadowSoftness;

        public bool RainEnabled;

        public float RainSpeed = 620f;

        public float RainMaxHeight = 420f;

        public float RainWidthRatio = 0.72f;

        public float RainWidthRatio2 = 0.55f;

        public float RainYOffset;

        public float RainYOffset2;

        public float RainCornerRadius = 7f;

        public float[] RainColor = new float[4] { 0.3f, 0.76f, 1f, 0.74f };

        public float[] RainEndColor = new float[4] { 0.48f, 0.34f, 1f, 0.08f };

        public float[] RainRightColor = new float[4] { 0.45f, 0.75f, 1f, 0.74f };

        public float[] RainColor2 = new float[4] { 0.5f, 0.8f, 1f, 0.74f };

        public float[] RainEndColor2 = new float[4] { 0.25f, 1f, 0.8f, 0.08f };

        public float[] RainRightColor2 = new float[4] { 1f, 0.65f, 0.35f, 0.74f };

        public bool RainGradientEnabled = true;

        public int RainGradientMode;

        public float RainGradientHeight = 1f;

        public float RainGradientPower = 1f;

        public bool RainHorizontalGradientEnabled;

        public int RainFadeMode = 1;

        public float RainFadeHeight = 0.22f;

        public float RainFadePower = 1f;

        public bool RainOutlineEnabled;

        public float[] RainOutlineColor = new float[4] { 0f, 0f, 0f, 0.85f };

        public float RainOutlineThickness = 1f;

        public bool RainShadowEnabled;

        public float[] RainShadowColor = new float[4] { 0f, 0f, 0f, 0.35f };

        public float[] RainShadowOffset = new float[2] { 2f, -2f };

        public float RainShadowSoftness = 12f;

        public float RainShadowStrength = 1f;

        public bool PressAnimationEnabled;

        public float PressAnimationDuration = 0.12f;

        public string PressAnimationEasing = "ease-out-quad";

        public bool PressAnimationAffectColors = true;

        public float PressAnimationScale = 0.94f;

        public float PressAnimationOffsetX;

        public float PressAnimationOffsetY;

        [XmlIgnore]
        public int CurrentKps;
    }

    [Serializable]
    public sealed class KvKey
    {
        public string Id = Guid.NewGuid().ToString("N");

        /// <summary>0=按键 1=KPS 2=Total 3=图片</summary>
        public int NodeType;

        public KeyCode Bind = (KeyCode)97;

        public string Label = "A";

        public string ImagePath = string.Empty;

        public string VideoPath = string.Empty;

        public bool VideoLoop = true;

        public float MediaScale = 1f;

        public float MediaOffsetX;

        public float MediaOffsetY;

        public float Opacity = 1f;

        public int Depth;

        public bool Locked;

        public int HitCount;

        public bool RainEnabled = true;

        public int RainRow = 1;

        public float PositionX;

        public float PositionY;

        public float Width = -1f;

        public float Height = -1f;

        public bool UseCustomStyle;

        public float CornerRadius = -1f;

        public float BorderThickness = -1f;

        public float LabelSize = -1f;

        public float CountSize = -1f;

        public string LabelFontPath = string.Empty;

        public string CountFontPath = string.Empty;

        public float LabelOffsetX;

        public float LabelOffsetY;

        public float CountOffsetX;

        public float CountOffsetY;

        /// <summary>数值文本对齐：0=左 1=中 2=右</summary>
        public int CountAlignment = 1;

        public bool HideCount;

        public float[] BackgroundNormal = new float[4] { 0.075f, 0.085f, 0.11f, 0.88f };

        public float[] BackgroundPressed = new float[4] { 0.3f, 0.76f, 1f, 0.96f };

        public float[] BorderNormal = new float[4] { 0.42f, 0.48f, 0.58f, 0.7f };

        public float[] BorderPressed = new float[4] { 0.72f, 0.92f, 1f, 1f };

        public float[] TextNormal = new float[4] { 0.94f, 0.96f, 1f, 1f };

        public float[] TextPressed = new float[4] { 0.015f, 0.025f, 0.04f, 1f };

        public bool UseCustomTextEffects;

        public bool LabelOutlineEnabled;

        public float[] LabelOutlineColor = new float[4] { 0f, 0f, 0f, 1f };

        public float LabelOutlineThickness = 1f;

        public bool CountOutlineEnabled;

        public float[] CountOutlineColor = new float[4] { 0f, 0f, 0f, 1f };

        public float CountOutlineThickness = 1f;

        public bool LabelShadowEnabled;

        public float[] LabelShadowColor = new float[4] { 0f, 0f, 0f, 0.7f };

        public float[] LabelShadowOffset = new float[2] { 2f, -2f };

        public float LabelShadowSoftness;

        public bool CountShadowEnabled;

        public float[] CountShadowColor = new float[4] { 0f, 0f, 0f, 0.7f };

        public float[] CountShadowOffset = new float[2] { 2f, -2f };

        public float CountShadowSoftness;

        public bool UseCustomRain;

        public float RainWidthRatio = 0.72f;

        public float RainYOffset;

        public float RainCornerRadius = 7f;

        public float[] RainColor = new float[4] { 0.3f, 0.76f, 1f, 0.74f };

        public float[] RainEndColor = new float[4] { 0.48f, 0.34f, 1f, 0.08f };

        public float[] RainRightColor = new float[4] { 0.45f, 0.75f, 1f, 0.74f };

        public bool RainGradientEnabled;

        public int RainGradientMode;

        public float RainGradientHeight = 1f;

        public float RainGradientPower = 1f;

        public bool RainHorizontalGradientEnabled;

        public int RainFadeMode = 1;

        public float RainFadeHeight = 0.22f;

        public float RainFadePower = 1f;

        public bool RainOutlineEnabled;

        public float[] RainOutlineColor = new float[4] { 0f, 0f, 0f, 0.85f };

        public float RainOutlineThickness = 1f;

        public bool RainShadowEnabled;

        public float[] RainShadowColor = new float[4] { 0f, 0f, 0f, 0.35f };

        public float[] RainShadowOffset = new float[2] { 2f, -2f };

        public float RainShadowSoftness = 12f;

        public float RainShadowStrength = 1f;

        public bool UseCustomAnimation;

        public bool PressAnimationEnabled;

        public float PressAnimationDuration = 0.12f;

        public string PressAnimationEasing = "ease-out-quad";

        public bool PressAnimationAffectColors = true;

        public float PressAnimationScale = 0.94f;

        public float PressAnimationOffsetX;

        public float PressAnimationOffsetY;
    }
}
