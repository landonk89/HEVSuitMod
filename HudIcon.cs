using UnityEngine;
using UnityEngine.UI;

namespace HEVSuitMod;

public enum EIconState
{
	Inactive,
	Active,
	Deactivate,
	Activate
}

public class HudIcon(Image image, EIconState state = EIconState.Inactive, bool critical = false, float timer = 0f, float transitionTime = 0.25f)
{
	public Image image = image;
	public EIconState state = state;
	public bool critical = critical;
	public float timer = timer;
	public float transitionTime = transitionTime;
	public Color lastColor = image.color;
}
