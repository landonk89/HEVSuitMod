using BepInEx.Logging;
using System;
using System.Collections.Generic;
using UnityEngine;

namespace HEVSuitMod;

/// <summary>
/// Parses sentences.txt predefined sentences for VoiceController.
/// </summary>
public class SentenceParser
{
	public static SentenceParser Instance { get; private set; }

	// Constants
	private const string SOUND_BASE_DIR = "assets/sounds/";
	private const string SOUND_MAKER_DIR = "weapons/maker/";
	private const string SOUND_MODEL_DIR = "weapons/model/";
	private const string SOUND_TYPES_DIR = "weapons/types/";
	private const string SENTENCES_FILE = "assets/scripts/sentences.txt";

	private ManualLogSource log = BepInEx.Logging.Logger.CreateLogSource("HEVSuitMod.SentenceParser");
	private AssetBundle assets;
	private List<string> allFiles = [];
	private List<string> missingFiles = []; // Catch 404s
	private const int weaponMakerIndex = 1;
	private const int weaponModelIndex = 2;
	private const int weaponCaliberIndex = 3;
	private const int typeCaliberIndex = 1;
	private const int typeNameIndex = 2;
	private const int typeExtendedNameIndex = 3;
	public readonly List<HEVSentence> allSentences = [];

	public SentenceParser(AssetBundle assetBundle)
	{
		if (Instance != null && Instance != this)
			return;
		else
			Instance = this;

		assets = assetBundle;
		if (assets == null)
		{
				log.LogError("Couldn't get assetbundle!");
				return;
		}

		allFiles = [..assets.GetAllAssetNames()];
		ParseAllSentences();
	}

	private string GetDirectory(int index, EParseType parseType)
	{
		// Types have every file is in the same place
		if (parseType == EParseType.Types)
			return SOUND_BASE_DIR + SOUND_TYPES_DIR;

		// Events have partial paths aready
		if (parseType == EParseType.Events)
			return SOUND_BASE_DIR;

		// Must be a weapon, index based directory
		switch (index)
		{
			case weaponMakerIndex: return SOUND_BASE_DIR + SOUND_MAKER_DIR;
			case weaponModelIndex: return SOUND_BASE_DIR + SOUND_MODEL_DIR;
			case weaponCaliberIndex: return SOUND_BASE_DIR + SOUND_TYPES_DIR;
			
			// Shouldn't happen ever
			default:
				log.LogError($"GetDirectory(index: {index}, parseType: {parseType}) failed");
				return null;
		}
	}

	public void Reparse()
	{
		log.LogWarning("Reparsing sentences...");
		VoiceController.Instance?.StopCoroutine(VoiceController.Instance.sentencePlayer);
		missingFiles.Clear();
		allSentences.Clear();
		ParseAllSentences();
	}

	public void ParseAllSentences()
	{
		TextAsset hevSentencesFile = assets.LoadAsset<TextAsset>(SENTENCES_FILE);
		if (hevSentencesFile == null)
		{
			log.LogError("Failed to load sentences!!");
			return;
		}

		EParseType parseType = EParseType.None;
		string[] hevSentences = hevSentencesFile.text.Split(new[] { '\n', '\r' }, StringSplitOptions.RemoveEmptyEntries);
		int sentenceCount = 0;
		foreach (string hevSentence in hevSentences)
		{
			if (hevSentence.StartsWith("//")) // Skip comments
				continue;

			if (hevSentence.StartsWith("$")) // Change parse mode
			{
				string sentenceTypeText = hevSentence.Substring(1);
				if (!Enum.TryParse(sentenceTypeText, out parseType))
					log.LogError($"Unknown parse mode {sentenceTypeText}!");
#if DEBUG
				log.LogInfo($"Parsing {sentenceTypeText}");
#endif
				continue;
			}
			sentenceCount++;
			allSentences.Add(ParseSentence(hevSentence, parseType));
		}

		log.LogInfo($"Parsed {sentenceCount} sentences.");
		if (missingFiles.Count > 0)
			log.LogWarning($"Encountered {missingFiles.Count} missing files:\n{Utils.FileTree(missingFiles)}");
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
	private HEVSentence ParseSentence(string sentence, EParseType parseType)
	{
		List<HEVAudioClip> clips = [];
		string[] tokens = sentence.Split(' ');
#if DEBUG
		log.LogInfo($"ParseSentence: {sentence}");
#endif
		// Parse tokenized sentence
		for (int i = 1; i < tokens.Length; i++)
		{
			// Skip NULLs for those fixed length sentences
			if (tokens[i].Contains("NULL"))
				continue;

			// Check if it's a weapon or type sentence, we may need to skip some parts
			bool skip =
				(parseType == EParseType.Types && i == typeExtendedNameIndex && !HEVMod.Instance.sayExtendedOnChamberCheck.Value) ||
				(parseType == EParseType.Types && i == typeCaliberIndex && !HEVMod.Instance.sayTypeOnChamberCheck.Value) ||
				(parseType == EParseType.Weapons && i == weaponCaliberIndex && !HEVMod.Instance.sayTypeOnInspect.Value) ||
				(parseType == EParseType.Weapons && i == weaponMakerIndex && !HEVMod.Instance.sayMakerOnInspect.Value);

			if (skip)
				continue;

			string clip;
			int loops = 1;
			float interval = 0f; // Default space between loops
			float pitch = 1f;
			float volume = HEVMod.Instance.globalVolume.Value;
			float delay = HEVMod.DEFAULT_PLAYBACK_DELAY;

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
				clip = GetDirectory(i, parseType) + tokens[i].Substring(tokens[i].IndexOf(']') + 1).ToLower() + ".wav";
			}
			else // Token is just filename, no params
			{
				clip = GetDirectory(i, parseType) + tokens[i].ToLower() + ".wav";
			}

			if (!allFiles.Contains(clip.ToLower()))
			{
				string missingFile = $"{HEVMod.BUNDLE_FILE}/{clip}";
				if (!missingFiles.Contains(missingFile))
					missingFiles.Add(missingFile);
				
				continue;
			}

			clips.Add(new HEVAudioClip(clip, loops, interval, pitch, volume, delay));
		}

		return new HEVSentence(tokens[0], clips);
	}
}
