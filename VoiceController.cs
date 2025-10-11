using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace HEVSuitMod
{
	public class VoiceController : MonoBehaviour
	{
		private AudioSource audioSource;
		private List<HEVSentence> allSentences = new();
		private List<HEVSentence> pendingSentences = new();
		private Coroutine sentencePlayer;

		private void Awake()
		{
			audioSource = gameObject.AddComponent<AudioSource>();
		}

		// TODO: Right now this will just play everything added to the list until it's empty,
		// this will need a little system to keep from adding duplicate sentences and stop things
		// from getting out of hand, we don't want to pile up 50 sentences and play them all.
		// We need to be smart about what to add and what to skip. For example, 2 bleeds at the
		// same time or within a certain time shouldn't both be played. Same for 2 fractures, and so on.
		private void Update()
		{
			if (pendingSentences.Count > 0 && sentencePlayer == null)
				sentencePlayer = StartCoroutine(PlaySentences());
		}

		// TODO: Sanity checks?
		public void AddSentence(HEVSentence sentence)
		{
			HEVMod.Log.LogInfo($"Adding sentence: {sentence.Identifier}");
			allSentences.Add(sentence);
		}

		// Called before reloading sentences.txt
		public void PurgeSentences()
		{
			if (sentencePlayer != null)
				StopCoroutine(sentencePlayer);

			pendingSentences.Clear();
			allSentences.Clear();
		}

		public void DebugPlayRandomSentence()
		{
			HEVSentence sentence = allSentences.PickRandom();
			HEVMod.Log.LogInfo($"Playing Sentence: {sentence.Identifier}");
			PlaySentence(sentence);
		}

		public void DebugValidateSentences()
		{
			string[] allFiles = HEVMod.Instance.Assets.GetAllAssetNames();
			foreach (var sentence in allSentences)
			{
				foreach (var file in sentence.Clips)
				{
					if (!Array.Exists(allFiles, s => s.ToLower() == file.ClipName.ToLower()))
					{
						HEVMod.Log.LogError($"Sentence {sentence.Identifier}, File not found: {file.ClipName}");
					}
				}
			}
		}

		// This handles the playback, triggered by Update() when needed
		private IEnumerator PlaySentences()
		{
			while (pendingSentences.Count > 0)
			{
				HEVSentence sentence = pendingSentences[0];
				foreach (HEVAudioClip clip in sentence.Clips)
				{
					audioSource.clip = HEVMod.Instance.Assets.LoadAsset<AudioClip>(clip.ClipName);
					audioSource.pitch = clip.Pitch;
					audioSource.volume = clip.Volume;

					// Handle missing files
					if (audioSource.clip == null)
					{
						HEVMod.Log.LogError($"Missing clip: {clip.ClipName}");
						continue;
					}

					yield return new WaitForSeconds(clip.Delay);
					for (int i = 0; i < clip.Loops; i++)
					{
						audioSource.Play();
						//Singleton<BetterAudio>.instance.PlayAtPoint(sentence.Clips[i], BetterAudio.AudioSourceGroupType.Character);
						yield return new WaitForSeconds(audioSource.clip.length + clip.Interval);
					}
				}
				pendingSentences.RemoveAt(0);
			}
			sentencePlayer = null;
		}

		public void PlaySentence(HEVSentence sentence)
		{
			pendingSentences.Add(sentence);
		}

		public void PlaySentenceById(string identifier)
		{
			pendingSentences.Add(GetSentenceById(identifier));
		}

		/// <summary>
		/// Get a parsed sentence. If more than one shares an identifier, picks a random one.
		/// </summary>
		/// <param name="identifier"></param>
		public HEVSentence GetSentenceById(string identifier)
		{
			return allSentences.Where(x => x.Identifier == identifier).PickRandom();
		}

		/// <summary>
		/// Get a sentence from an integer, ex: 25
		/// </summary>
		/// <param name="number"></param>
		/// <returns></returns>
		public static HEVSentence GetNumberSentence(int number)
		{
			List<HEVAudioClip> clips = new();
			string[] clipNames = GetNumberClips(number);

			for (int i = 0; i < clipNames.Length; i++)
			{
				clipNames[i] = $"assets/sounds/numbers/{clipNames[i]}.wav";
				clips.Add(new HEVAudioClip(clipNames[i], 1, 0f, 1f, HEVMod.Instance.globalVolume.Value, 0f));
			}

			return new HEVSentence(null, clips);
		}

		/// <summary>
		/// Get direction as a sentence
		/// </summary>
		/// <param name="bearing"></param>
		/// <returns></returns>
		public static HEVSentence GetDirectionSentence(int bearing)
		{
			return new HEVSentence(null, [new HEVAudioClip(GetDirectionClip(bearing))]);
		}

		/// <summary>
		/// Get direction clip from compass bearing
		/// </summary>
		/// <param name="bearing"></param>
		/// <returns></returns>
		public static string GetDirectionClip(int bearing)
		{
			string[] directions = {
				"North", "Northeast", "East", "Southeast",
				"South", "Southwest", "West", "Northwest"
			};
			int index = Mathf.FloorToInt((bearing + 22.5f) / 45f) % 8;
			return $"assets/sounds/compass/{directions[index]}.wav";
		}

		/// <summary>
		/// Convert an integer into clip file names for generating a number sentence
		/// </summary>
		/// <param name="number"></param>
		/// <returns>An array of file names for generating the HEVClip</returns>
		private static string[] GetNumberClips(int number)
		{
			if (number == 0)
				return ["zero"];

			List<string> clips = new();

			if (number < 0)
			{
				clips.Add("negative");
				number = -number;
			}

			if (number >= 1000)
			{
				int thousands = number / 1000;
				clips.AddRange(GetNumberClips(thousands));
				clips.Add("thousand");
				number %= 1000;
			}

			if (number >= 100)
			{
				int hundreds = number / 100;
				clips.AddRange(GetNumberClips(hundreds));
				clips.Add("hundred");
				number %= 100;
			}

			if (number >= 20)
			{
				int tens = number / 10;
				switch (tens)
				{
					case 2: clips.Add("twenty"); break;
					case 3: clips.Add("thirty"); break;
					case 4: clips.Add("forty"); break;
					case 5: clips.Add("fifty"); break;
					case 6: clips.Add("sixty"); break;
					case 7: clips.Add("seventy"); break;
					case 8: clips.Add("eighty"); break;
					case 9: clips.Add("ninety"); break;
				}
				number %= 10;
			}

			switch (number)
			{
				case 1: clips.Add("one"); break;
				case 2: clips.Add("two"); break;
				case 3: clips.Add("three"); break;
				case 4: clips.Add("four"); break;
				case 5: clips.Add("five"); break;
				case 6: clips.Add("six"); break;
				case 7: clips.Add("seven"); break;
				case 8: clips.Add("eight"); break;
				case 9: clips.Add("nine"); break;
				case 10: clips.Add("ten"); break;
				case 11: clips.Add("eleven"); break;
				case 12: clips.Add("twelve"); break;
				case 13: clips.Add("thirteen"); break;
				case 14: clips.Add("fourteen"); break;
				case 15: clips.Add("fifteen"); break;
				case 16: clips.Add("sixteen"); break;
				case 17: clips.Add("seventeen"); break;
				case 18: clips.Add("eighteen"); break;
				case 19: clips.Add("nineteen"); break;
			}

			return clips.ToArray();
		}
	}
}
