using UnityEngine;

/// <summary>드롭 시 인벤·장비·색모듈 간 실제 이동 처리</summary>
public static class InventoryDragResolver
{
    public static bool TryDropOnInventorySlot(int targetSlotIndex)
    {
        var inv = InventoryManager.Instance;
        if (inv == null) return false;

        switch (InventoryDragSession.ActiveKind)
        {
            case InventoryDragSession.Kind.InventorySlot:
                if (InventoryDragSession.InventorySlotIndex < 0) return false;
                inv.SwapSlots(InventoryDragSession.InventorySlotIndex, targetSlotIndex);
                inv.NotifyUiRefresh();
                return true;

            case InventoryDragSession.Kind.EquipmentSlot:
                return MoveEquipmentToInventorySlot(InventoryDragSession.EquipmentSlotIndex, targetSlotIndex);

            case InventoryDragSession.Kind.ColorModuleSlot:
                return MoveColorModuleToInventorySlot(InventoryDragSession.ColorModuleSlotIndex, targetSlotIndex);
        }
        return false;
    }

    public static bool TryDropOnEquipmentSlot(int eqSlotIndex)
    {
        switch (InventoryDragSession.ActiveKind)
        {
            case InventoryDragSession.Kind.InventorySlot:
                return EquipFromInventorySlot(InventoryDragSession.InventorySlotIndex, eqSlotIndex);

            case InventoryDragSession.Kind.EquipmentSlot:
                if (EquipmentManager.Instance == null) return false;
                EquipmentManager.Instance.SwapEquipmentSlots(InventoryDragSession.EquipmentSlotIndex, eqSlotIndex);
                EquipmentUI.Instance?.RefreshAll();
                InventoryManager.Instance?.NotifyUiRefresh();
                return true;
        }
        return false;
    }

    public static bool TryDropOnColorModuleSlot(int modSlotIndex)
    {
        var slots = ColorModuleSlots.Instance;
        if (slots == null) return false;

        switch (InventoryDragSession.ActiveKind)
        {
            case InventoryDragSession.Kind.InventorySlot:
                return EquipColorModuleItemFromInventory(InventoryDragSession.InventorySlotIndex, modSlotIndex);

            case InventoryDragSession.Kind.ColorModuleSlot:
                slots.SwapModuleSlots(InventoryDragSession.ColorModuleSlotIndex, modSlotIndex);
                ColorModuleUI.Instance?.RefreshAll();
                return true;

            case InventoryDragSession.Kind.ColorModuleList:
                if (InventoryDragSession.ColorModuleDragInstance == null) return false;
                slots.Equip(modSlotIndex, InventoryDragSession.ColorModuleDragInstance);
                ColorModuleUI.Instance?.RefreshAll();
                return true;
        }
        return false;
    }

    private static bool EquipFromInventorySlot(int invSlot, int eqSlot)
    {
        var inv = InventoryManager.Instance;
        var eq = EquipmentManager.Instance;
        var db = ItemDatabase.Instance;
        if (inv == null || eq == null || db == null) return false;

        int itemId = inv.GetSlotItemId(invSlot);
        if (itemId == 0) return false;
        var itemData = db.GetItemData(itemId);
        if (itemData == null || !itemData.IsEquipable || itemData.equipmentDefinition == null) return false;

        var newEq = itemData.equipmentDefinition;
        var oldEq = eq.GetSlot(eqSlot);

        if (!inv.RemoveItemFromSlot(invSlot, 1))
            return false;

        if (oldEq != null)
        {
            ItemData oldItem = db.GetItemByEquipment(oldEq);
            if (oldItem != null)
                inv.AddItemToSlot(invSlot, oldItem.itemID, oldItem.itemName, 1);
        }

        eq.SetSlotWithoutUnequip(eqSlot, newEq);
        EquipmentUI.Instance?.RefreshAll();
        inv.NotifyUiRefresh();
        return true;
    }

    private static bool MoveEquipmentToInventorySlot(int eqSlot, int invSlot)
    {
        var inv = InventoryManager.Instance;
        var eq = EquipmentManager.Instance;
        var db = ItemDatabase.Instance;
        if (inv == null || eq == null || db == null) return false;

        var equipment = eq.GetSlot(eqSlot);
        if (equipment == null) return false;

        ItemData itemData = db.GetItemByEquipment(equipment);
        if (itemData == null) return false;

        int targetId = inv.GetSlotItemId(invSlot);
        if (targetId != 0)
        {
            var targetItemData = db.GetItemData(targetId);
            if (targetItemData == null || !targetItemData.IsEquipable || targetItemData.equipmentDefinition == null)
                return false;

            if (!inv.RemoveItemFromSlot(invSlot, 1)) return false;

            eq.SetSlotWithoutUnequip(eqSlot, targetItemData.equipmentDefinition);
            inv.AddItemToSlot(invSlot, itemData.itemID, itemData.itemName, 1);
        }
        else
        {
            eq.SetSlotWithoutUnequip(eqSlot, null);
            inv.AddItemToSlot(invSlot, itemData.itemID, itemData.itemName, 1);
        }

        EquipmentUI.Instance?.RefreshAll();
        inv.NotifyUiRefresh();
        return true;
    }

    private static bool EquipColorModuleItemFromInventory(int invSlot, int modSlot)
    {
        var inv = InventoryManager.Instance;
        var slots = ColorModuleSlots.Instance;
        var db = ItemDatabase.Instance;
        if (inv == null || slots == null || db == null) return false;

        int itemId = inv.GetSlotItemId(invSlot);
        if (itemId == 0) return false;
        var itemData = db.GetItemData(itemId);
        if (itemData == null || !itemData.IsColorModule || itemData.colorModuleDefinition == null) return false;

        var newInst = new ColorModuleInstance(itemData.colorModuleDefinition);
        var old = slots.GetSlot(modSlot);

        if (!inv.RemoveItemFromSlot(invSlot, 1))
            return false;

        if (old != null)
            slots.Unequip(modSlot);

        slots.Equip(modSlot, newInst);

        if (old != null)
        {
            ItemData oldItem = db.GetItemByColorModule(old.definition);
            if (oldItem != null)
                inv.AddItemToSlot(invSlot, oldItem.itemID, oldItem.itemName, 1);
        }
        ColorModuleUI.Instance?.RefreshAll();
        inv.NotifyUiRefresh();
        return true;
    }

    private static bool MoveColorModuleToInventorySlot(int modSlot, int invSlot)
    {
        var inv = InventoryManager.Instance;
        var slots = ColorModuleSlots.Instance;
        var db = ItemDatabase.Instance;
        if (inv == null || slots == null || db == null) return false;

        var module = slots.GetSlot(modSlot);
        if (module == null) return false;

        ItemData itemData = db.GetItemByColorModule(module.definition);
        if (itemData == null) return false;

        int targetId = inv.GetSlotItemId(invSlot);
        if (targetId != 0)
        {
            var targetData = db.GetItemData(targetId);
            if (targetData == null || !targetData.IsColorModule || targetData.colorModuleDefinition == null)
                return false;

            if (!inv.RemoveItemFromSlot(invSlot, 1)) return false;

            var oldMod = slots.GetSlot(modSlot);
            ItemData oldItemData = oldMod != null ? db.GetItemByColorModule(oldMod.definition) : null;

            slots.Unequip(modSlot);
            slots.Equip(modSlot, new ColorModuleInstance(targetData.colorModuleDefinition));

            if (oldItemData != null)
                inv.AddItemToSlot(invSlot, oldItemData.itemID, oldItemData.itemName, 1);
        }
        else
        {
            slots.Unequip(modSlot);
            inv.AddItemToSlot(invSlot, itemData.itemID, itemData.itemName, 1);
        }

        ColorModuleUI.Instance?.RefreshAll();
        inv.NotifyUiRefresh();
        return true;
    }
}
