using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

namespace HEVSuitMod
{
	public class HudController : MonoBehaviour
	{
		private enum EHitDirection
		{
			Front,
			FrontRight,
			Right,
			BackRight,
			Back,
			BackLeft,
			Left,
			FrontLeft
		}

		// Just for readability
		const int front = 0;
		const int right = 1;
		const int back = 2;
		const int left = 3;

		private AssetBundle assets;
		private GameObject hudPrefab;
		//private Canvas canvas;
		private Image[] hitIndicators = new Image[4]; // Order: Up Right Down Left
		private Dictionary<EHitDirection, int[]> hitDirectionImages;
		private float[] hitIndicatorTimers = { 0f, 0f, 0f, 0f };
		//private bool[] hitIndicatorsActive = { false, false, false, false };

		private void Awake()
		{
			if (HEVMod.Instance == null)
			{
				// How did you even get here then???
				return;
			}

			assets = HEVMod.Instance.Assets;
			hudPrefab = assets.LoadAsset<GameObject>("assets/prefabs/hud.prefab");
			GameObject hud = Instantiate(hudPrefab);
			//canvas = hud.GetComponent<Canvas>();
			hitIndicators = hud.GetComponentsInChildren<Image>();
			hitIndicators[0].sprite = assets.LoadAsset<Sprite>("assets/sprites/damageup.png");
			hitIndicators[1].sprite = assets.LoadAsset<Sprite>("assets/sprites/damageright.png");
			hitIndicators[2].sprite = assets.LoadAsset<Sprite>("assets/sprites/damagedown.png");
			hitIndicators[3].sprite = assets.LoadAsset<Sprite>("assets/sprites/damageleft.png");

			// Indexes for hitIndicators
			hitDirectionImages = new Dictionary<EHitDirection, int[]>
			{
				{ EHitDirection.Front, [front] },
				{ EHitDirection.FrontRight, [front, right] },
				{ EHitDirection.Right, [right] },
				{ EHitDirection.BackRight, [back, right] },
				{ EHitDirection.Back, [back] },
				{ EHitDirection.BackLeft, [back, left] },
				{ EHitDirection.Left, [left] },
				{ EHitDirection.FrontLeft, [front, left] }
			};
		}

		private void Update()
		{
			// Handle hit indicators
			for (int i = 0; i < 4; i++)
			{
				if (hitIndicatorTimers[i] <= 0f)
					hitIndicators[i].SetEnabled(false);
				else
					hitIndicatorTimers[i] -= Time.deltaTime;
			}
		}

		private void ShowDamageIndicator(EHitDirection hitDirection)
		{
			if (!hitDirectionImages.TryGetValue(hitDirection, out int[] indexes))
				return; // Some shit happened

			foreach (int index in indexes)
			{
				hitIndicatorTimers[index] = 0.25f;
				hitIndicators[index].SetEnabled(true);
			}
		}
	}
}
