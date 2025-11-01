using Comfort.Common;
using EFT;
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
		if (!Singleton<GameWorld>.Instantiated)
		{
			//ConsoleScreen.LogError("Impulse commands only work in-raid.");
			//return;
		}

		if (!int.TryParse(impulse, out var impulseValue))
		{
			ConsoleScreen.LogError($"Couldn't parse int value {impulse}");
			return;
		}

		switch (impulseValue)
		{
			// Play a random sentence
			case 1:
				HEVMod.Instance.VoiceController?.PlaySentenceById(HEVMod.Instance.SentenceParser?.allSentences.PickRandom().Identifier);
				break;

			// TODO: super neato stuff will go here
			case 101:
				//GamePlayerOwner.MyPlayer.ActiveHealthController.Heal(EBodyPart.Common, 440f);
				//GamePlayerOwner.MyPlayer.ActiveHealthController.TryDoExternalBuff("BuffseTGchange");
				var itemFactory = Singleton<ItemFactoryClass>.Instance;
				// FIXME: Exception: Trying to get parent of an item that doesn't have a parent: Morphine
				// at ActiveHealthController.MedEffect.Residue()
				var stim = itemFactory.GetPresetItem("544fb3f34bdc2d03748b456a");
				var fakeStash = itemFactory.CreateFakeStash();
				fakeStash.Slots[0].Add(stim, false);
				GamePlayerOwner.MyPlayer.ActiveHealthController.DoMedEffect(stim, EBodyPart.Head);
				ConsoleScreen.Log("Cheater!");
				break;
		}
	}
}
