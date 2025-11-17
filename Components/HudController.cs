using BepInEx.Logging;
using Comfort.Common;
using EFT;
using EFT.InventoryLogic;
using HEVSuitMod.Types;
using System.Collections.Generic;
using UnityEngine;

namespace HEVSuitMod.Components;

public class HudController : MonoBehaviour
{
	public const float AMMO_RATIO_CRITICAL = 0.2f; // 20% left in mag+chamber
	public const int HEALTH_CRITICAL = 20;
	public const int MAX_NOTIFY_ICONS = 7; // The area fits 7 images at 100x100 comfortably
	public const int MAX_DMG_ICONS = 5;
	public const float NOTIFY_FLASH_TIME = 1.5f;
	public const float NOTIFY_STAY_TIME = 2f;
	public const float DMG_NOTIFY_TIME = 4f;
	public const float ACTIVATE_TIME = 0.25f;
	public const float FADE_TIME = 0.5f;
	public const float FLASH_TIME = 1f;

	private readonly ManualLogSource log = BepInEx.Logging.Logger.CreateLogSource(typeof(HudController).FullName);
	public readonly Color hudColor = new(1f, 0.627f, 0f, 0.4f); // Matches 'RGB_YELLOWISH' and 'MIN_ALPHA' from hl1\cl_dll\hud.h
	public readonly Color hudColorActive = new(1f, 0.8f, 0f, 1f); // Brighter
	public readonly Color hudColorCritical = new(1f, 0f, 0f, 0.4f); // Red
	public readonly Color hudColorCriticalActive = new(1f, 0f, 0f, 1f); // Brighter red
	public readonly Color damageIndicatorColor = new(1f, 1f, 1f, 0.6f); // Slightly transparent

	protected readonly Dictionary<Item, Sprite> spriteCache = [];
	protected readonly Sprite[] numberSprites = new Sprite[11]; // 0-9 plus a blank one

	HudWeaponSelection hudWeaponSelection;
	HudDamageIndicators hudDamageIndicators;
	HudDamageTypes hudDamageIcons;
	HudFlashlight hudFlashlight;
	HudAmmoCounter hudAmmoCounter;
	HudHealthCounter hudHealthCounter;
	HudSuitPowerCounter hudSuitPowerCounter;
	HudItemPickups hudItemPickups;
	HudCrosshair hudCrosshair;

	public GameObject Hud { get; private set; }
	private AssetBundle Assets => HEVSuitMod.Instance.Assets;

#pragma warning disable IDE0051
	private void Awake()
	{
		// Cache number sprites
		numberSprites[10] = Assets.LoadAsset<Sprite>($"Assets/sprites/hud_number_blank.tga");
		for (int i = 0; i < 10; i++)
			numberSprites[i] = Assets.LoadAsset<Sprite>($"Assets/sprites/hud_number_{i}.tga");

		// Instantiate HUD prefab and components for its elements
		Hud = Instantiate(Assets.LoadAsset<GameObject>("Assets/prefabs/hud.prefab"));
		hudWeaponSelection = Hud.AddComponent<HudWeaponSelection>();
		hudDamageIndicators = Hud.AddComponent<HudDamageIndicators>();
		hudDamageIcons = Hud.AddComponent<HudDamageTypes>();
		hudFlashlight = Hud.AddComponent<HudFlashlight>();
		hudAmmoCounter = Hud.AddComponent<HudAmmoCounter>();
		hudHealthCounter = Hud.AddComponent<HudHealthCounter>();
		hudSuitPowerCounter = Hud.AddComponent<HudSuitPowerCounter>();
		hudItemPickups = Hud.AddComponent<HudItemPickups>();
		hudCrosshair = Hud.AddComponent<HudCrosshair>();
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
		Destroy(hudItemPickups);
		Destroy(hudCrosshair);
	}
#pragma warning restore IDE0051

	private void OnInventoryOpened(Player player, bool closing)
	{
		if (player != GamePlayerOwner.MyPlayer)
			return;

		Hud.SetActive(!closing);
	}

	/// <summary>
	/// Initiates a transition of the specified HUD icon to a new visual State.
	/// </summary>
	/// <param name="icon">The HUD icon to transition. Cannot be null.</param>
	/// <param name="nextState">The State to which the HUD icon will transition.</param>
	public void StartTransition(HudIcon icon, EHudIconState nextState)
	{
		icon.Timer = 0f;
		icon.LastColor = icon.Image.color;
		icon.State = nextState;
	}

	/// <summary>
	/// Per frame update of a HUD icon transition towards a target color over a specified duration.
	/// </summary>
	/// <param name="icon">The HUD icon to update. Cannot be null</param>
	/// <param name="duration">Overall duration of the transition</param>
	/// <param name="target">Desired final color</param>
	/// <returns>False until transition completed, then True</returns>
	public bool UpdateTransition(HudIcon icon, float duration, Color target)
	{
		icon.Timer += Time.deltaTime;
		if (icon.Timer >= duration)
		{
			icon.Image.color = target;
			icon.LastColor = target;
			icon.Timer = 0f;
			return true;
		}
		float t = icon.Timer / duration;
		icon.Image.color = Color.Lerp(icon.LastColor, target, t);
		return false;
	}

	/// <summary>
	/// Default/template icon transition update method. Should be called every frame.
	/// </summary>
	public void IconUpdate(HudIcon[] icons)
	{
		foreach (HudIcon icon in icons)
		{
			Color activeColor = icon.Critical ? hudColorCriticalActive : hudColorActive;
			Color inactiveColor = icon.Critical ? hudColorCritical : hudColor;

			switch (icon.State)
			{
				case EHudIconState.Active:
				case EHudIconState.Inactive:
					break;

				case EHudIconState.Deactivate:
					if (UpdateTransition(icon, icon.TransitionTime, inactiveColor))
						icon.State = EHudIconState.Inactive;
					break;

				case EHudIconState.Activate:
					if (UpdateTransition(icon, icon.TransitionTime, activeColor))
						icon.State = EHudIconState.Active;
					break;
			}
		}
	}

	/// <summary>
	/// Sets the Critical status for the specified HUD icons, critical icons are red.
	/// </summary>
	/// <param name="icons">An array of HUD icons whose Critical status will be set. Cannot be null.</param>
	/// <param name="critical">A value indicating whether the specified icons should be marked as Critical.</param>
	public void IconSetCritical(HudIcon[] icons, bool critical)
	{
		foreach (HudIcon icon in icons)
		{
			icon.Critical = critical;
			if (icon.State == EHudIconState.Inactive)
				icon.Image.color = critical ? hudColorCritical : hudColor;
			else
				icon.Image.color = critical ? hudColorCriticalActive : hudColorActive;
		}
	}

	/// <summary>
	/// Flash a set of icons - instant active, fade back to inactive (requires use of IconUpdate per frame)
	/// </summary>
	/// <param name="icons">An array of HUD icons to be flashed. Cannot be null.</param>
	public void IconFlash(HudIcon[] icons)
	{
		foreach (HudIcon icon in icons)
		{
			icon.Image.color = icon.Critical ? hudColorCriticalActive : hudColorActive;
			icon.TransitionTime = FADE_TIME;
			StartTransition(icon, EHudIconState.Deactivate);
		}
	}

	/// <summary>
	/// Transition specified icons to the brighter active State (requires use of IconUpdate per frame)
	/// </summary>
	/// <param name="icons"></param>
	public void IconActivate(HudIcon[] icons)
	{
		foreach (HudIcon icon in icons)
		{
			icon.TransitionTime = ACTIVATE_TIME;
			StartTransition(icon, EHudIconState.Activate);
		}
	}

	/// <summary>
	/// Transition specified icons to the darker inactive State (requires use of IconUpdate per frame)
	/// </summary>
	/// <param name="icons"></param>
	public void IconDeactivate(HudIcon[] icons)
	{
		foreach (HudIcon icon in icons)
		{
			icon.TransitionTime = ACTIVATE_TIME;
			StartTransition(icon, EHudIconState.Deactivate);
		}
	}

	/// <summary>
	/// Updates the specified array of HUD digit icons to visually represent the given number, displaying each digit in its corresponding position.
	/// </summary>
	/// <remarks>Leading zeros are hidden except for the least significant digit, ensuring that only significant
	/// digits are shown. The number is padded with leading zeros if it has fewer digits than the length of the digitIcons
	/// array.</remarks>
	/// <param name="digitIcons">An array of HUD icon objects representing the digit positions to update. The length of the array determines the
	/// number of digits displayed.</param>
	/// <param name="number">The non-negative integer value to display. If the value is less than zero, it is clamped to zero.</param>
	public void IconSetDigits(HudIcon[] digitIcons, int number)
	{
		if (number < 0)
		{
			log.LogWarning($"IconSetDigits() value {number} below 0, clamping to 0");
			number = 0;
		}

		// Convert the number to string and pad with zeros to fit all digits
		string numString = number.ToString().PadLeft(digitIcons.Length, '0');
		char[] digits = numString.ToCharArray();
		bool foundNonZero = false;

		for (int i = 0; i < digitIcons.Length; i++)
		{
			int digit = digits[i] - '0';

			// Hide leading zeros (except for the last digit)
			if (!foundNonZero && digit == 0 && i != digitIcons.Length - 1)
			{
				digitIcons[i].Image.sprite = numberSprites[10]; // 10 = blank
			}
			else
			{
				foundNonZero = true;
				digitIcons[i].Image.sprite = numberSprites[digit];
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
			gray *= 1.25f;
			pixels[i] = new Color(gray, gray, gray, c.a);
		}

		tex.SetPixels(pixels);
		tex.Apply();

		Sprite output = Sprite.Create(tex, new Rect(0, 0, tex.width, tex.height), new Vector2(0.5f, 0.5f));
		spriteCache[item] = output;
		return output;
	}
}
