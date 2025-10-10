using BepInEx;
using BepInEx.Configuration;
using System;
using System.IO;
using System.Linq;
using System.Collections;
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
		public AssetBundle assets;
		private string bundlePath = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location) + "\\hevsuit.bundle";
		private const int weaponMakerIndex = 1;
		private const int weaponModelIndex = 2;
		private const int weaponCaliberIndex = 3;
		private const int typeCaliberIndex = 1;
		private const int typeNameIndex = 2;
		private const int typeExtendedNameIndex = 3;

		// Sound stuff
		//private AudioSource audioSource;
		//private List<HEVSentence> allSentences = new();
		//private List<HEVSentence> pendingSentences = new();
		//private Coroutine sentencePlayer;

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

		// Components
		VoiceController voiceController;

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
			assets = AssetBundle.LoadFromFile(bundlePath);
			if (assets == null)
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
					//allSentences.Clear();
					voiceController.PurgeSentences();
					ParseAllSentences();
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

			// Add debugging/temporary components
			debugCompass = gameObject.AddComponent<DebugUICompass>();
			
			ParseAllSentences();
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
				//pendingSentences.Add(GetSentence("LowHealth"));
				voiceController.PlaySentenceById("LowHealth");
			}
			else if (health < 100f)
			{
				// Say death is imminent, seek medical attention
				//pendingSentences.Add(GetSentence("NearDeath"));
				voiceController.PlaySentenceById("NearDeath");
			}
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
							//pendingSentences.Add(GetSentence("MajorFracture"));
							voiceController.PlaySentenceById("MajorFracture");
							break;

						case EBodyPart.LeftArm:
						case EBodyPart.RightArm:
							// "Minor Fracture" because a broken arm is no big deal
							//pendingSentences.Add(GetSentence("MinorFracture"));
							voiceController.PlaySentenceById("MinorFracture");
							break;
					}
					break;

				case "HeavyBleeding":
					//pendingSentences.Add(GetSentence("HeavyBleeding"));
					voiceController.PlaySentenceById("HeavyBleeding");
					break;

				case "LightBleeding":
					//pendingSentences.Add(GetSentence("LightBleeding"));
					voiceController.PlaySentenceById("LightBleeding");
					break;

				default:
					Logger.LogWarning($"HealthEffectStarted: Unhandled IEffect {effect.Type.Name}");
					break;
			}
		}

		public void SubscribeEvents()
		{
			// Detect fractures, bleeds, etc
			GamePlayerOwner.MyPlayer.HealthController.EffectAddedEvent += HealthEffectAdded;
			GamePlayerOwner.MyPlayer.HealthController.EffectRemovedEvent += HealthEffectRemoved;
			
			// Detect low health
			// Discard the params for this one, we don't need them. We just want to know that health has changed
			GamePlayerOwner.MyPlayer.HealthController.HealthChangedEvent += (_, _, _) => LowHealthEvent();
		}

		public void UnsubscribeEvents()
		{
			GamePlayerOwner.MyPlayer.HealthController.EffectAddedEvent -= HealthEffectAdded;
			GamePlayerOwner.MyPlayer.HealthController.EffectRemovedEvent -= HealthEffectRemoved;
			GamePlayerOwner.MyPlayer.HealthController.HealthChangedEvent -= (_, _, _) => LowHealthEvent();
		}

		private void ParseAllSentences()
		{
			// Parse event sentences
			TextAsset hevSentencesFile = assets.LoadAsset<TextAsset>("assets/scripts/sentences.txt");
			if (hevSentencesFile == null)
			{
				Logger.LogError("Failed to load sentences!!");
				return;
			}

			SentenceType sentenceType = SentenceType.None;
			string[] hevSentences = hevSentencesFile.text.Split(new[] { '\n', '\r' }, StringSplitOptions.RemoveEmptyEntries);
			foreach (string hevSentence in hevSentences)
			{
				if (hevSentence.StartsWith("//")) // Skip comments
					continue;

				if (hevSentence.StartsWith("$")) // Change parse mode
				{
					string sentenceTypeText = hevSentence.Substring(1);
					if (!Enum.TryParse(sentenceTypeText, out sentenceType))
						Logger.LogError($"Could not set SentenceType {sentenceTypeText}");

					continue;
				}

				//allSentences.Add(ParseSentence(hevSentence, sentenceType));
				voiceController.AddSentence(ParseSentence(hevSentence, sentenceType));
			}
		}

		// --------------------------------------------------------------
		// Sentence:
		// The first word is the event name or itemId like 'Death' or '5926bb2186f7744b1c6c6e60'
		// Then each sound filename is placed in line with tags before it enclosed in sqaure brackets [ ], each tag inside is separated by commas ','.
		// Multiple tags per file are supported, so something like '[delay:0.5,loop:2,pitch:1.2,volume:0.8]filename' will work
		// Example sentence: Death [loop:2]fx/beep [delay:0.1,loop:2]fx/beep [delay:0.1]fx/beep [delay:0.1]fx/beep [delay:0.1,pitch:1.2,volume:0.5]fx/flatline
		// 
		// This parser has a few modes set by a '$mode' in the parsed file:
		// $Events: These are normal, any length because there are no settings for them.
		// $Weapons: These are a fixed length of 3 audio clips so we can selectively disable maker or caliber voicelines.
		// $Types: These are also a fixed length of 3 so we can selectively disable caliber or extendedName voicelines.
		// --------------------------------------------------------------
		private HEVSentence ParseSentence(string sentence, SentenceType sentenceType)
		{
			Logger.LogInfo($"Parsing sentence of type {sentenceType}: {sentence}");

			List<HEVAudioClip> clips = new();
			string[] tokens = sentence.Split(' ');

			// Parse tokenized sentence
			for (int i = 1; i < tokens.Length; i++)
			{
				// Skip NULLs for those fixed length sentences
				if (tokens[i].Contains("NULL"))
					continue;

				// Check if it's a weapon or type sentence, we may need to skip some parts
				bool skip =
					(sentenceType == SentenceType.Types && i == typeExtendedNameIndex && !sayExtendedOnChamberCheck.Value) ||
					(sentenceType == SentenceType.Types && i == typeCaliberIndex && !sayTypeOnChamberCheck.Value) ||
					(sentenceType == SentenceType.Weapons && i == weaponCaliberIndex && !sayTypeOnInspect.Value) ||
					(sentenceType == SentenceType.Weapons && i == weaponMakerIndex && !sayMakerOnInspect.Value);

				if (skip)
					continue;

				string clip;
				int loops = 1;
				float interval = 0f; // Default space between loops
				float pitch = 1f;
				float volume = globalVolume.Value;
				float delay = Instance.defaultDelay.Value; // No delay for the first?

				// For each token there may be parameters formatted like [param:value,param2:value]
				if (tokens[i].StartsWith("["))
				{
					string[] parameters = tokens[i].Substring(1, tokens[i].IndexOf(']') - 1).Split(new char[] { ',' }, StringSplitOptions.RemoveEmptyEntries);
					for (int j = 0; j < parameters.Length; j++)
					{
						string[] paramValuePair = parameters[j].Split(':');

						string key = paramValuePair[0];
						string val = paramValuePair[1];

						switch (key)
						{
							case "loops" when int.TryParse(val, out int lps): loops = lps; break;
							case "interval" when float.TryParse(val, out float intvl): interval = intvl ; break;
							case "pitch" when float.TryParse(val, out float pch): pitch = pch; break;
							case "volume" when float.TryParse(val, out float vol): volume *= vol; break;
							case "delay" when float.TryParse(val, out float dly): delay = dly; break;
						}

					}
					clip = GetDirectory(i, sentenceType) + tokens[i].Substring(tokens[i].IndexOf(']') + 1).ToLower() + ".wav";
				}
				else // Token is just filename, no params
				{
					clip = GetDirectory(i, sentenceType) + tokens[i].ToLower() + ".wav";
				}

				clips.Add(new HEVAudioClip(clip, loops, interval, pitch, volume, delay));
			}

			return new HEVSentence(tokens[0], clips);
		}
	}
}
