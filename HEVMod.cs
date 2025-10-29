using BepInEx;
using BepInEx.Configuration;
using EFT.InventoryLogic;
using EFT;
using EFT.UI;
using System.IO;
using UnityEngine;

namespace HEVSuitMod;

[BepInPlugin(PluginInfo.PLUGIN_GUID, PluginInfo.PLUGIN_NAME, PluginInfo.PLUGIN_VERSION)]
public class HEVMod : BaseUnityPlugin
{
	// Constants
	public const float DEFAULT_PLAYBACK_DELAY = 0.25f;
	private const string SETTINGS_SECTION_GENERAL = "General Settings";

	// Singleton
	public static HEVMod Instance { get; private set; }

	// File related stuff
	public AssetBundle Assets { get; private set; }
	private readonly string bundlePath = Path.Combine(BepInEx.Paths.PluginPath, PluginInfo.PLUGIN_NAME, "hevsuit.bundle");

	// Config
	public ConfigEntry<KeyboardShortcut> flashlightKey;
	public ConfigEntry<float> globalVolume;
	public ConfigEntry<float> ignoreDuplicateEffectsTime;
	public ConfigEntry<bool> identifyWeapon;
	public ConfigEntry<bool> identifyAmmo;

	// Components
	private VoiceController voiceController;
	private HudController hudController;
	private Flashlight flashlight;

	private void Awake()
	{
		if (Instance != null && Instance != this)
		{
			Logger.LogFatal($"Attempted to create duplicate instance of {PluginInfo.PLUGIN_NAME}");
			Destroy(this);
			return;
		}
		else
			Instance = this;

		Logger.LogInfo($"Plugin {PluginInfo.PLUGIN_NAME} is loaded!");
		Assets = AssetBundle.LoadFromFile(bundlePath);
		if (Assets == null)
		{
			Logger.LogFatal($"Couldn't load assetbundle, please reinstall {PluginInfo.PLUGIN_NAME}");
			return;
		}

		// Config stuff
		flashlightKey = Config.Bind(
			SETTINGS_SECTION_GENERAL,
			"Flashlight key",
			new KeyboardShortcut(KeyCode.J),
			"What key toggles the HEV flashlight"
		);

		globalVolume = Config.Bind(
			SETTINGS_SECTION_GENERAL,
			"Volume",
			1.0f,
			new ConfigDescription("Volume", new AcceptableValueRange<float>(0f, 1f))
		);

		ignoreDuplicateEffectsTime = Config.Bind(
			SETTINGS_SECTION_GENERAL,
			"Ignore duplicate events time",
			30.0f,
			"Don't play the same voiceline more than once within this amount of time (seconds)"
		);

		identifyWeapon = Config.Bind(
			SETTINGS_SECTION_GENERAL,
			"Speak weapon name on inspect",
			false,
			"HEV will speak the name of your weapon when you inspect it."
		);

		identifyAmmo = Config.Bind(
			SETTINGS_SECTION_GENERAL,
			"Speak ammo name on chamber check",
			false,
			"HEV will speak the name of the ammo type in your weapon's chamber when you check it."
		);

		// Parse sentences file for the voicecontroller.
		new SentenceParser(Assets);

		// Enable patches
		new OnNewGame().Enable();
		new OnGameEnded().Enable();
		new OnInspectWeapon().Enable();
		new OnInspectChamber().Enable();
		new OnLoadSingleAmmo().Enable();

		// Register console commands
		ConsoleScreen.Processor.RegisterCommand<ImpulseCommand>();
	}

	private void CheckForSuit(Item item)
	{
		if (item == null)
		{
			voiceController.enabled = false;
			hudController.enabled = false;
			flashlight.enabled = false;
		}
		else //if (item.Name == "item_equipment_rig_strandhogg") // TODO: Replace with HEV when it's asset is created
		{
			voiceController.enabled = true;
			hudController.enabled = true;
			flashlight.enabled = true;
		}
	}

	public void OnGameStarted()
	{
		//GamePlayerOwner.MyPlayer.Equipment.GetSlot(EquipmentSlot.TacticalVest).OnAddOrRemoveItem += CheckForSuit;
		voiceController = gameObject.AddComponent<VoiceController>();
		flashlight = gameObject.AddComponent<Flashlight>();
		hudController = gameObject.AddComponent<HudController>();
		//CheckForSuit(GamePlayerOwner.MyPlayer.Equipment.GetSlot(EquipmentSlot.TacticalVest).ContainedItem);
	}

	public void OnGameEnded()
	{
		//GamePlayerOwner.MyPlayer.Equipment.GetSlot(EquipmentSlot.TacticalVest).OnAddOrRemoveItem -= CheckForSuit;
		Destroy(voiceController);
		Destroy(hudController);
		Destroy(flashlight);
	}
}
