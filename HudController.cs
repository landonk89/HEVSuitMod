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
		
		// TODO: Maybe make configurable? 0.5 Looks good
		private const float hitIndicatorFadeTime = 0.5f;

		private static ManualLogSource log = BepInEx.Logging.Logger.CreateLogSource("HEVSuitMod.HudController");
		private AssetBundle assets;
		private GameObject hudPrefab;
		private Image[] hitIndicators = new Image[4]; // Order: Up Right Down Left
		private float[] hitIndicatorTimers = new float[4];
		private Coroutine hideHitIndicators = null;

		private void Start()
		{
			if (HEVMod.Instance == null)
			{
				// How did you even get here then???
				return;
			}

			assets = HEVMod.Instance.Assets;
			hudPrefab = assets.LoadAsset<GameObject>("assets/prefabs/hud.prefab");
			GameObject hud = Instantiate(hudPrefab);
			//Utils.LogGameObjectHierarchy(hud);
			hitIndicators = hud.GetComponentsInChildren<Image>(true);
			hitIndicators[UP].sprite	= assets.LoadAsset<Sprite>("assets/sprites/damageup.tga");
			hitIndicators[RIGHT].sprite = assets.LoadAsset<Sprite>("assets/sprites/damageright.tga");
			hitIndicators[DOWN].sprite	= assets.LoadAsset<Sprite>("assets/sprites/damagedown.tga");
			hitIndicators[LEFT].sprite	= assets.LoadAsset<Sprite>("assets/sprites/damageleft.tga");

			// Hide them until we're hit
			hitIndicators[UP].enabled = false;
			hitIndicators[RIGHT].enabled = false;
			hitIndicators[DOWN].enabled = false;
			hitIndicators[LEFT].enabled = false;

			// HL1 style damage direction indicators
			GamePlayerOwner.MyPlayer.BeingHitAction += (damageInfo, _, _) => OnTakeDamage(damageInfo);
		}

		private void OnDestroy()
		{
			// Maybe not needed if MyPlayer clears by itself
			GamePlayerOwner.MyPlayer.BeingHitAction -= (damageInfo, _, _) => OnTakeDamage(damageInfo);
		}

		private IEnumerator HideHitIndicators()
		{
#if DEBUG
			log.LogInfo("HideHitIndicators() started");
#endif
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
#if DEBUG
					log.LogInfo("HideHitIndicators() stopped");
#endif
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
			int[] indicators;
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
			if (angle >= 337.5f || angle < 22.5f)
				indicators = [UP];                      // Front
			else if (angle >= 22.5f && angle < 67.5f)
				indicators = [UP, RIGHT];               // Front-Right
			else if (angle >= 67.5f && angle < 112.5f)
				indicators = [RIGHT];                      // Right
			else if (angle >= 112.5f && angle < 157.5f)
				indicators = [RIGHT, DOWN];                // Back-Right
			else if (angle >= 157.5f && angle < 202.5f)
				indicators = [DOWN];                       // Back
			else if (angle >= 202.5f && angle < 247.5f)
				indicators = [DOWN, LEFT];                 // Back-Left
			else if (angle >= 247.5f && angle < 292.5f)
				indicators = [LEFT];                       // Left
			else // 292.5–337.5
				indicators = [LEFT, UP];                // Front-Left

			// Show the indicators
			ShowHitIndicators(indicators);
		}
	}
}
