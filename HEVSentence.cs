using System.Collections.Generic;

namespace HEVSuitMod
{
	public enum ESentenceType
	{
		None,
		Events,
		Weapons,
		Types
	}

	public class HEVSentence
	{
		public string Identifier { get; }
		public List<HEVAudioClip> Clips { get; }

		public HEVSentence(string identifier, List<HEVAudioClip> sentence)
		{
			Identifier = identifier;
			Clips = sentence;
		}
	}
}
