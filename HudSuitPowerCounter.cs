using EFT;
using EFT.InventoryLogic;
using System;
using UnityEngine;
using UnityEngine.UI;

namespace HEVSuitMod
{
	public class HudSuitPowerCounter : MonoBehaviour
	{
		private readonly HudIcon[] suitIcons = new HudIcon[5]; // 3 digits[0,1,2] + fullicon[3] + emptyicon[4]
		private HudIcon[] SuitNumbers => [suitIcons[0], suitIcons[1], suitIcons[2]];
		private HudIcon SuitFull => suitIcons[3];
		private HudIcon SuitEmpty => suitIcons[4];
		private HudController HudController => HEVMod.Instance.HudController;

		private Action<DamageInfoStruct, EBodyPart, float> SuitPowerChangedAction;
		private Action<Item> SuitChangedAction;

		private void Awake()
		{
			SuitPowerChangedAction = (damageInfo, _, _) => SuitPowerChanged(damageInfo);
			SuitChangedAction = (_) => SuitPowerChanged(null);

			suitIcons[4] = new(transform.Find("HealthAndSuitPower/SuitIconEmpty").GetComponent<Image>());
			suitIcons[3] = new(transform.Find("HealthAndSuitPower/SuitIconFull").GetComponent<Image>());
			for (int i = 0; i < 3; i++)
				suitIcons[i] = new(transform.Find($"HealthAndSuitPower/SuitPowerValue/Digit{i}").GetComponent<Image>());

			GamePlayerOwner.MyPlayer.BeingHitAction += SuitPowerChangedAction;
			GamePlayerOwner.MyPlayer.Equipment.GetSlot(EquipmentSlot.TacticalVest).OnAddOrRemoveItem += SuitChangedAction;
		}

		private void Start()
		{
			SuitPowerChanged(null);
		}

		private void OnDestroy()
		{
			GamePlayerOwner.MyPlayer.BeingHitAction -= SuitPowerChangedAction;
			GamePlayerOwner.MyPlayer.Equipment.GetSlot(EquipmentSlot.TacticalVest).OnAddOrRemoveItem -= SuitChangedAction;
		}

		private void Update()
		{
			HudController.StateUpdate(suitIcons);
		}

		private void SuitPowerChanged(DamageInfoStruct? damageInfo)
		{
			// TODO: This will eventually be the HEV suit itself, using a Strandhogg for testing right now
			float current = 0, max = 0;
			if (GamePlayerOwner.MyPlayer.Equipment.GetSlot(EquipmentSlot.TacticalVest).ContainedItem is not VestItemClass vest)
			{
				SuitFull.image.fillAmount = 0f;
				HudController.SetNumberDigits(SuitNumbers, 0);
				return;
			}

			foreach (Slot slot in vest.Slots)
			{
				if (slot.ContainedItem == null)
					continue;

				ArmoredEquipmentItemClass component = slot.ContainedItem as ArmoredEquipmentItemClass;
				current += component.Repairable.Durability;
				max += component.Repairable.MaxDurability;
			}

			int normalized = Mathf.CeilToInt(current / max * 100f);
			SuitFull.image.fillAmount = normalized / 100f;
			HudController.SetNumberDigits(SuitNumbers, normalized);
			if (damageInfo?.DidArmorDamage < 0.01)
				return; // Don't highlight unless it was noticeable

			// HL1 doesn't set suit to red, TODO: check hl1 code to confirm
			HudController.Highlight(suitIcons);
		}
	}
}
