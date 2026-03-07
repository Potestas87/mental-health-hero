using System;
using System.Collections.Generic;

public sealed class UpgradeNodeDefinition
{
    public string Id { get; }
    public string ClassId { get; }
    public string Name { get; }
    public int CostSkillPoints { get; }
    public string[] RequiresNodeIds { get; }

    public int BonusMaxHp { get; }
    public int BonusAttackDamage { get; }
    public float BonusMoveSpeed { get; }
    public float BonusAttackRange { get; }
    public float AttackCooldownMultiplier { get; }

    public UpgradeNodeDefinition(
        string id,
        string classId,
        string name,
        int costSkillPoints,
        string[] requiresNodeIds,
        int bonusMaxHp,
        int bonusAttackDamage,
        float bonusMoveSpeed,
        float bonusAttackRange,
        float attackCooldownMultiplier)
    {
        Id = id;
        ClassId = classId;
        Name = name;
        CostSkillPoints = costSkillPoints;
        RequiresNodeIds = requiresNodeIds ?? Array.Empty<string>();
        BonusMaxHp = bonusMaxHp;
        BonusAttackDamage = bonusAttackDamage;
        BonusMoveSpeed = bonusMoveSpeed;
        BonusAttackRange = bonusAttackRange;
        AttackCooldownMultiplier = attackCooldownMultiplier <= 0f ? 1f : attackCooldownMultiplier;
    }
}

public static class UpgradeCatalog
{
    private static readonly List<UpgradeNodeDefinition> AllNodes = new List<UpgradeNodeDefinition>
    {
        // Warrior (8)
        new UpgradeNodeDefinition("war_hp_1", "warrior", "Iron Skin I", 1, null, 18, 0, 0f, 0f, 1f),
        new UpgradeNodeDefinition("war_dmg_1", "warrior", "Heavy Swing I", 1, null, 0, 3, 0f, 0f, 1f),
        new UpgradeNodeDefinition("war_hp_2", "warrior", "Iron Skin II", 1, new[] { "war_hp_1" }, 24, 0, 0f, 0f, 1f),
        new UpgradeNodeDefinition("war_dmg_2", "warrior", "Heavy Swing II", 1, new[] { "war_dmg_1" }, 0, 4, 0f, 0f, 1f),
        new UpgradeNodeDefinition("war_speed_1", "warrior", "Marching Plate", 1, new[] { "war_hp_1" }, 0, 0, 0.25f, 0f, 1f),
        new UpgradeNodeDefinition("war_cd_1", "warrior", "Battle Rhythm", 1, new[] { "war_dmg_1" }, 0, 0, 0f, 0f, 0.92f),
        new UpgradeNodeDefinition("war_hp_3", "warrior", "Iron Skin III", 1, new[] { "war_hp_2" }, 28, 0, 0f, 0f, 1f),
        new UpgradeNodeDefinition("war_range_1", "warrior", "Long Reach", 1, new[] { "war_cd_1" }, 0, 0, 0f, 0.15f, 1f),

        // Ranger (8)
        new UpgradeNodeDefinition("rng_speed_1", "ranger", "Fleetstep I", 1, null, 0, 0, 0.35f, 0f, 1f),
        new UpgradeNodeDefinition("rng_dmg_1", "ranger", "Critical Draw I", 1, null, 0, 2, 0f, 0f, 1f),
        new UpgradeNodeDefinition("rng_speed_2", "ranger", "Fleetstep II", 1, new[] { "rng_speed_1" }, 0, 0, 0.35f, 0f, 1f),
        new UpgradeNodeDefinition("rng_cd_1", "ranger", "Rapid Shot", 1, new[] { "rng_dmg_1" }, 0, 0, 0f, 0f, 0.9f),
        new UpgradeNodeDefinition("rng_range_1", "ranger", "Eagle Eye", 1, new[] { "rng_dmg_1" }, 0, 0, 0f, 0.25f, 1f),
        new UpgradeNodeDefinition("rng_hp_1", "ranger", "Leather Guard", 1, new[] { "rng_speed_1" }, 12, 0, 0f, 0f, 1f),
        new UpgradeNodeDefinition("rng_dmg_2", "ranger", "Critical Draw II", 1, new[] { "rng_cd_1" }, 0, 3, 0f, 0f, 1f),
        new UpgradeNodeDefinition("rng_range_2", "ranger", "Eagle Eye II", 1, new[] { "rng_range_1" }, 0, 0, 0f, 0.25f, 1f),

        // Mage (8)
        new UpgradeNodeDefinition("mag_range_1", "mage", "Arc Reach I", 1, null, 0, 0, 0f, 0.35f, 1f),
        new UpgradeNodeDefinition("mag_cd_1", "mage", "Quick Cast I", 1, null, 0, 0, 0f, 0f, 0.88f),
        new UpgradeNodeDefinition("mag_hp_1", "mage", "Arc Ward I", 1, null, 12, 0, 0f, 0f, 1f),
        new UpgradeNodeDefinition("mag_dmg_1", "mage", "Arc Power I", 1, null, 0, 2, 0f, 0f, 1f),
        new UpgradeNodeDefinition("mag_range_2", "mage", "Arc Reach II", 1, new[] { "mag_range_1" }, 0, 0, 0f, 0.35f, 1f),
        new UpgradeNodeDefinition("mag_cd_2", "mage", "Quick Cast II", 1, new[] { "mag_cd_1" }, 0, 0, 0f, 0f, 0.9f),
        new UpgradeNodeDefinition("mag_dmg_2", "mage", "Arc Power II", 1, new[] { "mag_dmg_1" }, 0, 3, 0f, 0f, 1f),
        new UpgradeNodeDefinition("mag_speed_1", "mage", "Blink Footwork", 1, new[] { "mag_hp_1" }, 0, 0, 0.3f, 0f, 1f),
    };

    private static readonly Dictionary<string, UpgradeNodeDefinition> ById = BuildByIdMap();

    public static IReadOnlyList<UpgradeNodeDefinition> GetNodesForClass(string classId)
    {
        var normalized = string.IsNullOrWhiteSpace(classId) ? "warrior" : classId.Trim().ToLowerInvariant();
        var results = new List<UpgradeNodeDefinition>();

        for (var i = 0; i < AllNodes.Count; i++)
        {
            if (AllNodes[i].ClassId == normalized)
            {
                results.Add(AllNodes[i]);
            }
        }

        return results;
    }

    public static bool TryGetNode(string nodeId, out UpgradeNodeDefinition node)
    {
        if (string.IsNullOrWhiteSpace(nodeId))
        {
            node = null;
            return false;
        }

        return ById.TryGetValue(nodeId.Trim().ToLowerInvariant(), out node);
    }

    private static Dictionary<string, UpgradeNodeDefinition> BuildByIdMap()
    {
        var map = new Dictionary<string, UpgradeNodeDefinition>(StringComparer.OrdinalIgnoreCase);
        for (var i = 0; i < AllNodes.Count; i++)
        {
            map[AllNodes[i].Id] = AllNodes[i];
        }

        return map;
    }
}
