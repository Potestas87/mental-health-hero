using System;
using Firebase.Analytics;
using UnityEngine;

public static class AnalyticsService
{
    public static void LogDungeonRunStart(string classId)
    {
        try
        {
            FirebaseAnalytics.LogEvent(
                "dungeon_run_start",
                new Parameter("class", SanitizeClass(classId))
            );
        }
        catch (Exception ex)
        {
            Debug.LogWarning("Analytics start log failed: " + ex.Message);
        }
    }

    public static void LogDungeonRunEnd(string classId, int floorsCleared, string result, int xpAwarded, int shardsAwarded, int runDurationSec)
    {
        try
        {
            FirebaseAnalytics.LogEvent(
                "dungeon_run_end",
                new Parameter("class", SanitizeClass(classId)),
                new Parameter("floors_cleared", floorsCleared),
                new Parameter("result", result ?? "unknown"),
                new Parameter("xp_awarded", xpAwarded),
                new Parameter("shards_awarded", shardsAwarded),
                new Parameter("run_duration_sec", Mathf.Max(0, runDurationSec))
            );

            if (string.Equals(result, "win", StringComparison.OrdinalIgnoreCase))
            {
                LogBossDefeated(classId, floorsCleared, runDurationSec);
            }

            if (shardsAwarded > 0)
            {
                LogShardsEarned(classId, shardsAwarded, result);
            }
        }
        catch (Exception ex)
        {
            Debug.LogWarning("Analytics end log failed: " + ex.Message);
        }
    }

    private static void LogBossDefeated(string classId, int floorsCleared, int runDurationSec)
    {
        FirebaseAnalytics.LogEvent(
            "boss_defeated",
            new Parameter("class", SanitizeClass(classId)),
            new Parameter("floors_cleared", floorsCleared),
            new Parameter("run_duration_sec", Mathf.Max(0, runDurationSec))
        );
    }

    private static void LogShardsEarned(string classId, int shardsEarned, string sourceResult)
    {
        FirebaseAnalytics.LogEvent(
            "shards_earned",
            new Parameter("class", SanitizeClass(classId)),
            new Parameter("amount", Mathf.Max(0, shardsEarned)),
            new Parameter("source_result", sourceResult ?? "unknown")
        );
    }

    private static string SanitizeClass(string classId)
    {
        return string.IsNullOrWhiteSpace(classId) ? "unknown" : classId.Trim().ToLowerInvariant();
    }
}
