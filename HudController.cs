using BepInEx.Logging;
using EFT;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;

namespace HEVSuitMod
{
	public class HudController : MonoBehaviour
	{
		public class ImageModulator(Image img, Color from, Color to)
		{
			public Image image = img;
			public Color colorFrom = from;
			public Color colorTo = to;
			public float elapsed = 0f;
		}

		// Just for readability
		private const int UP = 0;
		private const int RIGHT = 1;
		private const int DOWN = 2;
		private const int LEFT = 3;

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
		private Color hudColorHighlight = new(1f, 0.9f, 0f, 1f); // Lerp from here to hudColor over fadeTime
		private Color hudColorDanger = new(1f, 0f, 0f, 0.4f); // Red
		private Color hudColorDangerHighlight = new(1f, 0f, 0f, 1f); // Lerp from here to hudColorDanger over fadeTime
		private float highlightTime = 2f;
		private List<ImageModulator> activeHighlights = [];
		private Coroutine highlightHandler;

		// Pulse images like a blinking light, this is intended for world damage indicators (fire, etc)
		// ...but can be used on any hud image safely
		// TODO: Prevent pulsing and highlighting at the same time
		private List<ImageModulator> activePulses = [];
		private Coroutine pulseHandler;

		// Notification icons
		private Dictionary<GameObject, float> activeNotifyIcons = [];
		private float notifyIconLifetime = 3f; // TODO: tune
		private Coroutine notifyIconHandler;

		// Dynamic sprites
		private Sprite[] numberSprites = new Sprite[11]; // 0-9 plus a blank one

		// Health/SuitPower
		private Image healthIcon;
		private Image[] healthValue = new Image[3]; // Each digit of health
		private Image suitIcon;
		private Image[] suitPowerValue = new Image[3]; // Each digit of power

		// Ammo counter
		private Image ammoCounterIcon;
		private Image[] ammoCounterValue = new Image[3];

		// Flashlight
		private Image flashlightEmpty;
		private Image flashlightFull;
		private Image flashlightBeam;

		// Hit indicators
		private Image[] hitIndicators = new Image[4]; // Order: Up Right Down Left
		private Coroutine hideHitIndicators;
		private readonly float[] hitIndicatorTimers = new float[4];
		private readonly int[][] hitIndicatorDirections =
		{
			[UP],          // 0: Front
			[UP, RIGHT],   // 1: Front-Right
			[RIGHT],       // 2: Right
			[RIGHT, DOWN], // 3: Back-Right
			[DOWN],        // 4: Back
			[DOWN, LEFT],  // 5: Back-Left
			[LEFT],        // 6: Left
			[LEFT, UP]     // 7: Front-Left
		};

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
			for (int i = 0; i < 10; i++)
				numberSprites[i] = assets.LoadAsset<Sprite>($"assets/sprites/hud_number_{i}.tga");

			// Health digits and icon
			healthIcon = Utils.FindComponent<Image>(hud, "HealthAndSuitPower/HealthIcon");
			for (int i = 0; i < 3; i++)
				healthValue[i] = Utils.FindComponent<Image>(hud, $"HealthAndSuitPower/HealthValue/Digit{i}");

			// SuitPower digits and icon
			suitIcon = Utils.FindComponent<Image>(hud, "HealthAndSuitPower/SuitIconFull");
			for (int i = 0; i < 3; i++)
				suitPowerValue[i] = Utils.FindComponent<Image>(hud, $"HealthAndSuitPower/SuitPowerValue/Digit{i}");

			// Ammo counter
			ammoCounterIcon = Utils.FindComponent<Image>(hud, "AmmoCounter/Icon");
			for (int i = 0; i < 3; i++)
				ammoCounterValue[i] = Utils.FindComponent<Image>(hud, $"AmmoCounter/Value/Digit{i}");

			// Flashlight indicator
			flashlightEmpty = Utils.FindComponent<Image>(hud, "Flashlight/IconEmpty");
			flashlightFull = Utils.FindComponent<Image>(hud, "Flashlight/IconFull");
			flashlightBeam = Utils.FindComponent<Image>(hud, "Flashlight/Beam");

			flashlightBeam.enabled = false; // start off and full battery
			flashlightFull.fillAmount = 1f;

			// Hit indicators
			hitIndicators = Utils.FindComponentsInChildren<Image>(hud, "HitIndicators");
			hitIndicators[UP].enabled = false; // Hide indicators until we're hit
			hitIndicators[RIGHT].enabled = false;
			hitIndicators[DOWN].enabled = false;
			hitIndicators[LEFT].enabled = false;

			// Subscribe events
			GamePlayerOwner.MyPlayer.BeingHitAction += (damageInfo, _, _) => OnTakeDamage(damageInfo);
			GamePlayerOwner.MyPlayer.ActiveHealthController.HealthChangedEvent += (_, _, _) => HealthChanged();

			// Init value sprites
			HealthChanged();
			//SuitPowerChanged(440);
		}

		// For testing
		private void Update()
		{
			if (Input.GetKeyDown(KeyCode.F6))
			{
				NotifyIcon("assets/sprites/hud_item_healthkit.tga");
			}
		}

		private void OnDestroy()
		{
			// Maybe not needed if MyPlayer clears by itself
			GamePlayerOwner.MyPlayer.BeingHitAction -= (damageInfo, _, _) => OnTakeDamage(damageInfo);
			GamePlayerOwner.MyPlayer.ActiveHealthController.HealthChangedEvent -= (_, _, _) => HealthChanged();
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

				// Hide leading zeros until we hit a nonzero digit
				if (!foundNonZero && digit == 0 && i != 2) // Keep the last digit visible even if it's 0
				{
					healthValue[i].sprite = numberSprites[10]; // 10 = blank
				}
				else
				{
					foundNonZero = true;
					healthValue[i].sprite = numberSprites[digit];
				}
			}

			// TODO: Test
			Highlight([healthIcon, healthValue[0], healthValue[1], healthValue[2]], normalizedHealth <= 0.25f);

			// TODO: Temporary, just match suitpower to health until it does its own thing
			SuitPowerChanged(health);
		}

		// TODO: This is just temporary to get the display actually doing something
		public void SuitPowerChanged(float power)
		{
			int normalizedHealth = Mathf.CeilToInt(power / 440f * 100f);
			char[] digits = normalizedHealth.ToString("000").ToCharArray();

			suitIcon.fillAmount = normalizedHealth / 100f;

			bool foundNonZero = false;
			for (int i = 0; i < 3; i++)
			{
				int digit = digits[i] - '0';

				// Hide leading zeros until we hit a nonzero digit
				if (!foundNonZero && digit == 0 && i != 2) // Keep the last digit visible even if it's 0
				{
					suitPowerValue[i].sprite = numberSprites[10]; // 10 = blank
				}
				else
				{
					foundNonZero = true;
					suitPowerValue[i].sprite = numberSprites[digit];
				}
			}

			// Suit power never turns red in HL1
			Highlight([suitIcon, suitPowerValue[0], suitPowerValue[1], suitPowerValue[2]], false);
		}

		public void SetFlashlightBattery(float battery, bool isOn)
		{
			flashlightFull.fillAmount = battery;
			bool isLow = battery < 0.25f;
			Color baseColor = isLow ? hudColorDanger : hudColor;
			Color highlightColor = isLow ? hudColorDangerHighlight : hudColorHighlight;

			if (isOn)
			{
				flashlightEmpty.color = highlightColor;
				flashlightFull.color = highlightColor;
				flashlightBeam.color = highlightColor;
			}
			else
			{
				flashlightEmpty.color = baseColor;
				flashlightFull.color = baseColor;
				flashlightBeam.color = baseColor;
			}
		}


		public void FlashlightOff()
		{
			flashlightBeam.enabled = false;
		}

		public void FlashlightOn()
		{
			flashlightBeam.enabled = true;
		}

		/// <summary>
		/// Display a notification icon, if <paramref name="leftSide"/> is true display on left, else displays on right
		/// </summary>
		/// <param name="fileName"></param>
		/// <param name="leftSide"></param>
		private void NotifyIcon(string fileName)
		{
			GameObject iconGo = new("Pickup");
			iconGo.transform.parent = hud.transform.Find("RightNotifyArea");
			Image iconImage = iconGo.AddComponent<Image>();
			iconImage.sprite = assets.LoadAsset<Sprite>(fileName);
			ImageModulator modulator = Highlight(iconImage, false);
			modulator.colorTo = new(0, 0, 0, 0); // Override so it fades out completely
			activeNotifyIcons.Add(iconGo, notifyIconLifetime);
			notifyIconHandler ??= StartCoroutine(NotifyIconHandler());
		}

		private IEnumerator NotifyIconHandler()
		{
			while (true)
			{
				List<GameObject> expired = [];
				foreach (var icon in activeNotifyIcons.Keys.ToList())
				{
					activeNotifyIcons[icon] -= Time.deltaTime;
					if (activeNotifyIcons[icon] <= 0f)
						expired.Add(icon);
				}

				if (expired != null)
				{
					foreach (var icon in expired)
					{
						activeNotifyIcons.Remove(icon);
						Destroy(icon);
					}

					if (activeNotifyIcons.Count == 0)
					{
						notifyIconHandler = null;
						yield break;
					}
				}

				yield return null;
			}
		}

		private IEnumerator HandleHighlights()
		{
			while (true)
			{
				activeHighlights.RemoveAll(fade =>
				{
					if (fade.image == null)
						return true;

					fade.elapsed += Time.deltaTime;

					if (fade.elapsed >= highlightTime)
					{
						fade.image.color = fade.colorTo;
						return true;
					}

					float t = fade.elapsed / highlightTime;
					fade.image.color = Color.Lerp(fade.colorFrom, fade.colorTo, t);
					return false;
				});

				if (activeHighlights.Count == 0)
				{
					highlightHandler = null;
					yield break;
				}

				yield return null;
			}
		}

		// Returns reference so we can modify it if we want
		public ImageModulator Highlight(Image image, bool isDanger)
		{
			if (image == null)
				return null;

			Color from = isDanger ? hudColorDangerHighlight : hudColorHighlight;
			Color to = isDanger ? hudColorDanger : hudColor;
			ImageModulator modulator = new(image, from, to);
			activeHighlights.RemoveAll(f => f.image == image); // Remove existing
			activeHighlights.Add(modulator);
			highlightHandler ??= StartCoroutine(HandleHighlights());
			return modulator;
		}

		// Returns reference so we can modify it if we want
		public ImageModulator[] Highlight(Image[] images, bool isDanger)
		{
			if (images == null)
				return [];

			List<ImageModulator> modulators = [];
			Color from = isDanger ? hudColorDangerHighlight : hudColorHighlight;
			Color to = isDanger ? hudColorDanger : hudColor;
			foreach (Image image in images)
			{
				ImageModulator modulator = new(image, from, to);
				activeHighlights.RemoveAll(f => f.image == image);
				activeHighlights.Add(modulator);
				modulators.Add(modulator);
			}

			// Start coroutine if not already running
			highlightHandler ??= StartCoroutine(HandleHighlights());

			return [..modulators];
		}

		private IEnumerator HideHitIndicators()
		{
			while (true)
			{
				bool anyActive = false;
				for (int i = 0; i < 4; i++)
				{
					if (hitIndicators[i].enabled)
					{
						hitIndicatorTimers[i] -= Time.deltaTime;
						hitIndicators[i].color = new(1, 1, 1, hitIndicatorTimers[i] * 2);

						if (hitIndicatorTimers[i] <= 0f)
							hitIndicators[i].enabled = false;
						else
							anyActive = true;
					}
				}

				if (!anyActive)
				{
					hideHitIndicators = null;
					yield break; // Stop coroutine when nothing is visible
				}

				yield return null;
			}
		}

		private void ShowHitIndicators(params int[] list)
		{
			foreach (var i in list)
			{
				hitIndicators[i].enabled = true;
				hitIndicatorTimers[i] = HIT_FADE_TIME;
			}

			hideHitIndicators ??= StartCoroutine(HideHitIndicators());
		}

		/// <summary>
		/// Event
		/// </summary>
		/// <param name="damageInfo"></param>
		public void OnTakeDamage(DamageInfoStruct damageInfo)
		{
			int[] indicators = [0];
			if (damageInfo.Player == null)
			{
				// World damage, show all of them
				indicators = [UP, RIGHT, DOWN, LEFT];
				ShowHitIndicators(indicators);
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

			// Decide which directions to show based on angle
			int dirIndex = Mathf.FloorToInt(((angle + 22.5f) % 360f) / 45f);
			indicators = hitIndicatorDirections[dirIndex];

			// Show the indicators
			ShowHitIndicators(indicators);
		}
	}
}
