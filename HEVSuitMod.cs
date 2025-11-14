using BepInEx;
using BepInEx.Configuration;
using EFT.UI;
using HEVSuitMod.Components;
using HEVSuitMod.Patches;
using HEVSuitMod.Tools;
using System.IO;
using UnityEngine;

namespace HEVSuitMod;

[BepInPlugin(PluginInfo.PLUGIN_GUID, PluginInfo.PLUGIN_NAME, PluginInfo.PLUGIN_VERSION)]
public class HEVSuitMod : BaseUnityPlugin
{
	public const float DEFAULT_PLAYBACK_DELAY = 0.25f;
	private const string SETTINGS_SECTION_GENERAL = "General Settings";
	private readonly string bundlePath = Path.Combine(BepInEx.Paths.PluginPath, PluginInfo.PLUGIN_NAME, "hevsuit.bundle");

	public static HEVSuitMod Instance { get; private set; }

	public AssetBundle Assets { get; private set; }
	public GameObject ComponentContainer { get; private set; }
	public SentenceParser SentenceParser { get; private set; }
	public VoiceController VoiceController { get; private set; }
	public HudController HudController { get; private set; }
	public Flashlight Flashlight { get; private set; }
	public MedicalController MedicalController { get; private set; }

	public ConfigEntry<KeyboardShortcut> flashlightKey;
	public ConfigEntry<float> globalVolume;
	public ConfigEntry<float> ignoreDuplicateEffectsTime;
	public ConfigEntry<bool> identifyWeapon;
	public ConfigEntry<bool> identifyAmmo;
	public ConfigEntry<bool> milTime;

#pragma warning disable IDE0051 // Don't mark Unity methods as unused
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

		milTime = Config.Bind(
			SETTINGS_SECTION_GENERAL,
			"Use 24 hour time instead of AM/PM",
			false,
			"Anywhere the time is spoken or displayed, use 24 hour time instead of AM/PM"
		);

		// Parse sentences file for the voicecontroller.
		SentenceParser = new SentenceParser();

		// Enable patches
		new GameStartedPatch().Enable();
		new GameEndedPatch().Enable();
		new InspectWeaponPatch().Enable();
		new InspectChamberPatch().Enable();
		new SelectWeaponPatch().Enable();
		new PickupLootPatch().Enable();
		//new LoadSingleAmmoPatch().Enable();

		// Register console commands
		ConsoleScreen.Processor.RegisterCommand<ImpulseCommand>();
	}

#if DEBUG
	// We're just using this to test stuff right now
	private void Update()
	{
		if (Input.GetKeyDown(KeyCode.F1))
			MedicalController?.UseInjector("etgchange");

		if (Input.GetKeyDown(KeyCode.F2))
			MedicalController?.UseInjector("morphine");

		if (Input.GetKeyDown(KeyCode.F3))
			MedicalController?.UseInjector("zagustin");

		if (Input.GetKeyDown(KeyCode.F4))
		{
			VoiceController?.PlaySentenceById("MajorFracture");
			VoiceController?.PlaySentenceById("GiveMorphine");
		}

		if (Input.GetKeyDown(KeyCode.F5))
		{
			VoiceController?.PlaySentenceById("HeavyBleeding");
			VoiceController?.PlaySentenceById("GiveTourniquet");
		}
	}
#endif
#pragma warning restore IDE0051

	public void OnGameStarted()
	{
		ComponentContainer = new(PluginInfo.PLUGIN_NAME);
		VoiceController = ComponentContainer.AddComponent<VoiceController>();
		Flashlight = ComponentContainer.AddComponent<Flashlight>();
		HudController = ComponentContainer.AddComponent<HudController>();
		MedicalController = ComponentContainer.AddComponent<MedicalController>();
	}

	public void OnGameEnded()
	{
		Destroy(VoiceController);
		Destroy(Flashlight);
		Destroy(HudController);
		Destroy(MedicalController);
		Destroy(ComponentContainer);
	}
}
