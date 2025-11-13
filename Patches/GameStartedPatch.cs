using EFT;
using HarmonyLib;
using SPT.Reflection.Patching;
using System.Reflection;

namespace HEVSuitMod.Patches;

/// <summary>
/// Instantiates all mod components when a new game starts
/// </summary>
internal class GameStartedPatch : ModulePatch
{
    protected override MethodBase GetTargetMethod()
    {
        return AccessTools.Method(typeof(GameWorld), nameof(GameWorld.OnGameStarted));
    }

    [PatchPostfix]
    private static void GameStarted()
    {
        HEVSuitMod.Instance.OnGameStarted();
    }
}