using BepInEx.Logging;
using Comfort.Common;
using EFT;
using EFT.InventoryLogic;
using System;
using System.Collections.Generic;
using UnityEngine;

namespace HEVSuitMod;

public class HudController : MonoBehaviour
{
	public const float AMMO_RATIO_CRITICAL = 0.2f; // 20% left in mag+chamber
	public const int MAX_NOTIFICATIONS = 7; // The area fits 7 images at 100x100 comfortably
	public const float NOTIFY_TIME = 1.5f;
	public const float DMG_NOTIFY_TIME = 4f;
	public const float ACTIVATE_TIME = 0.25f;
	public const float FADE_TIME = 0.5f;
	public const float FLASH_TIME = 1f;

	private readonly ManualLogSource log = BepInEx.Logging.Logger.CreateLogSource($"{typeof(HudController).FullName}");

    // TODO: MIN_ALPHA 0.4 from hl1 looks a little too dark in Unity, maybe tinker with it?
    public Color hudColor = new(1f, 0.627f, 0f, 0.4f); // Matches 'RGB_YELLOWISH' and 'MIN_ALPHA' from hl1\cl_dll\hud.h
	public Color hudColorActive = new(1f, 0.8f, 0f, 1f); // Brighter
	public Color hudColorCritical = new(1f, 0f, 0f, 0.4f); // Red
	public Color hudColorCriticalActive = new(1f, 0f, 0f, 1f); // Brighter red
	public Color damageIndicatorColor = new(1f, 1f, 1f, 0.6f); // Slightly transparent

	// Number sprites
	private readonly Sprite[] numberSprites = new Sprite[11]; // 0-9 plus a blank one

	// Sub components
	HudWeaponSelection hudWeaponSelection;
	HudDamageIndicators hudDamageIndicators;
	HudDamageIcons hudDamageIcons;
	HudFlashlight hudFlashlight;
	HudAmmoCounter hudAmmoCounter;
	HudHealthCounter hudHealthCounter;
	HudSuitPowerCounter hudSuitPowerCounter;

	private readonly Dictionary<Item, Sprite> spriteCache = [];

	public GameObject Hud { get; private set; }

	private void Awake()
	{
		AssetBundle assets = HEVMod.Instance.Assets; // Shortcut
		
		// Instantiate HUD prefab and weapon selection controller
		Hud = Instantiate(assets.LoadAsset<GameObject>("assets/prefabs/hud.prefab"));

		// Cache number sprites, index 10 is a blank sprite
		for (int i = 0; i < 10; i++) numberSprites[i] = assets.LoadAsset<Sprite>($"assets/sprites/hud_number_{i}.tga");
		numberSprites[10] = assets.LoadAsset<Sprite>($"assets/sprites/hud_number_blank.tga");

		// TODO: Separate all hud functionality into their own components
		hudWeaponSelection = Hud.AddComponent<HudWeaponSelection>();
		hudDamageIndicators = Hud.AddComponent<HudDamageIndicators>();
		hudDamageIcons = Hud.AddComponent<HudDamageIcons>();
		hudFlashlight = Hud.AddComponent<HudFlashlight>();
		hudAmmoCounter = Hud.AddComponent<HudAmmoCounter>();
		hudHealthCounter = Hud.AddComponent<HudHealthCounter>();
		hudSuitPowerCounter = Hud.AddComponent<HudSuitPowerCounter>();
	}

	private void Start()
	{
		GamePlayerOwner.MyPlayer.OnInventoryOpened += OnInventoryOpened;
		Hud?.SetActive(true);
	}

	private void OnDestroy()
	{
		GamePlayerOwner.MyPlayer.OnInventoryOpened -= OnInventoryOpened;
		Destroy(hudWeaponSelection);
		Destroy(hudDamageIndicators);
		Destroy(hudDamageIcons);
		Destroy(hudFlashlight);
		Destroy(hudAmmoCounter);
		Destroy(hudHealthCounter);
		Destroy(hudSuitPowerCounter);
	}

	private void OnInventoryOpened(Player player, bool closing)
	{
		// Shouldn't happen but just be safe
		if (player != GamePlayerOwner.MyPlayer)
			return;

		enabled = !closing;
		Hud.SetActive(!closing);
	}

	public void StartTransition(HudIcon img, EIconState nextState)
	{
		img.timer = 0f;
		img.lastColor = img.image.color;
		img.state = nextState;
	}

	// Returns true if transition completed
	public bool UpdateTransition(HudIcon img, float duration, Color target)
	{
		img.timer += Time.deltaTime;
		if (img.timer >= duration)
		{
			img.image.color = target;
			img.timer = 0f;
			return true;
		}
		float t = img.timer / duration;
		img.image.color = Color.Lerp(img.lastColor, target, t);
		return false;
	}

	public void StateUpdate(HudIcon[] icons)
	{
		foreach (HudIcon icon in icons)
		{
			Color activeColor = icon.critical ? hudColorCriticalActive : hudColorActive;
			Color inactiveColor = icon.critical ? hudColorCritical : hudColor;

			switch (icon.state)
			{
				case EIconState.Active:
				case EIconState.Inactive:
					break;

				case EIconState.Deactivating:
					if (UpdateTransition(icon, ACTIVATE_TIME, inactiveColor))
						icon.state = EIconState.Inactive;
					break;

				case EIconState.Activating:
					if (UpdateTransition(icon, ACTIVATE_TIME, activeColor))
						icon.state = EIconState.Active;
					break;

				case EIconState.Highlight:
					icon.image.color = activeColor;
					StartTransition(icon, EIconState.FadeHighlight);
					break;

				case EIconState.FadeHighlight:
					if (UpdateTransition(icon, FADE_TIME, inactiveColor))
						icon.state = EIconState.Inactive;
					break;
			}
		}
	}

	public void SetCritical(HudIcon[] icons, bool critical)
	{
		foreach (HudIcon icon in icons)
		{
			icon.critical = critical;
			icon.image.color = icon.state switch
			{
				EIconState.Inactive => critical ? hudColorCritical : hudColor,
				_ => critical ? hudColorCriticalActive : hudColorActive
			};
		}
	}

	public void Highlight(HudIcon[] images)
	{
		foreach (HudIcon image in images)
			image.state = EIconState.Highlight;
	}

	public void SetNumberDigits(HudIcon[] digitImages, int number)
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
				digitImages[i].image.sprite = numberSprites[10]; // 10 = blank
			}
			else
			{
				foundNonZero = true;
				digitImages[i].image.sprite = numberSprites[digit];
			}
		}
	}

	/// <summary>
	/// Get a brightened greyscale hud sprite for a given item that will be colorized by code or in the hud prefab
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
