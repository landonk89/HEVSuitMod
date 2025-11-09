using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;
using EFT;

namespace HEVSuitMod;

public class HudDamageIcons : MonoBehaviour
{
	private class DamageIcon(Image img)
	{
		public Image image = img;
		public float timer = 0f;
	}

	private const int MAX_NOTIFICATIONS = 5;
	private const float NOTIFY_TIME = 4f;

	private AssetBundle Assets => HEVMod.Instance.Assets;
	private HudController HudController => HEVMod.Instance.HudController;
	
	private Sprite bulletDamage;
	private Sprite coldDamage;
	private Sprite fireDamage;
	private Sprite explosionDamage;
	private Sprite barbwireDamage;
	private Sprite toxinDamage;
	private Sprite radiationDamage;
	private Sprite dehydrationDamage;
	private Sprite exhaustionDamage;
	private Transform damageNotificationArea;
	private readonly List<HudIcon> activeIcons = [];

	private void Awake()
	{
		bulletDamage = Assets.LoadAsset<Sprite>("assets/sprites/hud_dmg_bullet.tga");
		coldDamage = Assets.LoadAsset<Sprite>("assets/sprites/hud_dmg_cold.tga");
		fireDamage = Assets.LoadAsset<Sprite>("assets/sprites/hud_dmg_heat.tga");
		explosionDamage = Assets.LoadAsset<Sprite>("assets/sprites/hud_dmg_explosion.tga");
		barbwireDamage = Assets.LoadAsset<Sprite>("assets/sprites/hud_dmg_barbed.tga");
		toxinDamage = Assets.LoadAsset<Sprite>("assets/sprites/hud_dmg_bio.tga");
		radiationDamage = Assets.LoadAsset<Sprite>("assets/sprites/hud_dmg_rad.tga");
		dehydrationDamage = Assets.LoadAsset<Sprite>("assets/sprites/hud_dmg_dehydrated.tga");
		exhaustionDamage = Assets.LoadAsset<Sprite>("assets/sprites/hud_dmg_exhausted.tga");
		damageNotificationArea = transform.Find("LeftNotifyArea");
		GamePlayerOwner.MyPlayer.BeingHitAction += OnPlayerHit;
	}

	private void OnDestroy()
	{
		GamePlayerOwner.MyPlayer.BeingHitAction -= OnPlayerHit;
	}

	// Don't use HudController's StateUpdate, these are unique
	private void Update()
	{
		if (activeIcons.Count == 0)
			return;

		foreach (HudIcon icon in activeIcons.ToArray()) // Copy to avoid modification during iteration
		{
			icon.timer += Time.deltaTime;
			icon.image.color = Color.Lerp(Color.clear, HudController.hudColorActive, (Mathf.Sin(icon.timer * 4f) + 1f) * 0.5f);
			if (icon.timer >= NOTIFY_TIME)
			{
				Destroy(icon.image.gameObject);
				activeIcons.Remove(icon);
			}
		}
	}

	private void OnPlayerHit(DamageInfoStruct damageInfo, EBodyPart part, float amount)
	{
		Sprite icon = null;
		switch (damageInfo.DamageType)
		{
			case var dt when (dt & (EDamageType.Landmine | EDamageType.Explosion | EDamageType.ThermobaricExplosion | EDamageType.GrenadeFragment)) != 0:
				icon = explosionDamage;
				break;

			case var dt when (dt & (EDamageType.HotGases | EDamageType.Flame)) != 0:
				icon = fireDamage;
				break;

			case var dt when (dt & (EDamageType.LethalToxin | EDamageType.Poison)) != 0:
				icon = toxinDamage;
				break;

			case var dt when (dt & EDamageType.Barbed) != 0:
				icon = barbwireDamage;
				break;

			case var dt when (dt & EDamageType.RadExposure) != 0:
				icon = radiationDamage;
				break;

			case var dt when (dt & EDamageType.Bullet) != 0:
				icon = bulletDamage;
				break;

			case var dt when (dt & EDamageType.Exhaustion) != 0:
				icon = exhaustionDamage;
				break;

			case var dt when (dt & EDamageType.Dehydration) != 0:
				icon = dehydrationDamage;
				break;

			case var dt when (dt & EDamageType.Environment) != 0: // TODO: Verify this is freezing in winter
				icon = coldDamage;
				break;
		}

		// Don't notify for the same damage type twice
		if (icon == null || activeIcons.Any(x => x.image.sprite == icon))
			return;

		GameObject iconObj = new("icon");
		iconObj.transform.parent = damageNotificationArea;
		Image iconImage = iconObj.AddComponent<Image>();
		iconImage.sprite = icon;
		iconImage.color = HudController.hudColorActive;
		HudIcon damageIcon = new(iconImage);
		activeIcons.Add(damageIcon);

		// TODO: Check if this causes issues with removing while iterating in Update
		if (activeIcons.Count > MAX_NOTIFICATIONS)
			activeIcons[0].timer = NOTIFY_TIME; // Force remove oldest
	}
}
