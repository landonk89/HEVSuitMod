using Comfort.Common;
using EFT;
using EFT.InventoryLogic;

namespace HEVSuitMod.Tools;

public static class ItemUtils
{
	public static bool TryGetEquipment<T>(EquipmentSlot slot, out T equipment) where T : class
	{
		equipment = null;

		if (!Singleton<GameWorld>.Instantiated)
			return false;
		
		Item slotItem = GamePlayerOwner.MyPlayer?.Equipment?.GetSlot(slot)?.ContainedItem;
		if (slotItem is T item)
		{
			equipment = item;
			return true;
		}

		return false;
	}
}
