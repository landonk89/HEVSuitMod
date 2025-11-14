using EFT;
using HEVSuitMod.Types;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;

namespace HEVSuitMod.Components;

public class HudDamageTypes : MonoBehaviour
{
	private class DamageIcon(Image img)
	{
		public Image image = img;
		public float timer = 0f;
	}

	private Sprite bulletDamage;
	private Sprite coldDamage;
	private Sprite fireDamage;
	private Sprite explosionDamage;
	private Sprite barbwireDamage;
	private Sprite toxinDamage;
	private Sprite radiationDamage;
	private Sprite dehydrationDamage;
	private Sprite exhaustionDamage;
	private Transform damageIconArea;
	private readonly List<HudIcon> activeIcons = [];

	private AssetBundle Assets => HEVSuitMod.Instance.Assets;
	private HudController Hud => HEVSuitMod.Instance.HudController;

#pragma warning disable IDE0051
	private void Awake()
	{
		bulletDamage = Assets.LoadAsset<Sprite>("Assets/sprites/hud_dmg_bullet.tga");
		coldDamage = Assets.LoadAsset<Sprite>("Assets/sprites/hud_dmg_cold.tga");
		fireDamage = Assets.LoadAsset<Sprite>("Assets/sprites/hud_dmg_heat.tga");
		explosionDamage = Assets.LoadAsset<Sprite>("Assets/sprites/hud_dmg_explosion.tga");
		barbwireDamage = Assets.LoadAsset<Sprite>("Assets/sprites/hud_dmg_barbed.tga");
		toxinDamage = Assets.LoadAsset<Sprite>("Assets/sprites/hud_dmg_bio.tga");
		radiationDamage = Assets.LoadAsset<Sprite>("Assets/sprites/hud_dmg_rad.tga");
		dehydrationDamage = Assets.LoadAsset<Sprite>("Assets/sprites/hud_dmg_dehydrated.tga");
		exhaustionDamage = Assets.LoadAsset<Sprite>("Assets/sprites/hud_dmg_exhausted.tga");
		damageIconArea = transform.Find("LeftNotifyArea");
		GamePlayerOwner.MyPlayer.BeingHitAction += OnPlayerHit;
	}

	private void OnDestroy()
	{
		GamePlayerOwner.MyPlayer.BeingHitAction -= OnPlayerHit;
	}

	// Don't use Hud's StateUpdate, these are unique
	private void Update()
	{
		// Unique State update, don't use base.IconUpdate
		if (activeIcons.Count == 0)
			return;

		foreach (HudIcon icon in activeIcons.ToArray()) // Copy to avoid modification during iteration
		{
			icon.Timer += Time.deltaTime;
			icon.Image.color = Color.Lerp(Color.clear, Hud.hudColorActive, (Mathf.Sin(icon.Timer * 4f) + 1f) * 0.5f);
			if (icon.Timer >= HudController.DMG_NOTIFY_TIME)
			{
				Destroy(icon.Image.gameObject);
				activeIcons.Remove(icon);
			}
		}
	}
#pragma warning restore IDE0051

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
		if (icon == null || activeIcons.Any(x => x.Image.sprite == icon))
			return;

		GameObject iconObj = new("icon");
		iconObj.transform.parent = damageIconArea;
		Image iconImage = iconObj.AddComponent<Image>();
		iconImage.sprite = icon;
		iconImage.color = Hud.hudColorActive;
		HudIcon damageIcon = new(iconImage);
		activeIcons.Add(damageIcon);

		if (activeIcons.Count > HudController.MAX_DMG_ICONS)
			activeIcons[0].Timer = HudController.DMG_NOTIFY_TIME; // Force remove oldest
	}
}
