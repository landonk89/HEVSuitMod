using EFT;
using EFT.InventoryLogic;
using HEVSuitMod.Patches;
using HEVSuitMod.Tools;
using System;
using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace HEVSuitMod.Components;

public class HudWeaponSelection : MonoBehaviour
{
	private class WeaponSelection
	{
		public GameObject selectedImg;
		public GameObject inactiveImg;
		public GameObject expander;
		public TextMeshProUGUI name;
		public Image icon;
		public Image ammoLevel;
	}

	public enum ESlot
	{
		Holster,
		Primary,
		Secondary,
		Scabbard,
		None = -1
	}

	private const float DISPLAY_TIME = 3.0f; // Seconds to display the weapon selection UI
	private const int NUM_WEAPONS = 4; // NOTENOTE: Update if more slots are added (like quickslots)

	//private readonly ManualLogSource log = BepInEx.Logging.Logger.CreateLogSource($"{typeof(HudWeaponSelection).FullName}");
	private GameObject weaponSelectionUI;
	private AudioSource audioSource;
	private readonly WeaponSelection[] weapons = new WeaponSelection[NUM_WEAPONS];
	private float activeTimer = 0f;

	private Action<Item> HolsterWeaponChanged;
	private Action<Item> PrimaryWeaponChanged;
	private Action<Item> SecondaryWeaponChanged;
	private Action<Item> ScabbardWeaponChanged;

	private AssetBundle Assets => HEVSuitMod.Instance.Assets;
	private HudController Hud => HEVSuitMod.Instance.HudController;
	private Slot Holster => GamePlayerOwner.MyPlayer.Equipment.GetSlot(EquipmentSlot.Holster);
	private Slot Primary => GamePlayerOwner.MyPlayer.Equipment.GetSlot(EquipmentSlot.FirstPrimaryWeapon);
	private Slot Secondary => GamePlayerOwner.MyPlayer.Equipment.GetSlot(EquipmentSlot.SecondPrimaryWeapon);
	private Slot Scabbard => GamePlayerOwner.MyPlayer.Equipment.GetSlot(EquipmentSlot.Scabbard);

	private Dictionary<ESlot, Slot> SlotMap => new()
	{
		{ ESlot.Holster, Holster },
		{ ESlot.Primary, Primary },
		{ ESlot.Secondary, Secondary },
		{ ESlot.Scabbard, Scabbard }
	};

#pragma warning disable IDE0051
	private void Awake()
	{
		// TODO: quick slots
		HolsterWeaponChanged = (item) => OnWeaponChanged(item, weapons[(int)ESlot.Holster]);
		PrimaryWeaponChanged = (item) => OnWeaponChanged(item, weapons[(int)ESlot.Primary]);
		SecondaryWeaponChanged = (item) => OnWeaponChanged(item, weapons[(int)ESlot.Secondary]);
		ScabbardWeaponChanged = (item) => OnWeaponChanged(item, weapons[(int)ESlot.Scabbard]);

		audioSource = GetComponent<AudioSource>();
		audioSource.volume = 0.5f;
		audioSource.playOnAwake = false;
		audioSource.clip = Assets.LoadAsset<AudioClip>("Assets/sounds/fx/wpn_moveselect.wav");
		weaponSelectionUI = transform.Find("WeaponSelection").gameObject;
		var allTransforms = weaponSelectionUI.GetComponentsInChildren<Transform>(true).ToDictionary(t => t.GetRelativePath(weaponSelectionUI.transform), t => t);
		for (int i = 0; i < NUM_WEAPONS; i++)
		{
			weapons[i] = new WeaponSelection
			{
				selectedImg = allTransforms[$"{i}/WeaponActive"].gameObject,
				inactiveImg = allTransforms[$"{i}/WeaponInactive"].gameObject,
				expander = allTransforms[$"Expander{i}"].gameObject,
				name = allTransforms[$"{i}/WeaponActive/WeaponName"].GetComponent<TextMeshProUGUI>(),
				icon = allTransforms[$"{i}/WeaponActive/Gun"].GetComponent<Image>(),
				ammoLevel = allTransforms[$"{i}/WeaponActive/AmmoBarFull"].GetComponent<Image>()
			};
		}
	}

	private void Start()
	{
		weaponSelectionUI.SetActive(false); // Start hidden
		SelectWeaponPatch.SelectionEvent += SelectWeapon;
		Holster.OnAddOrRemoveItem += HolsterWeaponChanged;
		Primary.OnAddOrRemoveItem += PrimaryWeaponChanged;
		Secondary.OnAddOrRemoveItem += SecondaryWeaponChanged;
		Scabbard.OnAddOrRemoveItem += ScabbardWeaponChanged;

		for (int i = 0; i < NUM_WEAPONS; i++)
			OnWeaponChanged(SlotMap[(ESlot)i].ContainedItem, weapons[i]);
	}

	private void OnDestroy()
	{
		SelectWeaponPatch.SelectionEvent -= SelectWeapon;
		Holster.OnAddOrRemoveItem -= HolsterWeaponChanged;
		Primary.OnAddOrRemoveItem -= PrimaryWeaponChanged;
		Secondary.OnAddOrRemoveItem -= SecondaryWeaponChanged;
	}

	private void OnWeaponChanged(Item weapon, WeaponSelection selection)
	{
		selection.name.text = weapon != null ? weapon.ShortName.Localized() : "Error";
		selection.icon.sprite = weapon != null ? Hud.GetItemSprite(weapon, new XYCellSizeStruct(5, 2)) : CacheResourcesPopAbstractClass.Pop<Sprite>("What");
	}

	void Update()
	{
		if (activeTimer > 0f)
		{
			activeTimer -= Time.deltaTime;
			if (activeTimer <= 0f)
				weaponSelectionUI.SetActive(false);
		}
	}
#pragma warning restore IDE0051

	private void SelectWeapon(ESlot index)
	{
		weaponSelectionUI.SetActive(true);
		audioSource.Play();
		activeTimer = DISPLAY_TIME;
		for (int i = 0; i < NUM_WEAPONS; i++)
		{
			Item item = SlotMap[(ESlot)i].ContainedItem;
			if (i != (int)index || item == null) // Not selected
			{
				weapons[i].expander.SetActive(false);
				weapons[i].inactiveImg.SetActive(true);
				weapons[i].selectedImg.SetActive(false);
			}
			else // Selected
			{
				weapons[i].expander.SetActive(true);
				weapons[i].inactiveImg.SetActive(false);
				weapons[i].selectedImg.SetActive(true);
				weapons[i].ammoLevel.fillAmount = item is Weapon weapon ? (float)weapon.GetCurrentMagazineCount() / weapon.GetMaxMagazineCount() : 0f;
			}
		}
	}
}