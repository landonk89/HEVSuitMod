using EFT;
using HarmonyLib;
using SPT.Reflection.Patching;
using System;
using System.Reflection;

namespace HEVSuitMod.Patches;

// FIXME: This isn't working as expected, revisit
internal class LoadSingleAmmoPatch : ModulePatch
{
	public static event Action SingleLoadAmmoEvent;

	protected override MethodBase GetTargetMethod()
	{
		return AccessTools.Method(typeof(Player.FirearmController), nameof(Player.FirearmController.method_38));
	}

	[PatchPostfix]
	private static void PostFix()
	{
		SingleLoadAmmoEvent?.Invoke();
	}
}
