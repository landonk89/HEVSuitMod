using EFT;
using EFT.HealthSystem;
using HarmonyLib;
using SPT.Reflection.Patching;
using System.Reflection;

namespace HEVSuitMod.Patches;

internal class PlayerKillPatch : ModulePatch
{
	protected override MethodBase GetTargetMethod()
	{
		return AccessTools.Method(typeof(ActiveHealthController), nameof(ActiveHealthController.Kill));
	}

	[PatchPrefix]
	private static bool Prefix(ref ActiveHealthController __instance)
	{
		if (__instance.Player != GamePlayerOwner.MyPlayer)
			return true;

		// Disallow death until health is actually zero because the suit's job is to keep us alive better
		if (__instance.GetBodyPartHealth(EBodyPart.Common).Current > 0f)
			return false;
		else
			return true;
	}
}
