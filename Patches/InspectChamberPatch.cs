using EFT;
using HarmonyLib;
using SPT.Reflection.Patching;
using System.Reflection;

namespace HEVSuitMod.Patches;

/// <summary>
/// Intercepts weapon chamber inspection and plays a voiceline of the ammotype
/// </summary>
internal class InspectChamberPatch : ModulePatch
{
    protected override MethodBase GetTargetMethod()
    {
        return AccessTools.Method(typeof(Player.FirearmController), nameof(Player.FirearmController.CheckChamber));
    }

    [PatchPostfix]
    private static void OnInspect()
    {
        if (HEVMod.Instance.identifyWeapon.Value)
            HEVMod.Instance.VoiceController?.ChamberInspectEvent();
    }
}