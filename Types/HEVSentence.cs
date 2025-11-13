using System.Collections.Generic;

namespace HEVSuitMod.Types;

public class HEVSentence(string identifier, List<HEVAudioClip> clips)
{
	public string Identifier { get; } = identifier;
	public List<HEVAudioClip> Clips { get; } = clips;
}
