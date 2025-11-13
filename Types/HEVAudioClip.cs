using UnityEngine;

namespace HEVSuitMod.Types;

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