using BepInEx.Logging;
using EFT;
using EFT.InventoryLogic;
using System;
using UnityEngine;
using UnityEngine.UI;

namespace HEVSuitMod;

public class HudAmmoCounter : MonoBehaviour
{
	private readonly ManualLogSource log = BepInEx.Logging.Logger.CreateLogSource($"{typeof(HudAmmoCounter).FullName}");
	private readonly HudIcon[] ammoIcons = new HudIcon[4]; // 3 digits[0,1,2] + icon[3]
	private IHandsController currentHandsController;
	
	private HudController HudController => HEVMod.Instance.HudController;
	private HudIcon[] AmmoNumbers => [ammoIcons[0], ammoIcons[1], ammoIcons[2]];

	private Action OnShotHandler;
	private Action<Item> OnMagChangedHandler;

	private void Awake()
	{
		currentHandsController = GamePlayerOwner.MyPlayer.HandsController;
		OnMagChangedHandler = (_) => AmmoChanged(currentHandsController);
		OnShotHandler = () => AmmoChanged(currentHandsController);
		GamePlayerOwner.MyPlayer.HandsChangedEvent += HandsChanged;
		Image iconImg = transform.Find("AmmoCounter/Icon").GetComponent<Image>();
		ammoIcons[3] = new(iconImg);
		for (int i = 0; i < 3; i++)
			ammoIcons[i] = new(transform.Find($"AmmoCounter/Value/Digit{i}").GetComponent<Image>());
	}

	private void OnDestroy()
	{
		GamePlayerOwner.MyPlayer.HandsChangedEvent -= HandsChanged;
	}

	private void Update()
	{
		HudController.StateUpdate(ammoIcons);
		if (Time.time % HudController.FLASH_TIME < Time.deltaTime)
		{
			if (ammoIcons[0].critical)
				HudController.Highlight(ammoIcons);
		}
	}

	public void HandsChanged(IHandsController handsController)
	{
		if (handsController is Player.FirearmController faController)
		{
			Player.FirearmController current = currentHandsController as Player.FirearmController;
			current.OnShot -= OnShotHandler;
			current.OnReadyToOperate -= AmmoChanged;
			current.Weapon.GetMagazineSlot().OnAddOrRemoveItem -= OnMagChangedHandler;

			OnShotHandler = () => AmmoChanged(faController);
			faController.Weapon.GetMagazineSlot().OnAddOrRemoveItem += OnMagChangedHandler;
			faController.OnShot += OnShotHandler;

			currentHandsController = faController;
			AmmoChanged(faController);
		}
	}

	public void AmmoChanged(IHandsController handsController)
	{
		if (handsController == null || handsController.Item is not Weapon weapon)
		{
			HudController.SetNumberDigits(AmmoNumbers, 0);
			HudController.SetCritical(ammoIcons, true);
			HudController.Highlight(ammoIcons);
			return;
		}

		// TODO/FIXME: This is not working for weapons with internal mags when reloading
		int maxAmmo = weapon.Chambers.Length + weapon.GetMaxMagazineCount();
		int ammoCount = weapon.ChamberAmmoCount + weapon.GetCurrentMagazineCount();
		float ammoRatio = (float)ammoCount / maxAmmo;
		bool critical = ammoRatio <= HudController.AMMO_RATIO_CRITICAL;
		log.LogDebug($"AmmoChanged: maxAmmo {maxAmmo}, ammoCount {ammoCount}, ammoRatio {ammoRatio}");
		HudController.SetNumberDigits(AmmoNumbers, ammoCount);
		HudController.SetCritical(ammoIcons, critical);
		HudController.Highlight(ammoIcons);
	}
}
