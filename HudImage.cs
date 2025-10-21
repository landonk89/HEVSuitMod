using UnityEngine.UI;

namespace HEVSuitMod
{
	public class HudImage(Image image, EImageState state = EImageState.Idle, bool isCritical = false, float timer = 0f)
	{
		public Image Image { get; set; } = image;
		public EImageState State { get; set; } = state;
		public bool Critical { get; set; } = isCritical;
		public float Timer { get; set; } = timer;
	}

	public enum EImageState
	{
		Idle,
		Deactivate,
		Activate,
		StartHighlight,
		EndHighlight,
		StartHitIndicator,
		EndHitIndicator,
		PulseBlank,
		PulseHighlight,
		Notify,
		Destroy
	}
}
