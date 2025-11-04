using BepInEx.Logging;
using EFT;
using EFT.InventoryLogic;
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;

namespace HEVSuitMod;

public class HudController : MonoBehaviour
{
	private const float AMMO_RATIO_CRITICAL = 0.2f; // 20% left in mag+chamber
	private const int MAX_NOTIFICATIONS = 7; // The area fits 7 images at 100x100 comfortably
	private const float NOTIFY_TIME = 1.5f;
	private const float DMG_NOTIFY_TIME = 4f;
	private const float ACTIVATE_TIME = 0.25f;
	private const float FADE_TIME = 0.5f;

	//public static HudController Instance { get; private set; }
	private ManualLogSource log = BepInEx.Logging.Logger.CreateLogSource("HEVSuitMod.HudController");
	public GameObject Hud { get; private set; }

	// TODO: MIN_ALPHA 0.4 from hl1 looks a little too dark in Unity, maybe tinker with it?
	private Color hudColor = new(1f, 0.627f, 0f, 0.4f); // Matches 'RGB_YELLOWISH' and 'MIN_ALPHA' from hl1\cl_dll\hud.h
	private Color hudColorActive = new(1f, 0.8f, 0f, 1f); // Brighter
	private Color hudColorCritical = new(1f, 0f, 0f, 0.4f); // Red
	private Color hudColorCriticalActive = new(1f, 0f, 0f, 1f); // Brighter red
	private Color hitIndicatorColor = new(1f, 1f, 1f, 0.75f); // Slightly transparent

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
	private HudImage ammoIcon;
	private HudImage[] ammoVal = new HudImage[3];
	private HudImage[] ammoGroup = new HudImage[4]; // Group of all ammo images

	// Flashlight
	private HudImage flashEmpty;
	private HudImage flashFull;
	private HudImage flashBeam;
	private HudImage[] flashGroup = new HudImage[3];

	// Hit indicators - Order in prefab: Up Right Down Left
	private HudImage hitIndicatorUp;
	private HudImage hitIndicatorRight;
	private HudImage hitIndicatorDown;
	private HudImage hitIndicatorLeft;
	private HudImage[][] hitIndicatorDirections;

	// Damage type notifications
	private Sprite bulletDamage;
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
	private readonly List<HudImage> activeNotifications = [];
	private readonly List<HudImage> activeDamageNotifications = [];

	// For state machine
	private readonly List<HudImage> allHudImages = [];

	// Separated weapon selection into its own component
	WeaponSelectionController weaponSelectionController;

	// Delegates
	private Action<EBodyPart, float, DamageInfoStruct> HealthChangedAction;
	private Action<DamageInfoStruct, EBodyPart, float> SuitPowerChangedAction;
	private Action<Item> SuitChangedAction;
	private GDelegate70 PlayerDeadAction;

	private void Awake()
	{
		AssetBundle assets = HEVMod.Instance.Assets; // Shortcut
		
		// Event stuff
		HealthChangedAction = (_, _, _) => HealthChanged();
		SuitPowerChangedAction = (damageInfo, _, _) => SuitPowerChanged(damageInfo);
		SuitChangedAction = (_) => SuitPowerChanged(null);
		PlayerDeadAction = (_, _, _, _) => HealthChanged();

		// Instantiate HUD prefab and weapon selection controller
		Hud = Instantiate(assets.LoadAsset<GameObject>("assets/prefabs/hud.prefab"));
		weaponSelectionController = Hud.AddComponent<WeaponSelectionController>();

		// Cache number sprites, index 10 is a blank sprite
		for (int i = 0; i < 10; i++) numberSprites[i] = assets.LoadAsset<Sprite>($"assets/sprites/hud_number_{i}.tga");
		numberSprites[10] = assets.LoadAsset<Sprite>($"assets/sprites/hud_number_blank.tga");

		// Health digits and icon
		for (int i = 0; i < 3; i++) healthVal[i] = new(Utils.FindComponent<Image>(Hud, $"HealthAndSuitPower/HealthValue/Digit{i}"));
		healthIcon = new(Utils.FindComponent<Image>(Hud, "HealthAndSuitPower/HealthIcon"));
		healthGroup = [healthIcon, healthVal[0], healthVal[1], healthVal[2]];

		// SuitPower digits and icon
		for (int i = 0; i < 3; i++) suitVal[i] = new(Utils.FindComponent<Image>(Hud, $"HealthAndSuitPower/SuitPowerValue/Digit{i}"));
		suitEmpty = new(Utils.FindComponent<Image>(Hud, "HealthAndSuitPower/SuitIconEmpty"));
		suitFull = new(Utils.FindComponent<Image>(Hud, "HealthAndSuitPower/SuitIconFull"));
		suitGroup = [suitFull, suitEmpty, suitVal[0], suitVal[1], suitVal[2]];

		// Ammo counter and icon
		for (int i = 0; i < 3; i++) ammoVal[i] = new(Utils.FindComponent<Image>(Hud, $"AmmoCounter/Value/Digit{i}"));
		ammoIcon = new(Utils.FindComponent<Image>(Hud, "AmmoCounter/Icon"));
		ammoGroup = [ammoIcon, ammoVal[0], ammoVal[1], ammoVal[2]];

		// Flashlight indicator
		flashEmpty = new(Utils.FindComponent<Image>(Hud, "Flashlight/IconEmpty"));
		flashFull = new(Utils.FindComponent<Image>(Hud, "Flashlight/IconFull"));
		flashBeam = new(Utils.FindComponent<Image>(Hud, "Flashlight/Beam"));
		flashBeam.Image.enabled = false; // start off and full battery
		flashFull.Image.fillAmount = 1f;
		flashGroup = [flashEmpty, flashFull, flashBeam];

		// Hit indicator child objects are in order: Up Right Down Left
		Image[] hitIndicatorImg = Utils.FindComponentsInChildren<Image>(Hud, "HitIndicators");
		hitIndicatorUp = new(hitIndicatorImg[0]);
		hitIndicatorRight = new(hitIndicatorImg[1]);
		hitIndicatorDown = new(hitIndicatorImg[2]);
		hitIndicatorLeft = new(hitIndicatorImg[3]);
		foreach (Image hit in hitIndicatorImg) hit.color = Color.clear; // Start transparent

		// Notification areas
		notificationArea = Hud.transform.Find("RightNotifyArea");
		damageNotificationArea = Hud.transform.Find("LeftNotifyArea");

		// Damage type sprites
		bulletDamage = assets.LoadAsset<Sprite>("assets/sprites/hud_dmg_bullet.tga");
		coldDamage = assets.LoadAsset<Sprite>("assets/sprites/hud_dmg_cold.tga");
		fireDamage = assets.LoadAsset<Sprite>("assets/sprites/hud_dmg_heat.tga");
		explosionDamage = assets.LoadAsset<Sprite>("assets/sprites/hud_dmg_explosion.tga");
		barbwireDamage = assets.LoadAsset<Sprite>("assets/sprites/hud_dmg_barbed.tga");
		toxinDamage = assets.LoadAsset<Sprite>("assets/sprites/hud_dmg_bio.tga");
		radiationDamage = assets.LoadAsset<Sprite>("assets/sprites/hud_dmg_rad.tga");
		dehydrationDamage = assets.LoadAsset<Sprite>("assets/sprites/hud_dmg_dehydrated.tga");
		exhaustionDamage = assets.LoadAsset<Sprite>("assets/sprites/hud_dmg_exhausted.tga");

		// Map the 8 hit directions to our indicators, like a compass for pain
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
		allHudImages.AddRange(healthGroup);
		allHudImages.AddRange(suitGroup);
		allHudImages.AddRange(flashGroup);
		allHudImages.AddRange(ammoGroup);
		allHudImages.AddRange([hitIndicatorUp, hitIndicatorRight, hitIndicatorDown, hitIndicatorLeft]);
	}

	private void OnEnable()
	{
		GamePlayerOwner.MyPlayer.BeingHitAction += TakeDamage;
		GamePlayerOwner.MyPlayer.BeingHitAction += SuitPowerChangedAction;
		GamePlayerOwner.MyPlayer.Equipment.GetSlot(EquipmentSlot.TacticalVest).OnAddOrRemoveItem += SuitChangedAction;
		GamePlayerOwner.MyPlayer.ActiveHealthController.HealthChangedEvent += HealthChangedAction;
		GamePlayerOwner.MyPlayer.HandsChangedEvent += HandsChanged;
		GamePlayerOwner.MyPlayer.OnPlayerDead += PlayerDeadAction;
		HEVMod.Instance.Flashlight.Toggled += FlashlightToggled;
		HEVMod.Instance.Flashlight.BatteryUpdate += FlashlightBatteryChanged;
		HEVMod.Instance.Flashlight.BatteryStateChanged += FlashlightBatteryCritical;
		Hud?.SetActive(true);
		HealthChanged();
		SuitPowerChanged(null);
	}

	private void OnDisable()
	{
		GamePlayerOwner.MyPlayer.BeingHitAction -= TakeDamage;
		GamePlayerOwner.MyPlayer.BeingHitAction -= SuitPowerChangedAction;
		GamePlayerOwner.MyPlayer.Equipment.GetSlot(EquipmentSlot.TacticalVest).OnAddOrRemoveItem -= SuitChangedAction;
		GamePlayerOwner.MyPlayer.ActiveHealthController.HealthChangedEvent -= HealthChangedAction;
		GamePlayerOwner.MyPlayer.HandsChangedEvent -= HandsChanged;
		GamePlayerOwner.MyPlayer.OnPlayerDead -= PlayerDeadAction;
		HEVMod.Instance.Flashlight.Toggled -= FlashlightToggled;
		HEVMod.Instance.Flashlight.BatteryUpdate -= FlashlightBatteryChanged;
		HEVMod.Instance.Flashlight.BatteryStateChanged -= FlashlightBatteryCritical;
		Hud?.SetActive(false);
	}

	private void OnDestroy()
	{
		Destroy(weaponSelectionController);
	}

	private void Update()
	{
#if DEBUG // Add a notification test
		if (Input.GetKeyDown(KeyCode.F6))
			NotifyIcon(fireDamage, false);
#endif
		// Iterate backward so we can safely RemoveAt(i) for notification icons
		for (int i = allHudImages.Count -1; i >= 0; i--)
		{
			HudImage img = allHudImages[i];
			Color activeColor = img.Critical ? hudColorCriticalActive : hudColorActive;
			Color inactiveColor = img.Critical ? hudColorCritical : hudColor;

			switch (img.State)
			{
				case EImageState.Active:
				case EImageState.Inactive:
					break;

				// Ramp the brightness down to normal and mark as 'inactive'
				case EImageState.Deactivating:
					if(UpdateTransition(img, ACTIVATE_TIME, inactiveColor))
						img.State = EImageState.Inactive;
					break;

				// Ramp the brightness up to max and mark as 'active'
				case EImageState.Activating:
					if(UpdateTransition(img, ACTIVATE_TIME, activeColor))
						img.State = EImageState.Active;
					break;

				// Highlight 1: Make the image brighter for an instant
				case EImageState.Highlight:
					img.Image.color = activeColor;
					StartTransition(img, EImageState.FadeHighlight);
					break;

				// 2: Fade back to normal
				case EImageState.FadeHighlight:
					if(UpdateTransition(img, FADE_TIME, inactiveColor))
						img.State = EImageState.Inactive;
					break;

				// HitIndicator 1: Set indicator to full opacity
				case EImageState.HitIndicator:
					img.Image.color = hitIndicatorColor;
					StartTransition(img, EImageState.FadeHitIndicator);
					break;

				// 2: Fade indicator to zero opacity
				case EImageState.FadeHitIndicator:
					if(UpdateTransition(img, FADE_TIME, Color.clear))
						img.State = EImageState.Inactive;
					break;

				// DamageNotification 1: A damage notification spawned, pulse it bright<->clear a few times
				case EImageState.DamageNotification:
					img.Timer += Time.deltaTime;
					img.Image.color = Color.Lerp(Color.clear, activeColor, (Mathf.Sin(img.Timer * 4f) + 1f) * 0.5f);
					if (img.Timer >= DMG_NOTIFY_TIME)
						StartTransition(img, EImageState.ExpireDamageNotification);
					break;

				// 2. Stop pulsing and fade away
				case EImageState.ExpireDamageNotification:
					if (UpdateTransition(img, FADE_TIME, Color.clear))
						img.State = EImageState.DestroyDamageNotification;
					break;

				// 3. Faded fully, destroy it
				case EImageState.DestroyDamageNotification:
					Destroy(img.Image.gameObject);
					allHudImages.RemoveAt(i);
					activeDamageNotifications.Remove(img);
					break;

				// Notification 1: A notification spawned, make it bright and fade to normal
				case EImageState.Notification:
					if (UpdateTransition(img, FADE_TIME, inactiveColor))
						StartTransition(img, EImageState.IdleNotification);
					break;

				// 2: Stay idle for a sec
				case EImageState.IdleNotification:
					img.Timer += Time.deltaTime;
					if (img.Timer >= NOTIFY_TIME)
						StartTransition(img, EImageState.ExpireNotification);
					break;

				// 3: Fade away
				case EImageState.ExpireNotification:
					if (UpdateTransition(img, FADE_TIME, Color.clear))
						img.State = EImageState.DestroyNotification;
					break;

				// 4: Faded fully, destroy it
				case EImageState.DestroyNotification:
					Destroy(img.Image.gameObject);
					allHudImages.RemoveAt(i);
					activeNotifications.Remove(img);
					break;
			}

			// Highlight critical health or ammo once every second
			if (Time.time % 1f < Time.deltaTime)
			{
				if (healthIcon.Critical)
					Highlight(healthGroup);

				if (ammoIcon.Critical)
					Highlight(ammoGroup);
			}
		}
	}

	private void StartTransition(HudImage img, EImageState nextState)
	{
		img.Timer = 0f;
		img.LastColor = img.Image.color;
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

	private void FlashlightBatteryChanged(float battery)
	{
		flashFull.Image.fillAmount = battery;
	}

	private void FlashlightBatteryCritical(bool isLow)
	{
		SetCritical(flashGroup, isLow);
	}

	private void FlashlightToggled(bool isOn)
	{
		flashBeam.Image.enabled = isOn;
		foreach (HudImage image in flashGroup)
			StartTransition(image, isOn ? EImageState.Activating : EImageState.Deactivating);
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
			AmmoChanged(faController);
		}
	}

	public void AmmoChanged(IHandsController handsController)
	{
		if (handsController == null || handsController.Item is not Weapon weapon)
		{
			SetNumberDigits(ammoVal, 0);
			SetCritical(ammoGroup, true);
			Highlight(ammoGroup);
			return;
		}

		// TODO/FIXME: This is not working for weapons with internal mags when reloading
		int maxAmmo = weapon.Chambers.Length + weapon.GetMaxMagazineCount();
		int ammoCount = weapon.ChamberAmmoCount + weapon.GetCurrentMagazineCount();
		float ammoRatio = (float)ammoCount / maxAmmo;
		bool critical = ammoRatio <= AMMO_RATIO_CRITICAL;
		log.LogDebug($"AmmoChanged: maxAmmo {maxAmmo}, ammoCount {ammoCount}, ammoRatio {ammoRatio}");
		SetNumberDigits(ammoVal, ammoCount);
		SetCritical(ammoGroup, critical);
		Highlight(ammoGroup);
	}

	private void HealthChanged(bool alive = true)
	{
		// FIXME/TODO: Assumes normal 440 max health player, may break if health is modded higher
		int normalizedHealth = 0;
		if (alive)
		{
			float health = GamePlayerOwner.MyPlayer.ActiveHealthController.GetBodyPartHealth(EBodyPart.Common).Current;
			normalizedHealth = Mathf.CeilToInt(health / 440f * 100f);
		}
		SetNumberDigits(healthVal, normalizedHealth);
		SetCritical(healthGroup, normalizedHealth <= /*25*/ 90); // FIXME: just testing, change back to 25 later
		Highlight(healthGroup);
	}

	private void SuitPowerChanged(DamageInfoStruct? damageInfo)
	{
		// TODO: This will eventually be the HEV suit itself, using a Strandhogg for testing right now
		float current = 0, max = 0;
		if (GamePlayerOwner.MyPlayer.Equipment.GetSlot(EquipmentSlot.TacticalVest).ContainedItem is not VestItemClass vest)
		{
			suitFull.Image.fillAmount = 0f;
			SetNumberDigits(suitVal, 0);
			return;
		}

		foreach (Slot slot in vest.Slots)
		{
			if (slot.ContainedItem == null)
				continue;

			ArmoredEquipmentItemClass component = slot.ContainedItem as ArmoredEquipmentItemClass;
			current += component.Repairable.Durability;
			max += component.Repairable.MaxDurability;
		}

		int normalized = Mathf.CeilToInt(current / max * 100f);
		suitFull.Image.fillAmount = normalized / 100f;
		SetNumberDigits(suitVal, normalized);
		if (damageInfo?.DidArmorDamage < 0.01)
			return; // Don't highlight unless it was noticeable

		// HL1 doesn't set suit to red, TODO: check hl1 code to confirm
		Highlight(suitGroup);
	}

	private void NotifyIcon(Sprite icon, bool isDamage)
	{
		EImageState iconState = isDamage ? EImageState.DamageNotification : EImageState.Notification;
		Transform iconParent = isDamage ? damageNotificationArea : notificationArea;
		GameObject iconObj = new("icon");
		iconObj.transform.parent = iconParent;
		Image iconImage = iconObj.AddComponent<Image>();
		iconImage.sprite = icon;
		iconImage.color = hudColorActive;
		HudImage hudImage = new(iconImage) { State = iconState };
		allHudImages.Add(hudImage);
		
		if (isDamage)
			activeDamageNotifications.Add(hudImage);
		else
			activeNotifications.Add(hudImage);

		// Don't overflow, kill the oldest one.
		if (activeNotifications.Count > MAX_NOTIFICATIONS)
			StartTransition(activeNotifications[0], EImageState.DestroyNotification);

		if (activeDamageNotifications.Count > MAX_NOTIFICATIONS)
			StartTransition(activeDamageNotifications[0], EImageState.DestroyDamageNotification);
	}

	private void TakeDamage(DamageInfoStruct damageInfo, EBodyPart part, float amount)
	{
		Sprite damageIcon = null;
		switch (damageInfo.DamageType)
		{
			case var dt when (dt & (EDamageType.Landmine | EDamageType.Explosion | EDamageType.ThermobaricExplosion | EDamageType.GrenadeFragment)) != 0:
				damageIcon = explosionDamage;
				break;

			case var dt when (dt & (EDamageType.HotGases | EDamageType.Flame)) != 0:
				damageIcon = fireDamage;
				break;

			case var dt when (dt & (EDamageType.LethalToxin | EDamageType.Poison)) != 0:
				damageIcon = toxinDamage;
				break;

			case var dt when (dt & EDamageType.Barbed) != 0:
				damageIcon = barbwireDamage;
				break;

			case var dt when (dt & EDamageType.RadExposure) != 0:
				damageIcon = radiationDamage;
				break;

			case var dt when (dt & EDamageType.Bullet) != 0:
				damageIcon = bulletDamage;
				break;

			case var dt when (dt & EDamageType.Exhaustion) != 0:
				damageIcon = exhaustionDamage;
				break;

			case var dt when (dt & EDamageType.Dehydration) != 0:
				damageIcon = dehydrationDamage;
				break;

			case var dt when (dt & EDamageType.Environment) != 0: // TODO: Verify this is freezing in winter
				damageIcon = coldDamage;
				break;
		}

		// Don't notify for the same damage type twice
		if (damageIcon != null && !activeDamageNotifications.Any(x => x.Image.sprite == damageIcon))
			NotifyIcon(damageIcon, true);

		// FIXME: World damage seems to always come from in front of the player which looks stupid
		Vector3 lookDir = GamePlayerOwner.MyPlayer.LookDirection.normalized;
		Vector3 localDir = Quaternion.Inverse(Quaternion.LookRotation(lookDir)) * -damageInfo.Direction;
		localDir.y = 0;
		localDir.Normalize();

		// Get angle in degrees (0 = front, 90 = right, 180 = back, 270 = left)
		float angle = Mathf.Atan2(localDir.x, localDir.z) * Mathf.Rad2Deg;
		if (angle < 0) angle += 360f;

		// Decide which hit indicators to show based on angle
		int dirIndex = Mathf.FloorToInt((angle + 22.5f) % 360f / 45f);
		foreach (var image in hitIndicatorDirections[dirIndex])
			StartTransition(image, EImageState.HitIndicator);
	}
}
