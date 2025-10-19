using BepInEx.Logging;
using EFT;
using System.Collections;
using UnityEngine;
using UnityEngine.UI;

namespace HEVSuitMod
{
	public class HudController : MonoBehaviour
	{
		// Just for readability
		private const int UP = 0;
		private const int RIGHT = 1;
		private const int DOWN = 2;
		private const int LEFT = 3;

		// TODO: 0.5 looks pretty good, tweak later
		private const float hitIndicatorFadeTime = 0.5f;
		
		//Singleton
		public static HudController Instance { get; private set; }

		private static ManualLogSource log = BepInEx.Logging.Logger.CreateLogSource("HEVSuitMod.HudController");
		private AssetBundle assets;
		private GameObject hudPrefab;

		// General purpose
		private Sprite[] numberSprites = new Sprite[11]; // 0-9 plus a blank one

		// Health/SuitPower
		private Image[] healthValue = new Image[3]; // Each digit of health
		private Image[] suitPowerValue = new Image[3]; // Each digit of health
		private Image suitIconFull;

		// Ammo counter
		private Image ammoCounterIcon;
		private Image[] ammoCounterValue = new Image[3];

		// Flashlight
		private Image flashlightFull;
		private Image flashlightBeam;

		// Hit indicators
		private Image[] hitIndicators = new Image[4]; // Order: Up Right Down Left
		private Coroutine hideHitIndicators = null;
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
			GameObject hud = Instantiate(hudPrefab);

			// Load number sprites, index 10 is a blank sprite
			numberSprites[10] = assets.LoadAsset<Sprite>($"assets/sprites/hud_number_blank.tga");
			for (int i = 0; i < 10; i++)
				numberSprites[i] = assets.LoadAsset<Sprite>($"assets/sprites/hud_number_{i}.tga");

			// Health digits
			for (int i = 0; i < 3; i++)
				healthValue[i] = Utils.FindInChildren<Image>(hud, $"HealthAndSuitPower/HealthValue/Digit{i}");

			// SuitPower digits and icon
			suitIconFull = Utils.FindInChildren<Image>(hud, "HealthAndSuitPower/SuitIconFull");
			for (int i = 0; i < 3; i++)
				suitPowerValue[i] = Utils.FindInChildren<Image>(hud, $"HealthAndSuitPower/SuitPowerValue/Digit{i}");

			// Ammo counter
			ammoCounterIcon = Utils.FindInChildren<Image>(hud, "AmmoCounter/Icon");
			for (int i = 0; i < 3; i++)
				ammoCounterValue[i] = Utils.FindInChildren<Image>(hud, $"AmmoCounter/Value/Digit{i}");

			// Flashlight indicator
			flashlightFull = Utils.FindInChildren<Image>(hud, "Flashlight/IconFull");
			flashlightBeam = Utils.FindInChildren<Image>(hud, "Flashlight/Beam");
			flashlightBeam.enabled = false; // start off and full battery
			flashlightFull.fillAmount = 1f;

			// Hit indicators
			hitIndicators = Utils.FindAllInChildren<Image>(hud, "HitIndicators");
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
				int digit = digits[i] - '0';

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


			// TODO: Temporary, just match suitpower to health until it does its own thing
			SuitPowerChanged(health);
		}

		// TODO: This is just temporary to get the display actually doing something
		public void SuitPowerChanged(float power)
		{
			int normalizedHealth = Mathf.CeilToInt(power / 440f * 100f);
			char[] digits = normalizedHealth.ToString("000").ToCharArray();

			suitIconFull.fillAmount = normalizedHealth / 100;

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
		}

		public void SetFlashlightBattery(float battery)
		{
			flashlightFull.fillAmount = battery;
		}

		public void FlashlightOff()
		{
			flashlightBeam.enabled = false;
		}

		public void FlashlightOn()
		{
			flashlightBeam.enabled = true;
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
				hitIndicatorTimers[i] = hitIndicatorFadeTime;
			}

			// In case we're hit 2 or more times within a short period
			if (hideHitIndicators == null)
				hideHitIndicators = StartCoroutine(HideHitIndicators());
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
