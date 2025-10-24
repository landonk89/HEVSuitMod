using UnityEngine;
using UnityEngine.UI;

namespace HEVSuitMod;

public enum EImageState
{
	Inactive,
	Active,
	Deactivating,
	Activating,
	Highlight,
	FadeHighlight,
	HitIndicator,
	FadeHitIndicator,
	DamageNotification,
	ExpireDamageNotification,
	DestroyDamageNotification,
	Notification,
	IdleNotification,
	ExpireNotification,
	DestroyNotification
}

public class HudImage(Image image, EImageState state = EImageState.Inactive, bool isCritical = false, float timer = 0f)
{
	public Image Image { get; set; } = image;
	public EImageState State { get; set; } = state;
	public bool Critical { get; set; } = isCritical;
	public float Timer { get; set; } = timer;
	public Color LastColor { get; set; } = image.color;
}
