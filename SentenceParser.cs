using BepInEx.Logging;
using System;
using System.Collections.Generic;
using UnityEngine;

namespace HEVSuitMod;

public class HEVAudioClip
{
    public AudioClip Clip { get; set; }
    public int Loops { get; }
    public float Interval { get; }
    public float Pitch { get; }
    public float Volume { get; }
    public float Delay { get; }

    public HEVAudioClip(AudioClip clip, int loops, float interval, float pitch, float volume, float delay)
    {
        Clip = clip;
        Loops = loops;
        Interval = interval;
        Pitch = pitch;
        Volume = volume;
        Delay = delay;
    }

    public HEVAudioClip(AudioClip clip)
    {
        Clip = clip;
        Loops = 1;
        Interval = 0f;
        Pitch = 1f;
        Volume = HEVMod.Instance.globalVolume.Value;
        Delay = HEVMod.DEFAULT_PLAYBACK_DELAY;
    }
}

public class HEVSentence(string identifier, List<HEVAudioClip> clips)
{
    public string Identifier { get; } = identifier;
    public List<HEVAudioClip> Clips { get; } = clips;
}

/// <summary>
/// Parses sentences.txt predefined sentences for VoiceController.
/// </summary>
public class SentenceParser
{
	private const string SENTENCES_FILE = "assets/scripts/sentences.txt";

	private readonly ManualLogSource log = BepInEx.Logging.Logger.CreateLogSource($"{typeof(SentenceParser).FullName}");
	private readonly AssetBundle assets;
	private readonly List<string> allFiles = [];
	private readonly List<string> missingFiles = []; // Catch 404s
	public readonly List<HEVSentence> allSentences = [];
	private string workingDirectory = "assets/sounds/";

	public SentenceParser(AssetBundle assetBundle)
	{
		assets = assetBundle;
		allFiles = [..assets.GetAllAssetNames()];
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

		string[] hevSentences = hevSentencesFile.text.Split(['\n', '\r'], StringSplitOptions.RemoveEmptyEntries);
		int sentenceCount = 0;
		foreach (string hevSentence in hevSentences)
		{
			if (hevSentence[0] == '/') // Skip comments
				continue;

			if (hevSentence[0] == '$') // Change working directory
			{
				workingDirectory = hevSentence.Substring(1);
				log.LogDebug($"Working directory changed to {workingDirectory}");
				continue;
			}
			sentenceCount++;
			allSentences.Add(ParseSentence(hevSentence));
		}

		log.LogInfo($"Parsed {sentenceCount} sentences.");
		if (missingFiles.Count > 0)
			log.LogWarning($"Encountered {missingFiles.Count} missing files:\n{Utils.FileTree(missingFiles)}");
	}

	// --------------------------------------------------------------
	// HEVSentence:
	// The first word is the event name or itemId like 'Death' or '5926bb2186f7744b1c6c6e60'
	// Then each sound filename is placed in line with tags before it enclosed in sqaure brackets [ ], each tag inside is separated by commas ','.
	// Multiple tags per file are supported, so something like '[delay:0.5,loop:2,pitch:1.2,volume:0.8]filename' will work
	// Example sentence: Death [loop:2]fx/beep [delay:0.1,loop:2]fx/beep [delay:0.1]fx/beep [delay:0.1]fx/beep [delay:0.1,pitch:1.2,volume:0.5]fx/flatline
	// --------------------------------------------------------------
	public HEVSentence ParseSentence(string sentence)
	{
		List<HEVAudioClip> clips = [];
		string[] tokens = sentence.Split(' ');
		log.LogDebug($"ParseSentence: {sentence}");

		// Parse tokenized sentence
		for (int i = 1; i < tokens.Length; i++)
		{
			string path;
			AudioClip clip;
			int loops = 1;
			float interval = 0f; // Default space between loops
			float pitch = 1f;
			float volume = HEVMod.Instance.globalVolume.Value;
			float delay = HEVMod.DEFAULT_PLAYBACK_DELAY;

			// For each token there may be parameters formatted like [param:value,param2:value]
			if (tokens[i].StartsWith("["))
			{
				string[] parameters = tokens[i].Substring(1, tokens[i].IndexOf(']') - 1).Split([','], StringSplitOptions.RemoveEmptyEntries);
				for (int j = 0; j < parameters.Length; j++)
				{
					string[] paramValuePair = parameters[j].Split(':');
					string key = paramValuePair[0];
					string val = paramValuePair[1];

					switch (key)
					{
						case "l" when int.TryParse(val, out int lps): loops = lps; break;
						case "i" when float.TryParse(val, out float intvl): interval = intvl; break;
						case "p" when float.TryParse(val, out float pch): pitch = pch; break;
						case "v" when float.TryParse(val, out float vol): volume *= vol; break;
						case "d" when float.TryParse(val, out float dly): delay = dly; break;
					}
				}
				path = workingDirectory + tokens[i].Substring(tokens[i].IndexOf(']') + 1).ToLower() + ".wav";
			}
			else // Token is just filename, no params
			{
				path = workingDirectory + tokens[i].ToLower() + ".wav";
			}

			if (!allFiles.Contains(path.ToLower()))
			{
				string missingFile = $"hevsuit.bundle/{path}";
				if (!missingFiles.Contains(missingFile))
					missingFiles.Add(missingFile);
				
				continue;
			}
			clip = assets.LoadAsset<AudioClip>(path);
			clips.Add(new HEVAudioClip(clip, loops, interval, pitch, volume, delay));
		}

		return new HEVSentence(tokens[0], clips);
	}
}
