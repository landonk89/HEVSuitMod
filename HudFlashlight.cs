using UnityEngine;
using UnityEngine.UI;

namespace HEVSuitMod;

public class HudFlashlight : MonoBehaviour
{
	private readonly HudIcon[] flashlightIcons = new HudIcon[3];
	private HudIcon EmptyIcon => flashlightIcons[0];
	private HudIcon FullIcon => flashlightIcons[1];
	private HudIcon BeamIcon => flashlightIcons[2];

	private HudController HudController => HEVMod.Instance.HudController;
	private Flashlight Flashlight => HEVMod.Instance.Flashlight;

	private void Awake()
	{
		flashlightIcons[0] = new(transform.Find("Flashlight/IconEmpty").GetComponent<Image>());
		flashlightIcons[1] = new(transform.Find("Flashlight/IconFull").GetComponent<Image>());
		flashlightIcons[2] = new(transform.Find("Flashlight/Beam").GetComponent<Image>());
		BeamIcon.image.enabled = false; // start off and full battery
		FullIcon.image.fillAmount = 1f;
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

	private void Update()
	{
		HudController.StateUpdate(flashlightIcons);
	}

	private void FlashlightToggled(bool turnedOn)
	{
		BeamIcon.image.enabled = turnedOn;
		foreach (HudIcon img in flashlightIcons)
			HudController.StartTransition(img, turnedOn ? EIconState.GoBright : EIconState.GoDark);
	}

	public void SetBatteryLevel(float level)
	{
		FullIcon.image.fillAmount = level;
	}

	private void SetBatteryCritical(bool critical)
	{
		HudController.SetCritical(flashlightIcons, critical);
	}
}
