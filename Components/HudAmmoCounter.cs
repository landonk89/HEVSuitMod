using BepInEx.Logging;
using EFT;
using EFT.InventoryLogic;
using HEVSuitMod.Patches;
using HEVSuitMod.Types;
using System;
using UnityEngine;
using UnityEngine.UI;

namespace HEVSuitMod.Components;

public class HudAmmoCounter : MonoBehaviour
{
	private readonly ManualLogSource log = BepInEx.Logging.Logger.CreateLogSource($"{typeof(HudAmmoCounter).FullName}");
	private readonly HudIcon[] ammoIcons = new HudIcon[4]; // 3 digits[0,1,2] + icon[3]
	private IHandsController currentHandsController;
	
	private Action OnShotHandler;
	private Action<Item> OnMagChangedHandler;
	private Action LoadSingleAmmoHandler;
	
	private HudIcon[] AmmoNumbers => [ammoIcons[0], ammoIcons[1], ammoIcons[2]];
	private HudController Hud => HEVSuitMod.Instance.HudController;

#pragma warning disable IDE0051
	private void Awake()
	{ 
		currentHandsController = GamePlayerOwner.MyPlayer.HandsController;
		OnMagChangedHandler = (_) => AmmoChanged(currentHandsController);
		OnShotHandler = () => AmmoChanged(currentHandsController);
		LoadSingleAmmoHandler = () => AmmoChanged(currentHandsController);
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
		Hud.IconUpdate(ammoIcons);
		if (Time.time % HudController.FLASH_TIME < Time.deltaTime)
		{
			if (ammoIcons[0].Critical)
				Hud.IconFlash(ammoIcons);
		}
	}
#pragma warning restore IDE0051

	public void HandsChanged(IHandsController handsController)
	{
		if (handsController is Player.FirearmController faController)
		{
			Player.FirearmController current = currentHandsController as Player.FirearmController;
			current.OnShot -= OnShotHandler;
			current.OnReadyToOperate -= AmmoChanged;
			current.Weapon.GetMagazineSlot()?.OnAddOrRemoveItem -= OnMagChangedHandler;
			LoadSingleAmmoPatch.SingleLoadAmmoEvent -= LoadSingleAmmoHandler;

			OnShotHandler = () => AmmoChanged(faController);
			LoadSingleAmmoHandler = () => AmmoChanged(faController);
			
			faController.OnShot += OnShotHandler;
			faController.OnReadyToOperate += AmmoChanged;
			faController.Weapon.GetMagazineSlot()?.OnAddOrRemoveItem += OnMagChangedHandler;
			LoadSingleAmmoPatch.SingleLoadAmmoEvent += LoadSingleAmmoHandler;

			currentHandsController = faController;
			AmmoChanged(faController);
		}
	}

	public void AmmoChanged(IHandsController handsController)
	{
		if (handsController == null || handsController.Item is not Weapon weapon)
		{
			Hud.IconSetDigits(AmmoNumbers, 0);
			Hud.IconSetCritical(ammoIcons, true);
			Hud.IconFlash(ammoIcons);
			return;
		}

		// FIXME: This is not working for weapons with internal mags when reloading
		int maxAmmo = weapon.Chambers.Length + weapon.GetMaxMagazineCount();
		int ammoCount = weapon.ChamberAmmoCount + weapon.GetCurrentMagazineCount();
		float ammoRatio = (float)ammoCount / maxAmmo;
		bool critical = ammoRatio <= HudController.AMMO_RATIO_CRITICAL;
		log.LogDebug($"AmmoChanged: maxAmmo {maxAmmo}, ammoCount {ammoCount}, ammoRatio {ammoRatio}");
		Hud.IconSetDigits(AmmoNumbers, ammoCount);
		Hud.IconSetCritical(ammoIcons, critical);
		Hud.IconFlash(ammoIcons);
	}
}
