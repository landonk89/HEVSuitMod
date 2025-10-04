using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using System;
using System.Reflection;

namespace HEVSuitMod
{
	public static class HEVSuit
	{
		public static float defaultDelay = 0.1f; // Should be configurable in settings?

		private static AssetBundle bundle;
		private static string bundlePath = Assembly.GetExecutingAssembly().Location + "/hevsuit.bundle";

		public static List<HEVSentence> sentences = new();

		static HEVSuit()
		{
			bundle = AssetBundle.LoadFromFile(bundlePath);

			// Parse event sentences
			TextAsset hevEventsFile = bundle.LoadAsset<TextAsset>("events");
			string[] hevEvents = hevEventsFile.text.Split(new[] { '\n', '\r' }, StringSplitOptions.RemoveEmptyEntries);
			foreach (string hevEvent in hevEvents)
			{
				if (hevEvent.StartsWith("//")) // Skip comments
					continue;

				sentences.Add(ParseSentence(hevEvent));
			}

			// Parse weapon sentences
			// CSV format is used here
			// itemId,maker(optional),model,type(optional)
			TextAsset weaponsFile = bundle.LoadAsset<TextAsset>("weapons");

			// Parse type sentences
			TextAsset typesFile = bundle.LoadAsset<TextAsset>("types");
		}

		// --------------------------------------------------------------
		// HEVSentence:
		// The first word is the event name like 'Death' or an itemId like '627e14b21713922ded6f2c15'
		// Then each sound filename is placed in line with tags before it enclosed in sqaure brackets [ ], each tag inside is separated by commas ','.
		// Multiple tags per file are supported, so something like '[delay:0.5,loop:2,pitch:1.2,volume:0.8]filename' will work
		// Example sentence: Death [loop:2]fx/beep [delay:0.1,loop:2]fx/beep [delay:0.1]fx/beep [delay:0.1]fx/beep [delay:0.1,pitch:1.2,volume:0.5]fx/flatline
		// --------------------------------------------------------------
		private static HEVSentence ParseSentence(string sentence)
		{
			List<HEVAudioClip> clips = new();
			string[] tokens = sentence.Split(' ');

			// Parse tokenized sentence
			for (int i = 1; i < tokens.Length; i++)
			{
				string clip;
				int loops = 1;
				float pitch = 1f;
				float volume = 1f;
				float delay = defaultDelay;

				// For each token there may be parameters
				if (tokens[i].StartsWith("["))
				{
					string[] parameters = tokens[i].Substring(1, tokens[i].IndexOf(']') - 1).Split(new char[] {','}, StringSplitOptions.RemoveEmptyEntries);
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
				else
				{
					clip = tokens[i];
				}

				clips.Add(new HEVAudioClip(clip, loops, pitch, volume, delay));
			}

			return new HEVSentence(tokens[0], clips);
		}

		// TODO
		public static IEnumerator PlaySentence(string sentence)
		{
			yield return null;
		}
	}
}
