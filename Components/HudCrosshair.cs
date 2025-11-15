using EFT;
using HEVSuitMod.Types;
using UnityEngine;
using UnityEngine.UI;

namespace HEVSuitMod.Components;

public class HudCrosshair : MonoBehaviour
{
	//private readonly ManualLogSource log = BepInEx.Logging.Logger.CreateLogSource(typeof(HudCrosshair).FullName);
	private Player.FirearmController currentFirearm;
	private HudIcon crosshair;
	private HudIcon crosshairOnTarget;
	private Camera camera;
	private readonly int layerMask = LayerMaskClass.PlayerCollisionsMask + LayerMaskClass.HitColliderMask; //537136073

	private HudController Hud => HEVSuitMod.Instance.HudController;

#pragma warning disable IDE0051
	private void Awake()
	{
		crosshair = new(transform.Find("Crosshair").GetComponent<Image>());
		crosshair.Image.enabled = false;
		crosshairOnTarget = new(crosshair.Image.transform.Find("OnTarget").GetComponent<Image>());
		crosshairOnTarget.Image.enabled = false;

		if (GamePlayerOwner.MyPlayer.HandsController is Player.FirearmController faController)
			currentFirearm = faController; // Only set if it's a gun

		GamePlayerOwner.MyPlayer.HandsChangedEvent += HandsChanged;
		currentFirearm?.OnAimingChanged += AimingChanged;
		camera = CameraClass.Instance.Camera;
	}

	private void OnDestroy()
	{
		GamePlayerOwner.MyPlayer.HandsChangedEvent -= HandsChanged;
	}

	private void Update()
	{
		// Fade it out if we're ADS, fade back in when not ADS
		switch (crosshair.State)
		{
			case EHudIconState.Activate:
				if (Hud.UpdateTransition(crosshair, HudController.FADE_TIME, Color.white) && Hud.UpdateTransition(crosshairOnTarget, HudController.FADE_TIME, Color.white))
				{
					crosshair.State = EHudIconState.Active;
					crosshairOnTarget.State = EHudIconState.Active;
				}
				break;

			case EHudIconState.Deactivate:
				if (Hud.UpdateTransition(crosshair, HudController.FADE_TIME, Color.clear) && Hud.UpdateTransition(crosshairOnTarget, HudController.FADE_TIME, Color.clear))
				{
					crosshair.State = EHudIconState.Inactive;
					crosshairOnTarget.State = EHudIconState.Inactive;
				}
				break;
		}

		if (currentFirearm == null)
			return;

		// Our crosshair shows where the bullet will hit instead of being centered all the time
		if (Physics.Raycast(currentFirearm.FireportPosition, currentFirearm.WeaponDirection, out RaycastHit hit, 1024f, layerMask))
			crosshair.Image.rectTransform.position = camera.WorldToScreenPoint(hit.point);

		// If we are aiming at a player/bot, activate the 'on target' indicator
		// FIXME: Only seems to activate if aiming at chest
		if (hit.collider?.GetComponent<IPlayer>() != null)
			crosshairOnTarget.Image.enabled = true;
		else
			crosshairOnTarget.Image.enabled = false;
	}
#pragma warning restore IDE0051

	private void AimingChanged(bool aiming)
	{
		EHudIconState newState = aiming ? EHudIconState.Deactivate : EHudIconState.Activate;
		Hud.StartTransition(crosshair, newState);
		Hud.StartTransition(crosshairOnTarget, newState);
	}

	private void HandsChanged(IHandsController controller)
	{
		currentFirearm?.OnAimingChanged -= AimingChanged;
		if (controller is Player.FirearmController faController)
		{
			faController.OnAimingChanged += AimingChanged;
			currentFirearm = faController;
			crosshair.Image.enabled = true;
		}
		else
		{
			currentFirearm = null;
			crosshair.Image.enabled = false;
			crosshairOnTarget.Image.enabled = false;
		}
	}
}
