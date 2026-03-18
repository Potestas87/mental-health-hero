using System;
using System.Collections.Generic;
using UnityEngine;

public enum CosmeticCategory
{
    Tint,
    Weapon,
    Armor,
    AttackFx,
    CompanionPet,
    DungeonMusic
}

public sealed class CosmeticDefinition
{
    public string Id { get; }
    public string Name { get; }
    public CosmeticCategory Category { get; }
    public int ShardCost { get; }
    public string Rarity { get; }
    public string TintHex { get; }
    public bool IsEnabledInMvp { get; }

    public CosmeticDefinition(
        string id,
        string name,
        CosmeticCategory category,
        int shardCost,
        string rarity,
        string tintHex = null,
        bool isEnabledInMvp = false)
    {
        Id = id;
        Name = name;
        Category = category;
        ShardCost = shardCost;
        Rarity = rarity;
        TintHex = tintHex;
        IsEnabledInMvp = isEnabledInMvp;
    }
}

public static class CosmeticsCatalog
{
    public const string TintCategoryKey = "tint";

    private static readonly List<CosmeticDefinition> All = new List<CosmeticDefinition>
    {
        // Tint (active in MVP)
        new CosmeticDefinition("tint_default", "Default (No Tint)", CosmeticCategory.Tint, 0, "common", "#FFFFFF", true),
        new CosmeticDefinition("tint_crimson", "Crimson Tint", CosmeticCategory.Tint, 10, "common", "#D64A4A", true),
        new CosmeticDefinition("tint_ocean", "Ocean Tint", CosmeticCategory.Tint, 10, "common", "#3A8EDB", true),
        new CosmeticDefinition("tint_forest", "Forest Tint", CosmeticCategory.Tint, 10, "common", "#3B9D5A", true),
        new CosmeticDefinition("tint_gold", "Gold Tint", CosmeticCategory.Tint, 10, "common", "#D8B33F", true),
        new CosmeticDefinition("tint_ember", "Ember Tint", CosmeticCategory.Tint, 10, "common", "#E07A2D", true),
        new CosmeticDefinition("tint_storm", "Storm Tint", CosmeticCategory.Tint, 10, "common", "#6B75A8", true),

        // Future categories (disabled in MVP for now)
        new CosmeticDefinition("wpn_ironfang", "Ironfang", CosmeticCategory.Weapon, 20, "rare"),
        new CosmeticDefinition("wpn_moonedge", "Moonedge", CosmeticCategory.Weapon, 20, "rare"),
        new CosmeticDefinition("wpn_sunflare", "Sunflare", CosmeticCategory.Weapon, 20, "rare"),
        new CosmeticDefinition("arm_guardian", "Guardian Set", CosmeticCategory.Armor, 40, "epic"),
        new CosmeticDefinition("fx_arcburst", "Arc Burst FX", CosmeticCategory.AttackFx, 25, "rare"),
        new CosmeticDefinition("pet_wisp", "Wisp Companion", CosmeticCategory.CompanionPet, 30, "rare"),
        new CosmeticDefinition("music_depths", "Depths Theme", CosmeticCategory.DungeonMusic, 15, "common")
    };

    private static readonly Dictionary<string, CosmeticDefinition> ById = BuildById();
    private static readonly List<CosmeticDefinition> EnabledInMvp = BuildEnabledList();

    public static IReadOnlyList<CosmeticDefinition> GetAll() => All;
    public static IReadOnlyList<CosmeticDefinition> GetEnabledInMvp() => EnabledInMvp;

    public static bool TryGet(string id, out CosmeticDefinition cosmetic)
    {
        cosmetic = null;
        if (string.IsNullOrWhiteSpace(id))
        {
            return false;
        }

        return ById.TryGetValue(id.Trim().ToLowerInvariant(), out cosmetic);
    }

    public static bool IsEnabledInMvp(CosmeticDefinition cosmetic)
    {
        return cosmetic != null && cosmetic.IsEnabledInMvp;
    }

    public static string GetCategoryKey(CosmeticCategory category)
    {
        switch (category)
        {
            case CosmeticCategory.Tint:
                return "tint";
            case CosmeticCategory.Weapon:
                return "weapon";
            case CosmeticCategory.Armor:
                return "armor";
            case CosmeticCategory.AttackFx:
                return "attack_fx";
            case CosmeticCategory.CompanionPet:
                return "companion_pet";
            case CosmeticCategory.DungeonMusic:
                return "dungeon_music";
            default:
                return "unknown";
        }
    }

    public static Color GetTintColor(string tintId)
    {
        if (!TryGet(tintId, out var def) || def.Category != CosmeticCategory.Tint || string.IsNullOrWhiteSpace(def.TintHex))
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

    private static List<CosmeticDefinition> BuildEnabledList()
    {
        var enabled = new List<CosmeticDefinition>();
        for (var i = 0; i < All.Count; i++)
        {
            if (All[i].IsEnabledInMvp)
            {
                enabled.Add(All[i]);
            }
        }

        return enabled;
    }
}
