using BepInEx.Logging;
using EFT;
using EFT.InventoryLogic;
using HEVSuitMod.Patches;
using HEVSuitMod.Types;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace HEVSuitMod.Components;

public class HudItemPickups : MonoBehaviour
{
	private readonly ManualLogSource log = BepInEx.Logging.Logger.CreateLogSource($"{typeof(HudItemPickups).FullName}");
	private Transform iconArea;
	private readonly List<HudIcon> activeIcons = [];
	private HudController Hud => HEVSuitMod.Instance.HudController;

#pragma warning disable IDE0051
	private void Awake()
	{
		iconArea = transform.Find("RightNotifyArea");
		PickupLootPatch.PickupLootEvent += Notification;
	}

	private void Update()
	{
#if DEBUG
		if (Input.GetKeyDown(KeyCode.F11))
			Notification(GamePlayerOwner.MyPlayer.LastEquippedWeaponOrKnifeItem);
#endif
		if (activeIcons.Count > 0)
			Hud.IconUpdate([.. activeIcons]); // Handle normally until inactive

		// Idle for a sec and fade away once inactive
		foreach (HudIcon icon in activeIcons.ToArray())
		{
			if (icon.State != EHudIconState.Inactive)
				continue;

			icon.Timer += Time.deltaTime;
			if (icon.Timer >= HudController.NOTIFY_STAY_TIME)
			{
				float t = (icon.Timer - icon.TransitionTime) / icon.TransitionTime;
				icon.Image.color = Color.Lerp(Hud.hudColor, Color.clear, t);
				if (t >= 1f)
				{
					activeIcons.Remove(icon);
					Destroy(icon.Image.gameObject);
				}
			}
		}
	}

	private void OnDestroy()
	{
		PickupLootPatch.PickupLootEvent -= Notification;
	}
#pragma warning restore IDE0051

	private void Notification(Item item)
	{
		GameObject iconObj = new("icon");
		iconObj.transform.parent = iconArea;
		Image iconImage = iconObj.AddComponent<Image>();
		iconImage.preserveAspect = true;
		iconImage.sprite = Hud.GetItemSprite(item, new(1, 1));
		iconImage.color = Hud.hudColorActive;
		HudIcon icon = new(iconImage, transitionTime: HudController.NOTIFY_FLASH_TIME, state: EHudIconState.Deactivate); // Starts bright, fades to normal
		activeIcons.Add(icon);

		if (activeIcons.Count > HudController.MAX_NOTIFY_ICONS)
		{
			Destroy(activeIcons[0].Image.gameObject);
			activeIcons.RemoveAt(0);
		}
	}
}
