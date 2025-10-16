using BepInEx.Logging;
using EFT;
using UnityEngine;
using UnityEngine.UI;

namespace HEVSuitMod
{
	public class HudController : MonoBehaviour
	{
		// Just for readability
		const int up = 0;
		const int right = 1;
		const int down = 2;
		const int left = 3;

		private static ManualLogSource log = BepInEx.Logging.Logger.CreateLogSource("HEVSuitMod.HudController");
		private AssetBundle assets;
		private GameObject hudPrefab;
		private Image[] hitIndicators = new Image[4]; // Order: Up Right Down Left
		private float[] hitIndicatorTimers = new float[4];

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
			Utils.LogGameObjectHierarchy(hud);
			hitIndicators = hud.GetComponentsInChildren<Image>(true);
			hitIndicators[up].sprite = assets.LoadAsset<Sprite>("assets/sprites/damageup.png");
			hitIndicators[right].sprite = assets.LoadAsset<Sprite>("assets/sprites/damageright.png");
			hitIndicators[down].sprite = assets.LoadAsset<Sprite>("assets/sprites/damagedown.png");
			hitIndicators[left].sprite = assets.LoadAsset<Sprite>("assets/sprites/damageleft.png");

			GamePlayerOwner.MyPlayer.BeingHitAction += (damageInfo, _, _) => OnTakeDamage(damageInfo);
		}

		private void OnDestroy()
		{
			GamePlayerOwner.MyPlayer.BeingHitAction -= (damageInfo, _, _) => OnTakeDamage(damageInfo);
		}

		private void Update()
		{
			// Handle hit indicators
			for (int i = 0; i < 4; i++)
			{
				if (hitIndicatorTimers[i] <= 0f && hitIndicators[i].enabled)
					hitIndicators[i].enabled = false;
				else
					hitIndicatorTimers[i] -= Time.deltaTime;
			}
		}

		private void ShowHitIndicators(params int[] list)
		{
			foreach (var i in list)
			{
				hitIndicators[i].enabled = true;
				hitIndicatorTimers[i] = 0.5f;
			}
		}

		/// <summary>
		/// Event
		/// </summary>
		/// <param name="damageInfo"></param>
		public void OnTakeDamage(DamageInfoStruct damageInfo)
		{
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
			int[] indicators;
			if (angle >= 337.5f || angle < 22.5f)
				indicators = [up];                      // Front
			else if (angle >= 22.5f && angle < 67.5f)
				indicators = [up, right];               // Front-Right
			else if (angle >= 67.5f && angle < 112.5f)
				indicators = [right];                      // Right
			else if (angle >= 112.5f && angle < 157.5f)
				indicators = [right, down];                // Back-Right
			else if (angle >= 157.5f && angle < 202.5f)
				indicators = [down];                       // Back
			else if (angle >= 202.5f && angle < 247.5f)
				indicators = [down, left];                 // Back-Left
			else if (angle >= 247.5f && angle < 292.5f)
				indicators = [left];                       // Left
			else // 292.5–337.5
				indicators = [left, up];                // Front-Left

			// Show the indicators
			ShowHitIndicators(indicators);
		}
	}
}
