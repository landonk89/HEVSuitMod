using System;
using BepInEx.Logging;
using EFT;
using EFT.InventoryLogic;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using EFT.UI;
using EFT.Console.Core;

namespace HEVSuitMod
{
	public class VoiceController : MonoBehaviour
	{
		public static VoiceController Instance { get; private set; }
		private static ManualLogSource log = BepInEx.Logging.Logger.CreateLogSource("HEVSuitMod.VoiceController");
		private AssetBundle assets;
		private AudioSource audioSource;
		public Coroutine sentencePlayer;
		private readonly List<HEVSentence> allSentences = SentenceParser.Instance.allSentences;
		public readonly List<HEVSentence> pendingSentences = [];
		private readonly HashSet<string> activeStatusEffects = [];

		private void Awake()
		{
			if (Instance != null && Instance != this)
			{
				Destroy(this);
				return;
			}
			else
				Instance = this;

			audioSource = gameObject.AddComponent<AudioSource>();
			assets = HEVMod.Instance.Assets;

			GamePlayerOwner.MyPlayer.HealthController.EffectStartedEvent += HealthEffectStartedEvent;
			GamePlayerOwner.MyPlayer.HealthController.EffectRemovedEvent += HealthEffectRemovedEvent;
			GamePlayerOwner.MyPlayer.OnPlayerDead += (_, _, _, _) => PlayerDiedEvent();
			GamePlayerOwner.MyPlayer.HandsChangedEvent += OnHandsChanged;
		}

		private void OnDestroy() 
		{
			GamePlayerOwner.MyPlayer.HealthController.EffectStartedEvent -= HealthEffectStartedEvent;
			GamePlayerOwner.MyPlayer.HealthController.EffectRemovedEvent -= HealthEffectRemovedEvent;
			GamePlayerOwner.MyPlayer.OnPlayerDead -= (_, _, _, _) => PlayerDiedEvent();
			GamePlayerOwner.MyPlayer.HandsChangedEvent -= OnHandsChanged;
			if (this == Instance) { Instance = null; }
		}

		// Update just monitors pendingSentences and starts playing if there are any
		// TODO: Priority sentences? Overlap them like hl1? This is fine now for testing
		private void Update()
		{
			if (pendingSentences.Count > 0 && sentencePlayer == null)
				sentencePlayer = StartCoroutine(PlaySentences());
		}

		public void OnHandsChanged(IHandsController handsController)
		{
			if (handsController.Item is Weapon weapon)
			{
				weapon.OnMalfunctionValidate += OnWeaponMalfunction;
			}
			if (handsController is Player.FirearmController faController)
			{
				faController.OnShot += () =>
				{
					if (faController.Weapon.GetCurrentMagazine().Count + faController.Weapon.ChamberAmmoCount == 0)
					{
						PlaySentenceById("OutOfAmmo");
					}
				};
			}
		}

		// Super crazy test zone

		[ConsoleCommand("hevplaysentence", "", null, "", [])]
		public static void DebugPlaySentence([ConsoleArgument("NearDeath", "Play a sentence from HEVSuitMod sentences.txt")] string sentence)
		{
			Instance.PlaySentenceById(sentence);
		}

		// End super crazy test zone

		/// <summary>
		/// Event triggered by player death
		/// </summary>
		private void PlayerDiedEvent()
		{
			PlaySentenceById("Death");
		}

		/// <summary>
		/// Event triggered by a body part being 'blacked'
		/// </summary>
		/// <param name="bodyPart"></param>
		/// <param name="damageType"></param>
		private void BodyPartDestroyedEvent(EBodyPart bodyPart, EDamageType damageType)
		{
			// TODO: HEV should say something like "Major injury, seek medical attention"
		}

		/// <summary>
		/// Play a sentence that describes the removed effect where the type is <paramref name="effect.Type.Name"/>
		/// </summary>
		/// <param name="effect"></param>
		private void HealthEffectRemovedEvent(IEffect effect)
		{
			// TODO: Auto-heal? and say stuff like "Bleeding has stopped" or "Splint Applied"
			Type effectType = effect.GetType(); // All effect classes are protected
			string effectName = effectType.Name;
			activeStatusEffects.Remove(effectName);
		}

		/// <summary>
		/// Play a sentence that describes the started effect where the type is <paramref name="effect.Type.Name"/>
		/// </summary>
		/// <param name="effect"></param>
		private void HealthEffectStartedEvent(IEffect effect)
		{
			Type effectType = effect.GetType(); // All effect classes are protected
			string effectName = effectType.Name;
			if (activeStatusEffects.Contains(effectName))
			{
#if DEBUG
				log.LogInfo($"HealthEffectStarted: Duplicate effect {effectName}");
#endif
				return;
			}

			AddEffect(effectName); // Prevent duplicates within ignoreDuplicateEffectsTime
			switch (effectName)
			{
				case "Fracture":
					switch (effect.BodyPart)
					{
						case EBodyPart.LeftLeg:
						case EBodyPart.RightLeg:
							// "Major Fracture" because we can't run
							PlaySentenceById("MajorFracture");
							break;

						case EBodyPart.LeftArm:
						case EBodyPart.RightArm:
							// "Minor Fracture" because a broken arm is no big deal
							PlaySentenceById("MinorFracture");
							break;
					}
					break;

				case "HeavyBleeding":
					PlaySentenceById("HeavyBleeding");
					break;

				case "LightBleeding":
					PlaySentenceById("LightBleeding");
					break;

				case "LowEdgeHealth":
					PlaySentenceById("NearDeath");
					break;

				case "Pain":
					break;

				case "PainKiller": // Grabbin pills
					break;

				case "Intoxication":
					break;

				case "Exhaustion":
					break;

				case "Dehydration":
					break;

				case "RadExposure":
					break;

				case "ZombieInfection":
					break;
			}
		}

		private void AddEffect(string effectName)
		{
			activeStatusEffects.Add(effectName);
			StartCoroutine(BeginExpireEffect(effectName));
#if DEBUG
			log.LogInfo($"HealthEffectStarted: {effectName}, ignoring duplicates for {HEVMod.Instance.ignoreDuplicateEffectsTime.Value} secs");
#endif
		}

		private IEnumerator BeginExpireEffect(string effectName)
		{
			yield return new WaitForSeconds(HEVMod.Instance.ignoreDuplicateEffectsTime.Value);

			activeStatusEffects.Remove(effectName);
		}

		public void WeaponInspectEvent()
		{
			// Play sentence with identifier matching held weapon
			string templateId = GamePlayerOwner.MyPlayer.HandsController.Item.StringTemplateId;
			if (templateId == null)
				return;

			PlaySentenceById(templateId);
		}

		public void ChamberInspectEvent()
		{
			// Play sentence with identifier matching ammo in chamber
			if (GamePlayerOwner.MyPlayer.HandsController.Item is not Weapon weapon)
				return;

			if (weapon.ChamberAmmoCount < 1)
				return;

			string templateId = weapon.Chambers[0].ContainedItem.StringTemplateId;
			if (templateId == null)
				return;

			PlaySentenceById(templateId);
		}

		public bool OnWeaponMalfunction(Weapon.EMalfunctionState state)
		{
			return false;
		}

#if DEBUG
		public void DebugPlayRandomSentence()
		{
			HEVSentence sentence = allSentences.PickRandom();
			log.LogInfo($"Playing Sentence: {sentence.Identifier}");
			PlaySentence(sentence);
		}
#endif
		// This handles the playback, triggered by Update() when needed
		private IEnumerator PlaySentences()
		{
			while (pendingSentences.Count > 0)
			{
				HEVSentence sentence = pendingSentences[0];
				foreach (HEVAudioClip clip in sentence.Clips)
				{
					audioSource.clip = assets.LoadAsset<AudioClip>(clip.ClipName);
					audioSource.pitch = clip.Pitch;
					audioSource.volume = clip.Volume;

					// Handle missing files
					if (audioSource.clip == null)
					{
						log.LogError($"Missing clip: {clip.ClipName}");
						continue;
					}

					yield return new WaitForSeconds(clip.Delay);
					for (int i = 0; i < clip.Loops; i++)
					{
						audioSource.Play();
						// TODO: Look into BetterAudio
						//Singleton<BetterAudio>.Instance.PlayAtPoint(GamePlayerOwner.MyPlayer.Position, audioSource.clip, CameraClass.Instance.Distance(GamePlayerOwner.MyPlayer.Position), BetterAudio.AudioSourceGroupType.Character, 15, 1f, EOcclusionTest.Fast);
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
			HEVSentence sentence = GetSentenceById(identifier);
			if (sentence == null)
			{
#if DEBUG
				log.LogError("GetSentenceById is null!");
#endif
				return;
			}

			PlaySentence(sentence);
		}

		/// <summary>
		/// Get a parsed sentence. If more than one shares an identifier, picks a random one.
		/// </summary>
		/// <param name="identifier"></param>
		public HEVSentence GetSentenceById(string identifier)
		{
			if (string.IsNullOrEmpty(identifier))
			{
				log.LogError("GetSentenceById was called with a null or empty identifier.");
				return null;
			}

			if (allSentences == null || allSentences.Count == 0)
			{
				log.LogWarning("GetSentenceById: allSentences is null or empty.");
				return null;
			}
			
			var matches = allSentences.Where(x => x != null && x.Identifier == identifier).ToList();
			if (matches.Count == 0)
			{
				log.LogWarning($"GetSentenceById: No sentence found for identifier '{identifier}'.");
				return null;
			}

			return matches.PickRandom();
		}

		/// <summary>
		/// Get a sentence from an integer, ex: 25
		/// </summary>
		/// <param name="number"></param>
		/// <returns></returns>
		public HEVSentence GetNumberSentence(int number)
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
		public HEVSentence GetDirectionSentence(int bearing)
		{
			// TODO: Add directions to sentences.txt instead of generating them
			return new HEVSentence(null, [new HEVAudioClip(GetDirectionClip(bearing))]);
		}

		/// <summary>
		/// Get direction clip from compass bearing
		/// </summary>
		/// <param name="bearing"></param>
		/// <returns></returns>
		public string GetDirectionClip(int bearing)
		{
			string[] directions = { "north", "northeast", "east", "southeast", "south", "southwest", "west", "northwest" };
			int index = Mathf.FloorToInt((bearing + 22.5f) / 45f) % 8;
			return $"assets/sounds/compass/{directions[index]}.wav";
		}

		/// <summary>
		/// Convert an integer into clip file names for generating a number sentence
		/// </summary>
		/// <param name="number"></param>
		/// <returns>An array of file names for generating the HEVClip</returns>
		private string[] GetNumberClips(int number)
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
