using HarmonyLib;
using SPT.Reflection.Patching;
using System.Reflection;

namespace HEVSuitMod.Patches;

// FIXME: This isn't working as expected, revisit
// NOTENOTE: Need to redo this entirely and use an event like the other patches do since HudController changed so much
#if FALSE
internal class OnLoadSingleAmmo : ModulePatch
{
	protected override MethodBase GetTargetMethod()
	{
		return AccessTools.Method(typeof(Player.FirearmController), nameof(Player.FirearmController.method_38));
	}

	[PatchPostfix]
	private static void UpdateHudAmmoCount()
	{
		HEVMod.Instance.HudController?.AmmoChanged(GamePlayerOwner.MyPlayer.HandsController);
	}
}
#endif
