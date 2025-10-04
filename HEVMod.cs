using BepInEx;
using BepInEx.Configuration;
using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;

namespace HEVSuitMod
{
	[BepInPlugin(PluginInfo.PLUGIN_GUID, PluginInfo.PLUGIN_NAME, PluginInfo.PLUGIN_VERSION)]
	public class HEVMod : BaseUnityPlugin
	{
		public static HEVMod Instance { get; private set; }

		private AssetBundle assets;
		private string bundlePath = Assembly.GetExecutingAssembly().Location + "/hevsuit.bundle";
		public List<HEVSentence> sentences = new();

		// Config
		public ConfigEntry<bool> sayMakerOnInspect;
		public ConfigEntry<bool> sayModelOnInspect;
		public ConfigEntry<bool> sayTypeOnInspect;
		public ConfigEntry<bool> sayTypeOnChamberCheck;
		public ConfigEntry<bool> sayNameOnChamberCheck;
		public ConfigEntry<bool> sayExtendedOnChamberCheck;
		public ConfigEntry<float> defaultDelay;
		private ConfigEntry<bool> applySettings;

		private void Awake()
		{
			// Plugin startup logic
			Logger.LogInfo($"Plugin {PluginInfo.PLUGIN_GUID} is loaded!");

			if (Instance == null)
			{
				Instance = this;
			}

			assets = AssetBundle.LoadFromFile(bundlePath);

			// Config stuff
			sayMakerOnInspect = Config.Bind(
					"Voicelines",
					"Say weapon maker",
					true,
					"When inspecting a weapon, the HEV will say the maker name first (ex: Colt)"
				);

			sayModelOnInspect = Config.Bind(
					"Voicelines",
					"Say weapon model",
					true,
					"When inspecting a weapon, the HEV will say the model name (ex: M4A1)"
				);

			sayTypeOnInspect = Config.Bind(
					"Voicelines",
					"Say weapon caliber",
					false,
					"When inspecting a weapon, the HEV will say its caliber/type after the name (ex: 5.56x45)"
				);

			sayTypeOnChamberCheck = Config.Bind(
					"Voicelines",
					"Say weapon caliber",
					false,
					"When inspecting a weapon, the HEV will say its caliber/type after the name (ex: 5.56x45)"
				);

			applySettings = Config.Bind(
				"Voicelines",
				"Apply and reload voice settings",
				false,
				"Check this box to reload voicelines after changing settings. It will automatically uncheck after running."
			);

			// Reload sentences when we need to
			applySettings.SettingChanged += (sender, args) =>
			{
				if (applySettings.Value)
				{
					sentences.Clear();
					ParseAllSentences();
				}
			};

			ParseAllSentences();
		}

		private void ParseAllSentences()
		{
			// Parse event sentences
			TextAsset hevSentencesFile = assets.LoadAsset<TextAsset>("scripts/sentences");
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

				sentences.Add(ParseSentence(hevSentence, sentenceType));
			}
		}

		// --------------------------------------------------------------
		// EventSentence:
		// The first word is the event name like 'Death'
		// Then each sound filename is placed in line with tags before it enclosed in sqaure brackets [ ], each tag inside is separated by commas ','.
		// Multiple tags per file are supported, so something like '[delay:0.5,loop:2,pitch:1.2,volume:0.8]filename' will work
		// Example sentence: Death [loop:2]fx/beep [delay:0.1,loop:2]fx/beep [delay:0.1]fx/beep [delay:0.1]fx/beep [delay:0.1,pitch:1.2,volume:0.5]fx/flatline
		// --------------------------------------------------------------
		private HEVSentence ParseSentence(string sentence, SentenceType sentenceType)
		{
			List<HEVAudioClip> clips = new();
			string[] tokens = sentence.Split(' ');

			// Parse tokenized sentence
			for (int i = 1; i < tokens.Length; i++)
			{
				if (tokens[i].Equals("NULL")) // Skip NULLs for those fixed length sentences
					continue;

				// Check if it's a weapon or type sentence, we need to selectively skip some parts
				// I know magic numbers equals BAD >:( but I don't care get over it
				if (sentenceType == SentenceType.Weapons && i == 1 && !sayMakerOnInspect.Value)
					continue;

				if (sentenceType == SentenceType.Weapons && i == 3 && !sayTypeOnInspect.Value)
					continue;

				if (sentenceType == SentenceType.Types && i == 1 && !sayTypeOnChamberCheck.Value)
					continue;

				if (sentenceType == SentenceType.Types && i == 3 && !sayExtendedOnChamberCheck.Value)
					continue;

				string clip;
				int loops = 1;
				float pitch = 1f;
				float volume = 1f;
				float delay = i == 1 ? 0f : Instance.defaultDelay.Value;

				// For each token there may be parameters
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
								{
									loops = loopsValue;
								}
								break;

							case "pitch":
								if (float.TryParse(paramValuePair[1], out float pitchValue))
								{
									pitch = pitchValue;
								}
								break;

							case "volume":
								if (float.TryParse(paramValuePair[1], out float volumeValue))
								{
									volume = volumeValue;
								}
								break;

							case "delay":
								if (float.TryParse(paramValuePair[1], out float delayValue))
								{
									delay = delayValue;
								}
								break;
						}
					}
					clip = tokens[i].Substring(tokens[i].IndexOf(']') + 1);
				}
				else // Token is just filename, no params
				{
					clip = tokens[i];
				}

				clips.Add(new HEVAudioClip(clip, loops, pitch, volume, delay));
			}

			return new HEVSentence(tokens[0], clips);
		}
	}
}
