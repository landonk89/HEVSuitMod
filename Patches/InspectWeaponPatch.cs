using EFT;
using HarmonyLib;
using SPT.Reflection.Patching;
using System;
using System.Reflection;

namespace HEVSuitMod.Patches;

/// <summary>
/// Intercepts weapon inspection and plays a voiceline of the weapon name
/// </summary>
internal class InspectWeaponPatch : ModulePatch
{
	public static event Action WeaponInspectEvent;

	protected override MethodBase GetTargetMethod()
	{
		return AccessTools.Method(typeof(Player.FirearmController), nameof(Player.FirearmController.ExamineWeapon));
	}

	[PatchPrefix]
	private static void OnInspect()
	{
		WeaponInspectEvent?.Invoke();
	}
}
