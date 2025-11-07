using System;
using System.Reflection;
using EFT;
using EFT.InputSystem;
using HarmonyLib;
using SPT.Reflection.Patching;
using ESlot = HEVSuitMod.HudWeaponSelection.ESlot;

// ===================================================================================
// These are patches for things that don't have events, or do and I haven't found them.
// ===================================================================================

namespace HEVSuitMod;

/// <summary>
/// Instantiates all mod components when a new game starts
/// </summary>
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

/// <summary>
/// Destroy all mod components when the game ends
/// </summary>
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

/// <summary>
/// Intercepts weapon inspection and plays a voiceline of the weapon name
/// </summary>
internal class OnInspectWeapon : ModulePatch
{
	protected override MethodBase GetTargetMethod()
	{
		return AccessTools.Method(typeof(Player.FirearmController), nameof(Player.FirearmController.ExamineWeapon));
	}

	[PatchPrefix]
	private static void OnInspect()
	{
		if (HEVMod.Instance.identifyAmmo.Value)
			HEVMod.Instance.VoiceController?.WeaponInspectEvent();
	}
}

/// <summary>
/// Intercepts weapon chamber inspection and plays a voiceline of the ammotype
/// </summary>
internal class OnInspectChamber : ModulePatch
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

/// <summary>
/// Intercepts weapon selection commands and raises an event when a weapon slot is selected.
/// </summary>
internal class SelectWeaponPatch : ModulePatch
{
	public static event Action<ESlot> SelectionEvent;

	protected override MethodBase GetTargetMethod()
	{
		return AccessTools.Method(typeof(Class1725), nameof(Class1725.TranslateCommand));
    }

	[PatchPostfix]
	private static void Postfix(ref ECommand command)
	{
		ESlot slot = command switch
		{
			ECommand.SelectFirstPrimaryWeapon => ESlot.Primary,
			ECommand.SelectSecondPrimaryWeapon => ESlot.Secondary,
			ECommand.SelectSecondaryWeapon => ESlot.Holster,
			ECommand.QuickSelectSecondaryWeapon => ESlot.Holster,
			ECommand.SelectKnife => ESlot.Scabbard,
            _ => ESlot.None
		};

		if (slot != ESlot.None)
			SelectionEvent?.Invoke(slot);
    }
}

// FIXME: This isn't working as expected, revisit
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
