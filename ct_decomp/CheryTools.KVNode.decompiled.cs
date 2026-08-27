using System;
using System.Xml.Serialization;

namespace CheryTools;

[Serializable]
public class KVNode
{
	public int NodeType;

	public string KeyBind = "None";

	public string CustomText = "";

	public string ImagePath = "";

	public string VideoPath = "";

	public bool VideoLoop = true;

	public float VideoContentScale = 1f;

	public float VideoContentOffsetX;

	public float VideoContentOffsetY;

	public bool IsUnselectable;

	public float Opacity = 1f;

	public int Depth;

	public float PositionX;

	public float PositionY;

	public float Width = 50f;

	public float Height = 50f;

	public float BorderThickness = -1f;

	public float CornerRadius = -1f;

	public float Scale = 1f;

	public float TextOffsetY;

	public float TextOffsetX;

	public float TextScale = 1f;

	public float CountOffsetY;

	public float CountOffsetX;

	public float CountScale = 1f;

	public int CountTextAlignment = 1;

	public string KeyFontPath = "";

	public string CountFontPath = "";

	public bool HideCountText;

	public bool UseCustomOutline;

	public bool KeyTextOutlineEnabled;

	public float[] KeyTextOutlineColor = new float[4] { 0f, 0f, 0f, 1f };

	public float KeyTextOutlineThickness = 1f;

	public bool CountTextOutlineEnabled;

	public float[] CountTextOutlineColor = new float[4] { 0f, 0f, 0f, 1f };

	public float CountTextOutlineThickness = 1f;

	public bool UseCustomShadow;

	public bool KeyTextShadowEnabled;

	public float[] KeyTextShadowColor = new float[4] { 0f, 0f, 0f, 0.7f };

	public float[] KeyTextShadowOffset = new float[2] { 2f, 2f };

	public float KeyTextShadowSoftness;

	public bool CountTextShadowEnabled;

	public float[] CountTextShadowColor = new float[4] { 0f, 0f, 0f, 0.7f };

	public float[] CountTextShadowOffset = new float[2] { 2f, 2f };

	public float CountTextShadowSoftness;

	public bool UseCustomColor;

	public float[] ColorBgNormal = new float[4] { 0.2f, 0.2f, 0.2f, 0.8f };

	public float[] ColorBgPressed = new float[4] { 0.8f, 0.8f, 0.8f, 0.8f };

	public float[] ColorBorderNormal = new float[4] { 0.4f, 0.4f, 0.4f, 1f };

	public float[] ColorBorderPressed = new float[4] { 1f, 1f, 1f, 1f };

	public float[] ColorTextNormal = new float[4] { 1f, 1f, 1f, 1f };

	public float[] ColorTextPressed = new float[4] { 0f, 0f, 0f, 1f };

	public int RainRow;

	public bool EnableKeyRain = true;

	public bool UseCustomRain;

	public float[] RainColor = new float[4] { 0.8f, 0.5f, 1f, 0.8f };

	public bool RainGradientEnabled;

	public bool RainHorizontalGradientEnabled;

	public float[] RainGradientEndColor = new float[4] { 1f, 0.25f, 0.8f, 0.8f };

	public float[] RainHorizontalGradientEndColor = new float[4] { 0.45f, 0.75f, 1f, 0.8f };

	public int RainGradientMode;

	public float RainFadeHeight = 1f;

	public float RainFadePower = 1f;

	public float RainGradientHeight = 1f;

	public float RainGradientPower = 1f;

	public float RainWidthRatio = 0.8f;

	public float RainYOffset;

	public float RainCornerRadius;

	public bool UseCustomRainShadow;

	public bool RainShadowEnabled;

	public float[] RainShadowColor = new float[4] { 0f, 0f, 0f, 0.35f };

	public float[] RainShadowOffset = new float[2];

	public float RainShadowSoftness = 12f;

	public float RainShadowStrength = 1f;

	public bool UseCustomRainOutline;

	public bool RainOutlineEnabled;

	public float[] RainOutlineColor = new float[4] { 0f, 0f, 0f, 1f };

	public float RainOutlineThickness = 2f;

	public bool UseCustomKeyPressAnimation;

	public bool KeyPressAnimationEnabled;

	public float KeyPressAnimationDuration = 0.12f;

	public string KeyPressAnimationEasing = "ease-out-quad";

	public bool KeyPressAnimationAffectColors = true;

	public float KeyPressAnimationScale = 1f;

	public float KeyPressAnimationOffsetX;

	public float KeyPressAnimationOffsetY;

	public int HitCount;

	[XmlIgnore]
	public int CachedHitCountValue = int.MinValue;

	[XmlIgnore]
	public string CachedHitCountText;

	public KVNode()
	{
	}

	public KVNode(string bind, float px, float py)
	{
		KeyBind = bind;
		PositionX = px;
		PositionY = py;
	}

	public KVNode(int type, float px, float py, float w, float h)
	{
		NodeType = type;
		PositionX = px;
		PositionY = py;
		Width = w;
		Height = h;
	}
}
