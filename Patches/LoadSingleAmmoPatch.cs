using EFT;
using HarmonyLib;
using SPT.Reflection.Patching;
using System;
using System.Reflection;

namespace HEVSuitMod.Patches;

internal class LoadSingleAmmoPatch : ModulePatch
{
	public static event Action SingleLoadAmmoEvent;

	protected override MethodBase GetTargetMethod()
	{
		return AccessTools.Method(typeof(Player.FirearmController), nameof(Player.FirearmController.method_34));
	}

	[PatchPostfix]
	private static void PostFix(Player.FirearmController __instance)
	{
		if (GamePlayerOwner.MyPlayer?.HandsController == __instance)
			SingleLoadAmmoEvent?.Invoke();
	}
}
