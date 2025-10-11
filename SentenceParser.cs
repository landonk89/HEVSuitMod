using System;
using System.Collections.Generic;
using UnityEngine;

namespace HEVSuitMod
{
	/// <summary>
	/// Parses sentences.txt predefined sentences for VoiceController.
	/// </summary>
	public class SentenceParser
	{
		private VoiceController voiceController;
		private AssetBundle assets;
		private const int weaponMakerIndex = 1;
		private const int weaponModelIndex = 2;
		private const int weaponCaliberIndex = 3;
		private const int typeCaliberIndex = 1;
		private const int typeNameIndex = 2;
		private const int typeExtendedNameIndex = 3;

		public SentenceParser(VoiceController vc, AssetBundle a)
		{
			if (HEVMod.Instance == null)
			{
				Debug.LogError("HEVSuitMod.SentenceParser: HEVMod.Instance is null!");
				return;
			}

			voiceController = vc;
			assets = a;

			if (voiceController == null || assets == null)
			{
					HEVMod.Log.LogError("SentenceParser: Null Reference in constructor");
					return;
			}

			ParseAllSentences();
		}

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

		public void Reparse()
		{
			HEVMod.Log.LogWarning("Reparsing sentences...");
			voiceController.PurgeSentences();
			ParseAllSentences();
		}

		public void ParseAllSentences()
		{
			// Parse event sentences
			TextAsset hevSentencesFile = assets.LoadAsset<TextAsset>("assets/scripts/sentences.txt");
			if (hevSentencesFile == null)
			{
				HEVMod.Log.LogError("Failed to load sentences!!");
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
						HEVMod.Log.LogError($"Could not set SentenceType {sentenceTypeText}");

					continue;
				}

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
			HEVMod.Log.LogInfo($"Parsing sentence of type {sentenceType}: {sentence}");

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
					(sentenceType == SentenceType.Types && i == typeExtendedNameIndex && !HEVMod.Instance.sayExtendedOnChamberCheck.Value) ||
					(sentenceType == SentenceType.Types && i == typeCaliberIndex && !HEVMod.Instance.sayTypeOnChamberCheck.Value) ||
					(sentenceType == SentenceType.Weapons && i == weaponCaliberIndex && !HEVMod.Instance.sayTypeOnInspect.Value) ||
					(sentenceType == SentenceType.Weapons && i == weaponMakerIndex && !HEVMod.Instance.sayMakerOnInspect.Value);

				if (skip)
					continue;

				string clip;
				int loops = 1;
				float interval = 0f; // Default space between loops
				float pitch = 1f;
				float volume = HEVMod.Instance.globalVolume.Value;
				float delay = HEVMod.Instance.defaultDelay.Value; // No delay for the first?

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
							case "interval" when float.TryParse(val, out float intvl): interval = intvl; break;
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
