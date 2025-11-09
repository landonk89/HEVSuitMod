using BepInEx.Logging;
using Comfort.Common;
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
	private readonly ManualLogSource log = BepInEx.Logging.Logger.CreateLogSource($"{typeof(HudController).FullName}");
	public GameObject Hud { get; private set; }

    // TODO: MIN_ALPHA 0.4 from hl1 looks a little too dark in Unity, maybe tinker with it?
    public Color hudColor = new(1f, 0.627f, 0f, 0.4f); // Matches 'RGB_YELLOWISH' and 'MIN_ALPHA' from hl1\cl_dll\hud.h
	public Color hudColorActive = new(1f, 0.8f, 0f, 1f); // Brighter
	public Color hudColorCritical = new(1f, 0f, 0f, 0.4f); // Red
	public Color hudColorCriticalActive = new(1f, 0f, 0f, 1f); // Brighter red

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

	// For state machine
	private readonly List<HudImage> allHudImages = [];

	// Sub components
	HudWeaponSelection hudWeaponSelection;
	HudDamageIndicators hudDamageIndicators;
	HudDamageIcons hudDamageIcons;
	HudFlashlight hudFlashlight;

	// Delegates
	private Action<EBodyPart, float, DamageInfoStruct> HealthChangedAction;
	private Action<DamageInfoStruct, EBodyPart, float> SuitPowerChangedAction;
	private Action<Item> SuitChangedAction;
	private GDelegate70 PlayerDeadAction;

	private Dictionary<Item, Sprite> spriteCache = [];

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

		// TODO: Separate all hud functionality into their own components
		hudWeaponSelection = Hud.AddComponent<HudWeaponSelection>();
		hudDamageIndicators = Hud.AddComponent<HudDamageIndicators>();
		hudDamageIcons = Hud.AddComponent<HudDamageIcons>();
		hudFlashlight = Hud.AddComponent<HudFlashlight>();

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

		// For state machine
		allHudImages.AddRange(healthGroup);
		allHudImages.AddRange(suitGroup);
		allHudImages.AddRange(ammoGroup);
	}

	private void Start()
	{
		GamePlayerOwner.MyPlayer.BeingHitAction += SuitPowerChangedAction;
		GamePlayerOwner.MyPlayer.Equipment.GetSlot(EquipmentSlot.TacticalVest).OnAddOrRemoveItem += SuitChangedAction;
		GamePlayerOwner.MyPlayer.ActiveHealthController.HealthChangedEvent += HealthChangedAction;
		GamePlayerOwner.MyPlayer.HandsChangedEvent += HandsChanged;
		GamePlayerOwner.MyPlayer.OnPlayerDead += PlayerDeadAction;
		GamePlayerOwner.MyPlayer.OnInventoryOpened += OnInventoryOpened;
		Hud?.SetActive(true);
		HealthChanged();
		SuitPowerChanged(null);
	}

	private void OnDestroy()
	{
		GamePlayerOwner.MyPlayer.BeingHitAction -= SuitPowerChangedAction;
		GamePlayerOwner.MyPlayer.Equipment.GetSlot(EquipmentSlot.TacticalVest).OnAddOrRemoveItem -= SuitChangedAction;
		GamePlayerOwner.MyPlayer.ActiveHealthController.HealthChangedEvent -= HealthChangedAction;
		GamePlayerOwner.MyPlayer.HandsChangedEvent -= HandsChanged;
		GamePlayerOwner.MyPlayer.OnPlayerDead -= PlayerDeadAction;
		GamePlayerOwner.MyPlayer.OnInventoryOpened -= OnInventoryOpened;
		Destroy(hudWeaponSelection);
		Destroy(hudDamageIndicators);
		Destroy(hudDamageIcons);
		Destroy(hudFlashlight);
	}

	private void Update()
	{
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

	private void OnInventoryOpened(Player player, bool closing)
	{
		// Shouldn't happen but just be safe
		if (player != GamePlayerOwner.MyPlayer)
			return;

		enabled = !closing;
		Hud.SetActive(!closing);
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
		SetCritical(healthGroup, normalizedHealth <= 20);
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
	
	/// <summary>
	/// Get a hud sprite for a given item.
	/// </summary>
	/// <param name="item"></param>
	/// <returns></returns>
	public Sprite GetItemSprite(Item item, XYCellSizeStruct gridSize)
	{
		if (item == null)
			return CacheResourcesPopAbstractClass.Pop<Sprite>("What");

		if (spriteCache.TryGetValue(item, out Sprite cachedSprite))
			return cachedSprite;

		GClass929 generatedImage = Singleton<GClass926>.Instance.GetItemIcon(item, gridSize);
		if (generatedImage == null || generatedImage.Sprite == null)
			return CacheResourcesPopAbstractClass.Pop<Sprite>("What");

		Texture2D tex = Instantiate(generatedImage.Sprite.texture);
		Color[] pixels = tex.GetPixels();
		for (int i = 0; i < pixels.Length; i++)
		{
			// Convert to grayscale and adjust exposure
			Color c = pixels[i];
			float gray = c.r * 0.299f + c.g * 0.587f + c.b * 0.114f;
			gray = Mathf.Lerp(gray, 1f, 0.5f);
			gray *= 1.5f;
			pixels[i] = new Color(gray, gray, gray, c.a);
		}

		tex.SetPixels(pixels);
		tex.Apply();

		Sprite output = Sprite.Create(tex, new Rect(0, 0, tex.width, tex.height), new Vector2(0.5f, 0.5f));
		spriteCache[item] = output;
		return output;
	}
}
