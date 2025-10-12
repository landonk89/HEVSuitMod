using System.Reflection;
using BepInEx.Logging;
using EFT;
using HarmonyLib;
using SPT.Reflection.Patching;

namespace HEVSuitMod
{
	internal class OnNewGame : ModulePatch
	{
		private static ManualLogSource log = BepInEx.Logging.Logger.CreateLogSource("HEVSuitMod.OnNewGame");

		protected override MethodBase GetTargetMethod()
		{
			return AccessTools.Method(typeof(GameWorld), nameof(GameWorld.OnGameStarted));
		}

		[PatchPostfix]
		private static void GameStarted()
		{
			log.LogInfo("OnGameStarted");
			HEVMod.Instance.OnGameStarted();
		}
	}

	internal class OnGameEnded : ModulePatch
	{
		private static ManualLogSource log = BepInEx.Logging.Logger.CreateLogSource("HEVSuitMod.OnGameEnded");

		protected override MethodBase GetTargetMethod()
		{
			return AccessTools.Method(typeof(Player), nameof(Player.OnGameSessionEnd));
		}

		[PatchPostfix]
		private static void GameEnded()
		{
			log.LogInfo("OnGameEnded");
			HEVMod.Instance.OnGameEnded();
		}
	}

	internal class OnInspectWeapon : ModulePatch
	{
		private static ManualLogSource log = BepInEx.Logging.Logger.CreateLogSource("HEVSuitMod.OnInspectWeapon");

		protected override MethodBase GetTargetMethod()
		{
			return AccessTools.Method(typeof(Player.FirearmController), nameof(Player.FirearmController.ExamineWeapon));
		}

		[PatchPrefix]
		private static void OnInspect()
		{
			log.LogInfo("OnInspectWeapon");
			HEVMod.Instance.WeaponInspectEvent();
		}
	}

	internal class OnInspectChamber : ModulePatch
	{
		private static ManualLogSource log = BepInEx.Logging.Logger.CreateLogSource("HEVSuitMod.OnInspectWeapon");

		protected override MethodBase GetTargetMethod()
		{
			return AccessTools.Method(typeof(Player.FirearmController), nameof(Player.FirearmController.CheckChamber));
		}

		[PatchPostfix]
		private static void OnInspect()
		{
			log.LogInfo("OnInspectChamber");
			HEVMod.Instance.ChamberInspectEvent();
		}
	}
}
