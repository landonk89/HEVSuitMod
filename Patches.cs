using System.Reflection;
using EFT;
using HarmonyLib;
using SPT.Reflection.Patching;

namespace HEVSuitMod
{
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
			HEVMod.Instance.WeaponInspectEvent();
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
			HEVMod.Instance.ChamberInspectEvent();
		}
	}
}
