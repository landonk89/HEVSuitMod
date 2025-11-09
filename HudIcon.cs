using UnityEngine;
using UnityEngine.UI;

namespace HEVSuitMod;

public enum EIconState
{
	Inactive,
	Active,
	Deactivating,
	Activating,
	Highlight,
	FadeHighlight,
}

public class HudIcon(Image image, EIconState state = EIconState.Inactive, bool isCritical = false, float timer = 0f)
{
	public Image image = image;
	public EIconState state = state;
	public bool critical = isCritical;
	public float timer = timer;
	public Color lastColor = image.color;
}
