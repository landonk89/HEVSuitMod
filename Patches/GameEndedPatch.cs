using EFT;
using HarmonyLib;
using SPT.Reflection.Patching;
using System.Reflection;

namespace HEVSuitMod.Patches;

/// <summary>
/// Destroy all mod components when the game ends
/// </summary>
internal class GameEndedPatch : ModulePatch
{
	protected override MethodBase GetTargetMethod()
	{
		return AccessTools.Method(typeof(Player), nameof(Player.OnGameSessionEnd));
	}

	[PatchPostfix]
	private static void GameEnded()
	{
		HEVSuitMod.Instance.OnGameEnded();
	}
}