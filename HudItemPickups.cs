using AnimationEventSystem;
using BepInEx.Logging;
using EFT;
using EFT.InventoryLogic;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;

namespace HEVSuitMod;

public class HudItemPickups : MonoBehaviour
{
	private readonly ManualLogSource log = BepInEx.Logging.Logger.CreateLogSource($"{typeof(HudItemPickups).FullName}");
	private GameObject iconArea;
	private readonly List<HudIcon> activeIcons = [];
	private HudController Hud => HEVMod.Instance.HudController;

#pragma warning disable IDE0051
	private void Awake()
	{
		iconArea = transform.Find("RightNotifyArea").gameObject;
		// FIXME: Not working, find the correct way to determine item being picked up
		if (GamePlayerOwner.MyPlayer.LeftHandController is GClass2725 leftHandController)
			leftHandController.IleftHandInteractionEvents_0.OnTakeEvent += OnTakeHandler;
		else
			log.LogError("LeftHandController is not GClass2725.");
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
			if (icon.state != EIconState.Inactive)
				continue;
			
			icon.timer += Time.deltaTime;
			if (icon.timer >= HudController.NOTIFY_STAY_TIME)
			{
				float t = (icon.timer - icon.transitionTime) / icon.transitionTime;
				icon.image.color = Color.Lerp(Hud.hudColor, Color.clear, t);
				if (t >= 1f)
				{
					activeIcons.Remove(icon);
					Destroy(icon.image.gameObject);
				}
			}
		}
	}

	private void OnDestroy()
	{
		(GamePlayerOwner.MyPlayer.LeftHandController as GClass2725)?.IleftHandInteractionEvents_0.OnTakeEvent -= OnTakeHandler;
	}
#pragma warning restore IDE0051

	private void OnTakeHandler(IAnimatorEventParameter param)
	{
		if (GamePlayerOwner.MyPlayer.LeftHandController is GClass2725 leftHand)
		{
			Notification(leftHand.Item_0);
		}
	}

	private void Notification(Item item)
	{
		GameObject iconObj = new("icon");
		iconObj.transform.parent = iconArea.transform;
		Image iconImage = iconObj.AddComponent<Image>();
		iconImage.preserveAspect = true;
		iconImage.sprite = Hud.GetItemSprite(item, new(1,1));
		iconImage.color = Hud.hudColorActive;
		HudIcon icon = new(iconImage, transitionTime: HudController.NOTIFY_FLASH_TIME, state: EIconState.Deactivate); // Starts bright, fades to normal
		activeIcons.Add(icon);

		if (activeIcons.Count > HudController.MAX_NOTIFY_ICONS)
		{
			Destroy(activeIcons[0].image.gameObject);
			activeIcons.RemoveAt(0);
		}
	}
}
