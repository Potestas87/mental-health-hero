using UnityEngine;

public static class XpProgressionService
{
    public const int LevelCap = 20;

    public struct XpApplyResult
    {
        public int Level;
        public int Xp;
        public int SkillPoints;
        public int LevelsGained;
    }

    public static int GetXpRequired(int level)
    {
        if (level < 1)
        {
            level = 1;
        }

        return 40 + (level - 1) * 18;
    }

    public static XpApplyResult ApplyXp(int currentLevel, int currentXp, int currentSkillPoints, int xpToAdd)
    {
        var result = new XpApplyResult
        {
            Level = Mathf.Clamp(currentLevel, 1, LevelCap),
            Xp = Mathf.Max(0, currentXp),
            SkillPoints = Mathf.Max(0, currentSkillPoints),
            LevelsGained = 0
        };

        if (xpToAdd <= 0)
        {
            return result;
        }

        if (result.Level >= LevelCap)
        {
            result.Xp = 0;
            return result;
        }

        result.Xp += xpToAdd;

        while (result.Level < LevelCap)
        {
            var required = GetXpRequired(result.Level);
            if (result.Xp < required)
            {
                break;
            }

            result.Xp -= required;
            result.Level += 1;
            result.SkillPoints += 1;
            result.LevelsGained += 1;
        }

        if (result.Level >= LevelCap)
        {
            result.Level = LevelCap;
            result.Xp = 0;
        }

        return result;
    }
}
