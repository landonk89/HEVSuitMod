using UnityEngine;
using UnityEngine.UI;

namespace HEVSuitMod;

public class HudFlashlight : MonoBehaviour
{
	private readonly HudIcon[] flashlightIcons = new HudIcon[3];
	private HudIcon FullIcon => flashlightIcons[1];
	private HudIcon BeamIcon => flashlightIcons[2];
	private HudController Hud => HEVMod.Instance.HudController;
	private Flashlight Flashlight => HEVMod.Instance.Flashlight;

#pragma warning disable IDE0051
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
		Hud.IconUpdate(flashlightIcons);
	}
#pragma warning restore IDE0051

	private void FlashlightToggled(bool turnedOn)
	{
		BeamIcon.image.enabled = turnedOn;
		if (turnedOn)
			Hud.IconActivate(flashlightIcons);
		else
			Hud.IconDeactivate(flashlightIcons);
	}

	public void SetBatteryLevel(float level)
	{
		FullIcon.image.fillAmount = level;
	}

	private void SetBatteryCritical(bool critical)
	{
		Hud.IconSetCritical(flashlightIcons, critical);
	}
}
