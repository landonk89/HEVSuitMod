using BepInEx;
using BepInEx.Configuration;
using System;
using System.IO;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;
using EFT;
using BepInEx.Logging;

namespace HEVSuitMod
{
	[BepInPlugin(PluginInfo.PLUGIN_GUID, PluginInfo.PLUGIN_NAME, PluginInfo.PLUGIN_VERSION)]
	public class HEVMod : BaseUnityPlugin
	{
		// Singleton
		public static HEVMod Instance { get; private set; }
		public static ManualLogSource Log;

		// File related stuff
		public AssetBundle Assets { get; private set; }
		private string bundlePath = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location) + "\\hevsuit.bundle";
		
		// Parser stuff
		private const int weaponMakerIndex = 1;
		private const int weaponModelIndex = 2;
		private const int weaponCaliberIndex = 3;
		private const int typeCaliberIndex = 1;
		private const int typeNameIndex = 2;
		private const int typeExtendedNameIndex = 3;

		// Config
		public ConfigEntry<bool> debugValidate;
		public ConfigEntry<bool> debugDrawCompass;
		public ConfigEntry<string> debugSentence;
		public ConfigEntry<string> debugNumberSentence;
		public ConfigEntry<float> globalVolume;
		public ConfigEntry<bool> sayMakerOnInspect;
		public ConfigEntry<bool> sayModelOnInspect;
		public ConfigEntry<bool> sayTypeOnInspect;
		public ConfigEntry<bool> sayTypeOnChamberCheck;
		public ConfigEntry<bool> sayNameOnChamberCheck;
		public ConfigEntry<bool> sayExtendedOnChamberCheck;
		public ConfigEntry<float> defaultDelay;
		public ConfigEntry<bool> applySettings;

		// TEMPORARY!!!
		private bool gameStarted = false;
		private bool subscribed = false;

		// Components
		VoiceController voiceController;
		SentenceParser parser;

		// Debug components
		DebugUICompass debugCompass;

		// Silly little helper
		private string GetDirectory(int index, SentenceType sentenceType)
		{
			if (sentenceType == SentenceType.Types)
				return "Assets/Sounds/Weapons/Types/"; // Every file is in the same place

			if (sentenceType == SentenceType.Events)
				return "Assets/Sounds/";

			// Must be a weapon
			switch (index)
			{
				case weaponMakerIndex: return "Assets/Sounds/Weapons/Maker/";
				case weaponModelIndex: return "Assets/Sounds/Weapons/Model/";
				case weaponCaliberIndex: return "Assets/Sounds/Weapons/Types/";
				default: return "ERROR/";
			}
		}

		private void Awake()
		{
			// Plugin startup logic
			Log = base.Logger;
			Log.LogInfo($"Plugin {PluginInfo.PLUGIN_GUID} is loaded!");

			if (Instance != null)
			{
				Logger.LogError($"Attempted to create duplicate instance of {PluginInfo.PLUGIN_GUID}!!");
				return;
			}

			Instance = this;
			//audioSource = gameObject.AddComponent<AudioSource>();

			Logger.LogWarning($"Bundle: {bundlePath}");
			Assets = AssetBundle.LoadFromFile(bundlePath);
			if (Assets == null)
			{
				Logger.LogError("Couldn't load assetbundle!!!");
			}

			// Config stuff
			debugValidate = Config.Bind(
					"Debug",
					"Validate all sentences",
					false,
					""
				);

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

			defaultDelay = Config.Bind(
					"HEV",
					"Default clip delay",
					0.25f,
					""
				);

			applySettings = Config.Bind(
				"Voicelines",
				"Apply and reload voice settings",
				false,
				"Check this box to reload voicelines after changing settings. It will automatically uncheck after running."
			);

			// Reload sentences when we need to
			// TODO: See if this really has any performance hit or stutter, just do it on every setting change if not
			applySettings.SettingChanged += (sender, args) =>
			{
				if (applySettings.Value)
				{
					parser.Reparse();
					applySettings.Value = false;
				}
			};

			debugValidate.SettingChanged += (sender, args) =>
			{
				if (debugValidate.Value)
				{
					voiceController.DebugValidateSentences();
					debugValidate.Value = false;
				}
			};

			debugDrawCompass.SettingChanged += (sender, args) =>
			{
				if (debugDrawCompass.Value)
					debugCompass.enabled = true;
				else
					debugCompass.enabled = false;
			};

			// Add components
			voiceController = gameObject.AddComponent<VoiceController>();
			parser = new(voiceController, Assets);

			// Add debugging/temporary components
			debugCompass = gameObject.AddComponent<DebugUICompass>();
		}

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

			// TODO: Just for testing here... Need to find a more correct way to do this.
			// Tried some patching with harmony and that was a giant failure, need to learn more
			// about GamePlayerOwner.MyPlayer and when it starts etc..
			if(GamePlayerOwner.MyPlayer != null && !gameStarted)
				gameStarted = true;
			
			if (!gameStarted)
				return;

			if (GamePlayerOwner.MyPlayer == null && subscribed)
			{
				gameStarted = false;
				UnsubscribeEvents();
				return;
			}

			if (!subscribed)
				SubscribeEvents();
		}

		private void DebugPlaySentence(string identifier)
		{
			HEVSentence sentence = voiceController.GetSentenceById(identifier);
			Logger.LogInfo($"Playing Sentence: {sentence.Identifier}");
			voiceController.PlaySentence(sentence);
		}

		private void DebugPlayNumberSentence(string number)
		{
			if (!int.TryParse(number, out int num))
			{
				Logger.LogError($"DebugPlayNumberSentence: int.TryParse(\"{number}\", out int num) failed.");
				return;
			}

			HEVSentence numberSentence = VoiceController.GetNumberSentence(num);
			Logger.LogInfo($"Playing number: {number}");
			voiceController.PlaySentence(numberSentence);
		}

		private void DebugCompassTest()
		{
			int lookDir = Compass.GetBearing(GamePlayerOwner.MyPlayer.LookDirection);
			Logger.LogInfo($"CompassTest: {VoiceController.GetDirectionClip(lookDir)}");
			voiceController.PlaySentence(VoiceController.GetDirectionSentence(lookDir));
		}

		public void SubscribeEvents()
		{
			// Detect fractures, bleeds, etc
			GamePlayerOwner.MyPlayer.HealthController.EffectAddedEvent += HealthEffectAdded;
			GamePlayerOwner.MyPlayer.HealthController.EffectRemovedEvent += HealthEffectRemoved;

			// Detect low health
			// Discard the params for this one, we don't need them. We just want to know that health has changed
			GamePlayerOwner.MyPlayer.HealthController.HealthChangedEvent += (_, _, _) => LowHealthEvent();
			GamePlayerOwner.MyPlayer.OnPlayerDead += (_, _, _, _) => PlayerDied();
			subscribed = true;
		}

		public void UnsubscribeEvents()
		{
			GamePlayerOwner.MyPlayer.HealthController.EffectAddedEvent -= HealthEffectAdded;
			GamePlayerOwner.MyPlayer.HealthController.EffectRemovedEvent -= HealthEffectRemoved;
			GamePlayerOwner.MyPlayer.HealthController.HealthChangedEvent -= (_, _, _) => LowHealthEvent();
			GamePlayerOwner.MyPlayer.OnPlayerDead -= (_, _, _, _) => PlayerDied();
			subscribed = false;
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

		private void PlayerDied()
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
		private void HealthEffectRemoved(IEffect effect)
		{
			switch (effect.Type.Name)
			{
				// TODO
				default:
					Logger.LogWarning($"HealthEffectRemoved: Unhandled IEffect {effect.Type.Name}");
					break;
			}
		}

		/// <summary>
		/// Play a sentence that describes the started effect where the type is <paramref name="effect.Type.Name"/>
		/// </summary>
		/// <param name="effect"></param>
		private void HealthEffectAdded(IEffect effect)
		{
			switch (effect.Type.Name)
			{
				case "Fracture":
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

				case "HeavyBleeding":
					voiceController.PlaySentenceById("HeavyBleeding");
					break;

				case "LightBleeding":
					voiceController.PlaySentenceById("LightBleeding");
					break;

				default:
					Logger.LogWarning($"HealthEffectStarted: Unhandled IEffect {effect.Type.Name}");
					break;
			}
		}
	}
}
