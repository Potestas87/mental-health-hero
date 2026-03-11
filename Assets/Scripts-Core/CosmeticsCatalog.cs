using System;
using System.Collections.Generic;
using UnityEngine;

public enum CosmeticSlot
{
    Tint,
    Weapon,
    Armor
}

public sealed class CosmeticDefinition
{
    public string Id { get; }
    public string Name { get; }
    public CosmeticSlot Slot { get; }
    public int ShardCost { get; }
    public string Rarity { get; }
    public string TintHex { get; }

    public CosmeticDefinition(string id, string name, CosmeticSlot slot, int shardCost, string rarity, string tintHex = null)
    {
        Id = id;
        Name = name;
        Slot = slot;
        ShardCost = shardCost;
        Rarity = rarity;
        TintHex = tintHex;
    }
}

public static class CosmeticsCatalog
{
    private static readonly List<CosmeticDefinition> All = new List<CosmeticDefinition>
    {
        // Tint (6)
        new CosmeticDefinition("tint_crimson", "Crimson Tint", CosmeticSlot.Tint, 10, "common", "#D64A4A"),
        new CosmeticDefinition("tint_ocean", "Ocean Tint", CosmeticSlot.Tint, 10, "common", "#3A8EDB"),
        new CosmeticDefinition("tint_forest", "Forest Tint", CosmeticSlot.Tint, 10, "common", "#3B9D5A"),
        new CosmeticDefinition("tint_gold", "Gold Tint", CosmeticSlot.Tint, 10, "common", "#D8B33F"),
        new CosmeticDefinition("tint_ember", "Ember Tint", CosmeticSlot.Tint, 10, "common", "#E07A2D"),
        new CosmeticDefinition("tint_storm", "Storm Tint", CosmeticSlot.Tint, 10, "common", "#6B75A8"),

        // Weapon skins (5)
        new CosmeticDefinition("wpn_ironfang", "Ironfang", CosmeticSlot.Weapon, 20, "rare"),
        new CosmeticDefinition("wpn_moonedge", "Moonedge", CosmeticSlot.Weapon, 20, "rare"),
        new CosmeticDefinition("wpn_sunflare", "Sunflare", CosmeticSlot.Weapon, 20, "rare"),
        new CosmeticDefinition("wpn_nightbite", "Nightbite", CosmeticSlot.Weapon, 20, "rare"),
        new CosmeticDefinition("wpn_riftcarve", "Riftcarve", CosmeticSlot.Weapon, 20, "rare"),

        // Armor sets (4)
        new CosmeticDefinition("arm_guardian", "Guardian Set", CosmeticSlot.Armor, 40, "epic"),
        new CosmeticDefinition("arm_pathfinder", "Pathfinder Set", CosmeticSlot.Armor, 40, "epic"),
        new CosmeticDefinition("arm_arcanist", "Arcanist Set", CosmeticSlot.Armor, 40, "epic"),
        new CosmeticDefinition("arm_dreadknight", "Dreadknight Set", CosmeticSlot.Armor, 40, "epic"),
    };

    private static readonly Dictionary<string, CosmeticDefinition> ById = BuildById();

    public static IReadOnlyList<CosmeticDefinition> GetAll() => All;

    public static bool TryGet(string id, out CosmeticDefinition cosmetic)
    {
        cosmetic = null;
        if (string.IsNullOrWhiteSpace(id))
        {
            return false;
        }

        return ById.TryGetValue(id.Trim().ToLowerInvariant(), out cosmetic);
    }

    public static Color GetTintColor(string tintId)
    {
        if (!TryGet(tintId, out var def) || def.Slot != CosmeticSlot.Tint || string.IsNullOrWhiteSpace(def.TintHex))
        {
            return Color.white;
        }

        if (ColorUtility.TryParseHtmlString(def.TintHex, out var color))
        {
            return color;
        }

        return Color.white;
    }

    private static Dictionary<string, CosmeticDefinition> BuildById()
    {
        var map = new Dictionary<string, CosmeticDefinition>(StringComparer.OrdinalIgnoreCase);
        for (var i = 0; i < All.Count; i++)
        {
            map[All[i].Id] = All[i];
        }

        return map;
    }
}
