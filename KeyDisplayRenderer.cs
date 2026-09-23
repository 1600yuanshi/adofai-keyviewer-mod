using System;
using System.Collections.Generic;
using UnityEngine;

namespace ADOFAI.AgentKeyViewer
{
    /// <summary>
    /// CT风格 + FreeMake 的按键显示渲染器：
    /// - 固定按键框 + 按下高亮缩放动画 + KPS/Total计数
    /// - 每个键可自定义颜色/位置/尺寸/标签/按键码
    /// - 键雨（Rain）：按下按键时从键位生成雨条，按住持续增长，松开后向上飞行消失，支持两排/单键自定义
    /// - 显示面板支持按住拖动移动位置
    /// </summary>
    public class KeyDisplayRenderer
    {
        private bool _isEnabled;
        private GUIStyle _keyLabelStyle;
        private GUIStyle _keyCountStyle;
        private GUIStyle _statLabelStyle;
        private GUIStyle _statValueStyle;
        private GUIStyle _dragHintStyle;
        private Texture2D _boxBgTexture;
        private Texture2D _shadowTexture;

        // 面板拖动（Drag to move position）
        private bool _isDraggingPanel;
        private Vector2 _dragStartMouse;
        private Vector2 _dragStartPosition;

        // 当前布局列表
        public List<KeyDefinition> CurrentLayout { get; private set; }

        // ===== 键雨（Rain）=====
        private readonly List<RainDrop> _rainDrops = new List<RainDrop>();

        // ===== GIF 动画播放状态（按下播放，空闲显示首帧）=====
        private class GifAnimState { public int frameIndex; public float timer; }
        private readonly Dictionary<string, GifAnimState> _gifStates = new Dictionary<string, GifAnimState>();

        public KeyDisplayRenderer() { }

        public void Enable()
        {
            _isEnabled = true;
            // 注意：不要在这里调用 EnsureStyles() —— 它访问 GUI.skin，
            // 在 OnToggle（OnGUI 之外）调用会抛 "You can only call GUI functions from inside OnGUI"。
            // 样式统一在 OnGUI() 开头懒创建。
            RebuildLayout();
            CoreEntry.ModEntry?.Logger.Log("[KeyDisplayRenderer] Enabled (CT+FreeMake style + Rain)");
        }

        public void Disable()
        {
            _isEnabled = false;
            _isDraggingPanel = false;
            _rainDrops.Clear();
            CoreEntry.ModEntry?.Logger.Log("[KeyDisplayRenderer] Disabled");
        }

        /// <summary>清空键雨（关闭键雨或切换布局时调用）</summary>
        public void DisableRainDrops()
        {
            _rainDrops.Clear();
        }

        /// <summary>
        /// 根据当前Settings重新生成布局（支持预设或FreeMake）
        /// </summary>
        public void RebuildLayout()
        {
            KeyDefinition[] keys;
            var s = CoreEntry.Settings;
            switch (s.LayoutType)
            {
                case 0: keys = KeyLayoutPresets.Create4KLayout(); break;
                case 1: keys = KeyLayoutPresets.Create6KLayout(); break;
                case 2: keys = KeyLayoutPresets.CreateArrowLayout(); break;
                case 3: keys = KeyLayoutPresets.Create2KLayout(); break;
                case 4: keys = KeyLayoutPresets.Create10KFullLayout(); break;
                case 5: // FreeMake自定义
                    if (s.CustomKeys == null || s.CustomKeys.Count == 0)
                    {
                        // 用户还没配置自定义布局 -> 提供一个4K作为起点
                        keys = KeyLayoutPresets.Create4KLayout();
                        s.CustomKeys = new List<KeyDefinition>(keys);
                    }
                    else
                    {
                        keys = s.CustomKeys.ToArray();
                    }
                    break;
                default:
                    keys = KeyLayoutPresets.Create4KLayout();
                    break;
            }

            // 自定义布局不强制附加鼠标键（用户自己决定）
            if (s.LayoutType != 5 && s.IncludeMouse)
                keys = keys.WithMouseButtons(true);

            // 预设布局：用「按键默认颜色」初始化新建的键（保证颜色设置一定生效并持久）
            if (s.LayoutType != 5)
            {
                float maxY = 0;
                foreach (var k in keys) maxY = Mathf.Max(maxY, k.OffsetY + k.Height);
                float rowThreshold = maxY * 0.25f; // 底部那排键归为第1排（两排键雨用）
                for (int i = 0; i < keys.Length; i++)
                {
                    keys[i].IdleColor = s.KeyIdleColor;
                    keys[i].PressedColor = s.KeyPressedColor;
                    keys[i].BorderColor = s.KeyBorderColor;
                    keys[i].TextColor = s.KeyTextColor;
                    keys[i].PressedTextColor = s.KeyPressedTextColor;
                    keys[i].RainRow = keys[i].OffsetY > rowThreshold ? 1 : 0;
                }
            }

            CurrentLayout = new List<KeyDefinition>(keys);
            _rainDrops.Clear();
            CoreEntry.ModEntry?.Logger.Log($"[KeyDisplayRenderer] Layout rebuilt: layoutType={s.LayoutType}, keys={CurrentLayout.Count}");
        }

        /// <summary>把当前CustomKeys同步回CurrentLayout，不丢失运行时hitCount等</summary>
        public void SyncCustomKeysToLayout()
        {
            if (CoreEntry.Settings.LayoutType != 5 || CoreEntry.Settings.CustomKeys == null) return;
            // 保留每个已存在键的HitCount / Animation状态
            var oldHits = new Dictionary<string, int>();
            if (CurrentLayout != null)
                foreach (var k in CurrentLayout) oldHits[k.Id] = k.HitCount;

            CurrentLayout = new List<KeyDefinition>(CoreEntry.Settings.CustomKeys);
            foreach (var k in CurrentLayout)
                if (oldHits.TryGetValue(k.Id, out var h)) k.HitCount = h;
        }

        private void EnsureStyles()
        {
            if (_boxBgTexture == null)
            {
                _boxBgTexture = new Texture2D(1, 1, TextureFormat.RGBA32, false);
                _boxBgTexture.SetPixel(0, 0, Color.white);
                _boxBgTexture.Apply();
            }
            if (_shadowTexture == null)
            {
                _shadowTexture = new Texture2D(1, 1, TextureFormat.RGBA32, false);
                _shadowTexture.SetPixel(0, 0, new Color(0, 0, 0, 0.5f));
                _shadowTexture.Apply();
            }
            _keyLabelStyle ??= new GUIStyle(GUI.skin.label)
            {
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleCenter,
                normal = { textColor = Color.white }
            };
            _keyCountStyle ??= new GUIStyle(GUI.skin.label)
            {
                fontStyle = FontStyle.Normal,
                alignment = TextAnchor.LowerRight,
                normal = { textColor = new Color(1, 1, 1, 0.85f) }
            };
            _statLabelStyle ??= new GUIStyle(GUI.skin.label)
            {
                fontStyle = FontStyle.Normal,
                alignment = TextAnchor.UpperCenter,
                normal = { textColor = new Color(0.9f, 0.9f, 0.95f, 0.9f) }
            };
            _statValueStyle ??= new GUIStyle(GUI.skin.label)
            {
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.LowerCenter,
                normal = { textColor = Color.white }
            };
            _dragHintStyle ??= new GUIStyle(GUI.skin.label)
            {
                alignment = TextAnchor.MiddleCenter,
                fontSize = 10,
                normal = { textColor = new Color(1, 1, 1, 0.75f) }
            };
        }

        /// <summary>
        /// 每帧更新：推进键雨动画 + 触发新键雨
        /// </summary>
        public void Update()
        {
            if (!_isEnabled || !CoreEntry.Settings.ShowKeyDisplay) return;
            if (CurrentLayout == null) return;

            float originX = CoreEntry.Settings.DisplayX;
            float originY = CoreEntry.Settings.DisplayY;
            float scale = CoreEntry.Settings.Scale;

            // 1. 键雨（CT风格）：按下→生成，按住→持续增长，松开→停止增长并向上飞行
            //    图片模式键（DisplayMode==1）不生成键雨，只显示图片。
            if (CoreEntry.Settings.EnableRain)
            {
                for (int i = 0; i < CurrentLayout.Count; i++)
                {
                    var key = CurrentLayout[i];
                    key.KeyJustPressed = false;
                    if (key.DisplayMode == 1) continue; // 图片模式：无键雨
                    if (key.IsPressed)
                    {
                        // 按住中但没有对应增长中的雨条 → 按下边沿，新建一条
                        if (FindGrowingRain(key) == null)
                        {
                            var drop = CreateRainDrop(key, originX, originY, scale);
                            if (drop != null) _rainDrops.Add(drop);
                        }
                    }
                    else
                    {
                        // 松开：停止增长，让雨条自行飞行直至消失
                        var trail = FindGrowingRain(key);
                        if (trail != null) trail.growing = false;
                    }
                }
            }
            else
            {
                for (int i = 0; i < CurrentLayout.Count; i++)
                    CurrentLayout[i].KeyJustPressed = false;
            }

            // 2. 推进既有键雨
            UpdateRain();

            // 3. 推进键背景图片的 GIF 动画（按下播放，空闲回退到首帧）
            UpdateGifAnimations(Time.unscaledDeltaTime);
        }

        /// <summary>
        /// 推进 GIF 动画：按下时按帧延迟推进，空闲时回到首帧。
        /// 状态按 "键ID|图片文件名" 区分，使用完毕后自动清理，避免内存堆积。
        /// </summary>
        private void UpdateGifAnimations(float dt)
        {
            if (!CoreEntry.Settings.EnableKeyImages) return;
            if (CurrentLayout == null || CurrentLayout.Count == 0) return;

            var active = new HashSet<string>();
            for (int i = 0; i < CurrentLayout.Count; i++)
            {
                var key = CurrentLayout[i];
                if (!key.UseImage || string.IsNullOrEmpty(key.ImageFile)) continue;

                var loaded = KeyImageManager.GetImage(key.ImageFile);
                if (loaded == null || !loaded.isGif || loaded.gifFrames == null || loaded.gifFrames.Count == 0) continue;

                string animKey = key.Id + "|" + key.ImageFile;
                active.Add(animKey);
                if (!_gifStates.TryGetValue(animKey, out var st))
                {
                    st = new GifAnimState();
                    _gifStates[animKey] = st;
                }

                if (key.IsPressed && loaded.gifFrames.Count > 1)
                {
                    // 按下：按帧延迟推进（循环播放）
                    st.timer += dt;
                    while (st.timer >= loaded.gifFrames[st.frameIndex].delaySec)
                    {
                        st.timer -= loaded.gifFrames[st.frameIndex].delaySec;
                        st.frameIndex = (st.frameIndex + 1) % loaded.gifFrames.Count;
                    }
                }
                else
                {
                    // 空闲：显示首帧
                    st.frameIndex = 0;
                    st.timer = 0f;
                }
            }

            // 清理不再使用的 GIF 状态
            if (_gifStates.Count > active.Count)
            {
                var stale = new List<string>();
                foreach (var kv in _gifStates)
                    if (!active.Contains(kv.Key)) stale.Add(kv.Key);
                foreach (var s in stale) _gifStates.Remove(s);
            }
        }

        /// <summary>
        /// OnGUI - CT+FreeMake 风格主渲染
        /// </summary>
        public void OnGUI()
        {
            if (!_isEnabled || !CoreEntry.Settings.ShowKeyDisplay) return;
            if (CurrentLayout == null || CurrentLayout.Count == 0) RebuildLayout();
            EnsureStyles();

            float originX = CoreEntry.Settings.DisplayX;
            float originY = CoreEntry.Settings.DisplayY;
            float scale = CoreEntry.Settings.Scale;
            float opacity = CoreEntry.Settings.Opacity;
            float padding = CoreEntry.Settings.PanelPadding * scale;

            // 1. 计算面板整体包围盒（用于面板拖拽命中 + KPS定位）
            float layoutMaxX = 0, layoutMaxY = 0;
            foreach (var key in CurrentLayout)
            {
                float right = key.OffsetX + key.Width;
                float bottom = key.OffsetY + key.Height;
                if (right > layoutMaxX) layoutMaxX = right;
                if (bottom > layoutMaxY) layoutMaxY = bottom;
            }
            float totalLayoutW = layoutMaxX * scale;
            float totalLayoutH = layoutMaxY * scale;

            float statsW = 0, statsGap = 0;
            if (CoreEntry.Settings.ShowKpsTotal)
            {
                statsW = CoreEntry.Settings.StatsBoxWidth * scale;
                statsGap = 12 * scale;
            }
            float statsH = CoreEntry.Settings.StatsBoxHeight * scale;
            float panelTotalW = totalLayoutW + statsW + (statsW > 0 ? statsGap : 0) + padding * 2;
            float panelTotalH = Mathf.Max(totalLayoutH, (statsH * 2 + 8 * scale)) + padding * 2;

            Rect panelRect = new Rect(originX - padding, originY - padding, panelTotalW, panelTotalH);

            // 2. 处理面板拖动（Drag to move）
            if (CoreEntry.Settings.EnablePanelDrag)
            {
                Event e = Event.current;
                if (e.isMouse)
                {
                    if (e.type == EventType.MouseDown && e.button == 0 && panelRect.Contains(e.mousePosition))
                    {
                        _isDraggingPanel = true;
                        _dragStartMouse = e.mousePosition;
                        _dragStartPosition = new Vector2(CoreEntry.Settings.DisplayX, CoreEntry.Settings.DisplayY);
                        e.Use();
                    }
                    else if (e.type == EventType.MouseUp && e.button == 0)
                    {
                        _isDraggingPanel = false;
                    }
                    else if (_isDraggingPanel && e.type == EventType.MouseDrag)
                    {
                        Vector2 delta = e.mousePosition - _dragStartMouse;
                        CoreEntry.Settings.DisplayX = Mathf.Clamp(_dragStartPosition.x + delta.x, 0, Math.Max(0, Screen.width - 50));
                        CoreEntry.Settings.DisplayY = Mathf.Clamp(_dragStartPosition.y + delta.y, 0, Math.Max(0, Screen.height - 50));
                        e.Use();
                    }
                }
            }

            // 3. 可选：整个面板背景（半透明圆角），帮助拖动时可见
            Color panelBg = CoreEntry.Settings.PanelBgColor;
            panelBg.a *= opacity;
            GUI.color = panelBg;
            if (CoreEntry.Settings.EnableRoundedCorners)
            {
                GUI.DrawTexture(panelRect, RoundedRectFactory.GetFilled(
                    Mathf.Max(1, Mathf.RoundToInt(panelTotalW)),
                    Mathf.Max(1, Mathf.RoundToInt(panelTotalH)),
                    Mathf.RoundToInt(CoreEntry.Settings.PanelCornerRadius)));
            }
            else
            {
                GUI.DrawTexture(panelRect, _boxBgTexture);
            }
            GUI.color = Color.white;

            // 拖动小提示
            if (_isDraggingPanel && CoreEntry.Settings.ShowDragHint)
            {
                GUI.color = new Color(1, 1, 1, opacity);
                GUI.Label(panelRect, "拖动中... 松开鼠标结束", _dragHintStyle);
                GUI.color = Color.white;
            }

            // 4. 绘制所有按键（使用每个KeyDefinition自己的颜色配置）
            Color prevGuiColor = GUI.color;
            for (int i = 0; i < CurrentLayout.Count; i++)
            {
                DrawKeyBox(CurrentLayout[i], originX, originY, scale, opacity);
            }
            GUI.color = prevGuiColor;

            // 5. 绘制键雨（在按键上方）
            DrawRain(opacity);

            // 6. 绘制KPS和Total统计框
            if (CoreEntry.Settings.ShowKpsTotal)
            {
                DrawKpsTotalPanel(originX + totalLayoutW + statsGap, originY, scale, opacity);
            }
        }

        /// <summary>
        /// 绘制单个按键框。
        /// 显示模式（DisplayMode）：
        ///   0 = 键模式：维持原样（阴影 + 每键自定义颜色 + 可选背景图片 + 按下缩放/高亮 + 标签 + 计数 + GIF）
        ///   1 = 图片模式：只显示图片（JPG/PNG/GIF），隐藏 key/rain/按键反馈。
        /// 圆角（CT语义）：整键背景+边框一起圆角，CornerRadius / BorderThickness 均为 0 时跟随全局。
        /// </summary>
        private void DrawKeyBox(KeyDefinition key, float originX, float originY, float scale, float opacity)
        {
            var s = CoreEntry.Settings;
            float anim = Mathf.Clamp01(key.PressAnimation);
            bool imageMode = key.DisplayMode == 1;

            // 图片模式：无按下外扩（不显示按键反馈），键框为原始尺寸
            float extraSize = imageMode ? 0f : anim * s.PressExpandSize * scale;

            float baseX = originX + key.OffsetX * scale;
            float baseY = originY + key.OffsetY * scale;
            float w = key.Width * scale;
            float h = key.Height * scale;

            float rectX = baseX - extraSize * 0.5f;
            float rectY = baseY - extraSize * 0.5f;
            float rectW = w + extraSize;
            float rectH = h + extraSize;

            Rect keyRect = new Rect(rectX, rectY, rectW, rectH);

            bool rounded = s.EnableRoundedCorners;
            // 圆角半径：单键设置 > 全局（0=跟随全局；单位 px，随缩放缩放）
            int radius = Mathf.RoundToInt((key.CornerRadius > 0f ? key.CornerRadius : s.KeyCornerRadius) * scale);
            // 边框粗细：单键设置 > 全局（0=跟随全局）
            float bwPx = key.BorderThickness > 0f ? key.BorderThickness : s.BorderThickness;
            float bw = Mathf.Max(1, bwPx * scale);
            int texW = Mathf.Max(1, Mathf.RoundToInt(rectW));
            int texH = Mathf.Max(1, Mathf.RoundToInt(rectH));

            // ---- 解析背景图片（JPG/PNG/GIF；GIF：按下播放动画，空闲显示首帧）----
            Texture2D imgTex = null;
            int imgW = 0, imgH = 0;
            if (s.EnableKeyImages && key.UseImage && !string.IsNullOrEmpty(key.ImageFile))
                ResolveKeyImage(key, out imgTex, out imgW, out imgH);

            // ================= 图片模式：只显示图片 =================
            if (imageMode)
            {
                if (imgTex != null)
                {
                    Rect drawRect = KeyImageManager.ComputeDrawRect(keyRect, imgW, imgH, s.KeyImageFit);
                    GUI.BeginClip(keyRect);
                    try
                    {
                        Rect localDraw = new Rect(drawRect.x - keyRect.x, drawRect.y - keyRect.y,
                                                  drawRect.width, drawRect.height);
                        GUI.color = new Color(1f, 1f, 1f, Mathf.Clamp01(key.ImageOpacity * opacity));
                        GUI.DrawTexture(localDraw, imgTex);
                    }
                    finally
                    {
                        GUI.EndClip();
                    }
                }
                GUI.color = Color.white;
                return; // 图片模式：不画键框、标签、计数、键雨、按键反馈
            }

            // ================= 键模式：维持原样 =================

            // 1. 阴影
            float shadowAlpha = (0.35f + anim * 0.35f) * opacity;
            Rect shadowRect = new Rect(rectX + 3, rectY + 4, rectW, rectH);
            GUI.color = new Color(0, 0, 0, shadowAlpha);
            if (rounded)
                GUI.DrawTexture(shadowRect, RoundedRectFactory.GetFilled(texW, texH, radius));
            else
                GUI.DrawTexture(shadowRect, _shadowTexture);

            // 2. 颜色（使用KeyDefinition自定义色）
            Color bgColor = key.IsPressed ? (Color)key.PressedColor : (Color)key.IdleColor;
            Color borderColor = (Color)key.BorderColor;
            Color textColor = key.IsPressed ? (Color)key.PressedTextColor : (Color)key.TextColor;
            // 按下时边框也略微提亮
            if (key.IsPressed)
            {
                borderColor.r = Mathf.Clamp01(borderColor.r + 0.35f);
                borderColor.g = Mathf.Clamp01(borderColor.g + 0.35f);
                borderColor.b = Mathf.Clamp01(borderColor.b + 0.35f);
            }
            bgColor.a *= opacity;
            borderColor.a *= opacity;
            textColor.a *= opacity;

            // 3. 背景填充（圆角矩形；整键圆角：背景与边框同一圆角矩形）
            GUI.color = bgColor;
            if (rounded)
                GUI.DrawTexture(keyRect, RoundedRectFactory.GetFilled(texW, texH, radius));
            else
                GUI.DrawTexture(keyRect, _boxBgTexture);

            // 4. 背景图片（覆盖在背景之上；用角落遮罩以键背景色切出圆角）
            if (imgTex != null)
            {
                DrawKeyImage(keyRect, imgTex, imgW, imgH, s.KeyImageFit,
                             rounded ? radius : 0, bgColor, key.ImageOpacity * opacity);
            }

            // 5. 边框（圆角描边环，使用单键边框粗细）
            GUI.color = borderColor;
            if (rounded)
                GUI.DrawTexture(keyRect, RoundedRectFactory.GetBorder(texW, texH, radius, Mathf.RoundToInt(bw)));
            else
            {
                GUI.DrawTexture(new Rect(rectX, rectY, rectW, bw), _boxBgTexture);
                GUI.DrawTexture(new Rect(rectX, rectY + rectH - bw, rectW, bw), _boxBgTexture);
                GUI.DrawTexture(new Rect(rectX, rectY, bw, rectH), _boxBgTexture);
                GUI.DrawTexture(new Rect(rectX + rectW - bw, rectY, bw, rectH), _boxBgTexture);
            }

            // 6. 标签文字
            int fontSize = key.FontSize > 0 ? Mathf.RoundToInt(key.FontSize * scale)
                                             : Mathf.RoundToInt(Mathf.Min(rectW, rectH) * s.KeyFontRatio);
            if (fontSize < 10) fontSize = 10;
            _keyLabelStyle.fontSize = fontSize;
            _keyLabelStyle.normal.textColor = textColor;
            GUI.Label(keyRect, key.Label, _keyLabelStyle);

            // 7. 单键命中次数
            if (key.HitCount > 0 && s.ShowPerKeyCount)
            {
                Color perKeyCount = s.PerKeyCountColor;
                int countFontSize = Mathf.RoundToInt(fontSize * s.HitCountFontRatio);
                if (countFontSize < 10) countFontSize = 10;
                _keyCountStyle.fontSize = countFontSize;
                _keyCountStyle.normal.textColor = new Color(perKeyCount.r, perKeyCount.g, perKeyCount.b,
                                                             perKeyCount.a * opacity);
                Rect countRect = new Rect(rectX + 2, rectY + 2, rectW - 4, rectH - 4);
                GUI.Label(countRect, key.HitCount.ToString(), _keyCountStyle);
            }
        }

        /// <summary>解析某键的背景图片纹理（GIF 按下取当前播放帧，空闲取首帧）。</summary>
        private void ResolveKeyImage(KeyDefinition key, out Texture2D tex, out int w, out int h)
        {
            tex = null; w = 0; h = 0;
            var loaded = KeyImageManager.GetImage(key.ImageFile);
            if (loaded == null) return;

            if (loaded.isGif && loaded.gifFrames != null && loaded.gifFrames.Count > 0)
            {
                int frameIdx = 0;
                if (key.IsPressed && _gifStates.TryGetValue(key.Id + "|" + key.ImageFile, out var gst))
                    frameIdx = Mathf.Clamp(gst.frameIndex, 0, loaded.gifFrames.Count - 1);
                var frame = loaded.gifFrames[frameIdx];
                tex = frame.texture;
                if (tex != null) { w = tex.width; h = tex.height; }
            }
            else if (loaded.staticTexture != null)
            {
                tex = loaded.staticTexture;
                w = tex.width; h = tex.height;
            }
        }

        /// <summary>
        /// 在键内绘制背景图片并按需"切"出圆角：
        /// - 先按缩放模式计算绘制矩形并绘制图片（用 BeginClip 裁剪到键内，避免"填充"模式溢出）。
        /// - 若启用圆角且图片覆盖到键的直角角落，用"角落遮罩"以键背景色覆盖图片直角，形成圆角外观。
        /// </summary>
        private void DrawKeyImage(Rect keyRect, Texture2D tex, int texW, int texH,
                                  int fit, int radius, Color bgColor, float imageAlpha)
        {
            Rect drawRect = KeyImageManager.ComputeDrawRect(keyRect, texW, texH, fit);

            // 1. 绘制图片（裁剪到键范围，防止"填充"模式溢出到相邻键）
            GUI.BeginClip(keyRect);
            try
            {
                Rect localDraw = new Rect(drawRect.x - keyRect.x, drawRect.y - keyRect.y,
                                          drawRect.width, drawRect.height);
                GUI.color = new Color(1f, 1f, 1f, Mathf.Clamp01(imageAlpha));
                GUI.DrawTexture(localDraw, tex);
            }
            finally
            {
                GUI.EndClip();
            }

            // 2. 圆角遮罩：仅当图片实际铺到键边缘（角落会露出直角）时才需要
            if (radius > 0)
            {
                bool imageTouchesCorner =
                    fit != 1 || // 拉伸/填充都会铺满或超出
                    (drawRect.x <= keyRect.x + 0.5f && drawRect.y <= keyRect.y + 0.5f &&
                     drawRect.xMax >= keyRect.xMax - 0.5f && drawRect.yMax >= keyRect.yMax - 0.5f);
                if (imageTouchesCorner)
                {
                    var mask = RoundedRectFactory.GetCornerMask(
                        Mathf.Max(1, Mathf.RoundToInt(keyRect.width)),
                        Mathf.Max(1, Mathf.RoundToInt(keyRect.height)), radius);
                    // 角落用键背景色（不透明）覆盖，形成干净的圆角
                    GUI.color = new Color(bgColor.r, bgColor.g, bgColor.b, 1f);
                    GUI.DrawTexture(keyRect, mask);
                }
            }
        }

        /// <summary>
        /// 绘制KPS / Total面板（使用全局颜色配置）
        /// </summary>
        private void DrawKpsTotalPanel(float x, float y, float scale, float opacity)
        {
            var s = CoreEntry.Settings;
            float boxW = s.StatsBoxWidth * scale;
            float boxH = s.StatsBoxHeight * scale;
            float gap = 8 * scale;

            DrawStatBox(x, y, boxW, boxH, opacity, "KPS",
                        CoreEntry.InputCapture != null ? CoreEntry.InputCapture.CurrentKps.ToString() : "0",
                        (Color)s.KpsTextColor);

            DrawStatBox(x, y + boxH + gap, boxW, boxH, opacity, "Total",
                        CoreEntry.InputCapture != null ? CoreEntry.InputCapture.TotalHits.ToString() : "0",
                        (Color)s.TotalTextColor);
        }

        private void DrawStatBox(float x, float y, float w, float h, float opacity,
                                 string label, string value, Color valueColor)
        {
            var s = CoreEntry.Settings;
            bool rounded = s.EnableRoundedCorners;
            int radius = Mathf.RoundToInt(s.StatsCornerRadius);
            int rw = Mathf.Max(1, Mathf.RoundToInt(w));
            int rh = Mathf.Max(1, Mathf.RoundToInt(h));

            // 阴影
            GUI.color = new Color(0, 0, 0, 0.45f * opacity);
            if (rounded)
                GUI.DrawTexture(new Rect(x + 2, y + 3, w, h), RoundedRectFactory.GetFilled(rw, rh, radius));
            else
                GUI.DrawTexture(new Rect(x + 2, y + 3, w, h), _shadowTexture);

            // 背景
            Color bg = s.PanelBgColor;
            bg.a = 0.92f * opacity;
            GUI.color = bg;
            if (rounded)
                GUI.DrawTexture(new Rect(x, y, w, h), RoundedRectFactory.GetFilled(rw, rh, radius));
            else
                GUI.DrawTexture(new Rect(x, y, w, h), _boxBgTexture);

            // 边框
            float bw = 2;
            GUI.color = new Color(0.32f, 0.32f, 0.38f, opacity);
            if (rounded)
            {
                GUI.DrawTexture(new Rect(x, y, w, h), RoundedRectFactory.GetBorder(rw, rh, radius, (int)bw));
            }
            else
            {
                GUI.DrawTexture(new Rect(x, y, w, bw), _boxBgTexture);
                GUI.DrawTexture(new Rect(x, y + h - bw, w, bw), _boxBgTexture);
                GUI.DrawTexture(new Rect(x, y, bw, h), _boxBgTexture);
                GUI.DrawTexture(new Rect(x + w - bw, y, bw, h), _boxBgTexture);
            }

            // Label
            int labelFontSize = Mathf.RoundToInt(h * s.StatLabelFontRatio);
            if (labelFontSize < 10) labelFontSize = 10;
            _statLabelStyle.fontSize = labelFontSize;
            _statLabelStyle.normal.textColor = new Color(0.85f, 0.85f, 0.9f, 0.9f * opacity);
            GUI.Label(new Rect(x, y + 2, w, h * 0.4f), label, _statLabelStyle);

            // Value
            int valueFontSize = Mathf.RoundToInt(h * s.StatValueFontRatio);
            if (valueFontSize < 12) valueFontSize = 12;
            _statValueStyle.fontSize = valueFontSize;
            _statValueStyle.normal.textColor = new Color(valueColor.r, valueColor.g, valueColor.b,
                                                         valueColor.a * opacity);
            GUI.Label(new Rect(x, y + h * 0.05f, w, h * 0.85f), value, _statValueStyle);
        }

        // ====================================================================
        //  键雨（Rain）实现
        //  CT风格：按下按键时从键位生成一条雨条，按住期间长度持续增长，
        //  松开后停止增长并向上飞行，直到消失。
        // ====================================================================

        private class RainDrop
        {
            public KeyDefinition owner; // 属于哪个键（用于按住时增长/松开时停止）
            public float x;             // 雨条左边缘（屏幕坐标）
            public float width;
            public float length;        // 当前长度（按下后随按住持续增长）
            public float y;             // 雨条底部（屏幕坐标；按住时锚定在键顶，松开后上移）
            public Color color;         // 基色（含alpha）
            public float baseAlpha;
            public float alpha;         // 当前alpha
            public float speed;         // 上升速度 px/s（松开后）
            public float maxTravel;     // 消失距离 px（同时也是长度上限）
            public float traveled;      // 松开后已飞行距离
            public int fadeMode;        // 0=高度裁剪，1=羽化透明
            public float ceilingY;      // 高度裁剪的上边界
            public bool growing;        // 是否仍在增长（按键按住中）
            public float growRate;      // 增长速度 px/s（按住时）
            public float anchorY;       // 按住时的锚点（键顶）
        }

        /// <summary>查找某个键当前正在增长中的雨条</summary>
        private RainDrop FindGrowingRain(KeyDefinition key)
        {
            for (int i = 0; i < _rainDrops.Count; i++)
            {
                var d = _rainDrops[i];
                if (d.growing && ReferenceEquals(d.owner, key)) return d;
            }
            return null;
        }

        /// <summary>生成一个键雨条（按下时调用）</summary>
        private RainDrop CreateRainDrop(KeyDefinition key, float originX, float originY, float scale)
        {
            var s = CoreEntry.Settings;
            if (!s.EnableRain) return null;

            // 解析该键的键雨参数：单键覆盖 > 排数设置 > 全局
            float speed = s.RainSpeed;
            float distance = s.RainDistance;
            SerializableColor color = s.RainColor;
            float widthRatio = s.RainWidthRatio;
            float heightOffset = s.RainHeightOffset;
            float growRate = s.RainGrowSpeed;
            float widthPx = s.RainWidthPx;

            if (s.EnableTwoRowRain && key.RainRow == 1)
            {
                speed = s.RainRow1Speed;
                distance = s.RainRow1Distance;
                color = s.RainRow1Color;
                widthRatio = s.RainRow1WidthRatio;
                heightOffset = s.RainRow1HeightOffset;
                growRate = s.RainRow1GrowSpeed;
                widthPx = s.RainRow1WidthPx;
            }
            if (key.UseCustomRain)
            {
                color = key.RainColor;
                widthRatio = key.RainWidthRatio;
                heightOffset = key.RainHeightOffset;
            }

            float baseX = originX + key.OffsetX * scale;
            float keyTop = originY + key.OffsetY * scale;
            float w = key.Width * scale;

            // 宽度：优先绝对像素（>0），否则按比例
            float barW = widthPx > 0f ? widthPx * scale : w * widthRatio;
            if (barW < 1f) barW = 1f;

            float anchorY = keyTop + heightOffset * scale;
            var drop = new RainDrop
            {
                owner = key,
                x = baseX + w * 0.5f - barW * 0.5f, // 水平居中
                y = anchorY,
                anchorY = anchorY,
                width = barW,
                length = 0f,                  // 初始长度固定为0，仅靠按住持续增长
                color = color,
                baseAlpha = Mathf.Clamp01(color.a),
                alpha = Mathf.Clamp01(color.a),
                speed = speed * scale,
                maxTravel = Mathf.Max(1, distance * scale),
                traveled = 0f,
                fadeMode = s.RainFadeMode,
                ceilingY = anchorY - distance * scale,
                growing = true,
                growRate = growRate * scale,
            };
            return drop;
        }

        /// <summary>推进键雨动画：按住增长 / 松开飞行 / 消失</summary>
        private void UpdateRain()
        {
            if (_rainDrops.Count == 0) return;
            float dt = Time.unscaledDeltaTime;

            for (int i = _rainDrops.Count - 1; i >= 0; i--)
            {
                var d = _rainDrops[i];

                if (d.growing)
                {
                    // 按住：底部锚定在键顶，长度持续增长（上限=消失距离）
                    d.y = d.anchorY;
                    d.length = Mathf.Min(d.length + d.growRate * dt, d.maxTravel);
                }
                else
                {
                    // 松开：整条向上飞行
                    float step = d.speed * dt;
                    d.y -= step;
                    d.traveled += step;
                }

                float top = d.y - d.length; // 顶部位置

                if (d.fadeMode == 1)
                {
                    // 羽化透明：按距离淡出（按住时traveled不变→保持清晰）
                    d.alpha = d.baseAlpha * Mathf.Clamp01(1f - d.traveled / d.maxTravel);
                    if (d.alpha <= 0.001f || d.traveled >= d.maxTravel)
                    {
                        _rainDrops.RemoveAt(i);
                        continue;
                    }
                }
                else
                {
                    // 高度裁剪：整条底部飞过顶边界后消失（alpha不变）
                    if (d.y < d.ceilingY)
                    {
                        _rainDrops.RemoveAt(i);
                        continue;
                    }
                }
            }

            // 性能上限：超过时丢弃最早的
            if (_rainDrops.Count > CoreEntry.Settings.RainMaxDrops)
                _rainDrops.RemoveRange(0, _rainDrops.Count - CoreEntry.Settings.RainMaxDrops);
        }

        /// <summary>绘制键雨</summary>
        private void DrawRain(float opacity)
        {
            if (_rainDrops.Count == 0) return;

            Color prev = GUI.color;
            for (int i = 0; i < _rainDrops.Count; i++)
            {
                var d = _rainDrops[i];
                float bottom = d.y;
                float top = d.y - d.length;

                if (d.fadeMode == 0)
                {
                    // 高度裁剪：只画在顶边界以下的部分
                    top = Mathf.Max(top, d.ceilingY);
                }

                float h = bottom - top;
                if (h <= 0.5f) continue;

                GUI.color = new Color(d.color.r, d.color.g, d.color.b, d.alpha * opacity);
                GUI.DrawTexture(new Rect(d.x, top, d.width, h), _boxBgTexture);
            }
            GUI.color = prev;
        }
    }
}
