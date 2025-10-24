using BepInEx;
using BepInEx.Configuration;
using EFT.UI;
using System.IO;
using UnityEngine;

namespace HEVSuitMod;

[BepInPlugin(PluginInfo.PLUGIN_GUID, PluginInfo.PLUGIN_NAME, PluginInfo.PLUGIN_VERSION)]
public class HEVMod : BaseUnityPlugin
{
	// Constants
	public const float DEFAULT_PLAYBACK_DELAY = 0.25f;
	public const string MOD_DIR = PluginInfo.PLUGIN_NAME;
	public const string BUNDLE_FILE = "hevsuit.bundle";

	// Singleton
	public static HEVMod Instance { get; private set; }

	// File related stuff
	public AssetBundle Assets { get; private set; }
	private readonly string bundlePath = Path.Combine(BepInEx.Paths.PluginPath, MOD_DIR, BUNDLE_FILE);

	// Config
	public ConfigEntry<float> globalVolume;
	public ConfigEntry<float> ignoreDuplicateEffectsTime;
	public ConfigEntry<bool> sayMakerOnInspect;
	public ConfigEntry<bool> sayModelOnInspect;
	public ConfigEntry<bool> sayTypeOnInspect;
	public ConfigEntry<bool> sayTypeOnChamberCheck;
	public ConfigEntry<bool> sayNameOnChamberCheck;
	public ConfigEntry<bool> sayExtendedOnChamberCheck;
	public ConfigEntry<bool> applySettings;

	// Components
	private SentenceParser parser;
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
		ignoreDuplicateEffectsTime = Config.Bind(
				"Suit Settings",
				"Ignore duplicate events time",
				30.0f,
				"Don't play the same voiceline more than once within this amount of time (seconds)"
			);

		globalVolume = Config.Bind(
				"Voicelines",
				"Volume",
				1.0f,
				new ConfigDescription("Volume", new AcceptableValueRange<float>(0f, 1f))
			);

		sayMakerOnInspect = Config.Bind(
				"Voicelines",
				"Say weapon maker when inspecting (ex: Colt)",
				true,
				"When inspecting a weapon, the HEV will say the maker name first"
			);

		sayModelOnInspect = Config.Bind(
				"Voicelines",
				"Say weapon model when inspecting (ex: M4A1)",
				true,
				"When inspecting a weapon, the HEV will say the model name"
			);

		sayTypeOnInspect = Config.Bind(
				"Voicelines",
				"Say weapon caliber when inspecting (ex: 5.56x45)",
				false,
				"When inspecting a weapon, the HEV will say its caliber/type after the name"
			);

		sayTypeOnChamberCheck = Config.Bind(
				"Voicelines",
				"Say ammo caliber when checking chamber (Ex: 5.56x45)",
				false,
				"When inspecting a weapon's chamber, the HEV will say its caliber/type first"
			);

		sayNameOnChamberCheck = Config.Bind(
				"Voicelines",
				"Say ammo name when checking chamber (Ex: M855)",
				false,
				"When inspecting a weapon's chamber, the HEV will say its name"
			);

		sayExtendedOnChamberCheck = Config.Bind(
				"Voicelines",
				"Say ammo exdended name when checking chamber (Ex: Subsonic, Tracer)",
				true,
				"When inspecting a weapon's chamber, the HEV will say its extended name last (ex: Tracer)"
			);

		applySettings = Config.Bind(
			"Voicelines",
			"Apply and reload voice settings",
			false,
			"Check this box to reload voicelines after changing settings. It will automatically uncheck after running."
		);

		// Reload sentences when we need to
		applySettings.SettingChanged += (_, _) =>
		{
			if (applySettings.Value)
			{
				parser.Reparse();
				applySettings.Value = false;
			}
		};

		// Parse sentences file for the voicecontroller.
		parser = new(Assets);

		// Enable patches
		new OnNewGame().Enable();
		new OnGameEnded().Enable();
		new OnInspectWeapon().Enable();
		new OnInspectChamber().Enable();
		new OnLoadSingleAmmo().Enable();

		// Register console commands
		ConsoleScreen.Processor.RegisterCommand<ImpulseCommand>();
	}

	/// <summary>
	/// Called by patches when player spawns
	/// </summary>
	public void OnGameStarted()
	{
		voiceController = gameObject.AddComponent<VoiceController>();
		flashlight = gameObject.AddComponent<Flashlight>();
		hudController = gameObject.AddComponent<HudController>();
	}

	/// <summary>
	/// Called by patches when player despawns
	/// </summary>
	public void OnGameEnded()
	{
		Destroy(voiceController);
		Destroy(hudController);
		Destroy(flashlight);
	}
}
