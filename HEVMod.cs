using BepInEx;
using BepInEx.Configuration;
using System;
using System.Collections.Generic;
using System.Collections;
using System.IO;
using UnityEngine;
using EFT;
using EFT.InventoryLogic;

namespace HEVSuitMod
{
	[BepInPlugin(PluginInfo.PLUGIN_GUID, PluginInfo.PLUGIN_NAME, PluginInfo.PLUGIN_VERSION)]
	public class HEVMod : BaseUnityPlugin
	{
		// Constants
		public const float DEFAULT_PLAYBACK_DELAY = 0.25f;
		public const string MOD_DIR = PluginInfo.PLUGIN_NAME;
		public const string BUNDLE_FILE = "hevsuit.bundle";
		private const string LIGHT_BLEEDING = "GInterface313";
		private const string HEAVY_BLEEDING = "GInterface314";
		private const string FRACTURE = "GInterface316";
		private const string DEHYDRATION = "GInterface317"; // Unverified
		private const string EXHAUSTION = "GInterface318"; // Unverified
		private const string RAD_EXPOSURE = "GInterface319"; // Unverified
		private const string INTOXICATION = "GInterface320"; // Might actually be GInterface309??
		private const string ZOMBIE_INFECTION = "GInterface329"; // Unverified
		private const string ON_PAINKILLERS = "GInterface332"; // Unverified
		private const string FROSTBITE = "GInterface346"; // Unverified

		// Singleton
		public static HEVMod Instance { get; private set; }

		// File related stuff
		public AssetBundle Assets { get; private set; }
		private readonly string bundlePath = Path.Combine(BepInEx.Paths.PluginPath, MOD_DIR, BUNDLE_FILE);

		// Config
#if DEBUG
		public ConfigEntry<bool> debugDrawCompass;
		public ConfigEntry<string> debugSentence;
		public ConfigEntry<string> debugNumberSentence;
#endif
		public ConfigEntry<float> globalVolume;
		public ConfigEntry<float> ignoreDuplicateEffectsTime;
		public ConfigEntry<bool> sayMakerOnInspect;
		public ConfigEntry<bool> sayModelOnInspect;
		public ConfigEntry<bool> sayTypeOnInspect;
		public ConfigEntry<bool> sayTypeOnChamberCheck;
		public ConfigEntry<bool> sayNameOnChamberCheck;
		public ConfigEntry<bool> sayExtendedOnChamberCheck;
		public ConfigEntry<bool> applySettings;

		// Track active effects for ignoreDuplicateEffectsTime
		private readonly HashSet<string> activeStatusEffects = [];

		// Components
		VoiceController voiceController;
		HudController hudController;
		SentenceParser parser;

		// Debug components
#if DEBUG
		DebugUICompass debugCompass;
#endif
		private void Awake()
		{
			Logger.LogInfo($"Plugin {PluginInfo.PLUGIN_NAME} is loaded!");

			if (Instance != null)
			{
				Logger.LogError($"Attempted to create duplicate instance of {PluginInfo.PLUGIN_NAME}!!");
				return;
			}

			Instance = this;
			Assets = AssetBundle.LoadFromFile(bundlePath);
			if (Assets == null)
			{
				Logger.LogFatal($"Couldn't load assetbundle, please reinstall {PluginInfo.PLUGIN_NAME}");
				return;
			}

			// Config stuff
#if DEBUG
			debugDrawCompass = Config.Bind(
					"Debug",
					"Draw temporary compass",
					false,
					""
				);

			debugSentence = Config.Bind(
					"Debug",
					"Sentence to play on F8 press",
					"Death",
					""
				);

			debugNumberSentence = Config.Bind(
					"Debug",
					"Number sentence to play on F10 press",
					"123",
					"Min:1 Max:9999"
				);
#endif
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

			// Add components
			voiceController = gameObject.AddComponent<VoiceController>();
			parser = new(voiceController, Assets);

			// Add debugging/temporary components
#if DEBUG
			debugCompass = gameObject.AddComponent<DebugUICompass>();
			debugDrawCompass.SettingChanged += (_, _) =>
			{
				if (debugDrawCompass.Value)
					debugCompass.enabled = true;
				else
					debugCompass.enabled = false;
			};
#endif
			// Enable patches
			new OnNewGame().Enable();
			new OnGameEnded().Enable();
			new OnInspectWeapon().Enable();
			new OnInspectChamber().Enable();
		}

#if DEBUG
		private void Update()
		{
			// For debugging/testing
			if (Input.GetKeyDown(KeyCode.F7))
				DebugCompassTest();

			if (Input.GetKeyDown(KeyCode.F8))
				DebugPlaySentence(debugSentence.Value);

			if (Input.GetKeyDown(KeyCode.F9))
				voiceController.DebugPlayRandomSentence();

			if (Input.GetKeyDown(KeyCode.F10))
				DebugPlayNumberSentence(debugNumberSentence.Value);
		}

		// TEMP for testing
		private void DebugPlaySentence(string identifier)
		{
			HEVSentence sentence = voiceController.GetSentenceById(identifier);
			Logger.LogInfo($"Playing Sentence: {sentence.Identifier}");
			voiceController.PlaySentence(sentence);
		}

		// TEMP for testing
		private void DebugPlayNumberSentence(string number)
		{
			if (!int.TryParse(number, out int num))
			{
				Logger.LogError($"DebugPlayNumberSentence: int.TryParse(\"{number}\", out int num) failed.");
				return;
			}

			HEVSentence numberSentence = voiceController.GetNumberSentence(num);
			Logger.LogInfo($"Playing number: {number}");
			voiceController.PlaySentence(numberSentence);
		}

		// TEMP for testing
		private void DebugCompassTest()
		{
			int lookDir = Compass.GetBearing(GamePlayerOwner.MyPlayer.LookDirection);
			Logger.LogInfo($"CompassTest: {voiceController.GetDirectionClip(lookDir)}");
			voiceController.PlaySentence(voiceController.GetDirectionSentence(lookDir));
		}
#endif

		public void OnGameStarted()
		{
			// Detect fractures, bleeds, etc
			GamePlayerOwner.MyPlayer.HealthController.EffectStartedEvent += HealthEffectStartedEvent;
			GamePlayerOwner.MyPlayer.HealthController.EffectRemovedEvent += HealthEffectRemovedEvent;
			GamePlayerOwner.MyPlayer.HealthController.HealthChangedEvent += (_, _, _) => LowHealthEvent();
			GamePlayerOwner.MyPlayer.OnPlayerDead += (_, _, _, _) => PlayerDiedEvent();

			hudController = gameObject.AddComponent<HudController>();
		}

		public void OnGameEnded()
		{
			GamePlayerOwner.MyPlayer.HealthController.EffectStartedEvent -= HealthEffectStartedEvent;
			GamePlayerOwner.MyPlayer.HealthController.EffectRemovedEvent -= HealthEffectRemovedEvent;
			GamePlayerOwner.MyPlayer.HealthController.HealthChangedEvent -= (_, _, _) => LowHealthEvent();
			GamePlayerOwner.MyPlayer.OnPlayerDead -= (_, _, _, _) => PlayerDiedEvent();

			Destroy(hudController);
		}

		/// <summary>
		/// Event triggered by player's HP falling below a threshold value
		/// </summary>
		private void LowHealthEvent()
		{
			// Determine current total HP, if below threshold say something dramatic
			// FIXME: Is there no built in way to get total hp?? This seems stupid but I can't find one
			float health = 0;
			foreach (EBodyPart part in Enum.GetValues(typeof(EBodyPart)))
				health += GamePlayerOwner.MyPlayer.ActiveHealthController.GetBodyPartHealth(part).Current;

			// TODO: replace this magic number with a ConfigEntry<float> name like lowHealth
			if (health < 250f)
			{
				// Say our health is low, suggest healing
				voiceController.PlaySentenceById("LowHealth");
			}
			else if (health < 100f)
			{
				// Say death is imminent, seek medical attention
				voiceController.PlaySentenceById("NearDeath");
			}
		}

		/// <summary>
		/// Event triggered by player death
		/// </summary>
		private void PlayerDiedEvent()
		{
			voiceController.PlaySentenceById("Death");
		}

		/// <summary>
		/// Event triggered by a body part being 'blacked'
		/// </summary>
		/// <param name="bodyPart"></param>
		/// <param name="damageType"></param>
		private void BodyPartDestroyedEvent(EBodyPart bodyPart, EDamageType damageType)
		{
			// TODO: Investigate ActiveHealthController.BodyPartDestroyedEvent further, HEV should say something like "Major injury, seek medical attention"
		}

		/// <summary>
		/// Play a sentence that describes the removed effect where the type is <paramref name="effect.Type.Name"/>
		/// </summary>
		/// <param name="effect"></param>
		private void HealthEffectRemovedEvent(IEffect effect)
		{
			// TODO: Auto-heal? and say stuff like "Bleeding has stopped" or "Splint Applied"
		}

		/// <summary>
		/// Play a sentence that describes the started effect where the type is <paramref name="effect.Type.Name"/>
		/// </summary>
		/// <param name="effect"></param>
		private void HealthEffectStartedEvent(IEffect effect)
		{
			string effectName = effect.Type.Name;
			if (activeStatusEffects.Contains(effectName))
			{
#if DEBUG
				Logger.LogInfo($"HealthEffectStarted: Duplicate effect {effectName}");
#endif
				return;
			}

			AddEffect(effectName); // Prevent duplicates within ignoreDuplicateEffectsTime

			switch (effectName)
			{
				case FRACTURE:
					switch (effect.BodyPart)
					{
						case EBodyPart.LeftLeg:
						case EBodyPart.RightLeg:
							// "Major Fracture" because we can't run
							voiceController.PlaySentenceById("MajorFracture");
							break;

						case EBodyPart.LeftArm:
						case EBodyPart.RightArm:
							// "Minor Fracture" because a broken arm is no big deal
							voiceController.PlaySentenceById("MinorFracture");
							break;
					}
					break;

				case HEAVY_BLEEDING:
					voiceController.PlaySentenceById("HeavyBleeding");
					break;

				case LIGHT_BLEEDING:
					voiceController.PlaySentenceById("LightBleeding");
					break;
			}
		}

		private void AddEffect(string effectName)
		{
			activeStatusEffects.Add(effectName);
			StartCoroutine(BeginExpireEffect(effectName));
#if DEBUG
			Logger.LogInfo($"HealthEffectStarted: {effectName}, ignoring duplicates for {ignoreDuplicateEffectsTime.Value} secs");
#endif
		}

		private IEnumerator BeginExpireEffect(string effectName)
		{
			yield return new WaitForSeconds(ignoreDuplicateEffectsTime.Value);

			activeStatusEffects.Remove(effectName);
		}
		
		public void WeaponInspectEvent()
		{
			// Play sentence with identifier matching held weapon
			string templateId = GamePlayerOwner.MyPlayer.HandsController.Item.StringTemplateId;
			if (templateId == null)
				return;

			voiceController.PlaySentenceById(templateId);
		}

		public void ChamberInspectEvent()
		{
			// Play sentence with identifier matching ammo in chamber
			if (GamePlayerOwner.MyPlayer.HandsController.Item is not Weapon weapon)
				return;

			string templateId = weapon.Chambers[0].ContainedItem.StringTemplateId;
			if (templateId == null)
				return;

			voiceController.PlaySentenceById(templateId);
		}
	}
}
