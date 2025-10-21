using BepInEx.Logging;
using EFT;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace HEVSuitMod
{
	public class HudController : MonoBehaviour
	{
		// TODO: 0.5 looks pretty good, tweak later
		private const float HIT_FADE_TIME = 0.5f;
		
		//Singleton
		public static HudController Instance { get; private set; }
		private ManualLogSource log = BepInEx.Logging.Logger.CreateLogSource("HEVSuitMod.HudController");
		private AssetBundle assets;
		private GameObject hudPrefab;
		private GameObject hud;
		
		// Highlight and fade images on demand
		private Color hudColor = new(1f, 0.627f, 0f, 0.4f); // Matches 'RGB_YELLOWISH' and 'MIN_ALPHA' from hl1\cl_dll\hud.h
		private Color hudColorBlank = new(0f, 0f, 0f, 0f);
		private Color hudColorActive = new(1f, 0.9f, 0f, 1f); // Lerp from here to hudColor over fadeTime
		private Color hudColorDanger = new(1f, 0f, 0f, 0.4f); // Red
		private Color hudColorDangerActive = new(1f, 0f, 0f, 1f); // Lerp from here to hudColorDanger over fadeTime
		private float flashTime = 0.5f;

		// Notification icons
		private float notifyIconLifetime = 3f; // TODO: tune

		// Dynamic sprites
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

		// Hit indicators
		//private Image[] hitIndicatorImg = new Image[4]; // Order: Up Right Down Left
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
				log.LogError("HEVMod.Instance == null!");
				return;
			}

			Instance = this;
			assets = HEVMod.Instance.Assets;
			if (assets == null) // Can't happen, but you can bet it will somehow...
			{
				log.LogError("Couldn't get assetbundle!");
				return;
			}

			hudPrefab = assets.LoadAsset<GameObject>("assets/prefabs/hud.prefab");
			hud = Instantiate(hudPrefab);

			// Load number sprites, index 10 is a blank sprite
			numberSprites[10] = assets.LoadAsset<Sprite>($"assets/sprites/hud_number_blank.tga");
			for (int i = 0; i < 10; i++) numberSprites[i] = assets.LoadAsset<Sprite>($"assets/sprites/hud_number_{i}.tga");

			// Health digits and icon
			for (int i = 0; i < 3; i++) healthVal[i] = new(Utils.FindComponent<Image>(hud, $"HealthAndSuitPower/HealthValue/Digit{i}"));
			healthIcon = new(Utils.FindComponent<Image>(hud, "HealthAndSuitPower/HealthIcon"));
			healthGroup = [ healthIcon, healthVal[0], healthVal[1], healthVal[2] ];

			// SuitPower digits and icon
			suitEmpty = new(Utils.FindComponent<Image>(hud, "HealthAndSuitPower/SuitIconEmpty"));
			suitFull = new(Utils.FindComponent<Image>(hud, "HealthAndSuitPower/SuitIconFull"));
			for (int i = 0; i < 3; i++) suitVal[i] = new(Utils.FindComponent<Image>(hud, $"HealthAndSuitPower/SuitPowerValue/Digit{i}"));
			suitGroup = [suitFull, suitEmpty, suitVal[0], suitVal[1], suitVal[2]];

			// Ammo counter
			ammo = new(Utils.FindComponent<Image>(hud, "AmmoCounter/Icon"));
			for (int i = 0; i < 3; i++)	ammoVal[i] = new(Utils.FindComponent<Image>(hud, $"AmmoCounter/Value/Digit{i}"));
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
			foreach (Image hit in hitIndicatorImg) // Make them start clear
				hit.color = Color.clear;

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
			HEVMod.Instance.flashlight.BatteryUpdated += SetFlashlightBattery;

			// Init value sprites
			HealthChanged();
			//SuitPowerChanged(440);
		}

		// For testing
		private void Update()
		{
#if DEBUG
			if (Input.GetKeyDown(KeyCode.F6))
				NotifyIcon("assets/sprites/hud_item_healthkit.tga");

			if (Input.GetKeyDown(KeyCode.F5))
				ammo.State = EImageState.PulseBlank;

			if (Input.GetKeyDown(KeyCode.F4))
				ammo.State = EImageState.PulseHighlight;

			if (Input.GetKeyDown(KeyCode.F3))
				ammo.State= EImageState.Activate;

			if (Input.GetKeyDown(KeyCode.F2))
				ammo.State = EImageState.Deactivate;

			if (Input.GetKeyDown(KeyCode.F1))
				ammo.State = EImageState.Notify;
#endif
			for (int i = allHudImages.Count -1; i >= 0; i--)
			{
				HudImage img = allHudImages[i];
				Color idleColor = img.Critical ? hudColorDanger : hudColor;
				Color activeColor = img.Critical ? hudColorDangerActive : hudColorActive;
				float t;
				switch (img.State)
				{
					case EImageState.Idle:
						break;

					case EImageState.Deactivate: // TODO: Gentle transition don't slam it
						img.Image.color = idleColor;
						img.State = EImageState.Idle;
						break;

					case EImageState.Activate: // This image is slightly brighter than normal
						img.Image.color = hudColorActive;
						img.State = EImageState.Idle;
						break;

					case EImageState.StartHighlight:
						img.Timer = 0f;
						img.Image.color = activeColor;
						img.State = EImageState.EndHighlight;
						break;

					case EImageState.EndHighlight:
						img.Timer += Time.deltaTime;
						if (img.Timer >= flashTime)
						{
							img.Image.color = idleColor;
							img.Timer = 0f;
							img.State = EImageState.Idle;
							break;
						}

						t = img.Timer / flashTime;
						img.Image.color = Color.Lerp(activeColor, idleColor, t);
						break;

					case EImageState.StartHitIndicator:
						img.Timer = 0f;
						img.Image.color = Color.white;
						img.State = EImageState.EndHitIndicator;
						break;

					case EImageState.EndHitIndicator:
						img.Timer += Time.deltaTime;
						if (img.Timer >= HIT_FADE_TIME)
						{
							img.Image.color = Color.clear;
							img.State = EImageState.Idle;
							break;
						}

						t = img.Timer / HIT_FADE_TIME;
						img.Image.color = Color.Lerp(Color.white, Color.clear, t);
						break;

					case EImageState.PulseBlank:
						t = (Mathf.Sin(Time.time * 4f) + 1f) * 0.5f;
						img.Image.color = Color.Lerp(hudColorBlank, idleColor, t);
						break;

					case EImageState.PulseHighlight:
						t = (Mathf.Sin(Time.time * 4f) + 1f) * 0.5f;
						img.Image.color = Color.Lerp(idleColor, activeColor, t);
						break;

					case EImageState.Notify: // Never used on permanent hud elements!!!
						img.Timer += Time.deltaTime;
						if (img.Timer >= notifyIconLifetime)
						{
							img.Image.color = idleColor;
							img.Timer = 0f;
							img.State = EImageState.Destroy;
							break;
						}

						t = img.Timer / flashTime;
						img.Image.color = Color.Lerp(activeColor, idleColor, t);
						break;

					case EImageState.Destroy: // Should ONLY be used by ImageState.Notify
						img.Timer += Time.deltaTime;
						if (img.Timer >= notifyIconLifetime)
						{
							Destroy(img.Image.gameObject);
							allHudImages.RemoveAt(i);
							break;
						}

						t = img.Timer / notifyIconLifetime;
						img.Image.color = Color.Lerp(idleColor, hudColorBlank, t);
						break;
				}
			}
		}

		private void OnDestroy()
		{
			// Maybe not needed if MyPlayer clears by itself
			GamePlayerOwner.MyPlayer.BeingHitAction -= (damageInfo, _, _) => OnTakeDamage(damageInfo);
			GamePlayerOwner.MyPlayer.ActiveHealthController.HealthChangedEvent -= (_, _, _) => HealthChanged();
			HEVMod.Instance.flashlight.Toggled -= FlashlightToggled;
			HEVMod.Instance.flashlight.BatteryUpdated -= SetFlashlightBattery;
		}

		public void HealthChanged()
		{
			// FIXME/TODO: Assumes normal 440 health player, may break if health is modded higher
			float health = GamePlayerOwner.MyPlayer.ActiveHealthController.GetBodyPartHealth(EBodyPart.Common).Current;
			int normalizedHealth = Mathf.CeilToInt(health / 440f * 100f);
			char[] digits = normalizedHealth.ToString("000").ToCharArray();

			bool foundNonZero = false;
			for (int i = 0; i < 3; i++)
			{
				int digit = digits[i] - '0'; // Neat trick so we don't need a call to int.TryParse

				// Hide leading zeros
				if (!foundNonZero && digit == 0 && i != 2) // Keep the last digit visible even if it's 0
				{
					//healthValImg[i].sprite = numberSprites[10]; // 10 = blank
					healthVal[i].Image.sprite = numberSprites[10];
				}
				else
				{
					foundNonZero = true;
					//healthValImg[i].sprite = numberSprites[digit];
					healthVal[i].Image.sprite = numberSprites[digit];
				}
			}

			// TODO: Test
			//Highlight([healthImg, healthValImg[0], healthValImg[1], healthValImg[2]], normalizedHealth <= 0.25f);
			SetCritical([healthIcon, healthVal[0], healthVal[1], healthVal[2]], normalizedHealth <= 0.25f);
			Highlight([healthIcon, healthVal[0], healthVal[1], healthVal[2]]);

			// TODO: Temporary, just match suitpower to health until it does its own thing
			SuitPowerChanged(health);
		}

		// TODO: This is just temporary to get the display actually doing something
		public void SuitPowerChanged(float power)
		{
			int normalizedHealth = Mathf.CeilToInt(power / 440f * 100f);
			char[] digits = normalizedHealth.ToString("000").ToCharArray();

			//suitFullImg.fillAmount = normalizedHealth / 100f;
			suitFull.Image.fillAmount = normalizedHealth / 100f;

			bool foundNonZero = false;
			for (int i = 0; i < 3; i++)
			{
				int digit = digits[i] - '0';

				// Hide leading zeros
				if (!foundNonZero && digit == 0 && i != 2) // Keep the last digit visible even if it's 0
				{
					//suitValImg[i].sprite = numberSprites[10]; // 10 = blank
					suitVal[i].Image.sprite = numberSprites[10]; // 10 = blank
				}
				else
				{
					foundNonZero = true;
					//suitValImg[i].sprite = numberSprites[digit];
					suitVal[i].Image.sprite = numberSprites[digit];
				}
			}

			SetCritical(suitGroup, normalizedHealth <= 25);
			Highlight(suitGroup);
		}

		public void SetCritical(HudImage[] images, bool critical)
		{
			foreach (HudImage image in images)
				image.Critical = critical;
		}

		public void Highlight(HudImage[] images)
		{
			foreach (HudImage image in images)
				image.State = EImageState.StartHighlight;
		}

		public void SetFlashlightBattery(float battery, bool isOn)
		{
			flashFull.Image.fillAmount = battery;
			bool isLow = battery < 0.25f;
			Color baseColor = isLow ? hudColorDanger : hudColor;
			Color highlightColor = isLow ? hudColorDangerActive : hudColorActive;

			foreach (HudImage image in flashGroup)
				image.Image.color = isOn ? highlightColor : baseColor;
		}

		public void FlashlightToggled(bool isOn)
		{
			flashBeam.Image.enabled = isOn;
			foreach (HudImage image in flashGroup)
				image.State = isOn ? EImageState.Activate : EImageState.Deactivate;
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
			HudImage hudImage = new(iconImage);
			hudImage.State = EImageState.Notify;
			allHudImages.Add(hudImage);
		}

		/// <summary>
		/// Event
		/// </summary>
		/// <param name="damageInfo"></param>
		public void OnTakeDamage(DamageInfoStruct damageInfo)
		{
			// FIXME: Switch to damageInfo.Direction when you finally make sense of it
			if (damageInfo.Player == null)
			{
				// World damage, show all damage indicators
				hitIndicatorUp.State = EImageState.StartHitIndicator;
				hitIndicatorRight.State = EImageState.StartHitIndicator;
				hitIndicatorDown.State = EImageState.StartHitIndicator;
				hitIndicatorLeft.State = EImageState.StartHitIndicator;
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
				image.State = EImageState.StartHitIndicator;
		}
	}
}
