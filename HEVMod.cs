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

namespace HEVSuitMod
{
	[BepInPlugin(PluginInfo.PLUGIN_GUID, PluginInfo.PLUGIN_NAME, PluginInfo.PLUGIN_VERSION)]
	public class HEVMod : BaseUnityPlugin
	{
		// Singleton
		public static HEVMod Instance { get; private set; }

		// File related stuff
		private AssetBundle assets;
		private string bundlePath = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location) + "\\hevsuit.bundle";
		private const int weaponMakerIndex = 1;
		private const int weaponTypeIndex = 3;
		private const int typeCaliberIndex = 1;
		private const int typeExtendedNameIndex = 3;

		// Sound stuff
		private AudioSource audioSource;
		private List<HEVSentence> allSentences = new();
		private List<HEVSentence> pendingSentences = new();
		private Coroutine sentencePlayer;

		// Config
		public ConfigEntry<float> globalVolume;
		public ConfigEntry<bool> sayMakerOnInspect;
		public ConfigEntry<bool> sayModelOnInspect;
		public ConfigEntry<bool> sayTypeOnInspect;
		public ConfigEntry<bool> sayTypeOnChamberCheck;
		public ConfigEntry<bool> sayNameOnChamberCheck;
		public ConfigEntry<bool> sayExtendedOnChamberCheck;
		public ConfigEntry<float> defaultDelay;
		public ConfigEntry<bool> applySettings;

		private void Awake()
		{
			// Plugin startup logic
			Logger.LogInfo($"Plugin {PluginInfo.PLUGIN_GUID} is loaded!");

			if (Instance != null)
			{
				Logger.LogError($"Attempted to create duplicate instance of {PluginInfo.PLUGIN_GUID}!!");
				return;
			}

			Instance = this;

			audioSource = gameObject.AddComponent<AudioSource>();

			Logger.LogWarning($"Bundle: {bundlePath}");
			assets = AssetBundle.LoadFromFile(bundlePath);
			if (assets == null)
			{
				Logger.LogError("Couldn't load assetbundle!!!");
			}

			foreach (var file in assets.GetAllAssetNames())
			{
				Logger.LogInfo(file);
			}

			// Config stuff
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
					false,
					"When inspecting a weapon's chamber, the HEV will say its extended name last (ex: Tracer)"
				);

			defaultDelay = Config.Bind(
					"HEV",
					"Default clip delay",
					0.1f,
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
					allSentences.Clear();
					ParseAllSentences();
				}
			};

			ParseAllSentences();
		}

		private void Update()
		{
			if (Input.GetKeyDown(KeyCode.F9))
				DebugPlayRandomSentence();

			if (pendingSentences.Count > 0 && sentencePlayer == null)
				sentencePlayer = StartCoroutine(PlaySentences());				
		}

		private void DebugPlayRandomSentence()
		{
			HEVSentence sentence = allSentences.PickRandom();
			Logger.LogInfo($"Playing Sentence: {sentence.Identifier}");
			pendingSentences.Add(sentence);
		}

		private IEnumerator PlaySentences()
		{
			while (pendingSentences.Count > 0)
			{
				var sentence = pendingSentences[0];

				foreach (var clip in sentence.Clips)
				{
					audioSource.clip = assets.LoadAsset<AudioClip>(clip.ClipName);
					audioSource.pitch = clip.Pitch;
					audioSource.volume = clip.Volume;

					// Handle missing files
					if (audioSource.clip == null)
					{
						Logger.LogError($"Missing clip: {clip.ClipName}");
						continue;
					}

					for (int i = 0; i < clip.Loops; i++)
					{
						yield return new WaitForSeconds(clip.Delay);
						audioSource.Play();
						yield return new WaitForSeconds(audioSource.clip.length);
					}
				}

				pendingSentences.RemoveAt(0);
			}

			sentencePlayer = null;
		}

		/// <summary>
		/// Pick a random sentence from the list that has the identifier '<paramref name="identifier"/>'
		/// </summary>
		/// <param name="identifier"></param>
		/// <returns></returns>
		private HEVSentence GetSentenceRandom(string identifier)
		{
			return allSentences.Where(x => x.Identifier == identifier).PickRandom();
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
				pendingSentences.Add(GetSentenceRandom("LowHealth"));
			}
			else if (health < 100f)
			{
				// Say death is imminent, seek medical attention
				pendingSentences.Add(GetSentenceRandom("NearDeath"));
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
							pendingSentences.Add(GetSentenceRandom("MajorFracture"));
							break;

						case EBodyPart.LeftArm:
						case EBodyPart.RightArm:
							// "Minor Fracture" because a broken arm is no big deal
							pendingSentences.Add(GetSentenceRandom("MinorFracture"));
							break;
					}
					break;

				case "HeavyBleeding":
					pendingSentences.Add(GetSentenceRandom("HeavyBleeding"));
					break;

				case "LightBleeding":
					pendingSentences.Add(GetSentenceRandom("LightBleeding"));
					break;

				default:
					Logger.LogWarning($"HealthEffectStarted: Unhandled IEffect {effect.Type.Name}");
					break;
			}
		}

		private void SubscribeEvents()
		{
			// Detect fractures, bleeds, etc
			GamePlayerOwner.MyPlayer.HealthController.EffectAddedEvent += HealthEffectAdded;
			GamePlayerOwner.MyPlayer.HealthController.EffectRemovedEvent += HealthEffectRemoved;
			
			// Detect low health
			// Discard the params for this one, we don't need them. We just want to know that health has changed
			GamePlayerOwner.MyPlayer.HealthController.HealthChangedEvent += (_, _, _) => LowHealthEvent();
		}

		private void UnsubscribeEvents()
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

				allSentences.Add(ParseSentence(hevSentence, sentenceType));
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
				if (tokens[i].Equals("NULL")) // Skip NULLs for those fixed length sentences
					continue;

				// Check if it's a weapon or type sentence, we may need to skip some parts
				if (sentenceType == SentenceType.Weapons && i == weaponMakerIndex && !sayMakerOnInspect.Value)
					continue;

				if (sentenceType == SentenceType.Weapons && i == weaponTypeIndex && !sayTypeOnInspect.Value)
					continue;

				if (sentenceType == SentenceType.Types && i == typeCaliberIndex && !sayTypeOnChamberCheck.Value)
					continue;

				if (sentenceType == SentenceType.Types && i == typeExtendedNameIndex && !sayExtendedOnChamberCheck.Value)
					continue;

				string clip;
				int loops = 1;
				float pitch = 1f;
				float volume = globalVolume.Value;
				//float delay = 0f;
				float delay = i == 1 ? 0f : Instance.defaultDelay.Value; // No delay for the first clip

				// For each token there may be parameters formatted like [param:value,param2:value]
				if (tokens[i].StartsWith("["))
				{
					string[] parameters = tokens[i].Substring(1, tokens[i].IndexOf(']') - 1).Split(new char[] { ',' }, StringSplitOptions.RemoveEmptyEntries);
					for (int j = 0; j < parameters.Length; j++)
					{
						string[] paramValuePair = parameters[j].Split(':');

						switch (paramValuePair[0])
						{
							case "loops":
								if (int.TryParse(paramValuePair[1], out int loopsValue))
									loops = loopsValue;

								break;

							case "pitch":
								if (float.TryParse(paramValuePair[1], out float pitchValue))
									pitch = pitchValue;

								break;

							case "volume":
								if (float.TryParse(paramValuePair[1], out float volumeValue))
									volume *= volumeValue; // TODO: Test me!!

								break;

							case "delay":
								if (float.TryParse(paramValuePair[1], out float delayValue))
									delay = delayValue;

								break;
						}
					}
					clip = "assets/sounds/" + tokens[i].Substring(tokens[i].IndexOf(']') + 1).ToLower() + ".wav";
				}
				else // Token is just filename, no params
				{
					clip = "assets/sounds/" + tokens[i].ToLower() + ".wav";
				}

				clips.Add(new HEVAudioClip(clip, loops, pitch, volume, delay));
			}

			return new HEVSentence(tokens[0], clips);
		}

		/// <summary>
		/// 
		/// </summary>
		/// <param name="sentence"></param>
		/// <returns></returns>
		private IEnumerator PlaySentence(HEVSentence sentence)
		{
			yield return null;
		}
	}
}
