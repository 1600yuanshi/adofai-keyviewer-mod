using System;
using System.Collections.Generic;
using UnityEngine;

namespace ADOFAI.AgentKeyViewer
{
    /// <summary>
    /// 按键输入捕获类，CT风格：根据布局中定义的按键动态捕获状态
    /// </summary>
    public class KeyInputCapture
    {
        private bool _isEnabled;

        // KPS统计
        private readonly Queue<float> _hitTimestamps = new Queue<float>();
        private int _currentKps;
        private float _nextKpsUpdateTime;
        private readonly HashSet<KeyCode> _countedThisFrame = new HashSet<KeyCode>();

        public int TotalHits { get; private set; }
        public int CurrentKps => _currentKps;

        public KeyInputCapture()
        {
        }

        public void Enable()
        {
            _isEnabled = true;
            ResetCounts();
            Main.ModEntry?.Logger.Log("[KeyInputCapture] Enabled (CT style)");
        }

        public void Disable()
        {
            _isEnabled = false;
            Main.ModEntry?.Logger.Log("[KeyInputCapture] Disabled");
        }

        public void ResetCounts()
        {
            TotalHits = 0;
            _hitTimestamps.Clear();
            _currentKps = 0;
            _countedThisFrame.Clear();
            _nextKpsUpdateTime = 0;
        }

        /// <summary>
        /// 每帧更新，根据布局按键捕获状态
        /// </summary>
        public void Update(List<KeyDefinition> layout)
        {
            if (!_isEnabled || !Main.Settings.ShowKeyDisplay || layout == null)
                return;

            float now = Time.time;
            _countedThisFrame.Clear();

            // 1. 轮询每个定义的按键的按下状态
            for (int i = 0; i < layout.Count; i++)
            {
                var key = layout[i];
                key.WasPressed = key.IsPressed;

                bool physicallyPressed = SafeGetKey(key.KeyCode);
                bool keyDown = SafeGetKeyDown(key.KeyCode);

                // CT风格的视觉保持：即使物理松开，仍短暂保持按下显示以应对卡顿/丢帧
                float visualHold = Main.Settings != null ? Main.Settings.VisualHoldTime : 0.05f;
                if (keyDown)
                {
                    float holdUntil = now + visualHold;
                    if (key.VisualPressedUntil < holdUntil)
                        key.VisualPressedUntil = holdUntil;
                }

                bool heldByVisual = now < key.VisualPressedUntil;
                if (!physicallyPressed && !heldByVisual)
                    key.VisualPressedUntil = 0f;

                key.IsPressed = physicallyPressed || heldByVisual;

                // 2. 处理按下事件（IsDown → 计数+动画+键雨触发标记）
                if (keyDown)
                {
                    key.KeyJustPressed = true;
                    // 防止同一帧内重复计数（多个相同KeyCode的Node）
                    if (_countedThisFrame.Add(key.KeyCode))
                    {
                        key.HitCount++;
                        TotalHits++;
                        _hitTimestamps.Enqueue(now);
                    }
                }

                // 3. 推进按下动画（0→1按下，1→0松开）
                float animDuration = Main.Settings != null ? Main.Settings.AnimDuration : 0.08f;
                float target = key.IsPressed ? 1f : 0f;
                float step = Time.unscaledDeltaTime / Mathf.Max(0.01f, animDuration);
                key.PressAnimation = Mathf.MoveTowards(key.PressAnimation, target, step);
            }

            // 4. 更新KPS（CT默认250ms刷新间隔）
            if (now >= _nextKpsUpdateTime)
            {
                float interval = 0.25f;
                float threshold = now - 1f; // 1秒窗口
                while (_hitTimestamps.Count > 0 && _hitTimestamps.Peek() < threshold)
                    _hitTimestamps.Dequeue();
                _currentKps = _hitTimestamps.Count;
                _nextKpsUpdateTime = now + interval;
            }
        }

        // 安全访问Input，防止无效KeyCode抛异常
        private static bool SafeGetKey(KeyCode keyCode)
        {
            try
            {
                // 对Mouse按钮使用专用API
                if (keyCode == KeyCode.Mouse0) return Input.GetMouseButton(0);
                if (keyCode == KeyCode.Mouse1) return Input.GetMouseButton(1);
                if (keyCode == KeyCode.Mouse2) return Input.GetMouseButton(2);
                return Input.GetKey(keyCode);
            }
            catch
            {
                return false;
            }
        }

        private static bool SafeGetKeyDown(KeyCode keyCode)
        {
            try
            {
                if (keyCode == KeyCode.Mouse0) return Input.GetMouseButtonDown(0);
                if (keyCode == KeyCode.Mouse1) return Input.GetMouseButtonDown(1);
                if (keyCode == KeyCode.Mouse2) return Input.GetMouseButtonDown(2);
                return Input.GetKeyDown(keyCode);
            }
            catch
            {
                return false;
            }
        }
    }

    /// <summary>
    /// 按键扫描器：用于"录制模式"——用户按下任意键后，系统识别出是哪个键。
    /// 覆盖字母、数字、符号、功能键、鼠标、方向键等常用键。
    /// </summary>
    public static class KeyScan
    {
        /// <summary>录制模式下扫描的键集合（按重要性排序）</summary>
        public static readonly KeyCode[] CommonKeys =
        {
            // 字母
            KeyCode.A, KeyCode.B, KeyCode.C, KeyCode.D, KeyCode.E, KeyCode.F, KeyCode.G,
            KeyCode.H, KeyCode.I, KeyCode.J, KeyCode.K, KeyCode.L, KeyCode.M, KeyCode.N,
            KeyCode.O, KeyCode.P, KeyCode.Q, KeyCode.R, KeyCode.S, KeyCode.T, KeyCode.U,
            KeyCode.V, KeyCode.W, KeyCode.X, KeyCode.Y, KeyCode.Z,
            // 数字
            KeyCode.Alpha0, KeyCode.Alpha1, KeyCode.Alpha2, KeyCode.Alpha3, KeyCode.Alpha4,
            KeyCode.Alpha5, KeyCode.Alpha6, KeyCode.Alpha7, KeyCode.Alpha8, KeyCode.Alpha9,
            // 符号键（Unity KeyCode 用名称表示）
            KeyCode.BackQuote, KeyCode.Minus, KeyCode.Equals, KeyCode.LeftBracket, KeyCode.RightBracket,
            KeyCode.Backslash, KeyCode.Semicolon, KeyCode.Quote, KeyCode.Comma, KeyCode.Period,
            KeyCode.Slash,
            // 功能/导航键
            KeyCode.F1, KeyCode.F2, KeyCode.F3, KeyCode.F4, KeyCode.F5, KeyCode.F6,
            KeyCode.F7, KeyCode.F8, KeyCode.F9, KeyCode.F10, KeyCode.F11, KeyCode.F12,
            KeyCode.Space, KeyCode.Return, KeyCode.Tab, KeyCode.Backspace, KeyCode.Delete, KeyCode.Escape,
            KeyCode.Home, KeyCode.End, KeyCode.Insert, KeyCode.PageUp, KeyCode.PageDown,
            KeyCode.LeftArrow, KeyCode.RightArrow, KeyCode.UpArrow, KeyCode.DownArrow,
            // 修饰键
            KeyCode.LeftShift, KeyCode.RightShift, KeyCode.LeftControl, KeyCode.RightControl,
            KeyCode.LeftAlt, KeyCode.RightAlt, KeyCode.LeftCommand, KeyCode.RightCommand,
            // 鼠标
            KeyCode.Mouse0, KeyCode.Mouse1, KeyCode.Mouse2, KeyCode.Mouse3, KeyCode.Mouse4,
        };

        /// <summary>
        /// 尝试获取本帧用户按下的键（仅检测 CommonKeys 中的键）
        /// </summary>
        public static bool TryGetPressedKey(out KeyCode pressed)
        {
            try
            {
                if (!Input.anyKeyDown)
                {
                    pressed = KeyCode.None;
                    return false;
                }
                for (int i = 0; i < CommonKeys.Length; i++)
                {
                    var k = CommonKeys[i];
                    bool down;
                    try
                    {
                        if (k == KeyCode.Mouse0) down = Input.GetMouseButtonDown(0);
                        else if (k == KeyCode.Mouse1) down = Input.GetMouseButtonDown(1);
                        else if (k == KeyCode.Mouse2) down = Input.GetMouseButtonDown(2);
                        else if (k == KeyCode.Mouse3) down = Input.GetMouseButtonDown(3);
                        else if (k == KeyCode.Mouse4) down = Input.GetMouseButtonDown(4);
                        else down = Input.GetKeyDown(k);
                    }
                    catch { down = false; }

                    if (down)
                    {
                        pressed = k;
                        return true;
                    }
                }
            }
            catch { /* ignore */ }
            pressed = KeyCode.None;
            return false;
        }

        /// <summary>把KeyCode转换成带符号的友好显示名（例如 Semicolon → ";(Semicolon)"）</summary>
        public static string ToFriendlyName(KeyCode code)
        {
            switch (code)
            {
                case KeyCode.Semicolon: return "; (Semicolon)";
                case KeyCode.Comma: return ", (Comma)";
                case KeyCode.Period: return ". (Period)";
                case KeyCode.Slash: return "/ (Slash)";
                case KeyCode.Backslash: return @"\ (Backslash)";
                case KeyCode.LeftBracket: return "[ (LeftBracket)";
                case KeyCode.RightBracket: return "] (RightBracket)";
                case KeyCode.Minus: return "- (Minus)";
                case KeyCode.Equals: return "= (Equals)";
                case KeyCode.BackQuote: return "` (BackQuote)";
                case KeyCode.Quote: return "' (Quote)";
                case KeyCode.Mouse0: return "鼠标左键 (LMB)";
                case KeyCode.Mouse1: return "鼠标右键 (RMB)";
                case KeyCode.Mouse2: return "鼠标中键 (MMB)";
                case KeyCode.Mouse3: return "鼠标侧键1";
                case KeyCode.Mouse4: return "鼠标侧键2";
                case KeyCode.Space: return "空格 (Space)";
                case KeyCode.Return: return "回车 (Enter)";
                case KeyCode.LeftArrow: return "← (Left)";
                case KeyCode.RightArrow: return "→ (Right)";
                case KeyCode.UpArrow: return "↑ (Up)";
                case KeyCode.DownArrow: return "↓ (Down)";
                default: return code.ToString();
            }
        }
    }
}
