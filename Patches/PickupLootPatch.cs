using EFT.InventoryLogic;
using HarmonyLib;
using SPT.Reflection.Patching;
using System;
using System.Reflection;

namespace HEVSuitMod.Patches;

/// <summary>
/// Fire an event when loot is picked up by the player
/// </summary>
internal class PickupLootPatch : ModulePatch
{
	public static event Action<Item> PickupLootEvent;

	protected override MethodBase GetTargetMethod()
	{
		return AccessTools.Method(typeof(GetActionsClass), nameof(GetActionsClass.smethod_10));
	}

	[PatchPostfix]
	private static void Postfix(ref Item rootItem)
	{
		if (rootItem != null)
			PickupLootEvent?.Invoke(rootItem);
	}
}
