using UnityEngine;
using UnityEngine.UI;

namespace HEVSuitMod;

public enum EIconState
{
	Dark,
	Bright,
	GoDark,
	GoBright,
	Highlight,
	FadeHighlight,
}

public class HudIcon(Image image, EIconState state = EIconState.Dark, bool isCritical = false, float timer = 0f)
{
	public Image image = image;
	public EIconState state = state;
	public bool critical = isCritical;
	public float timer = timer;
	public Color lastColor = image.color;
}
