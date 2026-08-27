using System;
using UnityEngine;

namespace ADOFAI.AgentKeyViewer
{
    /// <summary>
    /// 可序列化的Color结构体（UnityEngine.Color不能直接被XML序列化）
    /// </summary>
    [Serializable]
    public struct SerializableColor
    {
        public float r;
        public float g;
        public float b;
        public float a;

        public SerializableColor(float r, float g, float b, float a = 1f)
        {
            this.r = r; this.g = g; this.b = b; this.a = a;
        }

        public static implicit operator Color(SerializableColor c)
            => new Color(c.r, c.g, c.b, c.a);

        public static implicit operator SerializableColor(Color c)
            => new SerializableColor(c.r, c.g, c.b, c.a);

        public static SerializableColor FromHtml(string html)
        {
            if (ColorUtility.TryParseHtmlString(html, out var color))
                return color;
            return Color.white;
        }

        public string ToHtml()
        {
            Color c = this;
            return $"#{ColorUtility.ToHtmlStringRGBA(c)}";
        }
    }

    /// <summary>
    /// 单个按键的定义（支持FreeMake自定义：颜色、位置、尺寸、标签、按键码）
    /// </summary>
    [Serializable]
    public class KeyDefinition
    {
        /// <summary>唯一ID（FreeMake内不可重名）</summary>
        public string Id { get; set; }
        /// <summary>Unity KeyCode枚举值</summary>
        public KeyCode KeyCode { get; set; }
        /// <summary>按键标签（如"F"、"J"、"←"）</summary>
        public string Label { get; set; }
        /// <summary>在布局中的X偏移（像素，相对于显示区域原点）</summary>
        public float OffsetX { get; set; }
        /// <summary>在布局中的Y偏移（像素，相对于显示区域原点）</summary>
        public float OffsetY { get; set; }
        /// <summary>按键框宽度</summary>
        public float Width { get; set; }
        /// <summary>按键框高度</summary>
        public float Height { get; set; }
        /// <summary>按键类型：0=主按键，1=脚键，2=鼠标，3=自定义</summary>
        public int NodeType { get; set; }

        // ---- 自由定制颜色（CT FreeMake风格）----
        /// <summary>按键正常状态背景色</summary>
        public SerializableColor IdleColor { get; set; } = new Color(0.1f, 0.1f, 0.15f, 0.92f);
        /// <summary>按键按下状态背景色（高亮）</summary>
        public SerializableColor PressedColor { get; set; } = new Color(0.95f, 0.75f, 0.15f, 1f);
        /// <summary>按键边框颜色</summary>
        public SerializableColor BorderColor { get; set; } = new Color(0.55f, 0.55f, 0.55f, 1f);
        /// <summary>标签文字颜色</summary>
        public SerializableColor TextColor { get; set; } = Color.white;
        /// <summary>按下时文字颜色</summary>
        public SerializableColor PressedTextColor { get; set; } = Color.black;
        /// <summary>标签文字大小（0=跟随全局）</summary>
        public int FontSize { get; set; } = 0;

        // ---- 键雨（Rain）配置（FreeMake可单独设置）----
        /// <summary>键雨颜色（仅在UseCustomRain=true时使用）</summary>
        public SerializableColor RainColor { get; set; } = new Color(1f, 0.85f, 0.4f, 0.9f);
        /// <summary>是否使用单键自定义键雨设置</summary>
        public bool UseCustomRain { get; set; } = false;
        /// <summary>单键键雨宽度比例（相对键宽）</summary>
        public float RainWidthRatio { get; set; } = 0.12f;
        /// <summary>单键键雨高度偏移（相对键顶，像素）</summary>
        public float RainHeightOffset { get; set; } = 0f;
        /// <summary>键雨排数分组：0=第一排，1=第二排（两排分别设置时生效）</summary>
        public int RainRow { get; set; } = 0;

        // ---- 圆角（Rounded Corner，CT 语义：整键背景+边框一起圆角）----
        /// <summary>单键圆角半径（px，0=跟随全局设置）</summary>
        public float CornerRadius { get; set; } = 0f;
        /// <summary>单键边框粗细（px，0=跟随全局设置；CT 中 -1=跟随全局）</summary>
        public float BorderThickness { get; set; } = 0f;

        // ---- 显示模式（每键自定义）----
        /// <summary>0=键模式（维持原样：键框+标签+键雨+按键反馈），1=图片模式（只显示图片，隐藏key/rain/按键反馈）</summary>
        public int DisplayMode { get; set; } = 0;

        // ---- 背景图片（参考 CT "背景贴图"）----
        /// <summary>是否使用背景图片</summary>
        public bool UseImage { get; set; } = false;
        /// <summary>图片文件名（位于 游戏目录/AgentKeyViewer_config/images/ 下），空=无</summary>
        public string ImageFile { get; set; } = "";
        /// <summary>图片不透明度（1=不透明）</summary>
        public float ImageOpacity { get; set; } = 1f;

        // 运行时状态（不保存到配置）
        [NonSerialized] public bool IsPressed;
        [NonSerialized] public bool WasPressed;
        [NonSerialized] public bool KeyJustPressed; // 本帧刚按下（供键雨等触发）
        [NonSerialized] public int HitCount;
        [NonSerialized] public float PressAnimation; // 0~1
        [NonSerialized] public float VisualPressedUntil;

        public KeyDefinition() { }

        public KeyDefinition(string id, KeyCode keyCode, string label, float offsetX, float offsetY,
                             float width = 60, float height = 60, int nodeType = 0)
        {
            Id = id;
            KeyCode = keyCode;
            Label = label;
            OffsetX = offsetX;
            OffsetY = offsetY;
            Width = width;
            Height = height;
            NodeType = nodeType;
        }

        /// <summary>克隆（用于编辑器中复制等操作）</summary>
        public KeyDefinition Clone()
        {
            return new KeyDefinition
            {
                Id = Id,
                KeyCode = KeyCode,
                Label = Label,
                OffsetX = OffsetX,
                OffsetY = OffsetY,
                Width = Width,
                Height = Height,
                NodeType = NodeType,
                IdleColor = IdleColor,
                PressedColor = PressedColor,
                BorderColor = BorderColor,
                TextColor = TextColor,
                PressedTextColor = PressedTextColor,
                FontSize = FontSize,
                RainColor = RainColor,
                UseCustomRain = UseCustomRain,
                RainWidthRatio = RainWidthRatio,
                RainHeightOffset = RainHeightOffset,
                RainRow = RainRow,
                CornerRadius = CornerRadius,
                BorderThickness = BorderThickness,
                DisplayMode = DisplayMode,
                UseImage = UseImage,
                ImageFile = ImageFile,
                ImageOpacity = ImageOpacity,
            };
        }
    }

    /// <summary>
    /// 预设布局
    /// </summary>
    public static class KeyLayoutPresets
    {
        /// <summary>
        /// 创建4K标准布局（D F J K）
        /// </summary>
        public static KeyDefinition[] Create4KLayout()
        {
            float keyW = 70, keyH = 70, gap = 8;
            float startX = 0;

            return new KeyDefinition[]
            {
                new KeyDefinition("D", KeyCode.D, "D", startX + 0 * (keyW + gap), 0, keyW, keyH),
                new KeyDefinition("F", KeyCode.F, "F", startX + 1 * (keyW + gap), 0, keyW, keyH),
                new KeyDefinition("J", KeyCode.J, "J", startX + 2 * (keyW + gap), 0, keyW, keyH),
                new KeyDefinition("K", KeyCode.K, "K", startX + 3 * (keyW + gap), 0, keyW, keyH),
            };
        }

        /// <summary>
        /// 创建6K布局（D F Space J K L）
        /// </summary>
        public static KeyDefinition[] Create6KLayout()
        {
            float keyW = 60, keyH = 70, gap = 8;
            float startX = 0;

            return new KeyDefinition[]
            {
                new KeyDefinition("D", KeyCode.D, "D", startX + 0 * (keyW + gap), 0, keyW, keyH),
                new KeyDefinition("F", KeyCode.F, "F", startX + 1 * (keyW + gap), 0, keyW, keyH),
                new KeyDefinition("SPACE", KeyCode.Space, "Space", startX + 2 * (keyW + gap), 0, keyW * 2, keyH),
                new KeyDefinition("J", KeyCode.J, "J", startX + 4 * (keyW + gap), 0, keyW, keyH),
                new KeyDefinition("K", KeyCode.K, "K", startX + 5 * (keyW + gap), 0, keyW, keyH),
                new KeyDefinition("L", KeyCode.L, "L", startX + 6 * (keyW + gap), 0, keyW, keyH),
            };
        }

        /// <summary>
        /// 创建方向键布局（← ↑ ↓ →）
        /// </summary>
        public static KeyDefinition[] CreateArrowLayout()
        {
            float keyW = 60, keyH = 60, gap = 8;

            return new KeyDefinition[]
            {
                new KeyDefinition("LEFT",  KeyCode.LeftArrow,  "←", 0,                  keyH + gap, keyW, keyH),
                new KeyDefinition("DOWN",  KeyCode.DownArrow,  "↓", keyW + gap,         keyH + gap, keyW, keyH),
                new KeyDefinition("RIGHT", KeyCode.RightArrow, "→", (keyW + gap) * 2,   keyH + gap, keyW, keyH),
                new KeyDefinition("UP",    KeyCode.UpArrow,    "↑", keyW + gap,         0,          keyW, keyH),
            };
        }

        /// <summary>
        /// 创建2K布局（D K）
        /// </summary>
        public static KeyDefinition[] Create2KLayout()
        {
            float keyW = 80, keyH = 80, gap = 12;

            return new KeyDefinition[]
            {
                new KeyDefinition("D", KeyCode.D, "D", 0, 0, keyW, keyH),
                new KeyDefinition("K", KeyCode.K, "K", keyW + gap, 0, keyW, keyH),
            };
        }

        /// <summary>
        /// 创建完整10K布局（包含方向键+常用字母键）
        /// </summary>
        public static KeyDefinition[] Create10KFullLayout()
        {
            float keyW = 55, keyH = 55, gap = 6;
            float row1Y = 0;
            float row2Y = keyH + gap;

            return new KeyDefinition[]
            {
                new KeyDefinition("Q", KeyCode.Q, "Q", 0 * (keyW + gap), row1Y, keyW, keyH),
                new KeyDefinition("W", KeyCode.W, "W", 1 * (keyW + gap), row1Y, keyW, keyH),
                new KeyDefinition("E", KeyCode.E, "E", 2 * (keyW + gap), row1Y, keyW, keyH),
                new KeyDefinition("R", KeyCode.R, "R", 3 * (keyW + gap), row1Y, keyW, keyH),
                new KeyDefinition("T", KeyCode.T, "T", 4 * (keyW + gap), row1Y, keyW, keyH),
                new KeyDefinition("Y", KeyCode.Y, "Y", 5 * (keyW + gap), row1Y, keyW, keyH),
                new KeyDefinition("U", KeyCode.U, "U", 6 * (keyW + gap), row1Y, keyW, keyH),
                new KeyDefinition("I", KeyCode.I, "I", 7 * (keyW + gap), row1Y, keyW, keyH),
                new KeyDefinition("O", KeyCode.O, "O", 8 * (keyW + gap), row1Y, keyW, keyH),
                new KeyDefinition("P", KeyCode.P, "P", 9 * (keyW + gap), row1Y, keyW, keyH),
                new KeyDefinition("A", KeyCode.A, "A", 0.5f * (keyW + gap), row2Y, keyW, keyH),
                new KeyDefinition("S", KeyCode.S, "S", 1.5f * (keyW + gap), row2Y, keyW, keyH),
                new KeyDefinition("D", KeyCode.D, "D", 2.5f * (keyW + gap), row2Y, keyW, keyH),
                new KeyDefinition("F", KeyCode.F, "F", 3.5f * (keyW + gap), row2Y, keyW, keyH),
                new KeyDefinition("G", KeyCode.G, "G", 4.5f * (keyW + gap), row2Y, keyW, keyH),
                new KeyDefinition("H", KeyCode.H, "H", 5.5f * (keyW + gap), row2Y, keyW, keyH),
                new KeyDefinition("J", KeyCode.J, "J", 6.5f * (keyW + gap), row2Y, keyW, keyH),
                new KeyDefinition("K", KeyCode.K, "K", 7.5f * (keyW + gap), row2Y, keyW, keyH),
                new KeyDefinition("L", KeyCode.L, "L", 8.5f * (keyW + gap), row2Y, keyW, keyH),
            };
        }

        /// <summary>
        /// 添加鼠标按键到现有布局的右侧
        /// </summary>
        public static KeyDefinition[] WithMouseButtons(this KeyDefinition[] baseLayout, bool includeMouse = true)
        {
            if (!includeMouse) return baseLayout;

            float maxRight = 0;
            foreach (var k in baseLayout)
                maxRight = Mathf.Max(maxRight, k.OffsetX + k.Width);

            float gap = 12;
            float keyW = 50, keyH = 50;
            float startY = 0;

            var mouseKeys = new KeyDefinition[]
            {
                new KeyDefinition("LMB", KeyCode.Mouse0, "LMB", maxRight + gap,                  startY,        keyW, keyH, 2),
                new KeyDefinition("RMB", KeyCode.Mouse1, "RMB", maxRight + gap + keyW + gap,     startY,        keyW, keyH, 2),
                new KeyDefinition("MMB", KeyCode.Mouse2, "MMB", maxRight + gap + (keyW + gap) * 2, startY,    keyW, keyH, 2),
            };

            var result = new KeyDefinition[baseLayout.Length + mouseKeys.Length];
            Array.Copy(baseLayout, result, baseLayout.Length);
            Array.Copy(mouseKeys, 0, result, baseLayout.Length, mouseKeys.Length);
            return result;
        }
    }
}
