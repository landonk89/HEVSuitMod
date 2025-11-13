using UnityEngine;
using UnityEngine.UI;

namespace HEVSuitMod.Types;

public enum EHudIconState
{
	Inactive,
	Active,
	Deactivate,
	Activate
}

public class HudIcon(Image image, EHudIconState state = EHudIconState.Inactive, bool critical = false, float timer = 0f, float transitionTime = 0.25f)
{
	public Image Image { get; } = image;
	public EHudIconState State { get; set; } = state;
	public bool Critical { get; set; } = critical;
	public float Timer { get; set; } = timer;
	public float TransitionTime { get; set; } = transitionTime;
	public Color LastColor { get; set; } = image.color;
}
