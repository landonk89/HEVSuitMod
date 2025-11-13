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

	private readonly ManualLogSource log = BepInEx.Logging.Logger.CreateLogSource($"{typeof(HudController).FullName}");
	public Color hudColor = new(1f, 0.627f, 0f, 0.4f); // Matches 'RGB_YELLOWISH' and 'MIN_ALPHA' from hl1\cl_dll\hud.h
	public Color hudColorActive = new(1f, 0.8f, 0f, 1f); // Brighter
	public Color hudColorCritical = new(1f, 0f, 0f, 0.4f); // Red
	public Color hudColorCriticalActive = new(1f, 0f, 0f, 1f); // Brighter red
	public Color damageIndicatorColor = new(1f, 1f, 1f, 0.6f); // Slightly transparent

	protected static Dictionary<Item, Sprite> spriteCache = [];
	protected static Sprite[] numberSprites = new Sprite[11]; // 0-9 plus a blank one

	HudWeaponSelection hudWeaponSelection;
	HudDamageIndicators hudDamageIndicators;
	HudDamageTypes hudDamageIcons;
	HudFlashlight hudFlashlight;
	HudAmmoCounter hudAmmoCounter;
	HudHealthCounter hudHealthCounter;
	HudSuitPowerCounter hudSuitPowerCounter;
	HudItemPickups hudItemPickups;

	public GameObject Hud { get; private set; }
	private AssetBundle Assets => HEVMod.Instance.Assets;

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
	}
#pragma warning restore IDE0051

	private void OnInventoryOpened(Player player, bool closing)
	{
		if (player != GamePlayerOwner.MyPlayer)
			return;

		Hud.SetActive(!closing);
	}

	/// <summary>
	/// Initiates a transition of the specified HUD icon to a new visual state.
	/// </summary>
	/// <param name="icon">The HUD icon to transition. Cannot be null.</param>
	/// <param name="nextState">The state to which the HUD icon will transition.</param>
	public void StartTransition(HudIcon icon, EIconState nextState)
	{
		icon.timer = 0f;
		icon.lastColor = icon.image.color;
		icon.state = nextState;
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
		icon.timer += Time.deltaTime;
		if (icon.timer >= duration)
		{
			icon.image.color = target;
			icon.lastColor = target; // TODO: TEST
			icon.timer = 0f;
			return true;
		}
		float t = icon.timer / duration;
		icon.image.color = Color.Lerp(icon.lastColor, target, t);
		return false;
	}

	/// <summary>
	/// Updates the state and appearance of the specified HUD icons based on their current state and critical status. Should be called every frame.
	/// </summary>
	/// <remarks>This method processes each icon in the array and may change its state or color to reflect
	/// transitions such as activation, deactivation, or highlighting. The method does not return a value; changes are
	/// applied directly to the provided icon objects.</remarks>
	/// <param name="icons">An array of HUD icons to update. Each icon's state and visual appearance will be modified according to its current
	/// state and whether it is marked as critical.</param>
	public void IconUpdate(HudIcon[] icons)
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

				case EIconState.Deactivate:
					if (UpdateTransition(icon, icon.transitionTime, inactiveColor))
						icon.state = EIconState.Inactive;
					break;

				case EIconState.Activate:
					if (UpdateTransition(icon, icon.transitionTime, activeColor))
						icon.state = EIconState.Active;
					break;
			}
		}
	}

	/// <summary>
	/// Sets the critical status for the specified HUD icons, updating their appearance accordingly.
	/// </summary>
	/// <param name="icons">An array of HUD icons whose critical status will be set. Cannot be null.</param>
	/// <param name="critical">A value indicating whether the specified icons should be marked as critical.</param>
	public void IconSetCritical(HudIcon[] icons, bool critical)
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

	/// <summary>
	/// Flash a set of icons - instant active, fade back to inactive (requires use of IconUpdate per frame)
	/// </summary>
	/// <param name="icons">An array of HUD icons to be flashed. Cannot be null.</param>
	public void IconFlash(HudIcon[] icons)
	{
		foreach (HudIcon icon in icons)
		{
			icon.image.color = icon.critical ? hudColorCriticalActive : hudColorActive;
			icon.transitionTime = FADE_TIME;
			StartTransition(icon, EIconState.Deactivate);
		}
	}

	/// <summary>
	/// Transition specified icons to the brighter active state (requires use of IconUpdate per frame)
	/// </summary>
	/// <param name="icons"></param>
	public void IconActivate(HudIcon[] icons)
	{
		foreach (HudIcon icon in icons)
		{
			icon.transitionTime = ACTIVATE_TIME;
			StartTransition(icon, EIconState.Activate);
		}
	}

	/// <summary>
	/// Transition specified icons to the darker inactive state (requires use of IconUpdate per frame)
	/// </summary>
	/// <param name="icons"></param>
	public void IconDeactivate(HudIcon[] icons)
	{
		foreach (HudIcon icon in icons)
		{
			icon.transitionTime = ACTIVATE_TIME;
			StartTransition(icon, EIconState.Deactivate);
		}
	}

	/// <summary>
	/// Updates the specified array of HUD digit icons to visually represent the given number, displaying each digit in its
	/// corresponding position.
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
				digitIcons[i].image.sprite = numberSprites[10]; // 10 = blank
			}
			else
			{
				foundNonZero = true;
				digitIcons[i].image.sprite = numberSprites[digit];
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
