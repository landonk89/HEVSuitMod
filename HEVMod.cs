using BepInEx;
using BepInEx.Configuration;
using EFT;
using EFT.InventoryLogic;
using EFT.UI;
using UnityEngine.UI;
using System.IO;
using UnityEngine;

namespace HEVSuitMod;

[BepInPlugin(PluginInfo.PLUGIN_GUID, PluginInfo.PLUGIN_NAME, PluginInfo.PLUGIN_VERSION)]
public class HEVMod : BaseUnityPlugin
{
	public const float DEFAULT_PLAYBACK_DELAY = 0.25f;
	private const string SETTINGS_SECTION_GENERAL = "General Settings";
	private readonly string bundlePath = Path.Combine(BepInEx.Paths.PluginPath, PluginInfo.PLUGIN_NAME, "hevsuit.bundle");

	public static HEVMod Instance { get; private set; }

	public GameObject componentContainer;
	public AssetBundle Assets { get; private set; }
	public SentenceParser SentenceParser { get; private set; }
	public VoiceController VoiceController { get; private set; }
	public HudController HudController { get; private set; }
	public Flashlight Flashlight { get; private set; }

	public ConfigEntry<KeyboardShortcut> flashlightKey;
	public ConfigEntry<float> globalVolume;
	public ConfigEntry<float> ignoreDuplicateEffectsTime;
	public ConfigEntry<bool> identifyWeapon;
	public ConfigEntry<bool> identifyAmmo;

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
		SentenceParser = new SentenceParser(Assets);

		// Enable patches
		new OnNewGame().Enable();
		new OnGameEnded().Enable();
		new OnInspectWeapon().Enable();
		new OnInspectChamber().Enable();
		//new OnLoadSingleAmmo().Enable();

		// Register console commands
		ConsoleScreen.Processor.RegisterCommand<ImpulseCommand>();
	}

	// FIXME: Not working, I assume item isn't null when no rig equipped? need to check
	private void CheckForSuit(Item item)
	{
		if (item == null)
		{
			VoiceController.enabled = false;
			Flashlight.enabled = false;
			HudController.enabled = false;
		}
		else //if (item.Name == "item_equipment_rig_strandhogg") // TODO: Replace with HEV when it's asset is created
		{
			VoiceController.enabled = true;
			Flashlight.enabled = true;
			HudController.enabled = true;
		}
	}

	private void OnInventoryOpened(Player player, bool closing)
	{
		if (player != GamePlayerOwner.MyPlayer)
			return;

		HudController.enabled = !closing;
		HudController.Hud.SetActive(!closing);
	}

	public void OnGameStarted()
	{
		//GamePlayerOwner.MyPlayer.Equipment.GetSlot(EquipmentSlot.TacticalVest).OnAddOrRemoveItem += CheckForSuit;
		GamePlayerOwner.MyPlayer.OnInventoryOpened += OnInventoryOpened;
		componentContainer = new("HEVSuitMod");
		VoiceController = componentContainer.AddComponent<VoiceController>();
		Flashlight = componentContainer.AddComponent<Flashlight>();
		HudController = componentContainer.AddComponent<HudController>();
		//CheckForSuit(GamePlayerOwner.MyPlayer.Equipment.GetSlot(EquipmentSlot.TacticalVest).ContainedItem);
	}

	public void OnGameEnded()
	{
		//GamePlayerOwner.MyPlayer.Equipment.GetSlot(EquipmentSlot.TacticalVest).OnAddOrRemoveItem -= CheckForSuit;
		GamePlayerOwner.MyPlayer.OnInventoryOpened -= OnInventoryOpened;
		Destroy(VoiceController);
		Destroy(Flashlight);
		Destroy(HudController);
		Destroy(componentContainer);
	}
}
