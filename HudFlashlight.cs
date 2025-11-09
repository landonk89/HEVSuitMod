using UnityEngine;
using UnityEngine.UI;

namespace HEVSuitMod
{
    public class HudFlashlight : MonoBehaviour
    {
		private Image empty;
		private Image full;
		private Image beam;
		private bool isCritical;
		private bool isOn;
		private HudController HudController => HEVMod.Instance.HudController;
		private Flashlight Flashlight => HEVMod.Instance.Flashlight;

		private void Awake()
		{
			empty = transform.Find("Flashlight/IconEmpty").GetComponent<Image>();
			full = transform.Find("Flashlight/IconFull").GetComponent<Image>();
			beam = transform.Find("Flashlight/Beam").GetComponent<Image>();
			beam.enabled = false; // start off and full battery
			isOn = false;
			isCritical = false;
			full.fillAmount = 1f;
			Flashlight.Toggled += FlashlightToggled;
			Flashlight.BatteryUpdate += SetBatteryLevel;
			Flashlight.BatteryStateChanged += SetBatteryCritical;
		}

		private void OnDestroy()
		{
			Flashlight.Toggled -= FlashlightToggled;
			Flashlight.BatteryUpdate -= SetBatteryLevel;
			Flashlight.BatteryStateChanged -= SetBatteryCritical;
		}

		private void FlashlightToggled(bool turnedOn)
		{
			Color color = isCritical switch
			{
				true => turnedOn ? HudController.hudColorCriticalActive : HudController.hudColorCritical,
				false => turnedOn ? HudController.hudColorActive : HudController.hudColor
			};

			isOn = turnedOn;
			beam.enabled = turnedOn;
			empty.color = color;
			full.color = color;
			beam.color = color;
		}

		public void SetBatteryLevel(float level)
		{
			full.fillAmount = level;
		}

		private void SetBatteryCritical(bool critical)
		{
			Color color = critical switch
			{
				true => isOn ? HudController.hudColorCriticalActive : HudController.hudColorCritical,
				false => isOn ? HudController.hudColorActive : HudController.hudColor
			};

			isCritical = critical;
			empty.color = color;
			full.color = color;
			beam.color = color;
		}
	}
}
