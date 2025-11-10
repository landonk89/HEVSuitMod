using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;
using EFT;

namespace HEVSuitMod
{
	public class HudDamageIndicators : MonoBehaviour
	{
		private Color damageIndicatorColor = new(1f, 1f, 1f, 0.6f); // Slightly transparent
		private readonly HudIcon[] indicators = new HudIcon[4]; // Order: Up, Right, Down, Left

		private Dictionary<int, HudIcon[]> Directions => new()
		{
			[0] = [indicators[0]], // Front
			[1] = [indicators[0], indicators[1]], // Front-Right
			[2] = [indicators[1]], // Right
			[3] = [indicators[1], indicators[2]], // Back-Right
			[4] = [indicators[2]], // Back
			[5] = [indicators[2], indicators[3]], // Back-Left
			[6] = [indicators[3]], // Left
			[7] = [indicators[3], indicators[0]] // Front-Left
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
			if (indicators.All(x => x.state == EIconState.Dark))
				return; // Only proceed if any indicators are active

			foreach (HudIcon indicator in indicators)
			{
				if (indicator.state != EIconState.Bright)
					continue;

				indicator.timer += Time.deltaTime;
				if (indicator.timer >= HudController.FADE_TIME)
				{
					indicator.timer = 0f;
					indicator.image.color = Color.clear;
					indicator.state = EIconState.Dark;
					continue;
				}

				float t = indicator.timer / HudController.FADE_TIME;
				indicator.image.color = Color.Lerp(damageIndicatorColor, Color.clear, t);
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
				indicator.state = EIconState.Bright;
			}
		}
	}
}
