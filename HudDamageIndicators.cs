using EFT;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;

namespace HEVSuitMod
{
	public class HudDamageIndicators : MonoBehaviour
	{
		private class DamageIndicator(Image img)
		{
			public Image image = img;
			public bool active = false;
			public float timer = 0f;
		}

		private const float FADE_TIME = 0.5f;
		private Color indicatorColor = new(1f, 1f, 1f, 0.4f); // Slightly transparent
		private readonly DamageIndicator[] indicators = new DamageIndicator[4]; // Order: Up, Right, Down, Left

		private Dictionary<int, DamageIndicator[]> Directions => new()
        {
			[0] = [indicators[0]],
			[1] = [indicators[0], indicators[1]],
			[2] = [indicators[1]],
			[3] = [indicators[1], indicators[2]],
			[4] = [indicators[2]],
			[5] = [indicators[2], indicators[3]],
			[6] = [indicators[3]],
			[7] = [indicators[3], indicators[0]]
		};

		private void Awake()
		{
			Image[] indicatorImg = transform.Find("HitIndicators").GetComponentsInChildren<Image>();
			indicators[0] = new(indicatorImg[0]);
			indicators[1] = new(indicatorImg[1]);
			indicators[2] = new(indicatorImg[2]);
			indicators[3] = new(indicatorImg[3]);
			foreach (Image image in indicatorImg)
				image.color = Color.clear; // Start transparent

			GamePlayerOwner.MyPlayer.BeingHitAction += OnPlayerHit;
		}

		private void OnDestroy()
		{
			GamePlayerOwner.MyPlayer.BeingHitAction -= OnPlayerHit;
		}

		private void Update()
        {
            if (!indicators.Any(x => x.active))
                return;

            foreach (DamageIndicator indicator in indicators)
            {
                if (!indicator.active)
                    continue;

                indicator.timer += Time.deltaTime;
                if (indicator.timer >= FADE_TIME)
                {
                    indicator.timer = 0f;
                    indicator.image.color = Color.clear;
                    indicator.active = false;
					continue;
				}

                float t = indicator.timer / FADE_TIME;
                indicator.image.color = Color.Lerp(indicatorColor, Color.clear, t);
            }
        }

        private void OnPlayerHit(DamageInfoStruct damageInfo, EBodyPart bodyPart, float damage)
		{
			Vector3 lookDir = GamePlayerOwner.MyPlayer.LookDirection.normalized;
			Vector3 localDir = Quaternion.Inverse(Quaternion.LookRotation(lookDir)) * -damageInfo.Direction;
			localDir.y = 0;
			localDir.Normalize();

			// Get angle in degrees (0 = front, 90 = right, 180 = back, 270 = left)
			float angle = Mathf.Atan2(localDir.x, localDir.z) * Mathf.Rad2Deg;
			if (angle < 0) angle += 360f;

			// Decide which hit indicators to show based on angle
			int dirIndex = Mathf.FloorToInt((angle + 22.5f) % 360f / 45f);
			foreach (var indicator in Directions[dirIndex])
			{
				indicator.timer = 0f; // In case it's already active, reset timer
				indicator.active = true;
			}
		}
	}
}
