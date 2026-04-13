using UnityEngine;

/// <summary>인벤·장비·색모듈 간 드래그 상태 (한 번에 하나)</summary>
public static class InventoryDragSession
{
    public enum Kind { None, InventorySlot, EquipmentSlot, ColorModuleSlot, ColorModuleList }

    public static Kind ActiveKind;
    public static int InventorySlotIndex = -1;
    public static int EquipmentSlotIndex = -1;
    public static int ColorModuleSlotIndex = -1;
    public static ColorModuleInstance ColorModuleDragInstance;

    public static GameObject DragGhost;

    public static void BeginInventory(int slotIndex)
    {
        Clear();
        ActiveKind = Kind.InventorySlot;
        InventorySlotIndex = slotIndex;
    }

    public static void BeginEquipment(int eqSlot)
    {
        Clear();
        ActiveKind = Kind.EquipmentSlot;
        EquipmentSlotIndex = eqSlot;
    }

    public static void BeginColorModuleSlot(int modSlot)
    {
        Clear();
        ActiveKind = Kind.ColorModuleSlot;
        ColorModuleSlotIndex = modSlot;
    }

    public static void BeginColorModuleList(ColorModuleInstance module)
    {
        Clear();
        ActiveKind = Kind.ColorModuleList;
        ColorModuleDragInstance = module;
    }

    public static void Clear()
    {
        ActiveKind = Kind.None;
        InventorySlotIndex = -1;
        EquipmentSlotIndex = -1;
        ColorModuleSlotIndex = -1;
        ColorModuleDragInstance = null;
        if (DragGhost != null)
        {
            Object.Destroy(DragGhost);
            DragGhost = null;
        }
    }
}
