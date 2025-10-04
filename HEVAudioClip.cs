namespace HEVSuitMod
{
	public class HEVAudioClip
	{
		public string Clip { get; set; }
		public int Loops { get; }
		public float Pitch { get; }
		public float Volume { get; }
		public float Delay { get; }

		public HEVAudioClip(string clip, int loops, float pitch, float volume, float delay)
		{
			Clip = clip;
			Loops = loops;
			Pitch = pitch;
			Volume = volume;
			Delay = delay;
		}
	}
}
