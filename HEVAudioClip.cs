namespace HEVSuitMod
{
	public class HEVAudioClip
	{
		public string ClipName { get; set; }
		public int Loops { get; }
		public float Pitch { get; }
		public float Volume { get; }
		public float Delay { get; }

		public HEVAudioClip(string clip, int loops, float pitch, float volume, float delay)
		{
			ClipName = clip;
			Loops = loops;
			Pitch = pitch;
			Volume = volume;
			Delay = delay;
		}
	}
}
