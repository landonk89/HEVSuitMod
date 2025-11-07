using Comfort.Common;
using EFT;
using EFT.InventoryLogic;
using System;
using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace HEVSuitMod;

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

    private const int NUM_WEAPONS = 4; // NOTENOTE: Update if more slots are added (like quickslots)
    private GameObject weaponSelectionUI;
	private AudioSource audioSource;
	private AssetBundle assets = HEVMod.Instance.Assets;
	private WeaponSelection[] weapons = new WeaponSelection[NUM_WEAPONS];
	private float activeTimer = 0f;
	private Dictionary<Item, Sprite> weaponIconCache = [];

	private Action<Item> HolsterWeaponChanged;
	private Action<Item> PrimaryWeaponChanged;
	private Action<Item> SecondaryWeaponChanged;
	private Action<Item> ScabbardWeaponChanged;

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
		ScabbardWeaponChanged = (item) => OnWeaponChanged(item, weapons[(int)ESlot.Scabbard]);

        audioSource = GetComponent<AudioSource>();
		audioSource.volume = 0.5f;
		audioSource.playOnAwake = false;
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
        SelectWeaponPatch.SelectionEvent += SelectWeapon;
		Holster.OnAddOrRemoveItem += HolsterWeaponChanged;
		Primary.OnAddOrRemoveItem += PrimaryWeaponChanged;
		Secondary.OnAddOrRemoveItem += SecondaryWeaponChanged;
		Scabbard.OnAddOrRemoveItem += ScabbardWeaponChanged;

        for (int i = 0; i < NUM_WEAPONS; i++)
			OnWeaponChanged(slotMap[(ESlot)i].ContainedItem, weapons[i]);
	}

	private void OnDisable()
	{
        weaponSelectionUI.SetActive(false); // Hide on disable
		SelectWeaponPatch.SelectionEvent -= SelectWeapon;
        Holster.OnAddOrRemoveItem -= HolsterWeaponChanged;
		Primary.OnAddOrRemoveItem -= PrimaryWeaponChanged;
		Secondary.OnAddOrRemoveItem -= SecondaryWeaponChanged;
	}

	private void OnWeaponChanged(Item weapon, WeaponSelection selection)
	{
		selection.name.text = weapon != null ? weapon.ShortName.Localized() : "Error";
		selection.icon.sprite = weapon != null ? GetWeaponSprite(weapon) : CacheResourcesPopAbstractClass.Pop<Sprite>("What");
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

	private void SelectWeapon(ESlot index)
	{
		weaponSelectionUI.SetActive(true);
		audioSource.Play();
		activeTimer = 2f;
		for (int i = 0; i < NUM_WEAPONS; i++)
		{
            Item item = slotMap[(ESlot)i].ContainedItem;
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

	// New version testing
	private Sprite GetWeaponSprite(Item item)
	{
        if (item == null)
            return CacheResourcesPopAbstractClass.Pop<Sprite>("What");

        if (weaponIconCache.TryGetValue(item, out Sprite cachedSprite))
            return cachedSprite;

        GClass929 generatedImage = Singleton<GClass926>.Instance.GetItemIcon(item, new XYCellSizeStruct(5, 2));
        if (generatedImage == null || generatedImage.Sprite == null)
            return CacheResourcesPopAbstractClass.Pop<Sprite>("What");

        Texture2D tex = Instantiate(generatedImage.Sprite.texture);
		Color[] pixels = tex.GetPixels();
		for (int i = 0; i < pixels.Length; i++)
		{
			// Convert to grayscale and adjust exposure
			Color c = pixels[i];
			float gray = c.r * 0.299f + c.g * 0.587f + c.b * 0.114f;
			gray = Mathf.Lerp(gray, 1f, 0.5f);
			gray *= 1.5f;
			pixels[i] = new Color(gray, gray, gray, c.a);
		}

		tex.SetPixels(pixels);
		tex.Apply();

		Sprite output = Sprite.Create(tex, new Rect(0, 0, tex.width, tex.height), new Vector2(0.5f, 0.5f));
		weaponIconCache[item] = output;
		return output;
    }

	// Generates a white silhouette icon for the weapon that can be colorized.
	// BUGBUG: This gave me a null reference exception exactly one time, no idea what happened.
#if FALSE
	private Sprite GetWeaponSprite(Item item)
	{
		if (item == null)
            return CacheResourcesPopAbstractClass.Pop<Sprite>("What");

        if (weaponIconCache.TryGetValue(item, out Sprite cachedSprite))
            return cachedSprite;

        GClass929 generatedImage = Singleton<GClass926>.Instance.GetItemIcon(item, new XYCellSizeStruct(5, 2));
        if (generatedImage == null || generatedImage.Sprite == null)
            return CacheResourcesPopAbstractClass.Pop<Sprite>("What");

        Sprite originalSprite = generatedImage.Sprite;
        if (originalSprite.texture == null)
            return CacheResourcesPopAbstractClass.Pop<Sprite>("What");

        Rect texRect = originalSprite.textureRect;
        Color[] pixels = originalSprite.texture.GetPixels((int)texRect.x, (int)texRect.y, (int)texRect.width, (int)texRect.height); ;
        for (int i = 0; i < pixels.Length; i++)
        {
            float alpha = pixels[i].a;
            pixels[i] = new Color(1f, 1f, 1f, alpha);
        }

        Texture2D silhouetteTex = new((int)texRect.width, (int)texRect.height, TextureFormat.RGBA32, false);
        silhouetteTex.SetPixels(pixels);
        silhouetteTex.Apply();

		Vector2 pivot = new(originalSprite.pivot.x / originalSprite.rect.width, originalSprite.pivot.y / originalSprite.rect.height);
        Sprite output = Sprite.Create(silhouetteTex, new Rect(0, 0, silhouetteTex.width, silhouetteTex.height), pivot, originalSprite.pixelsPerUnit);
        weaponIconCache[item] = output;
        return output;
	}
#endif
}