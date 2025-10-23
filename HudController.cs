using BepInEx.Logging;
using EFT;
using EFT.InventoryLogic;
using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace HEVSuitMod;

public class HudController : MonoBehaviour
{		
	public static HudController Instance { get; private set; }
	private ManualLogSource log = BepInEx.Logging.Logger.CreateLogSource("HEVSuitMod.HudController");
	private AssetBundle assets;
	private GameObject hudPrefab;
	private GameObject hud;
	
	// Colors and times
	private Color hudColor = new(1f, 0.627f, 0f, 0.4f); // Matches 'RGB_YELLOWISH' and 'MIN_ALPHA' from hl1\cl_dll\hud.h
	private Color hudColorActive = new(1f, 0.9f, 0f, 1f); // Brighter yellow
	private Color hudColorCritical = new(1f, 0f, 0f, 0.4f); // Red
	private Color hudColorCriticalActive = new(1f, 0f, 0f, 1f); // Brighter red
	private float fadeTime = 0.5f;
	private float notifyIconLifetime = 1.5f;
	private float damageIconLifetime = 4f;
	private float activationTime = 0.25f;

	// Number sprites
	private Sprite[] numberSprites = new Sprite[11]; // 0-9 plus a blank one

	// Health/SuitPower
	private HudImage healthIcon;
	private HudImage[] healthVal = new HudImage[3];
	private HudImage[] healthGroup = new HudImage[4]; // Group of all health images
	private HudImage suitEmpty;
	private HudImage suitFull;
	private HudImage[] suitVal = new HudImage[3];
	private HudImage[] suitGroup = new HudImage[5]; // Group of all suit images

	// Ammo counter
	private HudImage ammo;
	private HudImage[] ammoVal = new HudImage[3];
	private HudImage[] ammoGroup = new HudImage[4]; // Group of all ammo images

	// Flashlight
	private HudImage flashEmpty;
	private HudImage flashFull;
	private HudImage flashBeam;
	private HudImage[] flashGroup = new HudImage[3];

	// Hit indicators - Order in scene: Up Right Down Left
	private HudImage hitIndicatorUp;
	private HudImage hitIndicatorRight;
	private HudImage hitIndicatorDown;
	private HudImage hitIndicatorLeft;
	private HudImage[][] hitIndicatorDirections;

	// Damage type notifications
	private Sprite coldDamage;
	private Sprite fireDamage;
	private Sprite explosionDamage;
	private Sprite barbwireDamage;
	private Sprite toxinDamage;
	private Sprite radiationDamage;
	private Sprite dehydrationDamage;
	private Sprite exhaustionDamage;

	// Active notifications
	private Transform notificationArea;
	private Transform damageNotificationArea;
	private int maxNotifications = 7; // The area fits 7 images at 100x100 comfortably
	private List<HudImage> activeNotifications = [];
	private List<HudImage> activeDamageNotifications = [];

	// For state machine
	private List<HudImage> allHudImages = [];

	private void Awake()
	{
		if (Instance != null && Instance != this)
		{
			Destroy(this);
			return;
		}
		else
			Instance = this;

		assets = HEVMod.Instance.Assets;
		if (assets == null) // Can't happen, but you can bet it will somehow...
		{
			log.LogFatal("Couldn't get assetbundle!");
			return;
		}

		hudPrefab = assets.LoadAsset<GameObject>("assets/prefabs/hud.prefab");
		hud = Instantiate(hudPrefab);

		// Load number sprites, index 10 is a blank sprite
		for (int i = 0; i < 10; i++) numberSprites[i] = assets.LoadAsset<Sprite>($"assets/sprites/hud_number_{i}.tga");
		numberSprites[10] = assets.LoadAsset<Sprite>($"assets/sprites/hud_number_blank.tga");

		// Health digits and icon
		for (int i = 0; i < 3; i++) healthVal[i] = new(Utils.FindComponent<Image>(hud, $"HealthAndSuitPower/HealthValue/Digit{i}"));
		healthIcon = new(Utils.FindComponent<Image>(hud, "HealthAndSuitPower/HealthIcon"));
		healthGroup = [ healthIcon, healthVal[0], healthVal[1], healthVal[2] ];

		// SuitPower digits and icon
		for (int i = 0; i < 3; i++) suitVal[i] = new(Utils.FindComponent<Image>(hud, $"HealthAndSuitPower/SuitPowerValue/Digit{i}"));
		suitEmpty = new(Utils.FindComponent<Image>(hud, "HealthAndSuitPower/SuitIconEmpty"));
		suitFull = new(Utils.FindComponent<Image>(hud, "HealthAndSuitPower/SuitIconFull"));
		suitGroup = [suitFull, suitEmpty, suitVal[0], suitVal[1], suitVal[2]];

		// Ammo counter
		for (int i = 0; i < 3; i++)	ammoVal[i] = new(Utils.FindComponent<Image>(hud, $"AmmoCounter/Value/Digit{i}"));
		ammo = new(Utils.FindComponent<Image>(hud, "AmmoCounter/Icon"));
		ammoGroup = [ammo, ammoVal[0], ammoVal[1], ammoVal[2]];

		// Flashlight indicator
		flashEmpty = new(Utils.FindComponent<Image>(hud, "Flashlight/IconEmpty"));
		flashFull = new(Utils.FindComponent<Image>(hud, "Flashlight/IconFull"));
		flashBeam = new(Utils.FindComponent<Image>(hud, "Flashlight/Beam"));
		flashBeam.Image.enabled = false; // start off and full battery
		flashFull.Image.fillAmount = 1f;
		flashGroup = [flashEmpty, flashFull, flashBeam];

		// Hit indicator child objects are in order: Up Right Down Left
		Image[] hitIndicatorImg = Utils.FindComponentsInChildren<Image>(hud, "HitIndicators");
		hitIndicatorUp = new(hitIndicatorImg[0]);
		hitIndicatorRight = new(hitIndicatorImg[1]);
		hitIndicatorDown = new(hitIndicatorImg[2]);
		hitIndicatorLeft = new(hitIndicatorImg[3]);
		foreach (Image hit in hitIndicatorImg) hit.color = Color.clear;

		// Notification areas
		notificationArea = hud.transform.Find("RightNotifyArea");
		damageNotificationArea = hud.transform.Find("LeftNotifyArea");

		// Damage type sprites
		coldDamage = assets.LoadAsset<Sprite>("assets/sprites/hud_dmg_cold.tga");
		fireDamage = assets.LoadAsset<Sprite>("assets/sprites/hud_dmg_heat.tga");
		explosionDamage = assets.LoadAsset<Sprite>("assets/sprites/hud_dmg_heat.tga"); // TODO: make unique sprite
		barbwireDamage = assets.LoadAsset<Sprite>("assets/sprites/hud_dmg_heat.tga");
		toxinDamage = assets.LoadAsset<Sprite>("assets/sprites/hud_dmg_bio.tga");
		radiationDamage = assets.LoadAsset<Sprite>("assets/sprites/hud_dmg_rad.tga");
		dehydrationDamage = assets.LoadAsset<Sprite>("assets/sprites/hud_dmg_chem.tga"); // TODO: make unique sprite
		exhaustionDamage = assets.LoadAsset<Sprite>("assets/sprites/hud_dmg_chem.tga"); // TODO: make unique sprite

		// Map the 8 hit directions to our indicators
		hitIndicatorDirections =
		[
			[hitIndicatorUp],
			[hitIndicatorUp, hitIndicatorRight],
			[hitIndicatorRight],
			[hitIndicatorRight, hitIndicatorDown],
			[hitIndicatorDown],
			[hitIndicatorDown, hitIndicatorLeft],
			[hitIndicatorLeft],
			[hitIndicatorLeft, hitIndicatorUp]
		];

		// For state machine
		allHudImages.Add(healthIcon);
		allHudImages.Add(healthVal[0]);
		allHudImages.Add(healthVal[1]);
		allHudImages.Add(healthVal[2]);
		allHudImages.Add(suitEmpty);
		allHudImages.Add(suitFull);
		allHudImages.Add(suitVal[0]);
		allHudImages.Add(suitVal[1]);
		allHudImages.Add(suitVal[2]);
		allHudImages.Add(flashEmpty);
		allHudImages.Add(flashFull);
		allHudImages.Add(flashBeam);
		allHudImages.Add(ammo);
		allHudImages.Add(ammoVal[0]);
		allHudImages.Add(ammoVal[1]);
		allHudImages.Add(ammoVal[2]);
		allHudImages.Add(hitIndicatorUp);
		allHudImages.Add(hitIndicatorRight);
		allHudImages.Add(hitIndicatorDown);
		allHudImages.Add(hitIndicatorLeft);
	}

	private void Start()
	{
		// Subscribe events
		GamePlayerOwner.MyPlayer.BeingHitAction += (damageInfo, _, _) => OnTakeDamage(damageInfo);
		GamePlayerOwner.MyPlayer.ActiveHealthController.HealthChangedEvent += (_, _, _) => HealthChanged();
		GamePlayerOwner.MyPlayer.HandsChangedEvent += HandsChanged; // subscribe to weapon events
		GamePlayerOwner.MyPlayer.OnPlayerDead += (_, _, _, _) => HealthChanged();
		Flashlight.Instance.Toggled += FlashlightToggled;
		Flashlight.Instance.BatteryUpdate += SetFlashlightBattery;
		Flashlight.Instance.BatteryLow += SetFlashlightBatteryCritical;

		// Init value sprites
		HealthChanged();
		SuitPowerChanged();
	}

	private void OnDestroy()
	{
		// GamePlayerOwner stuff may not be needed if MyPlayer clears by itself, look into that
		GamePlayerOwner.MyPlayer.BeingHitAction -= (damageInfo, _, _) => OnTakeDamage(damageInfo);
		GamePlayerOwner.MyPlayer.ActiveHealthController.HealthChangedEvent -= (_, _, _) => HealthChanged();
		GamePlayerOwner.MyPlayer.HandsChangedEvent -= HandsChanged;
		GamePlayerOwner.MyPlayer.OnPlayerDead -= (_, _, _, _) => HealthChanged();
		Flashlight.Instance.Toggled -= FlashlightToggled;
		Flashlight.Instance.BatteryUpdate -= SetFlashlightBattery;
		Flashlight.Instance.BatteryLow -= SetFlashlightBatteryCritical;
	}

	private void Update()
	{
#if DEBUG
		if (Input.GetKeyDown(KeyCode.F6))
			NotifyIcon(fireDamage, UnityEngine.Random.Range(0f, 1f) > 0.5f);
#endif
		// Iterate backward so we can safely RemoveAt() for notification icons
		for (int i = allHudImages.Count -1; i >= 0; i--)
		{
			HudImage img = allHudImages[i];
			Color idleColor = img.Critical ? hudColorCritical : hudColor;
			Color activeColor = img.Critical ? hudColorCriticalActive : hudColorActive;

			switch (img.State)
			{
				case EImageState.Active:
					break;

				case EImageState.Inactive:
					break;

				case EImageState.Deactivate:
					img.Image.color = activeColor;
					StartTransition(img, EImageState.Deactivating);
					break;

				case EImageState.Deactivating:
					if(UpdateTransition(img, activationTime, idleColor))
						img.State = EImageState.Inactive;
					break;

				case EImageState.Activate:
					img.Image.color = idleColor;
					//img.LastState = img.State;
					StartTransition(img, EImageState.Activating);
					break;

				case EImageState.Activating:
					if(UpdateTransition(img, activationTime, activeColor))
						img.State = EImageState.Active;
					break;

				case EImageState.Highlight:
					img.Image.color = activeColor;
					StartTransition(img, EImageState.FadeHighlight);
					break;

				case EImageState.FadeHighlight:
					if(UpdateTransition(img, fadeTime, idleColor))
						img.State = EImageState.Inactive;
					break;

				case EImageState.HitIndicator:
					img.Image.color = Color.white;
					StartTransition(img, EImageState.FadeHitIndicator);
					break;

				case EImageState.FadeHitIndicator:
					if(UpdateTransition(img, fadeTime, Color.clear))
						img.State = EImageState.Inactive;
					break;

				case EImageState.PulseLow:
					img.Image.color = Color.Lerp(Color.clear, idleColor, (Mathf.Sin(Time.time * 4f) + 1f) * 0.5f);
					break;

				case EImageState.DamageNotification:
					img.Timer += Time.deltaTime;
					img.Image.color = Color.Lerp(Color.clear, activeColor, (Mathf.Sin(img.Timer * 4f) + 1f) * 0.5f);
					if (img.Timer >= damageIconLifetime)
						StartTransition(img, EImageState.DestroyDamageNotification);
					break;
					/*
				case EImageState.ExpireDamageNotification:
					img.Timer += Time.deltaTime;
					if (img.Timer >= fadeTime)
						StartTransition(img, EImageState.DestroyDamageNotification);
					break;
					*/
				case EImageState.DestroyDamageNotification:
					if (UpdateTransition(img, fadeTime, Color.clear))
						img.State = EImageState.DestroyDamageNotificationImmediate;
					break;

				case EImageState.DestroyDamageNotificationImmediate:
					Destroy(img.Image.gameObject);
					allHudImages.RemoveAt(i);
					activeDamageNotifications.Remove(img);
					break;

				case EImageState.Notification:
					if (UpdateTransition(img, fadeTime, idleColor))
						StartTransition(img, EImageState.ExpireNotification);
					break;

				case EImageState.ExpireNotification:
					img.Timer += Time.deltaTime;
					if (img.Timer >= notifyIconLifetime)
						StartTransition(img, EImageState.DestroyNotification);
					break;

				case EImageState.DestroyNotification:
					if (UpdateTransition(img, fadeTime, Color.clear))
						img.State = EImageState.DestroyNotificationImmediate;
					break;

				case EImageState.DestroyNotificationImmediate:
					Destroy(img.Image.gameObject);
					allHudImages.RemoveAt(i);
					activeNotifications.Remove(img);
					break;
			}
		}
	}

	private void StartTransition(HudImage img, EImageState nextState)
	{
		img.Timer = 0f;
		img.LastColor = img.Image.color;
		//img.LastState = img.State;
		img.State = nextState;
	}

	// Returns true if transition completed
	private bool UpdateTransition(HudImage img, float duration, Color target)
	{
		img.Timer += Time.deltaTime;
		if (img.Timer >= duration)
		{
			img.Image.color = target;
			img.Timer = 0f;
			return true;
		}
		float t = img.Timer / duration;
		img.Image.color = Color.Lerp(img.LastColor, target, t);
		return false;
	}

	private void SetCritical(HudImage[] images, bool critical)
	{
		foreach (HudImage image in images)
		{
			image.Critical = critical;
			image.Image.color = image.State switch
			{
				EImageState.Inactive => critical ? hudColorCritical : hudColor,
				_ => critical ? hudColorCriticalActive : hudColorActive
			};
		}
	}

	private void Highlight(HudImage[] images)
	{
		foreach (HudImage image in images)
			image.State = EImageState.Highlight;
	}

	private void SetFlashlightBattery(float battery)
	{
		flashFull.Image.fillAmount = battery;
	}

	private void SetFlashlightBatteryCritical(bool isLow)
	{
		SetCritical(flashGroup, isLow);
	}

	private void FlashlightToggled(bool isOn)
	{
		flashBeam.Image.enabled = isOn;
		foreach (HudImage image in flashGroup)
			image.State = isOn ? EImageState.Activate : EImageState.Deactivate;
	}

	private void SetNumberDigits(HudImage[] digitImages, int number)
	{
		if (number < 0 || number > 999)
		{
			log.LogWarning($"SetNumberDigits() value {number} out of range, min:0 max:999");
			number = Mathf.Clamp(number, 0, 999);
		}

		if (digitImages.Length != 3)
		{
			string error = $"SetNumberDigits() expected 3 digit images but got {digitImages.Length}";
			log.LogError(error);
			throw new InvalidOperationException(error);
		}

		char[] digits = number.ToString("000").ToCharArray();
		bool foundNonZero = false;
		for (int i = 0; i < 3; i++)
		{
			int digit = digits[i] - '0'; // Neat trick so we don't need a call to int.TryParse

			// Hide leading zeros
			if (!foundNonZero && digit == 0 && i != 2) // Keep the last digit visible even if it's 0
			{
				digitImages[i].Image.sprite = numberSprites[10]; // 10 = blank
			}
			else
			{
				foundNonZero = true;
				digitImages[i].Image.sprite = numberSprites[digit];
			}
		}
	}

	private void HandsChanged(IHandsController handsController)
	{
		if (handsController is Player.FirearmController faController)
		{
			faController.OnShot += () => AmmoChanged(faController);
			faController.OnReadyToOperate += AmmoChanged;
			faController.Weapon.GetMagazineSlot().OnAddOrRemoveItem += (_) => AmmoChanged(faController);
		}
	}

	private void AmmoChanged(IHandsController handsController)
	{
		if (handsController == null || handsController.Item is not Weapon weapon)
		{
			SetNumberDigits(ammoVal, 0);
			SetCritical(ammoGroup, true);
			Highlight(ammoGroup);
			return;
		}

		int ammoCount = 0;
		if (handsController is Player.FirearmController faController)
		{
			ammoCount += weapon.ChamberAmmoCount;
			ammoCount += weapon.GetCurrentMagazineCount();
		}

		SetNumberDigits(ammoVal, ammoCount);
		SetCritical(ammoGroup, ammoCount == 0); // TODO: Calculate ammo vs total possible with mag, base on percentage
		Highlight(ammoGroup);
	}

	private void HealthChanged()
	{
		// FIXME/TODO: Assumes normal 440 max health player, may break if health is modded higher
		float health = GamePlayerOwner.MyPlayer.ActiveHealthController.GetBodyPartHealth(EBodyPart.Common).Current;
		int normalizedHealth = Mathf.CeilToInt(health / 440f * 100f);
		SetNumberDigits(healthVal, normalizedHealth);
		SetCritical([healthIcon, healthVal[0], healthVal[1], healthVal[2]], normalizedHealth <= 25);
		Highlight([healthIcon, healthVal[0], healthVal[1], healthVal[2]]);
	}

	private void SuitPowerChanged()
	{
		int temp = 100;
		char[] digits = temp.ToString("000").ToCharArray();
		suitFull.Image.fillAmount = temp / 100f;
		SetNumberDigits(suitVal, temp);
		//SetCritical(suitGroup, temp);
		Highlight(suitGroup);
	}

	/// <summary>
	/// Display a notification icon at the right side of the screen, managed by a VerticalLayoutGroup
	/// </summary>
	/// <param name="fileName"></param>
	private void NotifyIcon(Sprite icon, bool isDamage)
	{
		// TODO: Cache icon images. Just load on demand for now
		GameObject iconObj = new("icon");
		iconObj.transform.parent = isDamage ? damageNotificationArea : notificationArea;
		Image iconImage = iconObj.AddComponent<Image>();
		iconImage.sprite = icon;
		iconImage.color = hudColorActive;
		EImageState state = isDamage ? EImageState.DamageNotification : EImageState.Notification;
		HudImage hudImage = new(iconImage) { State = state };
		allHudImages.Add(hudImage);
		
		if (isDamage)
			activeDamageNotifications.Add(hudImage);
		else
			activeNotifications.Add(hudImage);

		// Don't overflow, kill the oldest one.
		// FIXME: separate the two areas
		if (activeNotifications.Count > maxNotifications)
			activeNotifications[0].State = EImageState.DestroyNotificationImmediate;
		
		if (activeDamageNotifications.Count > maxNotifications)
			activeDamageNotifications[0].State = EImageState.DestroyDamageNotificationImmediate;
	}

	/// <summary>
	/// Event
	/// </summary>
	/// <param name="damageInfo"></param>
	private void OnTakeDamage(DamageInfoStruct damageInfo)
	{
		// FIXME: Switch to damageInfo.Direction when you finally make sense of it
		if (damageInfo.Player == null)
		{
			// World damage, show all damage indicators
			hitIndicatorUp.State = EImageState.HitIndicator;
			hitIndicatorRight.State = EImageState.HitIndicator;
			hitIndicatorDown.State = EImageState.HitIndicator;
			hitIndicatorLeft.State = EImageState.HitIndicator;
			return;
		}

		Vector3 attackerPos = damageInfo.Player.iPlayer.Position;
		Vector3 myPos = GamePlayerOwner.MyPlayer.Position;
		Vector3 myLookDir = GamePlayerOwner.MyPlayer.LookDirection.normalized;

		// Get direction to attacker
		Vector3 toAttacker = (attackerPos - myPos).normalized;

		// Convert world space direction to local space relative to where I'm looking
		Vector3 localDir = Quaternion.Inverse(Quaternion.LookRotation(myLookDir)) * toAttacker;
		localDir.y = 0;
		localDir.Normalize();

		// Get horizontal angle in degrees (0 = front, 90 = right, 180 = back, 270 = left)
		float angle = Mathf.Atan2(localDir.x, localDir.z) * Mathf.Rad2Deg;
		if (angle < 0) angle += 360f;

		// Decide which hit indicators to show based on angle
		int dirIndex = Mathf.FloorToInt((angle + 22.5f) % 360f / 45f);
		foreach (var image in hitIndicatorDirections[dirIndex])
			image.State = EImageState.HitIndicator;
	}
}
