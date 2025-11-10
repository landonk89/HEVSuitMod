using System;
using BepInEx.Logging;
using EFT;
using EFT.InventoryLogic;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace HEVSuitMod;

public class VoiceController : MonoBehaviour
{
	private readonly ManualLogSource log = BepInEx.Logging.Logger.CreateLogSource($"{typeof(VoiceController).FullName}");
	private AudioSource audioSource;
	public Coroutine sentencePlayer;
	private List<HEVSentence> allSentences;
	public readonly List<HEVSentence> pendingSentences = [];
	private HashSet<IEffect> activeEffects = [];
	private IHandsController currentHandsController;

	private AssetBundle Assets => HEVMod.Instance.Assets;
	private Flashlight Flashlight => HEVMod.Instance.Flashlight;

	private Action OnShotHandler;
	private GDelegate71 PlayerDeadHandler; // TODO: Test this, was GDelegate70/OnPlayerDead before

	private void Awake()
	{
		PlayerDeadHandler = (_) => PlayerDied();
		audioSource = gameObject.AddComponent<AudioSource>();
		//Assets = HEVMod.Instance.Assets;
		allSentences = HEVMod.Instance.SentenceParser.allSentences;
		currentHandsController = GamePlayerOwner.MyPlayer.HandsController;
		GamePlayerOwner.MyPlayer.HealthController.EffectStartedEvent += EffectStarted;
		GamePlayerOwner.MyPlayer.OnPlayerDeadOrUnspawn += PlayerDeadHandler;
		GamePlayerOwner.MyPlayer.HandsChangedEvent += HandsChanged;
		Flashlight.BatteryStateChanged += FlashlightCritical;
	}

	private void Update()
	{
		if (pendingSentences.Count > 0 && sentencePlayer == null)
			sentencePlayer = StartCoroutine(PlaySentences());
	}

	private void OnDestroy()
	{
		if (sentencePlayer != null)
		{
			audioSource.Stop();
			pendingSentences.Clear();
			StopAllCoroutines();
			sentencePlayer = null;
		}

		GamePlayerOwner.MyPlayer.HealthController.EffectStartedEvent -= EffectStarted;
		GamePlayerOwner.MyPlayer.OnPlayerDeadOrUnspawn -= PlayerDeadHandler;
		GamePlayerOwner.MyPlayer.HandsChangedEvent -= HandsChanged;
		Flashlight.BatteryStateChanged -= FlashlightCritical;
	}

	// This handles the playback, triggered by Update() when needed
	private IEnumerator PlaySentences()
	{
		while (pendingSentences.Count > 0)
		{
			HEVSentence sentence = pendingSentences[0];
			foreach (HEVAudioClip clip in sentence.Clips)
			{
				audioSource.clip = clip.Clip;
				audioSource.pitch = clip.Pitch;
				audioSource.volume = clip.Volume;

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
		if (!pendingSentences.Contains(sentence)) // Don't play the same one agian
		pendingSentences.Add(sentence);
	}

	public void PlaySentenceById(string identifier)
	{
		HEVSentence sentence = GetSentenceById(identifier);
		if (sentence == null)
		{
			log.LogError("PlaySentenceById identifier is null!");
			return;
		}

		PlaySentence(sentence);
	}

	private HEVSentence GetSentenceById(string identifier)
	{
		var matches = allSentences.Where(x => x != null && x.Identifier == identifier).ToList();
		if (matches.Count == 0)
		{
			log.LogWarning($"GetSentenceById: No sentence found for identifier '{identifier}'.");
			return null;
		}

		return matches.PickRandom();
	}

	// Demand load number clips
	private HEVSentence GetNumberSentence(int number)
	{
		// See if we've already generated this number first
		HEVSentence sentence = GetSentenceById(number.ToString());
		if (sentence != null)
			return sentence;

		List<HEVAudioClip> clips = [];
		string[] clipNames = GetNumberClips(number);

		// TODO: Caching all of the number related clips might be better?
		for (int i = 0; i < clipNames.Length; i++)
		{
			clipNames[i] = $"assets/sounds/numbers/{clipNames[i]}.wav";
			AudioClip clip = Assets.LoadAsset<AudioClip>(clipNames[i]);
			clips.Add(new HEVAudioClip(clip, 1, 0f, 1f, HEVMod.Instance.globalVolume.Value, 0f));
		}

		sentence = new(number.ToString(), clips);
		allSentences.Add(sentence);
		return sentence;
	}

	private HEVSentence GetDirectionSentence(int bearing)
	{
		string[] directions = { "N", "NE", "E", "SE", "S", "SW", "W", "NW" };
		int index = Mathf.FloorToInt((bearing + 22.5f) / 45f) % 8;
		return GetSentenceById(directions[index]);
	}

	private string[] GetNumberClips(int number)
	{
		if (number == 0)
			return ["zero"];

		List<string> clips = [];

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

	public void HandsChanged(IHandsController handsController)
	{
		if (handsController is Player.FirearmController faController)
		{
			Player.FirearmController current = currentHandsController as Player.FirearmController;
			current.OnShot -= OnShotHandler;
			OnShotHandler = () =>
			{
				if (faController.Weapon.GetCurrentMagazine().Count + faController.Weapon.ChamberAmmoCount == 0)
					PlaySentenceById("OutOfAmmo");
			};
			currentHandsController = faController;
			faController.OnShot += OnShotHandler;
		}
	}

	private void FlashlightCritical(bool critical)
	{
		if (critical)
			PlaySentenceById("FlashlightLow");
	}

	private void PlayerDied()
	{
		if (sentencePlayer != null)
		{
			StopCoroutine(sentencePlayer);
			pendingSentences.Clear();
			audioSource.Stop();
		}
		PlaySentenceById("Death");
	}

	private void EffectStarted(IEffect effect)
	{
		Type effectType = effect.GetType(); // All effect classes are protected
		string effectName = effectType.Name;
		if (activeEffects.Contains(effect))
		{
			log.LogDebug($"Duplicate effect {effectName}");
			return;
		}

		bool handled = false;
		switch (effectName)
		{
			case "Fracture":
				switch (effect.BodyPart)
				{
					case EBodyPart.LeftLeg:
					case EBodyPart.RightLeg:
						// "Major Fracture" because we can't run
						PlaySentenceById("MajorFracture");
						PlaySentenceById("GiveMorphine");
						break;

					case EBodyPart.LeftArm:
					case EBodyPart.RightArm:
						// "Minor Fracture" because a broken arm is no big deal
						PlaySentenceById("MinorFracture");
						break;
				}
				handled = true;
				break;

			case "HeavyBleeding":
				PlaySentenceById("HeavyBleeding");
				PlaySentenceById("GiveTourniquet");
				handled = true;
				break;

			case "LightBleeding":
				PlaySentenceById("LightBleeding");
				PlaySentenceById("GiveBandage"); // FIXME: Doesn't exist yet
				handled = true;
				break;

			case "LowEdgeHealth":
				PlaySentenceById("NearDeath"); // TODO: Better voice line?
				handled = true;
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

		if (handled)
		{
			activeEffects.Add(effect);
			log.LogDebug($"Effect {effectName} added");
		}
	}

	private void EffectRemoved(IEffect effect)
	{
		activeEffects.Remove(effect);
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
}
