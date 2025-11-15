using EFT.InputSystem;
using HarmonyLib;
using SPT.Reflection.Patching;
using System;
using System.Reflection;
using EItemSlot = HEVSuitMod.Components.HudWeaponSelection.EItemSlot;

namespace HEVSuitMod.Patches;

/// <summary>
/// A patch that raises an event when a weapon slot is selected.
/// </summary>
internal class SelectWeaponPatch : ModulePatch
{
	public static event Action<EItemSlot> SelectionEvent;

	protected override MethodBase GetTargetMethod()
	{
		return AccessTools.Method(typeof(Class1725), nameof(Class1725.TranslateCommand));
	}

	[PatchPostfix]
	private static void Postfix(ref ECommand command)
	{
		EItemSlot slot = command switch
		{
			ECommand.SelectFirstPrimaryWeapon => EItemSlot.Primary,
			ECommand.SelectSecondPrimaryWeapon => EItemSlot.Secondary,
			ECommand.SelectSecondaryWeapon => EItemSlot.Holster,
			ECommand.QuickSelectSecondaryWeapon => EItemSlot.Holster,
			ECommand.SelectKnife => EItemSlot.Scabbard,
			_ => EItemSlot.None
		};

		if (slot != EItemSlot.None)
			SelectionEvent?.Invoke(slot);
	}
}
