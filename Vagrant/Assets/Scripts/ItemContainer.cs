using System;
using UnityEngine;

[Serializable]
public struct ItemStack
{
    public string id;      // null / empty when the slot is free
    public int amount;

    public bool IsEmpty { get { return string.IsNullOrEmpty(id) || amount <= 0; } }
    public static readonly ItemStack Empty = default(ItemStack);
}

/// A fixed number of item slots. Shared by storage boxes and the player inventory.
/// Not a MonoBehaviour: it lives on whatever component owns it (StorageBox, PlayerInventory).
public class ItemContainer
{
    readonly ItemStack[] slots;

    /// Raised whenever the contents change (the UI listens and refreshes).
    public event Action Changed;

    public ItemContainer(int slotCount)
    {
        slots = new ItemStack[Mathf.Max(1, slotCount)];
    }

    public int SlotCount { get { return slots.Length; } }

    public ItemStack GetSlot(int index)
    {
        return (index >= 0 && index < slots.Length) ? slots[index] : ItemStack.Empty;
    }

    /// Adds as much as possible, filling partial stacks first. Returns how many were actually added.
    public int Add(string id, int amount)
    {
        if (string.IsNullOrEmpty(id) || amount <= 0) return 0;

        int max = ItemDB.MaxStack(id);
        int added = 0;

        for (int i = 0; i < slots.Length && added < amount; i++)
        {
            if (slots[i].id != id || slots[i].amount >= max) continue;
            int put = Mathf.Min(max - slots[i].amount, amount - added);
            slots[i].amount += put;
            added += put;
        }

        for (int i = 0; i < slots.Length && added < amount; i++)
        {
            if (!slots[i].IsEmpty) continue;
            int put = Mathf.Min(max, amount - added);
            slots[i] = new ItemStack { id = id, amount = put };
            added += put;
        }

        if (added > 0) RaiseChanged();
        return added;
    }

    /// Removes up to `amount` of an item. Returns how many were actually removed.
    public int Remove(string id, int amount)
    {
        if (string.IsNullOrEmpty(id) || amount <= 0) return 0;

        int removed = 0;
        for (int i = slots.Length - 1; i >= 0 && removed < amount; i--)
        {
            if (slots[i].id != id) continue;
            int take = Mathf.Min(slots[i].amount, amount - removed);
            slots[i].amount -= take;
            removed += take;
            if (slots[i].amount <= 0) slots[i] = ItemStack.Empty;
        }

        if (removed > 0) RaiseChanged();
        return removed;
    }

    /// Moves a whole slot's stack to another container (as much as fits). Returns true if anything moved.
    public bool TryMoveSlotTo(int index, ItemContainer other)
    {
        if (other == null || other == this) return false;
        if (index < 0 || index >= slots.Length) return false;

        ItemStack stack = slots[index];
        if (stack.IsEmpty) return false;

        int moved = other.Add(stack.id, stack.amount);
        if (moved <= 0) return false;

        stack.amount -= moved;
        slots[index] = stack.amount > 0 ? stack : ItemStack.Empty;
        RaiseChanged();
        return true;
    }

    /// Moves a slot's stack into a specific slot of another container (or another slot of the
    /// same container): merges into matching stacks, fills empties, swaps with different items.
    /// Returns true if anything changed.
    public bool TryMoveSlotToSlot(int index, ItemContainer other, int targetIndex)
    {
        if (other == null) return false;
        if (index < 0 || index >= slots.Length) return false;
        if (targetIndex < 0 || targetIndex >= other.slots.Length) return false;
        if (other == this && targetIndex == index) return false;

        ItemStack from = slots[index];
        if (from.IsEmpty) return false;

        ItemStack to = other.GetSlot(targetIndex);

        if (to.IsEmpty)
        {
            int put = Mathf.Min(ItemDB.MaxStack(from.id), from.amount);
            other.SetSlot(targetIndex, new ItemStack { id = from.id, amount = put });
            from.amount -= put;
            slots[index] = from.amount > 0 ? from : ItemStack.Empty;
        }
        else if (to.id == from.id)
        {
            int put = Mathf.Min(ItemDB.MaxStack(from.id) - to.amount, from.amount);
            if (put <= 0) return false;
            other.SetSlot(targetIndex, new ItemStack { id = to.id, amount = to.amount + put });
            from.amount -= put;
            slots[index] = from.amount > 0 ? from : ItemStack.Empty;
        }
        else
        {
            // different items: swap the two stacks
            other.SetSlot(targetIndex, from);
            slots[index] = to;
        }

        RaiseChanged();
        return true;
    }

    /// Writes a stack straight into a slot and notifies listeners.
    public void SetSlot(int index, ItemStack stack)
    {
        if (index < 0 || index >= slots.Length) return;
        slots[index] = (stack.amount > 0) ? stack : ItemStack.Empty;
        RaiseChanged();
    }

    public void Clear()
    {
        for (int i = 0; i < slots.Length; i++) slots[i] = ItemStack.Empty;
        RaiseChanged();
    }

    void RaiseChanged()
    {
        if (Changed != null) Changed();
    }
}
