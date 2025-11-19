using EFT;
using EFT.HealthSystem;
using HEVSuitMod.Types;
using System;
using UnityEngine;
using UnityEngine.UI;

namespace HEVSuitMod.Components;

public class HudHealthCounter : MonoBehaviour
{
	private readonly HudIcon[] healthIcons = new HudIcon[4]; // 3 digits[0,1,2] + icon[3]
	private Action<EBodyPart, float, DamageInfoStruct> HealthChangedAction;
	private GDelegate71 PlayerDeadHandler;

	private HudIcon[] HealthNumbers => [healthIcons[0], healthIcons[1], healthIcons[2]];
	private HudController Hud => HEVSuitMod.Instance.HudController;
	private ActiveHealthController HealthController => GamePlayerOwner.MyPlayer.ActiveHealthController;

#pragma warning disable IDE0051
	private void Awake()
	{
		HealthChangedAction = (_, _, _) => HealthChanged();
		PlayerDeadHandler = (_) => HealthChanged();
		healthIcons[3] = new(transform.Find("HealthAndSuitPower/HealthIcon").GetComponent<Image>());
		for (int i = 0; i < 3; i++)
			healthIcons[i] = new(transform.Find($"HealthAndSuitPower/HealthValue/Digit{i}").GetComponent<Image>());

		HealthController.HealthChangedEvent += HealthChangedAction;
		GamePlayerOwner.MyPlayer.OnPlayerDeadOrUnspawn += PlayerDeadHandler;
	}

	private void Start()
	{
		HealthChanged();
	}

	private void OnDestroy()
	{
		HealthController.HealthChangedEvent -= HealthChangedAction;
		GamePlayerOwner.MyPlayer.OnPlayerDeadOrUnspawn -= PlayerDeadHandler;
	}

	private void Update()
	{
		Hud.IconUpdate(healthIcons);
		if (Time.time % HudController.FLASH_TIME < Time.deltaTime && healthIcons[0].Critical)
			Hud.IconFlash(healthIcons);
	}
#pragma warning restore IDE0051

	private void HealthChanged()
	{
		int health = Mathf.FloorToInt(100 * HealthController.GetBodyPartHealth(EBodyPart.Common).Normalized);
		Hud.IconSetDigits(HealthNumbers, health);
		Hud.IconSetCritical(healthIcons, health <= HudController.HEALTH_CRITICAL);
		Hud.IconFlash(healthIcons);
	}
}
