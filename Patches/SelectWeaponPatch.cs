using EFT.InputSystem;
using HarmonyLib;
using SPT.Reflection.Patching;
using System;
using System.Reflection;
using ESlot = HEVSuitMod.Components.HudWeaponSelection.ESlot;

namespace HEVSuitMod.Patches;

/// <summary>
/// A patch that raises an event when a weapon slot is selected.
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
