using System.Collections.Generic;
using UnityEngine;

/// Definition of an item type. Add new items by registering them in ItemDB.
public class ItemDef
{
    public readonly string id;
    public readonly string displayName;
    public readonly int maxStack;
    public readonly Color color;   // placeholder icon colour until real icons exist

    public ItemDef(string id, string displayName, int maxStack, Color color)
    {
        this.id = id;
        this.displayName = displayName;
        this.maxStack = Mathf.Max(1, maxStack);
        this.color = color;
    }
}

/// Static item catalog shared by the player inventory and storage boxes.
public static class ItemDB
{
    static readonly Dictionary<string, ItemDef> byId = new Dictionary<string, ItemDef>();

    static ItemDB()
    {
        Register(new ItemDef("wood", "Wood", 1000, new Color(0.55f, 0.36f, 0.18f)));
        Register(new ItemDef("stone", "Stone", 1000, new Color(0.55f, 0.55f, 0.58f)));
        Register(new ItemDef("metal_fragments", "Metal Fragments", 1000, new Color(0.62f, 0.66f, 0.72f)));
        Register(new ItemDef("scrap", "Scrap", 1000, new Color(0.45f, 0.52f, 0.30f)));
        Register(new ItemDef("cloth", "Cloth", 1000, new Color(0.78f, 0.72f, 0.58f)));
        Register(new ItemDef("low_grade_fuel", "Low Grade Fuel", 1000, new Color(0.85f, 0.55f, 0.15f)));
        Register(new ItemDef("sulfur", "Sulfur", 1000, new Color(0.85f, 0.75f, 0.20f)));
        Register(new ItemDef("charcoal", "Charcoal", 1000, new Color(0.25f, 0.22f, 0.20f)));        Register(new ItemDef("bullet", "Bullet", 1000, new Color(0.82f, 0.65f, 0.25f)));
    }

    static void Register(ItemDef def) { byId[def.id] = def; }

    public static ItemDef Get(string id)
    {
        if (string.IsNullOrEmpty(id)) return null;
        return byId.TryGetValue(id, out ItemDef def) ? def : null;
    }

    public static int MaxStack(string id)
    {
        ItemDef def = Get(id);
        return def != null ? def.maxStack : 1;
    }
}
