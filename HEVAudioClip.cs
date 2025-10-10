namespace HEVSuitMod
{
	public class HEVAudioClip
	{
		public string ClipName { get; set; }
		public int Loops { get; }
		public float Interval { get; }
		public float Pitch { get; }
		public float Volume { get; }
		public float Delay { get; }

		public HEVAudioClip(string clip, int loops, float interval, float pitch, float volume, float delay)
		{
			ClipName = clip;
			Loops = loops;
			Interval = interval;
			Pitch = pitch;
			Volume = volume;
			Delay = delay;
		}

		public HEVAudioClip(string clip)
		{
			ClipName = clip;
			Loops = 1;
			Interval = 0f;
			Pitch = 1f;
			Volume = HEVMod.Instance.globalVolume.Value;
			Delay = 0f;
		}
	}
}
