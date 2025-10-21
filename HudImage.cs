using UnityEngine;
using UnityEngine.UI;

namespace HEVSuitMod
{
	public class HudImage(Image image, EImageState state = EImageState.Inactive, bool isCritical = false, float timer = 0f)
	{
		public Image Image { get; set; } = image;
		public EImageState State { get; set; } = state;
		public EImageState LastState { get; set; } = state;
		public bool Critical { get; set; } = isCritical;
		public float Timer { get; set; } = timer;
		public Color PreviousColor { get; set; } = image.color;
	}

	public enum EImageState
	{
		Inactive,
		Active,
		SetCritical,
		Deactivate,
		Deactivating,
		Activate,
		Activating,
		Highlight,
		FadeHighlight,
		HitIndicator,
		FadeHitIndicator,
		PulseLow,
		PulseHi,
		Notify,
		Destroy,
		Destroying
	}
}
