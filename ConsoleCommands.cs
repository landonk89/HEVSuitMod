using EFT.Console.Core;
using EFT.UI;

namespace HEVSuitMod;

[ConsoleCommand("impulse", "", null, "Try 101 :)", ["i"])]
public class ImpulseCommand(string impulse) : SyncCommand
{
	public override object[] ArgumentsValue { get { return [impulse]; } }
	private string impulse = impulse;

	public override void Execute()
	{
		if (!int.TryParse(impulse, out var impulseValue))
		{
			ConsoleScreen.LogError($"Couldn't parse int value {impulse}");
			return;
		}

		switch (impulseValue)
		{
			// Play a random sentence
			case 1:
				VoiceController.Instance?.PlaySentenceById(SentenceParser.Instance.allSentences.PickRandom().Identifier);
				break;

			case 101:
				// TODO: super neato stuff will go here
				ConsoleScreen.Log("Cheater!");
				break;
		}
	}
}
