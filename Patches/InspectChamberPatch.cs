using EFT;
using HarmonyLib;
using SPT.Reflection.Patching;
using System;
using System.Reflection;

namespace HEVSuitMod.Patches;

/// <summary>
/// Intercepts weapon chamber inspection and plays a voiceline of the ammotype
/// </summary>
internal class InspectChamberPatch : ModulePatch
{
	public static event Action ChamberInspectEvent;

	protected override MethodBase GetTargetMethod()
	{
		return AccessTools.Method(typeof(Player.FirearmController), nameof(Player.FirearmController.CheckChamber));
	}

	[PatchPostfix]
	private static void PostFix(Player.FirearmController __instance)
	{
		if (GamePlayerOwner.MyPlayer?.HandsController == __instance)
			ChamberInspectEvent?.Invoke();
	}
}
