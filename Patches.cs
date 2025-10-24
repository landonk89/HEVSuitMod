using System.Reflection;
using EFT;
using HarmonyLib;
using SPT.Reflection.Patching;

// ===================================================================================
// These are patches for things that don't have events, or do and I haven't found them.
// ===================================================================================

namespace HEVSuitMod;

internal class OnNewGame : ModulePatch
{
	protected override MethodBase GetTargetMethod()
	{
		return AccessTools.Method(typeof(GameWorld), nameof(GameWorld.OnGameStarted));
	}

	[PatchPostfix]
	private static void GameStarted()
	{
		HEVMod.Instance.OnGameStarted();
	}
}

internal class OnGameEnded : ModulePatch
{
	protected override MethodBase GetTargetMethod()
	{
		return AccessTools.Method(typeof(Player), nameof(Player.OnGameSessionEnd));
	}

	[PatchPostfix]
	private static void GameEnded()
	{
		HEVMod.Instance.OnGameEnded();
	}
}

internal class OnInspectWeapon : ModulePatch
{
	protected override MethodBase GetTargetMethod()
	{
		return AccessTools.Method(typeof(Player.FirearmController), nameof(Player.FirearmController.ExamineWeapon));
	}

	[PatchPrefix]
	private static void OnInspect()
	{
		VoiceController.Instance.WeaponInspectEvent();
	}
}

internal class OnInspectChamber : ModulePatch
{
	protected override MethodBase GetTargetMethod()
	{
		return AccessTools.Method(typeof(Player.FirearmController), nameof(Player.FirearmController.CheckChamber));
	}

	[PatchPostfix]
	private static void OnInspect()
	{
		VoiceController.Instance.ChamberInspectEvent();
	}
}

// TODO: Test this for guns that can top load (sks, m700, etc)
internal class OnLoadSingleAmmo : ModulePatch
{
	protected override MethodBase GetTargetMethod()
	{
		return AccessTools.Method(typeof(Player.FirearmController), nameof(Player.FirearmController.method_38));
	}

	[PatchPostfix]
	private static void UpdateHudAmmoCount()
	{
		HudController.Instance.AmmoChanged(GamePlayerOwner.MyPlayer.HandsController);
	}
}
