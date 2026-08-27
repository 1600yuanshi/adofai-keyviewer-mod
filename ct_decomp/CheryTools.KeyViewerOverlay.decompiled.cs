using System;
using System.Collections.Generic;
using UnityEngine;

namespace CheryTools;

public class KeyViewerOverlay : MonoBehaviour
{
	private sealed class NodeRenderIds
	{
		public string BackgroundImage;

		public string Box;

		public string KeyText;

		public string CountText;

		public string KpsLabel;

		public string KpsValue;

		public string TotalLabel;

		public string TotalValue;
	}

	private sealed class TextDrawState
	{
		public string Text;

		public string FontPath;

		public float FontSize;

		public Vector2 Position;

		public Vector2 Size;

		public int Alignment;

		public uint Color;

		public bool UseGradient;

		public uint ColorTopLeft;

		public uint ColorTopRight;

		public uint ColorBottomRight;

		public uint ColorBottomLeft;

		public bool OutlineEnabled;

		public uint OutlineColor;

		public float OutlineThickness;

		public bool ShadowEnabled;

		public uint ShadowColor;

		public Vector2 ShadowOffset;

		public float ShadowSoftness;

		public int SortingOrder;
	}

	private sealed class BakedKvNode
	{
		public KVConfiguration Owner;

		public NodeRenderIds Ids;

		public float FinalScale;

		public Vector2 BasePosition;

		public Vector2 BaseSize;

		public uint BackgroundNormal;

		public uint BackgroundPressed;

		public uint BorderNormal;

		public uint BorderPressed;

		public uint TextNormal;

		public uint TextPressed;

		public bool KeyOutlineEnabled;

		public bool CountOutlineEnabled;

		public uint KeyOutlineColor;

		public uint CountOutlineColor;

		public float KeyOutlineThickness;

		public float CountOutlineThickness;

		public bool KeyShadowEnabled;

		public bool CountShadowEnabled;

		public uint KeyShadowColor;

		public uint CountShadowColor;

		public Vector2 KeyShadowOffset;

		public Vector2 CountShadowOffset;

		public float KeyShadowSoftness;

		public float CountShadowSoftness;

		public bool HideCountText;

		public KeyPressAnimationSettings Animation;

		public float BorderThickness;

		public float CornerRadius;

		public int GraphicSortingOrder;

		public int TextSortingOrder;

		public string KeyFontPath;

		public string CountFontPath;

		public string Label;

		public bool RainEnabled;

		public float RainWidthRatio;

		public uint RainBaseColor;

		public uint RainFarColor;

		public bool RainGradientEnabled;

		public bool RainHorizontalGradientEnabled;

		public int RainGradientMode;

		public uint RainHorizontalColor;

		public float RainYOffset;

		public float RainCornerRadius;

		public float RainFadeHeight;

		public float RainFadePower;

		public float RainGradientHeight;

		public float RainGradientPower;

		public bool RainShadowEnabled;

		public uint RainShadowColor;

		public Vector2 RainShadowOffset;

		public float RainShadowSoftness;

		public float RainShadowStrength;

		public int RainShadowSortingOrder;

		public int RainSortingOrder;

		public bool RainOutlineEnabled;

		public uint RainOutlineColor;

		public float RainOutlineThickness;

		public int RainOutlineSortingOrder;

		public bool ImageBaked;

		public float ImageBakedAlpha;

		public bool KeyTextBaked;

		public uint KeyTextBakedColor;

		public bool CountTextBaked;

		public uint CountTextBakedColor;

		public int CountTextBakedValue;

		public bool PairLabelBaked;
	}

	private struct ColorCorners
	{
		public uint TopLeft;

		public uint TopRight;

		public uint BottomRight;

		public uint BottomLeft;

		public static ColorCorners Solid(uint color)
		{
			ColorCorners result = default(ColorCorners);
			result.TopLeft = color;
			result.TopRight = color;
			result.BottomRight = color;
			result.BottomLeft = color;
			return result;
		}
	}

	private readonly Dictionary<KVNode, NodeRenderIds> _nodeRenderIds = new Dictionary<KVNode, NodeRenderIds>();

	private readonly List<KeyDrop> _rainRow1Buffer = new List<KeyDrop>();

	private readonly List<KeyDrop> _rainRow2Buffer = new List<KeyDrop>();

	private readonly Dictionary<string, TextDrawState> _textDrawStates = new Dictionary<string, TextDrawState>();

	private int _nextNodeRenderId = 1;

	private bool _hadVideoLastFrame;

	private readonly Dictionary<KVNode, BakedKvNode> _bakedNodes = new Dictionary<KVNode, BakedKvNode>();

	private long _kvBakeRevision = -1L;

	private int _kvBakeScreenWidth = -1;

	private int _kvBakeScreenHeight = -1;

	public static KeyViewerOverlay Instance { get; private set; }

	private void Awake()
	{
		Instance = this;
	}

	private void OnDestroy()
	{
		if ((Object)(object)Instance == (Object)(object)this)
		{
			_bakedNodes.Clear();
			Instance = null;
		}
	}

	private NodeRenderIds GetNodeRenderIds(KVNode node)
	{
		if (node == null)
		{
			return null;
		}
		if (_nodeRenderIds.TryGetValue(node, out var value))
		{
			return value;
		}
		string text = "KV_" + _nextNodeRenderId++;
		value = new NodeRenderIds
		{
			BackgroundImage = text + "_bg",
			Box = text + "_box",
			KeyText = text + "_key",
			CountText = text + "_count",
			KpsLabel = text + "_kps_label",
			KpsValue = text + "_kps_value",
			TotalLabel = text + "_total_label",
			TotalValue = text + "_total_value"
		};
		_nodeRenderIds[node] = value;
		return value;
	}

	private uint Vector4ToColor(float[] arr)
	{
		if (arr == null || arr.Length < 4)
		{
			return uint.MaxValue;
		}
		byte b = (byte)(Mathf.Clamp01(arr[0]) * 255f);
		byte b2 = (byte)(Mathf.Clamp01(arr[1]) * 255f);
		byte b3 = (byte)(Mathf.Clamp01(arr[2]) * 255f);
		return (uint)(((byte)(Mathf.Clamp01(arr[3]) * 255f) << 24) | (b3 << 16) | (b2 << 8) | b);
	}

	private uint MultiplyAlpha(uint color, float ratio)
	{
		byte num = (byte)((color >> 24) & 0xFF);
		byte b = (byte)((color >> 16) & 0xFFu);
		byte b2 = (byte)((color >> 8) & 0xFFu);
		byte b3 = (byte)(color & 0xFFu);
		return (uint)(((byte)((float)(int)num * Mathf.Clamp01(ratio)) << 24) | (b << 16) | (b2 << 8) | b3);
	}

	private uint LerpColor(uint from, uint to, float t)
	{
		t = Mathf.Clamp01(t);
		byte num = (byte)(from & 0xFF);
		byte b = (byte)((from >> 8) & 0xFFu);
		byte b2 = (byte)((from >> 16) & 0xFFu);
		byte b3 = (byte)((from >> 24) & 0xFFu);
		byte b4 = (byte)(to & 0xFFu);
		byte b5 = (byte)((to >> 8) & 0xFFu);
		byte b6 = (byte)((to >> 16) & 0xFFu);
		byte b7 = (byte)((to >> 24) & 0xFFu);
		byte b8 = (byte)Mathf.RoundToInt(Mathf.Lerp((float)(int)num, (float)(int)b4, t));
		byte b9 = (byte)Mathf.RoundToInt(Mathf.Lerp((float)(int)b, (float)(int)b5, t));
		byte b10 = (byte)Mathf.RoundToInt(Mathf.Lerp((float)(int)b2, (float)(int)b6, t));
		return (uint)(((byte)Mathf.RoundToInt(Mathf.Lerp((float)(int)b3, (float)(int)b7, t)) << 24) | (b10 << 16) | (b9 << 8) | b8);
	}

	private uint MatchAlpha(uint color, uint alphaSource)
	{
		byte num = (byte)((color >> 24) & 0xFF);
		byte b = (byte)((color >> 16) & 0xFFu);
		byte b2 = (byte)((color >> 8) & 0xFFu);
		byte b3 = (byte)(color & 0xFFu);
		float num2 = (float)((alphaSource >> 24) & 0xFFu) / 255f;
		return (uint)(((byte)Mathf.RoundToInt((float)(int)num * num2) << 24) | (b << 16) | (b2 << 8) | b3);
	}

	private ColorCorners LerpCorners(ColorCorners from, ColorCorners to, float t)
	{
		ColorCorners result = default(ColorCorners);
		result.TopLeft = LerpColor(from.TopLeft, to.TopLeft, t);
		result.TopRight = LerpColor(from.TopRight, to.TopRight, t);
		result.BottomRight = LerpColor(from.BottomRight, to.BottomRight, t);
		result.BottomLeft = LerpColor(from.BottomLeft, to.BottomLeft, t);
		return result;
	}

	private bool IsSolid(ColorCorners colors)
	{
		if (colors.TopLeft == colors.TopRight && colors.TopLeft == colors.BottomRight)
		{
			return colors.TopLeft == colors.BottomLeft;
		}
		return false;
	}

	private float GetKeyPressAnimationProgress(KVNode node, bool pressed, KeyPressAnimationSettings animationSettings)
	{
		if (node == null || !animationSettings.Enabled)
		{
			if (!pressed)
			{
				return 0f;
			}
			return 1f;
		}
		float t = (pressed ? 1f : 0f);
		if ((Object)(object)KeyViewerManager.Instance != (Object)null && KeyViewerManager.Instance.KeyPressAnimationProgress.TryGetValue(node, out var value))
		{
			t = value;
		}
		return EasingUtil.EvaluateEasing(t, animationSettings.Easing);
	}

	private void PrepareKvRuntimeBake()
	{
		long revision = OverlayRenderInvalidator.Revision;
		if (_kvBakeRevision != revision || _kvBakeScreenWidth != Screen.width || _kvBakeScreenHeight != Screen.height)
		{
			_bakedNodes.Clear();
			_kvBakeRevision = revision;
			_kvBakeScreenWidth = Screen.width;
			_kvBakeScreenHeight = Screen.height;
		}
	}

	private BakedKvNode GetBakedNode(KVConfiguration config, KVNode node, Vector2 center, float globalScale, float rounding, float configBorderThickness)
	{
		//IL_005d: Unknown result type (might be due to invalid IL or missing references)
		//IL_006d: Unknown result type (might be due to invalid IL or missing references)
		//IL_007d: Unknown result type (might be due to invalid IL or missing references)
		//IL_0082: Unknown result type (might be due to invalid IL or missing references)
		//IL_009a: Unknown result type (might be due to invalid IL or missing references)
		//IL_009f: Unknown result type (might be due to invalid IL or missing references)
		//IL_037d: Unknown result type (might be due to invalid IL or missing references)
		//IL_0382: Unknown result type (might be due to invalid IL or missing references)
		//IL_0412: Unknown result type (might be due to invalid IL or missing references)
		//IL_0417: Unknown result type (might be due to invalid IL or missing references)
		//IL_06a8: Unknown result type (might be due to invalid IL or missing references)
		//IL_06af: Unknown result type (might be due to invalid IL or missing references)
		//IL_06b4: Unknown result type (might be due to invalid IL or missing references)
		if (_bakedNodes.TryGetValue(node, out var value) && value.Owner == config)
		{
			return value;
		}
		bool useCustomColor = node.UseCustomColor;
		bool useCustomOutline = node.UseCustomOutline;
		bool useCustomShadow = node.UseCustomShadow;
		float num = globalScale * node.Scale;
		value = new BakedKvNode
		{
			Owner = config,
			Ids = GetNodeRenderIds(node),
			FinalScale = num,
			BasePosition = new Vector2(center.x + node.PositionX * globalScale, center.y + node.PositionY * globalScale),
			BaseSize = new Vector2(node.Width * num, node.Height * num),
			BackgroundNormal = Vector4ToColor(useCustomColor ? node.ColorBgNormal : config.ColorBgNormal),
			BackgroundPressed = Vector4ToColor(useCustomColor ? node.ColorBgPressed : config.ColorBgPressed),
			BorderNormal = Vector4ToColor(useCustomColor ? node.ColorBorderNormal : config.ColorBorderNormal),
			BorderPressed = Vector4ToColor(useCustomColor ? node.ColorBorderPressed : config.ColorBorderPressed),
			TextNormal = Vector4ToColor(useCustomColor ? node.ColorTextNormal : config.ColorTextNormal),
			TextPressed = Vector4ToColor(useCustomColor ? node.ColorTextPressed : config.ColorTextPressed),
			KeyOutlineEnabled = (useCustomOutline ? node.KeyTextOutlineEnabled : config.KeyTextOutlineEnabled),
			CountOutlineEnabled = (useCustomOutline ? node.CountTextOutlineEnabled : config.CountTextOutlineEnabled),
			KeyOutlineColor = (useCustomOutline ? TextStyleRenderer.ColorArrayToU32(node.KeyTextOutlineColor, 4278190080u) : TextStyleRenderer.ColorArrayToU32(config.KeyTextOutlineColor, 4278190080u)),
			CountOutlineColor = (useCustomOutline ? TextStyleRenderer.ColorArrayToU32(node.CountTextOutlineColor, 4278190080u) : TextStyleRenderer.ColorArrayToU32(config.CountTextOutlineColor, 4278190080u)),
			KeyOutlineThickness = (useCustomOutline ? node.KeyTextOutlineThickness : config.KeyTextOutlineThickness),
			CountOutlineThickness = (useCustomOutline ? node.CountTextOutlineThickness : config.CountTextOutlineThickness),
			KeyShadowEnabled = (useCustomShadow ? node.KeyTextShadowEnabled : config.KeyTextShadowEnabled),
			CountShadowEnabled = (useCustomShadow ? node.CountTextShadowEnabled : config.CountTextShadowEnabled),
			HideCountText = (config.HideCountText || node.HideCountText),
			Animation = KeyPressAnimationSettings.Resolve(config, node),
			BorderThickness = ((node.BorderThickness >= 0f) ? node.BorderThickness : configBorderThickness),
			CornerRadius = ResolveNodeCornerRadius(node, rounding, globalScale),
			GraphicSortingOrder = RenderDepth.ToSortingOrder(node.Depth, 2),
			TextSortingOrder = RenderDepth.ToSortingOrder(node.Depth, 3),
			KeyFontPath = GetKeyFontPath(config, node),
			CountFontPath = GetCountFontPath(config, node),
			Label = ((!string.IsNullOrEmpty(node.CustomText)) ? node.CustomText : ((node.NodeType == 0) ? KeyDisplayNames.GetKeySymbol(node.KeyBind) : ((node.NodeType == 1) ? "KPS" : "Total")))
		};
		if (value.KeyShadowEnabled)
		{
			value.KeyShadowColor = (useCustomShadow ? TextStyleRenderer.ColorArrayToU32(node.KeyTextShadowColor, 3003121664u) : TextStyleRenderer.ColorArrayToU32(config.KeyTextShadowColor, 3003121664u));
			float[] array = (useCustomShadow ? node.KeyTextShadowOffset : config.KeyTextShadowOffset);
			value.KeyShadowOffset = new Vector2((array != null && array.Length != 0) ? array[0] : 2f, (array != null && array.Length > 1) ? array[1] : 2f);
			value.KeyShadowSoftness = (useCustomShadow ? node.KeyTextShadowSoftness : config.KeyTextShadowSoftness);
		}
		if (value.CountShadowEnabled)
		{
			value.CountShadowColor = (useCustomShadow ? TextStyleRenderer.ColorArrayToU32(node.CountTextShadowColor, 3003121664u) : TextStyleRenderer.ColorArrayToU32(config.CountTextShadowColor, 3003121664u));
			float[] array2 = (useCustomShadow ? node.CountTextShadowOffset : config.CountTextShadowOffset);
			value.CountShadowOffset = new Vector2((array2 != null && array2.Length != 0) ? array2[0] : 2f, (array2 != null && array2.Length > 1) ? array2[1] : 2f);
			value.CountShadowSoftness = (useCustomShadow ? node.CountTextShadowSoftness : config.CountTextShadowSoftness);
		}
		bool flag = node.RainRow == 1;
		bool useCustomRain = node.UseCustomRain;
		value.RainEnabled = (useCustomRain ? node.EnableKeyRain : config.EnableKeyRain);
		value.RainWidthRatio = (useCustomRain ? node.RainWidthRatio : (flag ? config.KeyRainWidthRatio1 : config.KeyRainWidthRatio2));
		value.RainBaseColor = Vector4ToColor(useCustomRain ? node.RainColor : (flag ? config.KeyRainColorRow1 : config.KeyRainColorRow2));
		value.RainFarColor = Vector4ToColor(useCustomRain ? node.RainGradientEndColor : (flag ? config.KeyRainGradientEndColorRow1 : config.KeyRainGradientEndColorRow2));
		value.RainGradientEnabled = (useCustomRain ? node.RainGradientEnabled : config.KeyRainGradientEnabled);
		value.RainHorizontalGradientEnabled = (useCustomRain ? node.RainHorizontalGradientEnabled : config.KeyRainHorizontalGradientEnabled);
		value.RainGradientMode = (useCustomRain ? node.RainGradientMode : config.KeyRainGradientMode);
		value.RainHorizontalColor = MatchAlpha(Vector4ToColor(useCustomRain ? node.RainHorizontalGradientEndColor : (flag ? config.KeyRainHorizontalGradientEndColorRow1 : config.KeyRainHorizontalGradientEndColorRow2)), value.RainBaseColor);
		value.RainYOffset = (useCustomRain ? node.RainYOffset : (flag ? config.KeyRainYOffsetRow1 : config.KeyRainYOffsetRow2));
		value.RainCornerRadius = Mathf.Max(0f, useCustomRain ? node.RainCornerRadius : config.KeyRainCornerRadius) * globalScale;
		value.RainFadeHeight = (useCustomRain ? node.RainFadeHeight : config.KeyRainFadeHeight);
		value.RainFadePower = (useCustomRain ? node.RainFadePower : config.KeyRainFadePower);
		value.RainGradientHeight = (useCustomRain ? node.RainGradientHeight : config.KeyRainGradientHeight);
		value.RainGradientPower = (useCustomRain ? node.RainGradientPower : config.KeyRainGradientPower);
		bool useCustomRainShadow = node.UseCustomRainShadow;
		value.RainShadowEnabled = (useCustomRainShadow ? node.RainShadowEnabled : config.KeyRainShadowEnabled);
		value.RainShadowStrength = Mathf.Clamp01(useCustomRainShadow ? node.RainShadowStrength : config.KeyRainShadowStrength);
		if (value.RainShadowEnabled && value.RainShadowStrength > 0f)
		{
			value.RainShadowColor = Vector4ToColor(useCustomRainShadow ? node.RainShadowColor : config.KeyRainShadowColor);
			value.RainShadowEnabled = Alpha01(value.RainShadowColor) > 0f;
			value.RainShadowOffset = ResolvePair(useCustomRainShadow ? node.RainShadowOffset : config.KeyRainShadowOffset, 0f, 0f) * globalScale;
			value.RainShadowSoftness = Mathf.Max(0f, useCustomRainShadow ? node.RainShadowSoftness : config.KeyRainShadowSoftness) * globalScale;
		}
		value.RainShadowSortingOrder = RenderDepth.ToSortingOrder(node.Depth, 0);
		value.RainSortingOrder = RenderDepth.ToSortingOrder(node.Depth, 1);
		bool useCustomRainOutline = node.UseCustomRainOutline;
		value.RainOutlineEnabled = (useCustomRainOutline ? node.RainOutlineEnabled : config.KeyRainOutlineEnabled);
		if (value.RainOutlineEnabled && value.RainCornerRadius > 0.1f)
		{
			value.RainOutlineEnabled = false;
		}
		if (value.RainOutlineEnabled)
		{
			value.RainOutlineColor = Vector4ToColor(useCustomRainOutline ? node.RainOutlineColor : config.KeyRainOutlineColor);
			value.RainOutlineEnabled = Alpha01(value.RainOutlineColor) > 0f;
			value.RainOutlineThickness = Mathf.Max(0f, useCustomRainOutline ? node.RainOutlineThickness : config.KeyRainOutlineThickness) * globalScale;
		}
		value.RainOutlineSortingOrder = RenderDepth.ToSortingOrder(node.Depth, 1) + 1;
		_bakedNodes[node] = value;
		return value;
	}

	private static float Alpha01(uint color)
	{
		return (float)((color >> 24) & 0xFFu) / 255f;
	}

	private Vector4 ColorU32ToVector4(uint color)
	{
		//IL_0044: Unknown result type (might be due to invalid IL or missing references)
		return new Vector4((float)(color & 0xFFu) / 255f, (float)((color >> 8) & 0xFFu) / 255f, (float)((color >> 16) & 0xFFu) / 255f, (float)((color >> 24) & 0xFFu) / 255f);
	}

	private static float TextBoxHeight(float fontSize)
	{
		return Mathf.Max(1f, fontSize * 1.25f);
	}

	private static bool Approximately(float a, float b)
	{
		return Mathf.Abs(a - b) < 0.001f;
	}

	private static bool Approximately(Vector2 a, Vector2 b)
	{
		//IL_0000: Unknown result type (might be due to invalid IL or missing references)
		//IL_0006: Unknown result type (might be due to invalid IL or missing references)
		//IL_0013: Unknown result type (might be due to invalid IL or missing references)
		//IL_0019: Unknown result type (might be due to invalid IL or missing references)
		if (Approximately(a.x, b.x))
		{
			return Approximately(a.y, b.y);
		}
		return false;
	}

	private static float ResolveNodeCornerRadius(KVNode node, float defaultRadius, float globalScale)
	{
		if (node == null)
		{
			return defaultRadius;
		}
		float cornerRadius = node.CornerRadius;
		if (float.IsNaN(cornerRadius) || float.IsInfinity(cornerRadius) || cornerRadius < 0f)
		{
			return defaultRadius;
		}
		return Mathf.Max(0f, cornerRadius * globalScale);
	}

	private string GetKeyFontPath(KVConfiguration config, KVNode node)
	{
		if (node != null && !string.IsNullOrEmpty(node.KeyFontPath))
		{
			return node.KeyFontPath;
		}
		if (config != null && !string.IsNullOrEmpty(config.FontPath))
		{
			return config.FontPath;
		}
		return string.Empty;
	}

	private string GetCountFontPath(KVConfiguration config, KVNode node)
	{
		if (node != null && !string.IsNullOrEmpty(node.CountFontPath))
		{
			return node.CountFontPath;
		}
		if (config != null && !string.IsNullOrEmpty(config.FontPath))
		{
			return config.FontPath;
		}
		return string.Empty;
	}

	private void DrawText(string id, string text, string fontPath, float fontSize, Vector2 pos, Vector2 size, int alignment, uint color, bool outlineEnabled, uint outlineColor, float outlineThickness, bool shadowEnabled, uint shadowColor, Vector2 shadowOffset, float shadowSoftness, int sortingOrder)
	{
		//IL_0006: Unknown result type (might be due to invalid IL or missing references)
		//IL_0008: Unknown result type (might be due to invalid IL or missing references)
		//IL_001d: Unknown result type (might be due to invalid IL or missing references)
		DrawText(id, text, fontPath, fontSize, pos, size, alignment, ColorCorners.Solid(color), outlineEnabled, outlineColor, outlineThickness, shadowEnabled, shadowColor, shadowOffset, shadowSoftness, sortingOrder);
	}

	private void DrawText(string id, string text, string fontPath, float fontSize, Vector2 pos, Vector2 size, int alignment, ColorCorners colors, bool outlineEnabled, uint outlineColor, float outlineThickness, bool shadowEnabled, uint shadowColor, Vector2 shadowOffset, float shadowSoftness, int sortingOrder)
	{
		//IL_001d: Unknown result type (might be due to invalid IL or missing references)
		//IL_002e: Unknown result type (might be due to invalid IL or missing references)
		//IL_01bc: Unknown result type (might be due to invalid IL or missing references)
		//IL_01af: Unknown result type (might be due to invalid IL or missing references)
		//IL_01b5: Unknown result type (might be due to invalid IL or missing references)
		//IL_01c1: Unknown result type (might be due to invalid IL or missing references)
		//IL_01c8: Unknown result type (might be due to invalid IL or missing references)
		//IL_01ca: Unknown result type (might be due to invalid IL or missing references)
		//IL_01d0: Unknown result type (might be due to invalid IL or missing references)
		//IL_01da: Unknown result type (might be due to invalid IL or missing references)
		//IL_01e5: Unknown result type (might be due to invalid IL or missing references)
		//IL_01e7: Unknown result type (might be due to invalid IL or missing references)
		//IL_01f4: Unknown result type (might be due to invalid IL or missing references)
		//IL_0201: Unknown result type (might be due to invalid IL or missing references)
		//IL_020e: Unknown result type (might be due to invalid IL or missing references)
		//IL_021b: Unknown result type (might be due to invalid IL or missing references)
		//IL_025a: Unknown result type (might be due to invalid IL or missing references)
		//IL_025c: Unknown result type (might be due to invalid IL or missing references)
		//IL_0263: Unknown result type (might be due to invalid IL or missing references)
		//IL_0264: Unknown result type (might be due to invalid IL or missing references)
		//IL_02ea: Unknown result type (might be due to invalid IL or missing references)
		//IL_02ec: Unknown result type (might be due to invalid IL or missing references)
		//IL_00b0: Unknown result type (might be due to invalid IL or missing references)
		//IL_00b5: Unknown result type (might be due to invalid IL or missing references)
		//IL_00c3: Unknown result type (might be due to invalid IL or missing references)
		//IL_00c8: Unknown result type (might be due to invalid IL or missing references)
		//IL_0180: Unknown result type (might be due to invalid IL or missing references)
		//IL_0185: Unknown result type (might be due to invalid IL or missing references)
		string text2 = text ?? string.Empty;
		string text3 = fontPath ?? string.Empty;
		Vector2 val = default(Vector2);
		((Vector2)(ref val))..ctor(Mathf.Max(1f, size.x), Mathf.Max(1f, size.y));
		bool flag = !IsSolid(colors);
		uint topLeft = colors.TopLeft;
		if (!_textDrawStates.TryGetValue(id, out var value) || value.SortingOrder != sortingOrder || !string.Equals(value.Text, text2, StringComparison.Ordinal) || !string.Equals(value.FontPath, text3, StringComparison.Ordinal) || !Approximately(value.FontSize, fontSize) || !Approximately(value.Position, pos) || !Approximately(value.Size, val) || value.Alignment != alignment || value.Color != topLeft || value.UseGradient != flag || value.ColorTopLeft != colors.TopLeft || value.ColorTopRight != colors.TopRight || value.ColorBottomRight != colors.BottomRight || value.ColorBottomLeft != colors.BottomLeft || value.OutlineEnabled != outlineEnabled || value.OutlineColor != outlineColor || !Approximately(value.OutlineThickness, outlineThickness) || value.ShadowEnabled != shadowEnabled || value.ShadowColor != shadowColor || !Approximately(value.ShadowOffset, shadowOffset) || !Approximately(value.ShadowSoftness, shadowSoftness) || !SdfTextRenderer.TouchScreenText(id, sortingOrder))
		{
			Vector4 shadowColor2 = (Vector4)(shadowEnabled ? ColorU32ToVector4(shadowColor) : default(Vector4));
			SdfTextRenderer.DrawScreenText(id, text2, text3, fontSize, pos, val, alignment, ColorU32ToVector4(topLeft), outlineEnabled, ColorU32ToVector4(outlineColor), outlineThickness, sortingOrder, shadowEnabled, shadowColor2, shadowOffset, shadowSoftness, flag, ColorU32ToVector4(colors.TopLeft), ColorU32ToVector4(colors.TopRight), ColorU32ToVector4(colors.BottomRight), ColorU32ToVector4(colors.BottomLeft));
			if (value == null)
			{
				value = new TextDrawState();
				_textDrawStates[id] = value;
			}
			value.Text = text2;
			value.FontPath = text3;
			value.FontSize = fontSize;
			value.Position = pos;
			value.Size = val;
			value.Alignment = alignment;
			value.Color = topLeft;
			value.UseGradient = flag;
			value.ColorTopLeft = colors.TopLeft;
			value.ColorTopRight = colors.TopRight;
			value.ColorBottomRight = colors.BottomRight;
			value.ColorBottomLeft = colors.BottomLeft;
			value.OutlineEnabled = outlineEnabled;
			value.OutlineColor = outlineColor;
			value.OutlineThickness = outlineThickness;
			value.ShadowEnabled = shadowEnabled;
			value.ShadowColor = shadowColor;
			value.ShadowOffset = shadowOffset;
			value.ShadowSoftness = shadowSoftness;
			value.SortingOrder = sortingOrder;
		}
	}

	public void RenderUI()
	{
		//IL_0117: Unknown result type (might be due to invalid IL or missing references)
		//IL_0129: Unknown result type (might be due to invalid IL or missing references)
		//IL_013d: Unknown result type (might be due to invalid IL or missing references)
		if (!ShouldRender())
		{
			KeyViewerUnityRenderer.HideAll();
			PauseVideoIfNeeded();
			return;
		}
		if (Main.Settings.KeyViewerConfigurations == null || Main.Settings.KeyViewerConfigurations.Count == 0)
		{
			KeyViewerUnityRenderer.HideAll();
			PauseVideoIfNeeded();
			return;
		}
		PrepareKvRuntimeBake();
		Vector2 center = default(Vector2);
		((Vector2)(ref center))..ctor((float)Screen.width * 0.5f, (float)Screen.height * 0.5f);
		bool flag = false;
		bool hasVideoThisFrame = false;
		bool isPlaying = Main.IsGamePlaying();
		bool isOpen = FreeMakeEditor.IsOpen;
		KeyViewerUnityRenderer.BeginFrame();
		foreach (KVConfiguration keyViewerConfiguration in Main.Settings.KeyViewerConfigurations)
		{
			if (keyViewerConfiguration != null && keyViewerConfiguration.IsEnabled && keyViewerConfiguration.Nodes != null && keyViewerConfiguration.Nodes.Count != 0 && IsConfigVisible(keyViewerConfiguration, isPlaying, isOpen))
			{
				float scale = keyViewerConfiguration.Scale;
				float rounding = (float)Math.Floor(6f * scale);
				float borderThickness = keyViewerConfiguration.BorderThickness;
				uint kpsColor = Vector4ToColor(keyViewerConfiguration.ColorKps);
				uint totalColor = Vector4ToColor(keyViewerConfiguration.ColorTotal);
				DrawKeyRain(keyViewerConfiguration, center, scale);
				DrawBackgroundImages(keyViewerConfiguration, keyViewerConfiguration.Nodes, center, scale, ref hasVideoThisFrame);
				DrawNodes(keyViewerConfiguration, keyViewerConfiguration.Nodes, center, scale, rounding, borderThickness, kpsColor, totalColor);
				flag = true;
			}
		}
		KeyViewerUnityRenderer.EndFrame();
		if (hasVideoThisFrame)
		{
			VideoTextureManager.EndFrame("KV");
		}
		else if (_hadVideoLastFrame)
		{
			VideoTextureManager.PauseAll("KV");
		}
		_hadVideoLastFrame = hasVideoThisFrame;
		if (!flag)
		{
			KeyViewerUnityRenderer.HideAll();
			PauseVideoIfNeeded();
		}
	}

	private void PauseVideoIfNeeded()
	{
		if (_hadVideoLastFrame)
		{
			VideoTextureManager.PauseAll("KV");
			_hadVideoLastFrame = false;
		}
	}

	private static void BeginVideoFrameIfNeeded(ref bool hasVideoThisFrame)
	{
		if (!hasVideoThisFrame)
		{
			VideoTextureManager.BeginFrame("KV");
			hasVideoThisFrame = true;
		}
	}

	private bool ShouldRender()
	{
		if (!Main.IsEnabled || Main.Settings == null || !Main.Settings.EnableKeyViewer)
		{
			return false;
		}
		if ((Object)(object)KeyViewerManager.Instance == (Object)null)
		{
			return false;
		}
		bool keyViewerOnlyShowPlaying = Main.Settings.KeyViewerOnlyShowPlaying;
		bool flag = Main.IsGamePlaying();
		bool isOpen = FreeMakeEditor.IsOpen;
		if (keyViewerOnlyShowPlaying && !flag && !isOpen)
		{
			return false;
		}
		return true;
	}

	private static bool IsConfigVisible(KVConfiguration config, bool isPlaying, bool editMode)
	{
		if (config == null)
		{
			return false;
		}
		if (editMode)
		{
			return true;
		}
		if (!config.ShowInGame && isPlaying)
		{
			return false;
		}
		if (config.OnlyShowPlaying && !isPlaying)
		{
			return false;
		}
		return true;
	}

	public bool ShouldRenderOverlayNow()
	{
		if (!ShouldRender())
		{
			return false;
		}
		List<KVConfiguration> keyViewerConfigurations = Main.Settings.KeyViewerConfigurations;
		if (keyViewerConfigurations == null || keyViewerConfigurations.Count == 0)
		{
			return false;
		}
		bool isPlaying = Main.IsGamePlaying();
		bool isOpen = FreeMakeEditor.IsOpen;
		for (int i = 0; i < keyViewerConfigurations.Count; i++)
		{
			KVConfiguration kVConfiguration = keyViewerConfigurations[i];
			if (kVConfiguration != null && kVConfiguration.IsEnabled && kVConfiguration.Nodes != null && kVConfiguration.Nodes.Count != 0 && IsConfigVisible(kVConfiguration, isPlaying, isOpen))
			{
				return true;
			}
		}
		return false;
	}

	private void DrawBackgroundImages(KVConfiguration config, List<KVNode> activeNodes, Vector2 center, float globalScale, ref bool hasVideoThisFrame)
	{
		//IL_006d: Unknown result type (might be due to invalid IL or missing references)
		//IL_0084: Unknown result type (might be due to invalid IL or missing references)
		//IL_0089: Unknown result type (might be due to invalid IL or missing references)
		//IL_008d: Unknown result type (might be due to invalid IL or missing references)
		//IL_0092: Unknown result type (might be due to invalid IL or missing references)
		//IL_0135: Unknown result type (might be due to invalid IL or missing references)
		//IL_013c: Unknown result type (might be due to invalid IL or missing references)
		//IL_0103: Unknown result type (might be due to invalid IL or missing references)
		//IL_0114: Unknown result type (might be due to invalid IL or missing references)
		//IL_015d: Unknown result type (might be due to invalid IL or missing references)
		//IL_015f: Unknown result type (might be due to invalid IL or missing references)
		foreach (KVNode activeNode in activeNodes)
		{
			if (activeNode == null || (activeNode.NodeType != 3 && activeNode.NodeType != 4))
			{
				continue;
			}
			bool value = false;
			KeyViewerManager.Instance.IsNodePressed.TryGetValue(activeNode, out value);
			float num = ((!activeNode.UseCustomColor) ? activeNode.Opacity : (value ? activeNode.ColorBgPressed[3] : activeNode.ColorBgNormal[3]));
			BakedKvNode bakedNode = GetBakedNode(config, activeNode, center, globalScale, 0f, config.BorderThickness);
			Vector2 basePosition = bakedNode.BasePosition;
			Vector2 baseSize = bakedNode.BaseSize;
			NodeRenderIds ids = bakedNode.Ids;
			if (ids == null || (activeNode.NodeType == 3 && bakedNode.ImageBaked && Approximately(bakedNode.ImageBakedAlpha, num) && KeyViewerUnityRenderer.KeepImageAlive(ids.BackgroundImage, bakedNode.GraphicSortingOrder)))
			{
				continue;
			}
			Texture val = null;
			if (activeNode.NodeType == 4)
			{
				BeginVideoFrameIfNeeded(ref hasVideoThisFrame);
				val = VideoTextureManager.GetOrCreateVideoTexture("KV", ids.BackgroundImage, activeNode.VideoPath, loop: true, Mathf.CeilToInt(Mathf.Abs(baseSize.x)), Mathf.CeilToInt(Mathf.Abs(baseSize.y)), shouldPlay: true);
			}
			else
			{
				val = (Texture)(object)TextureManager.GetOrCreateTexture2D(activeNode.ImagePath, baseSize.x, baseSize.y);
			}
			if (!((Object)(object)val == (Object)null))
			{
				KeyViewerUnityRenderer.DrawImage(ids.BackgroundImage, val, basePosition, baseSize, num, bakedNode.CornerRadius, bakedNode.GraphicSortingOrder);
				if (activeNode.NodeType == 3)
				{
					bakedNode.ImageBaked = true;
					bakedNode.ImageBakedAlpha = num;
				}
			}
		}
	}

	private void DrawNodes(KVConfiguration config, List<KVNode> activeNodes, Vector2 center, float globalScale, float rounding, float borderThickness, uint kpsColor, uint totalColor)
	{
		//IL_0035: Unknown result type (might be due to invalid IL or missing references)
		//IL_00c2: Unknown result type (might be due to invalid IL or missing references)
		//IL_00c7: Unknown result type (might be due to invalid IL or missing references)
		//IL_00ca: Unknown result type (might be due to invalid IL or missing references)
		//IL_00cf: Unknown result type (might be due to invalid IL or missing references)
		//IL_0161: Unknown result type (might be due to invalid IL or missing references)
		//IL_0189: Unknown result type (might be due to invalid IL or missing references)
		//IL_01a2: Unknown result type (might be due to invalid IL or missing references)
		//IL_01a8: Unknown result type (might be due to invalid IL or missing references)
		//IL_01ad: Unknown result type (might be due to invalid IL or missing references)
		//IL_01c8: Unknown result type (might be due to invalid IL or missing references)
		//IL_01ca: Unknown result type (might be due to invalid IL or missing references)
		//IL_01cc: Unknown result type (might be due to invalid IL or missing references)
		//IL_01d6: Unknown result type (might be due to invalid IL or missing references)
		//IL_01db: Unknown result type (might be due to invalid IL or missing references)
		//IL_01e0: Unknown result type (might be due to invalid IL or missing references)
		//IL_01e2: Unknown result type (might be due to invalid IL or missing references)
		//IL_01e7: Unknown result type (might be due to invalid IL or missing references)
		//IL_0176: Unknown result type (might be due to invalid IL or missing references)
		//IL_017d: Unknown result type (might be due to invalid IL or missing references)
		//IL_0184: Unknown result type (might be due to invalid IL or missing references)
		//IL_0564: Unknown result type (might be due to invalid IL or missing references)
		//IL_0566: Unknown result type (might be due to invalid IL or missing references)
		//IL_0671: Unknown result type (might be due to invalid IL or missing references)
		//IL_0673: Unknown result type (might be due to invalid IL or missing references)
		//IL_0294: Unknown result type (might be due to invalid IL or missing references)
		//IL_0296: Unknown result type (might be due to invalid IL or missing references)
		//IL_030b: Unknown result type (might be due to invalid IL or missing references)
		//IL_033c: Unknown result type (might be due to invalid IL or missing references)
		//IL_0343: Unknown result type (might be due to invalid IL or missing references)
		//IL_0320: Unknown result type (might be due to invalid IL or missing references)
		//IL_0606: Unknown result type (might be due to invalid IL or missing references)
		//IL_0608: Unknown result type (might be due to invalid IL or missing references)
		//IL_0624: Unknown result type (might be due to invalid IL or missing references)
		//IL_062c: Unknown result type (might be due to invalid IL or missing references)
		//IL_0712: Unknown result type (might be due to invalid IL or missing references)
		//IL_0714: Unknown result type (might be due to invalid IL or missing references)
		//IL_0730: Unknown result type (might be due to invalid IL or missing references)
		//IL_0738: Unknown result type (might be due to invalid IL or missing references)
		//IL_03a4: Unknown result type (might be due to invalid IL or missing references)
		//IL_03a6: Unknown result type (might be due to invalid IL or missing references)
		//IL_03af: Unknown result type (might be due to invalid IL or missing references)
		//IL_03c1: Unknown result type (might be due to invalid IL or missing references)
		//IL_044f: Unknown result type (might be due to invalid IL or missing references)
		//IL_0460: Unknown result type (might be due to invalid IL or missing references)
		//IL_0467: Unknown result type (might be due to invalid IL or missing references)
		//IL_04dc: Unknown result type (might be due to invalid IL or missing references)
		//IL_04de: Unknown result type (might be due to invalid IL or missing references)
		//IL_04e7: Unknown result type (might be due to invalid IL or missing references)
		//IL_0504: Unknown result type (might be due to invalid IL or missing references)
		Vector2 val2 = default(Vector2);
		Vector2 pos = default(Vector2);
		Vector2 pos2 = default(Vector2);
		foreach (KVNode activeNode in activeNodes)
		{
			if (activeNode == null || activeNode.NodeType == 3 || activeNode.NodeType == 4)
			{
				continue;
			}
			BakedKvNode bakedNode = GetBakedNode(config, activeNode, center, globalScale, rounding, borderThickness);
			uint backgroundNormal = bakedNode.BackgroundNormal;
			uint backgroundPressed = bakedNode.BackgroundPressed;
			uint borderNormal = bakedNode.BorderNormal;
			uint borderPressed = bakedNode.BorderPressed;
			uint textNormal = bakedNode.TextNormal;
			uint textPressed = bakedNode.TextPressed;
			bool keyOutlineEnabled = bakedNode.KeyOutlineEnabled;
			bool countOutlineEnabled = bakedNode.CountOutlineEnabled;
			uint keyOutlineColor = bakedNode.KeyOutlineColor;
			uint countOutlineColor = bakedNode.CountOutlineColor;
			float keyOutlineThickness = bakedNode.KeyOutlineThickness;
			float countOutlineThickness = bakedNode.CountOutlineThickness;
			bool keyShadowEnabled = bakedNode.KeyShadowEnabled;
			bool countShadowEnabled = bakedNode.CountShadowEnabled;
			uint keyShadowColor = bakedNode.KeyShadowColor;
			uint countShadowColor = bakedNode.CountShadowColor;
			Vector2 keyShadowOffset = bakedNode.KeyShadowOffset;
			Vector2 countShadowOffset = bakedNode.CountShadowOffset;
			float keyShadowSoftness = bakedNode.KeyShadowSoftness;
			float countShadowSoftness = bakedNode.CountShadowSoftness;
			bool hideCountText = bakedNode.HideCountText;
			bool value = false;
			KeyViewerManager.Instance.IsNodePressed.TryGetValue(activeNode, out value);
			KeyPressAnimationSettings animation = bakedNode.Animation;
			float num = ((activeNode.NodeType == 0) ? GetKeyPressAnimationProgress(activeNode, value, animation) : 0f);
			float num2 = ((activeNode.NodeType == 0 && animation.Enabled) ? Mathf.Lerp(1f, animation.Scale, num) : 1f);
			Vector2 val = ((activeNode.NodeType == 0 && animation.Enabled) ? (new Vector2(animation.OffsetX, animation.OffsetY) * globalScale * num) : Vector2.zero);
			float finalScale = bakedNode.FinalScale;
			float num3 = finalScale * num2;
			float num4 = globalScale * num2;
			Vector2 basePosition = bakedNode.BasePosition;
			Vector2 baseSize = bakedNode.BaseSize;
			((Vector2)(ref val2))..ctor(activeNode.Width * num3, activeNode.Height * num3);
			Vector2 val3 = basePosition + (baseSize - val2) * 0.5f + val;
			NodeRenderIds ids = bakedNode.Ids;
			if (ids == null)
			{
				continue;
			}
			int graphicSortingOrder = bakedNode.GraphicSortingOrder;
			int textSortingOrder = bakedNode.TextSortingOrder;
			float cornerRadius = bakedNode.CornerRadius * num2;
			if (activeNode.NodeType == 0)
			{
				float t = ((animation.Enabled && animation.AffectColors) ? num : (value ? 1f : 0f));
				ColorCorners colorCorners = LerpCorners(ColorCorners.Solid(backgroundNormal), ColorCorners.Solid(backgroundPressed), t);
				ColorCorners colorCorners2 = LerpCorners(ColorCorners.Solid(borderNormal), ColorCorners.Solid(borderPressed), t);
				ColorCorners colors = LerpCorners(ColorCorners.Solid(textNormal), ColorCorners.Solid(textPressed), t);
				KeyViewerUnityRenderer.DrawRect(ids.Box, val3, val2, colorCorners.TopLeft, colorCorners.TopRight, colorCorners.BottomRight, colorCorners.BottomLeft, colorCorners2.TopLeft, colorCorners2.TopRight, colorCorners2.BottomRight, colorCorners2.BottomLeft, bakedNode.BorderThickness, cornerRadius, graphicSortingOrder);
				string label = bakedNode.Label;
				float fontSize = 20f * num4 * activeNode.TextScale;
				string keyFontPath = bakedNode.KeyFontPath;
				float num5 = TextBoxHeight(fontSize);
				((Vector2)(ref pos))..ctor(val3.x + activeNode.TextOffsetX * num3, hideCountText ? (val3.y + (val2.y - num5) * 0.5f + activeNode.TextOffsetY * num3) : (val3.y + 5f * num3 + activeNode.TextOffsetY * num3));
				if (animation.Enabled || !bakedNode.KeyTextBaked || bakedNode.KeyTextBakedColor != colors.TopLeft || !SdfTextRenderer.KeepAlive(ids.KeyText, textSortingOrder))
				{
					DrawText(ids.KeyText, label, keyFontPath, fontSize, pos, new Vector2(val2.x, num5), 1, colors, keyOutlineEnabled, keyOutlineColor, keyOutlineThickness, keyShadowEnabled, keyShadowColor, keyShadowOffset, keyShadowSoftness, textSortingOrder);
					if (!animation.Enabled)
					{
						bakedNode.KeyTextBaked = true;
						bakedNode.KeyTextBakedColor = colors.TopLeft;
					}
				}
				if (hideCountText)
				{
					continue;
				}
				if (activeNode.CachedHitCountText == null || activeNode.CachedHitCountValue != activeNode.HitCount)
				{
					activeNode.CachedHitCountValue = activeNode.HitCount;
					activeNode.CachedHitCountText = activeNode.HitCount.ToString();
				}
				string cachedHitCountText = activeNode.CachedHitCountText;
				float fontSize2 = 20f * num4 * activeNode.CountScale;
				string countFontPath = bakedNode.CountFontPath;
				float num6 = TextBoxHeight(fontSize2);
				((Vector2)(ref pos2))..ctor(val3.x + activeNode.CountOffsetX * num3, val3.y + val2.y - num6 - 5f * num3 + activeNode.CountOffsetY * num3);
				if (animation.Enabled || !bakedNode.CountTextBaked || bakedNode.CountTextBakedValue != activeNode.HitCount || bakedNode.CountTextBakedColor != colors.TopLeft || !SdfTextRenderer.KeepAlive(ids.CountText, textSortingOrder))
				{
					DrawText(ids.CountText, cachedHitCountText, countFontPath, fontSize2, pos2, new Vector2(val2.x, num6), ClampTextAlignment(activeNode.CountTextAlignment, 1), colors, countOutlineEnabled, countOutlineColor, countOutlineThickness, countShadowEnabled, countShadowColor, countShadowOffset, countShadowSoftness, textSortingOrder);
					if (!animation.Enabled)
					{
						bakedNode.CountTextBaked = true;
						bakedNode.CountTextBakedValue = activeNode.HitCount;
						bakedNode.CountTextBakedColor = colors.TopLeft;
					}
				}
			}
			else if (activeNode.NodeType == 1)
			{
				ColorCorners colorCorners3 = ColorCorners.Solid(backgroundNormal);
				ColorCorners colorCorners4 = ColorCorners.Solid(borderNormal);
				KeyViewerUnityRenderer.DrawRect(ids.Box, val3, val2, colorCorners3.TopLeft, colorCorners3.TopRight, colorCorners3.BottomRight, colorCorners3.BottomLeft, colorCorners4.TopLeft, colorCorners4.TopRight, colorCorners4.BottomRight, colorCorners4.BottomLeft, bakedNode.BorderThickness, cornerRadius, graphicSortingOrder);
				string label2 = bakedNode.Label;
				int currentKps = KeyViewerManager.Instance.GetCurrentKps(config);
				if (config.CachedKpsText == null || config.CachedKpsValue != currentKps)
				{
					config.CachedKpsValue = currentKps;
					config.CachedKpsText = currentKps.ToString();
				}
				string cachedKpsText = config.CachedKpsText;
				uint color = (activeNode.UseCustomColor ? textNormal : kpsColor);
				DrawPairText(config, activeNode, val3, val2, finalScale, globalScale, label2, cachedKpsText, color, keyOutlineEnabled, keyOutlineColor, keyOutlineThickness, countOutlineEnabled, countOutlineColor, countOutlineThickness, keyShadowEnabled, keyShadowColor, keyShadowOffset, keyShadowSoftness, countShadowEnabled, countShadowColor, countShadowOffset, countShadowSoftness, ids.KpsLabel, ids.KpsValue, hideCountText, textSortingOrder, bakedNode);
			}
			else if (activeNode.NodeType == 2)
			{
				ColorCorners colorCorners5 = ColorCorners.Solid(backgroundNormal);
				ColorCorners colorCorners6 = ColorCorners.Solid(borderNormal);
				KeyViewerUnityRenderer.DrawRect(ids.Box, val3, val2, colorCorners5.TopLeft, colorCorners5.TopRight, colorCorners5.BottomRight, colorCorners5.BottomLeft, colorCorners6.TopLeft, colorCorners6.TopRight, colorCorners6.BottomRight, colorCorners6.BottomLeft, bakedNode.BorderThickness, cornerRadius, graphicSortingOrder);
				string label3 = bakedNode.Label;
				if (config.CachedTotalHitsText == null || config.CachedTotalHitsValue != config.TotalHits)
				{
					config.CachedTotalHitsValue = config.TotalHits;
					config.CachedTotalHitsText = config.TotalHits.ToString();
				}
				string cachedTotalHitsText = config.CachedTotalHitsText;
				uint color2 = (activeNode.UseCustomColor ? textNormal : totalColor);
				DrawPairText(config, activeNode, val3, val2, finalScale, globalScale, label3, cachedTotalHitsText, color2, keyOutlineEnabled, keyOutlineColor, keyOutlineThickness, countOutlineEnabled, countOutlineColor, countOutlineThickness, keyShadowEnabled, keyShadowColor, keyShadowOffset, keyShadowSoftness, countShadowEnabled, countShadowColor, countShadowOffset, countShadowSoftness, ids.TotalLabel, ids.TotalValue, hideCountText, textSortingOrder, bakedNode);
			}
		}
	}

	private void DrawPairText(KVConfiguration config, KVNode node, Vector2 pos, Vector2 size, float finalScale, float globalScale, string label, string value, uint color, bool keyOutlineEnabled, uint keyOutlineColor, float keyOutlineThickness, bool countOutlineEnabled, uint countOutlineColor, float countOutlineThickness, bool keyShadowEnabled, uint keyShadowColor, Vector2 keyShadowOffset, float keyShadowSoftness, bool countShadowEnabled, uint countShadowColor, Vector2 countShadowOffset, float countShadowSoftness, string labelId, string valueId, bool hideValue, int sortingOrder, BakedKvNode baked)
	{
		//IL_00aa: Unknown result type (might be due to invalid IL or missing references)
		//IL_00ba: Unknown result type (might be due to invalid IL or missing references)
		//IL_0028: Unknown result type (might be due to invalid IL or missing references)
		//IL_0038: Unknown result type (might be due to invalid IL or missing references)
		//IL_003e: Unknown result type (might be due to invalid IL or missing references)
		//IL_00f3: Unknown result type (might be due to invalid IL or missing references)
		//IL_00f4: Unknown result type (might be due to invalid IL or missing references)
		//IL_00fc: Unknown result type (might be due to invalid IL or missing references)
		//IL_010e: Unknown result type (might be due to invalid IL or missing references)
		//IL_0078: Unknown result type (might be due to invalid IL or missing references)
		//IL_007a: Unknown result type (might be due to invalid IL or missing references)
		//IL_0082: Unknown result type (might be due to invalid IL or missing references)
		//IL_0094: Unknown result type (might be due to invalid IL or missing references)
		//IL_0146: Unknown result type (might be due to invalid IL or missing references)
		//IL_0156: Unknown result type (might be due to invalid IL or missing references)
		//IL_015c: Unknown result type (might be due to invalid IL or missing references)
		//IL_0188: Unknown result type (might be due to invalid IL or missing references)
		//IL_018a: Unknown result type (might be due to invalid IL or missing references)
		//IL_0193: Unknown result type (might be due to invalid IL or missing references)
		//IL_01b0: Unknown result type (might be due to invalid IL or missing references)
		float fontSize = 20f * globalScale * node.TextScale;
		string keyFontPath = baked.KeyFontPath;
		float num = TextBoxHeight(fontSize);
		if (hideValue)
		{
			Vector2 pos2 = default(Vector2);
			((Vector2)(ref pos2))..ctor(pos.x + node.TextOffsetX * finalScale, pos.y + (size.y - num) * 0.5f + node.TextOffsetY * finalScale);
			if (!baked.PairLabelBaked || !SdfTextRenderer.KeepAlive(labelId, sortingOrder))
			{
				DrawText(labelId, label, keyFontPath, fontSize, pos2, new Vector2(size.x, num), 1, color, keyOutlineEnabled, keyOutlineColor, keyOutlineThickness, keyShadowEnabled, keyShadowColor, keyShadowOffset, keyShadowSoftness, sortingOrder);
				baked.PairLabelBaked = true;
			}
			return;
		}
		Vector2 pos3 = default(Vector2);
		((Vector2)(ref pos3))..ctor(pos.x + node.TextOffsetX * finalScale, pos.y + 5f * finalScale + node.TextOffsetY * finalScale);
		if (!baked.PairLabelBaked || !SdfTextRenderer.KeepAlive(labelId, sortingOrder))
		{
			DrawText(labelId, label, keyFontPath, fontSize, pos3, new Vector2(size.x, num), 1, color, keyOutlineEnabled, keyOutlineColor, keyOutlineThickness, keyShadowEnabled, keyShadowColor, keyShadowOffset, keyShadowSoftness, sortingOrder);
			baked.PairLabelBaked = true;
		}
		float fontSize2 = 20f * globalScale * node.CountScale;
		string countFontPath = baked.CountFontPath;
		float num2 = TextBoxHeight(fontSize2);
		Vector2 pos4 = default(Vector2);
		((Vector2)(ref pos4))..ctor(pos.x + node.CountOffsetX * finalScale, pos.y + size.y - num2 - 5f * finalScale + node.CountOffsetY * finalScale);
		DrawText(valueId, value, countFontPath, fontSize2, pos4, new Vector2(size.x, num2), ClampTextAlignment(node.CountTextAlignment, 1), color, countOutlineEnabled, countOutlineColor, countOutlineThickness, countShadowEnabled, countShadowColor, countShadowOffset, countShadowSoftness, sortingOrder);
	}

	private static int ClampTextAlignment(int alignment, int fallback)
	{
		if (alignment < 0 || alignment > 2)
		{
			return fallback;
		}
		return alignment;
	}

	private void DrawKeyRain(KVConfiguration config, Vector2 center, float globalScale)
	{
		//IL_013e: Unknown result type (might be due to invalid IL or missing references)
		//IL_0472: Unknown result type (might be due to invalid IL or missing references)
		//IL_0481: Unknown result type (might be due to invalid IL or missing references)
		//IL_048c: Unknown result type (might be due to invalid IL or missing references)
		//IL_04af: Unknown result type (might be due to invalid IL or missing references)
		//IL_04be: Unknown result type (might be due to invalid IL or missing references)
		//IL_0590: Unknown result type (might be due to invalid IL or missing references)
		//IL_059f: Unknown result type (might be due to invalid IL or missing references)
		//IL_0541: Unknown result type (might be due to invalid IL or missing references)
		//IL_0550: Unknown result type (might be due to invalid IL or missing references)
		if (config == null || KeyViewerManager.Instance.ActiveDrops.Count <= 0)
		{
			return;
		}
		float keyRainSpeed = config.KeyRainSpeed;
		float keyRainMaxHeight = config.KeyRainMaxHeight;
		int keyRainFadeMode = config.KeyRainFadeMode;
		float time = RenderTimelineClock.Time;
		_rainRow1Buffer.Clear();
		_rainRow2Buffer.Clear();
		List<KeyDrop> activeDrops = KeyViewerManager.Instance.ActiveDrops;
		for (int i = 0; i < activeDrops.Count; i++)
		{
			KeyDrop keyDrop = activeDrops[i];
			KVNode node = keyDrop.Node;
			if (node == null || node.NodeType != 0 || (node.UseCustomRain ? (!node.EnableKeyRain) : (!config.EnableKeyRain)))
			{
				continue;
			}
			if (keyDrop.Config != null)
			{
				if (keyDrop.Config != config)
				{
					continue;
				}
			}
			else if (config.Nodes == null || !config.Nodes.Contains(node))
			{
				continue;
			}
			if (node.RainRow == 1)
			{
				_rainRow1Buffer.Add(keyDrop);
			}
			else
			{
				_rainRow2Buffer.Add(keyDrop);
			}
		}
		for (int j = 0; j < 2; j++)
		{
			List<KeyDrop> list = ((j == 0) ? _rainRow1Buffer : _rainRow2Buffer);
			for (int k = 0; k < list.Count; k++)
			{
				KeyDrop keyDrop2 = list[k];
				KVNode node2 = keyDrop2.Node;
				BakedKvNode bakedNode = GetBakedNode(config, node2, center, globalScale, (float)Math.Floor(6f * globalScale), config.BorderThickness);
				if (!bakedNode.RainEnabled)
				{
					continue;
				}
				float rainWidthRatio = bakedNode.RainWidthRatio;
				uint rainBaseColor = bakedNode.RainBaseColor;
				uint rainFarColor = bakedNode.RainFarColor;
				bool rainGradientEnabled = bakedNode.RainGradientEnabled;
				bool rainHorizontalGradientEnabled = bakedNode.RainHorizontalGradientEnabled;
				int rainGradientMode = bakedNode.RainGradientMode;
				uint rainHorizontalColor = bakedNode.RainHorizontalColor;
				float rainFadeHeight = bakedNode.RainFadeHeight;
				float rainFadePower = bakedNode.RainFadePower;
				float rainGradientHeight = bakedNode.RainGradientHeight;
				float rainGradientPower = bakedNode.RainGradientPower;
				float x = bakedNode.BaseSize.x;
				float x2 = bakedNode.BasePosition.x;
				float num = bakedNode.BasePosition.y - bakedNode.RainYOffset;
				float num2 = x * rainWidthRatio;
				float num3 = x2 + (x - num2) * 0.5f;
				float valueOrDefault = keyDrop2.EndTime.GetValueOrDefault(time);
				float num4 = num - keyRainSpeed * (time - valueOrDefault);
				float val = num - keyRainSpeed * (time - keyDrop2.StartTime);
				if (num4 < num - keyRainMaxHeight && keyRainFadeMode == 0)
				{
					continue;
				}
				float num5 = Math.Min(num4, num);
				float num6 = Math.Max(val, num - keyRainMaxHeight);
				if (num5 <= num6)
				{
					continue;
				}
				uint num9;
				uint num10;
				if (rainGradientEnabled && rainGradientMode == 1)
				{
					float num7 = Mathf.Max(1f, keyRainMaxHeight * Mathf.Clamp(rainGradientHeight, 0.05f, 3f));
					float num8 = Mathf.Clamp(rainGradientPower, 0.1f, 5f);
					float t = Mathf.Pow(Mathf.Clamp01((num - num6) / num7), num8);
					float t2 = Mathf.Pow(Mathf.Clamp01((num - num5) / num7), num8);
					num9 = LerpColor(rainBaseColor, rainFarColor, t);
					num10 = LerpColor(rainBaseColor, rainFarColor, t2);
				}
				else
				{
					num9 = (rainGradientEnabled ? rainFarColor : rainBaseColor);
					num10 = rainBaseColor;
				}
				if (keyRainFadeMode == 1)
				{
					float num11 = Mathf.Max(1f, keyRainMaxHeight * Mathf.Clamp(rainFadeHeight, 0.05f, 3f));
					float num12 = Mathf.Clamp(rainFadePower, 0.1f, 5f);
					float ratio = Mathf.Pow(1f - Mathf.Clamp01((num - num5) / num11), num12);
					float ratio2 = Mathf.Pow(1f - Mathf.Clamp01((num - num6) / num11), num12);
					num10 = MultiplyAlpha(num10, ratio);
					num9 = MultiplyAlpha(num9, ratio2);
				}
				float num13 = Mathf.Round(num3);
				float num14 = Mathf.Round(num3 + num2);
				float num15 = Mathf.Round(num6);
				float num16 = Mathf.Round(num5);
				if (!(num14 <= num13) && !(num16 <= num15))
				{
					bool num17 = (keyRainFadeMode == 1 && (Mathf.Abs(rainFadeHeight - 1f) > 0.0001f || Mathf.Abs(rainFadePower - 1f) > 0.0001f)) || (rainGradientEnabled && rainGradientMode == 1 && (Mathf.Abs(rainGradientHeight - 1f) > 0.0001f || Mathf.Abs(rainGradientPower - 1f) > 0.0001f));
					if (bakedNode.RainShadowEnabled)
					{
						DrawKeyRainShadow(topColor: MultiplyAlpha(bakedNode.RainShadowColor, bakedNode.RainShadowStrength * Alpha01(num9)), bottomColor: MultiplyAlpha(bakedNode.RainShadowColor, bakedNode.RainShadowStrength * Alpha01(num10)), topLeft: new Vector2(num13, num15), size: new Vector2(num14 - num13, num16 - num15), offset: bakedNode.RainShadowOffset, softness: bakedNode.RainShadowSoftness, sortingOrder: bakedNode.RainShadowSortingOrder);
					}
					if (num17)
					{
						KeyViewerUnityRenderer.DrawKeyRainCurveRect("rain", new Vector2(num13, num15), new Vector2(num14 - num13, num16 - num15), rainBaseColor, rainFarColor, rainGradientEnabled, rainGradientMode == 1, keyRainFadeMode, num, keyRainMaxHeight, rainFadeHeight, rainFadePower, rainGradientHeight, rainGradientPower, rainHorizontalGradientEnabled, rainHorizontalColor, bakedNode.RainCornerRadius, bakedNode.RainSortingOrder);
					}
					else
					{
						uint topRightColor = (rainHorizontalGradientEnabled ? MatchAlpha(rainHorizontalColor, num9) : num9);
						uint bottomRightColor = ((!rainHorizontalGradientEnabled) ? num10 : (rainGradientEnabled ? LerpColor(num10, MatchAlpha(rainHorizontalColor, num10), 0.5f) : MatchAlpha(rainHorizontalColor, num10)));
						KeyViewerUnityRenderer.DrawGradientRect("rain", new Vector2(num13, num15), new Vector2(num14 - num13, num16 - num15), num9, topRightColor, bottomRightColor, num10, bakedNode.RainCornerRadius, bakedNode.RainSortingOrder);
					}
					if (bakedNode.RainOutlineEnabled && bakedNode.RainOutlineThickness > 0f)
					{
						KeyViewerUnityRenderer.DrawRectOutline("rain_outline", new Vector2(num13, num15), new Vector2(num14 - num13, num16 - num15), bakedNode.RainOutlineColor, bakedNode.RainOutlineThickness, 0f, bakedNode.RainOutlineSortingOrder);
					}
				}
			}
		}
	}

	private static Vector2 ResolvePair(float[] value, float fallbackX, float fallbackY)
	{
		//IL_001c: Unknown result type (might be due to invalid IL or missing references)
		return new Vector2((value != null && value.Length != 0) ? value[0] : fallbackX, (value != null && value.Length > 1) ? value[1] : fallbackY);
	}

	private void DrawKeyRainShadow(Vector2 topLeft, Vector2 size, uint topColor, uint bottomColor, Vector2 offset, float softness, int sortingOrder)
	{
		//IL_0021: Unknown result type (might be due to invalid IL or missing references)
		//IL_0022: Unknown result type (might be due to invalid IL or missing references)
		//IL_0024: Unknown result type (might be due to invalid IL or missing references)
		//IL_0029: Unknown result type (might be due to invalid IL or missing references)
		if (!(Alpha01(topColor) <= 0f) || !(Alpha01(bottomColor) <= 0f))
		{
			KeyViewerUnityRenderer.DrawSoftGradientShadowRect("rain_shadow", topLeft + offset, size, topColor, bottomColor, softness, sortingOrder);
		}
	}
}
