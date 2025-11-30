using BepInEx.Logging;
using HEVSuitMod.Types;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using UnityEngine;

namespace HEVSuitMod.Tools;

/// <summary>
/// Parses sentences.txt predefined sentences for VoiceController.
/// </summary>
public class SentenceParser
{
	private const string SENTENCES_FILE = "Assets/scripts/sentences.txt";
	private const string DEFAULT_DIRECTORY = "Assets/Sounds/";

	private readonly ManualLogSource log = BepInEx.Logging.Logger.CreateLogSource(typeof(SentenceParser).FullName);
	private readonly List<string> allFiles = [];
	private readonly List<string> missingFiles = []; // Catch 404s
	public readonly List<HEVSentence> allSentences = [];
	private string workingDirectory = DEFAULT_DIRECTORY;

	private AssetBundle Assets => HEVSuitMod.Instance.Assets;

	public SentenceParser()
	{
		allFiles = [..Assets.GetAllAssetNames()];
		ParseAllSentences();
	}

	public void ParseAllSentences()
	{
		TextAsset hevSentencesFile = Assets.LoadAsset<TextAsset>(SENTENCES_FILE);
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

			if (hevSentence[0] == '$') // Change working workingDirectory
			{
				workingDirectory = hevSentence.Substring(1);
				log.LogDebug($"Working workingDirectory changed to {workingDirectory}");
				continue;
			}
			sentenceCount++;
			allSentences.Add(ParseSentence(hevSentence));
		}

		// Reset to the base workingDirectory for future use
		workingDirectory = DEFAULT_DIRECTORY;
		log.LogInfo($"Parsed {sentenceCount} sentences.");
		if (missingFiles.Count > 0)
			log.LogWarning($"Encountered {missingFiles.Count} missing files:\n{BundleUtils.FileTree(missingFiles)}");
	}

	// --------------------------------------------------------------
	// HEVSentence:
	// The first word is the event name or itemId like 'Death' or '5926bb2186f7744b1c6c6e60'
	// Then each sound filename is placed in line with tags before it enclosed in square brackets [ ], each tag inside is separated by commas ','.
	// Multiple tags per file are supported, so something like '[delay:0.5,loop:2,pitch:1.2,volume:0.8]filename' will work
	// Example sentence: Death [loop:2]fx/beep [delay:0.1,loop:2]fx/beep [delay:0.1]fx/beep [delay:0.1]fx/beep [delay:0.1,pitch:1.2,volume:0.5]fx/flatline
	// --------------------------------------------------------------
	public HEVSentence ParseSentence(string sentence)
	{
		ReadOnlySpan<char> span = sentence.AsSpan();
		List<HEVAudioClip> clips = [];

		int index = span.IndexOf(' ');
		int position = index + 1;
		var firstToken = span[..index];
		while (position <= span.Length)
		{
			int next = span[position..].IndexOf(' ');
			ReadOnlySpan<char> token;
			if (next == -1)
			{
				token = span[position..];
				position = span.Length + 1;
			}
			else
			{
				token = span.Slice(position, next);
				position += next + 1;
			}

			if (token.IsEmpty)
				continue;

			int loops = 1;
			float interval = 0f; // Default space between loops
			float pitch = 1f;
			float volume = HEVSuitMod.Instance.globalVolume.Value;
			float delay = HEVSuitMod.DEFAULT_PLAYBACK_DELAY;

			var file = token;
			if (!token.IsEmpty && token[0] == '[')
			{
				int end = token.IndexOf(']');
				var parameters = token[1..end];
				while (!parameters.IsEmpty)
				{
					int comma = parameters.IndexOf(',');
					ReadOnlySpan<char> pair;
					if (comma == -1)
					{
						pair = parameters;
						parameters = [];
					}
					else
					{
						pair = parameters[..comma];
						parameters = parameters[(comma + 1)..];
					}

					int colon = pair.IndexOf(':');
					var key = pair[..colon];
					var val = pair[(colon + 1)..];

					if (key.SequenceEqual("l"))
						loops = int.Parse(val);
					else if (key.SequenceEqual("i"))
						interval = float.Parse(val);
					else if (key.SequenceEqual("p"))
						pitch = float.Parse(val);
					else if (key.SequenceEqual("v"))
						volume *= float.Parse(val);
					else if (key.SequenceEqual("d"))
						delay = float.Parse(val);
				}
				file = token[(end + 1)..];
			}

			string path = (workingDirectory + file.ToString().ToLower() + ".wav");
			if (!allFiles.Contains(path.ToLower()))
			{
				string missingFile = $"hevsuit.bundle/{path}";
				if (!missingFiles.Contains(missingFile))
					missingFiles.Add(missingFile);

				continue;
			}
			var clipAsset = Assets.LoadAsset<AudioClip>(path);
			clips.Add(new HEVAudioClip(clipAsset, loops, interval, pitch, volume, delay));
		}
		return new HEVSentence(firstToken.ToString(), clips);
	}
}
