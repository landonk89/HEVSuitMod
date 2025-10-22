using BepInEx.Logging;
using EFT;
using EFT.InventoryLogic;
using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace HEVSuitMod
{
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

		// For state machine
		private List<HudImage> allHudImages = [];

		private void Awake()
		{
			if (HEVMod.Instance == null) // How the hell did you even get here then???
			{
				log.LogFatal("HEVMod.Instance == null!");
				return;
			}

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

			// Hit indicators
			Image[] hitIndicatorImg = Utils.FindComponentsInChildren<Image>(hud, "HitIndicators");
			hitIndicatorUp = new(hitIndicatorImg[0]);
			hitIndicatorRight = new(hitIndicatorImg[1]);
			hitIndicatorDown = new(hitIndicatorImg[2]);
			hitIndicatorLeft = new(hitIndicatorImg[3]);
			foreach (Image hit in hitIndicatorImg) hit.color = Color.clear;

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

			// For Update()
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
			HEVMod.Instance.flashlight.Toggled += FlashlightToggled;
			HEVMod.Instance.flashlight.BatteryUpdate += SetFlashlightBattery;
			HEVMod.Instance.flashlight.BatteryLow += SetFlashlightBatteryCritical;

			// Testing zone
			GamePlayerOwner.MyPlayer.HandsChangingEvent += () => SetAmmoCounter(null as IHandsController);
			GamePlayerOwner.MyPlayer.HandsChangedEvent += SubscribeOnHandsChanged; // subscribe to weapon events
			//End testing

			// Init value sprites
			HealthChanged();
			//SuitPowerChanged(440);
		}

		private void SubscribeOnHandsChanged(IHandsController handsController)
		{
			if (handsController is Player.FirearmController faController)
			{
				faController.OnShot += () => SetAmmoCounter(faController);
			}
			if (handsController.Item is Weapon weapon)
			{
				weapon.GetMagazineSlot().OnAddOrRemoveItem += (item) => SetAmmoCounter(item as MagazineItemClass);
			}
		}

		private void OnDestroy()
		{
			// Maybe not needed if MyPlayer clears by itself
			GamePlayerOwner.MyPlayer.BeingHitAction -= (damageInfo, _, _) => OnTakeDamage(damageInfo);
			GamePlayerOwner.MyPlayer.ActiveHealthController.HealthChangedEvent -= (_, _, _) => HealthChanged();
			HEVMod.Instance.flashlight.Toggled -= FlashlightToggled;
			HEVMod.Instance.flashlight.BatteryUpdate -= SetFlashlightBattery;
			HEVMod.Instance.flashlight.BatteryLow -= SetFlashlightBatteryCritical;
		}

		private void Update()
		{
#if DEBUG
			if (Input.GetKeyDown(KeyCode.F6))
				NotifyIcon("assets/sprites/hud_item_healthkit.tga");

			if (Input.GetKeyDown(KeyCode.F5))
				SetAmmoCounter(GamePlayerOwner.MyPlayer.HandsController as Player.FirearmController);
#endif
			// Iterate backward so we can safely RemoveAt() for notification icons
			for (int i = allHudImages.Count -1; i >= 0; i--)
			{
				HudImage img = allHudImages[i];
				Color idleColor = img.Critical ? hudColorCritical : hudColor;
				Color activeColor = img.Critical ? hudColorCriticalActive : hudColorActive;

				switch (img.State)
				{
					case EImageState.Inactive:
						break;

					case EImageState.Active:
						break;

					case EImageState.Deactivate:
						img.Image.color = activeColor;
						StartTransition(img, EImageState.Deactivating, idleColor);
						break;

					case EImageState.Deactivating:
						UpdateTransition(img, activationTime, idleColor);
						break;

					case EImageState.Activate:
						img.Image.color = idleColor;
						img.LastState = img.State;
						StartTransition(img, EImageState.Activating, activeColor);
						break;

					case EImageState.Activating:
						UpdateTransition(img, activationTime, activeColor);
						break;

					case EImageState.Highlight:
						img.Image.color = activeColor;
						StartTransition(img, EImageState.FadeHighlight, idleColor);
						break;

					case EImageState.FadeHighlight:
						UpdateTransition(img, fadeTime, idleColor);
						break;

					case EImageState.HitIndicator:
						img.Image.color = Color.white;
						StartTransition(img, EImageState.FadeHitIndicator, Color.clear);
						break;

					case EImageState.FadeHitIndicator:
						UpdateTransition(img, fadeTime, Color.clear);
						break;

					case EImageState.PulseLow:
						img.Image.color = Color.Lerp(Color.clear, idleColor, (Mathf.Sin(Time.time * 4f) + 1f) * 0.5f);
						break;

					case EImageState.PulseHi:
						img.Image.color = Color.Lerp(idleColor, activeColor, (Mathf.Sin(Time.time * 4f) + 1f) * 0.5f);
						break;

					case EImageState.Notify:
						if (UpdateTransition(img, fadeTime, idleColor))
							img.Timer = 0f;
						break;

					case EImageState.Destroy:
						img.Timer += Time.deltaTime;
						if (img.Timer > notifyIconLifetime)
							StartTransition(img, EImageState.Destroying, Color.clear);
						break;

					case EImageState.Destroying:
						if (UpdateTransition(img, fadeTime, Color.clear))
						{
							Destroy(img.Image.gameObject);
							allHudImages.RemoveAt(i);
						}
						break;
				}
			}
		}

		private void StartTransition(HudImage img, EImageState nextState, Color target)
		{
			img.Timer = 0f;
			img.LastColor = img.Image.color;
			img.LastState = img.State;
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
				if (img.State == EImageState.Notify)
				{
					img.State = EImageState.Destroy;
					return true;
				}
				img.State = img.State == EImageState.Activating ? EImageState.Active : EImageState.Inactive;
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

		// TODO: The idea behind this method is to be invoked when:
		// 1. The player's HandsController changed
		// 2. The weapon in player's hands magazine was removed or inserted
		// 3. The weapon was fired, or the bolt was otherwise cycled for some reason
		private void SetAmmoCounter(IHandsController handsController)
		{
			if (handsController == null || handsController.Item is not Weapon weapon)
			{
				SetNumberDigits(ammoVal, 0);
				Highlight(ammoGroup);
				return;
			}

			// No mag
			if (weapon.GetCurrentMagazine() == null)
			{
				SetNumberDigits(ammoVal, 0 + weapon.ChamberAmmoCount);
			}
			else
			{
				SetNumberDigits(ammoVal, weapon.GetCurrentMagazine().Count + weapon.ChamberAmmoCount);
			}

			Highlight(ammoGroup);
		}

		private void SetAmmoCounter(MagazineItemClass magazine)
		{
			Weapon weapon = GamePlayerOwner.MyPlayer.HandsController.Item as Weapon;
			if (magazine.GetCurrentMagazine() == null)
			{		
				SetNumberDigits(ammoVal, 0 + weapon.ChamberAmmoCount);
			}
			else
			{
				SetNumberDigits(ammoVal, magazine.GetCurrentMagazine().Count + weapon.ChamberAmmoCount);
			}

			Highlight(ammoGroup);
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
				log.LogError($"SetNumberDigits() expected 3 digit images but got {digitImages.Length}");
				throw new InvalidOperationException();
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

		private void HealthChanged()
		{
			// FIXME/TODO: Assumes normal 440 health player, may break if health is modded higher
			float health = GamePlayerOwner.MyPlayer.ActiveHealthController.GetBodyPartHealth(EBodyPart.Common).Current;
			int normalizedHealth = Mathf.CeilToInt(health / 440f * 100f);
			/*
			char[] digits = normalizedHealth.ToString("000").ToCharArray();
			bool foundNonZero = false;
			for (int i = 0; i < 3; i++)
			{
				int digit = digits[i] - '0'; // Neat trick so we don't need a call to int.TryParse

				// Hide leading zeros
				if (!foundNonZero && digit == 0 && i != 2) // Keep the last digit visible even if it's 0
				{
					healthVal[i].Image.sprite = numberSprites[10]; // 10 = blank
				}
				else
				{
					foundNonZero = true;
					healthVal[i].Image.sprite = numberSprites[digit];
				}
			}
			*/
			SetNumberDigits(healthVal, normalizedHealth);
			SetCritical([healthIcon, healthVal[0], healthVal[1], healthVal[2]], normalizedHealth <= 25);
			Highlight([healthIcon, healthVal[0], healthVal[1], healthVal[2]]);

			// TODO: Temporary, just match suitpower to health until it does its own thing
			SuitPowerChanged(health);
		}

		// TODO: This is just temporary to get the display actually doing something
		private void SuitPowerChanged(float power)
		{
			int normalizedHealth = Mathf.CeilToInt(power / 440f * 100f);
			char[] digits = normalizedHealth.ToString("000").ToCharArray();
			suitFull.Image.fillAmount = normalizedHealth / 100f;

			bool foundNonZero = false;
			for (int i = 0; i < 3; i++)
			{
				int digit = digits[i] - '0';

				// Hide leading zeros
				if (!foundNonZero && digit == 0 && i != 2) // Keep the last digit visible even if it's 0
				{
					suitVal[i].Image.sprite = numberSprites[10]; // 10 = blank
				}
				else
				{
					foundNonZero = true;
					suitVal[i].Image.sprite = numberSprites[digit];
				}
			}

			//SetCritical(suitGroup, normalizedHealth <= 25);
			Highlight(suitGroup);
		}

		/// <summary>
		/// Display a notification icon, if <paramref name="leftSide"/> is true display on left, else displays on right
		/// </summary>
		/// <param name="fileName"></param>
		/// <param name="leftSide"></param>
		private void NotifyIcon(string fileName)
		{
			GameObject iconObj = new("icon");
			iconObj.transform.parent = hud.transform.Find("RightNotifyArea");
			Image iconImage = iconObj.AddComponent<Image>();
			iconImage.sprite = assets.LoadAsset<Sprite>(fileName);
			iconImage.color = hudColorActive;
			HudImage hudImage = new(iconImage);
			hudImage.State = EImageState.Notify;
			allHudImages.Add(hudImage);
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
}
