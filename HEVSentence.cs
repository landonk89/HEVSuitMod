using System.Collections.Generic;

namespace HEVSuitMod;

public class HEVSentence(string identifier, List<HEVAudioClip> clips)
{
	public string Identifier { get; } = identifier;
	public List<HEVAudioClip> Clips { get; } = clips;
}
