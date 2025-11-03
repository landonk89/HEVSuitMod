using EFT;
using HEVSuitMod;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using EFT.InventoryLogic;
using System;
using Comfort.Common;
using System.Collections.Generic;

// Defensive Weapon Selection System (tm)
public class WeaponSelectionController : MonoBehaviour
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

    private enum ESlot
    {
        Holster,
        Primary,
        Secondary,
        Scabbard
    }

    private const int NUM_WEAPONS = 3;
    private GameObject weaponSelectionUI;
    private AudioSource audioSource;
    private AssetBundle assets = HEVMod.Instance.Assets;
    private WeaponSelection[] weapons = new WeaponSelection[NUM_WEAPONS];
    private float activeTimer = 0f;

    private Action<Item> HolsterWeaponChanged;
    private Action<Item> PrimaryWeaponChanged;
    private Action<Item> SecondaryWeaponChanged;
    //private Action<Item> MeleeWeaponChanged; // Not implemented on HUD prefab yet

    private Slot Holster => GamePlayerOwner.MyPlayer.Equipment.GetSlot(EquipmentSlot.Holster);
    private Slot Primary => GamePlayerOwner.MyPlayer.Equipment.GetSlot(EquipmentSlot.FirstPrimaryWeapon);
    private Slot Secondary => GamePlayerOwner.MyPlayer.Equipment.GetSlot(EquipmentSlot.SecondPrimaryWeapon);
    private Slot Scabbard => GamePlayerOwner.MyPlayer.Equipment.GetSlot(EquipmentSlot.Scabbard);

    private Dictionary<ESlot, Slot> slotMap => new()
    {
        { ESlot.Holster, Holster },
        { ESlot.Primary, Primary },
        { ESlot.Secondary, Secondary },
        { ESlot.Scabbard, Scabbard }
    };

    private void Awake()
    {
        HolsterWeaponChanged = (item) => OnWeaponChanged(item, weapons[(int)ESlot.Holster]);
        PrimaryWeaponChanged = (item) => OnWeaponChanged(item, weapons[(int)ESlot.Primary]);
        SecondaryWeaponChanged = (item) => OnWeaponChanged(item, weapons[(int)ESlot.Secondary]);
        //MeleeWeaponChanged = (item) => OnWeaponChanged(item, weapons[3]);

        audioSource = GetComponent<AudioSource>();
        audioSource.volume = 0.5f;
        audioSource.clip = assets.LoadAsset<AudioClip>("assets/sounds/fx/wpn_moveselect.wav");
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

    private void OnEnable()
    {
        weaponSelectionUI.SetActive(false); // Start hidden
        Holster.OnAddOrRemoveItem += HolsterWeaponChanged;
        Primary.OnAddOrRemoveItem += PrimaryWeaponChanged;
        Secondary.OnAddOrRemoveItem += SecondaryWeaponChanged;

        for (int i = 0; i < NUM_WEAPONS; i++)
            OnWeaponChanged(slotMap[(ESlot)i].ContainedItem, weapons[i]);
    }

    private void OnDisable()
    {
        weaponSelectionUI.SetActive(false); // Hide on disable
        Holster.OnAddOrRemoveItem -= HolsterWeaponChanged;
        Primary.OnAddOrRemoveItem -= PrimaryWeaponChanged;
        Secondary.OnAddOrRemoveItem -= SecondaryWeaponChanged;
    }

    private void OnWeaponChanged(Item weapon, WeaponSelection selection)
    {
        // FIXME: Not having a weapon should hide the slot instead of showing "Unidentified Weapon"
        selection.name.text = weapon != null ? weapon.ShortName.Localized() : "Unidentified Weapon";
        selection.icon.sprite = weapon != null ? CreateWeaponIcon(weapon) : CacheResourcesPopAbstractClass.Pop<Sprite>("What");
    }

    void Update()
    {
        if (Input.GetKeyDown(KeyCode.F1))
        {
            SelectWeapon(0);
        }
        else if (Input.GetKeyDown(KeyCode.F2))
        {
            SelectWeapon(1);
        }
        else if (Input.GetKeyDown(KeyCode.F3))
        {
            SelectWeapon(2);
        }

        if (activeTimer > 0f)
        {
            activeTimer -= Time.deltaTime;
            if (activeTimer <= 0f)
            {
                weaponSelectionUI.SetActive(false);
            }
        }
    }

    private void SelectWeapon(int index)
    {
        weaponSelectionUI.SetActive(true);
        audioSource.Play();
        activeTimer = 3f;
        for (int i = 0; i < NUM_WEAPONS; i++)
        {
            if (i != index)
            {
                weapons[i].expander.SetActive(false);
                weapons[i].inactiveImg.SetActive(true);
                weapons[i].selectedImg.SetActive(false);
            }
            else
            {
                weapons[i].expander.SetActive(true);
                weapons[i].inactiveImg.SetActive(false);
                weapons[i].selectedImg.SetActive(true);
                weapons[i].ammoLevel.fillAmount = 
                    slotMap[(ESlot)i].ContainedItem is Weapon weapon ? (float)weapon.GetCurrentMagazineCount() / weapon.GetMaxMagazineCount() : 0f;
            }
        }
    }

    // Generates a white silhouette icon for the weapon that can be colorized.
    private Sprite CreateWeaponIcon(Item item)
    {
        Sprite original;
        ResourceKey prefab = item.Prefab;
        if (!(prefab == null) && !string.IsNullOrEmpty(prefab.path))
            original = Singleton<GClass926>.Instance.GetItemIcon(item, new XYCellSizeStruct(5, 2)).Sprite;
        else
            return CacheResourcesPopAbstractClass.Pop<Sprite>("What");

        Rect rect = original.textureRect;
        Color[] pixels = original.texture.GetPixels((int)rect.x, (int)rect.y, (int)rect.width, (int)rect.height);
        Texture2D silhouetteTex = new((int)rect.width, (int)rect.height, TextureFormat.RGBA32, false);
        for (int i = 0; i < pixels.Length; i++)
        {
            float alpha = pixels[i].a;
            pixels[i] = new Color(1f, 1f, 1f, alpha); // White color, preserve alpha
        }

        silhouetteTex.SetPixels(pixels);
        silhouetteTex.Apply();
        return Sprite.Create(silhouetteTex, new Rect(0, 0, silhouetteTex.width, silhouetteTex.height), original.rect.size, original.pixelsPerUnit);
    }
}