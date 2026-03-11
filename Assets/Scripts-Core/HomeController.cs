using System;
using System.Collections.Generic;
using Firebase.Firestore;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class HomeController : MonoBehaviour
{
    private const int MaxRunsPerDay = 2;

    [Header("Optional UI Text Fields")]
    public TMP_Text classText;
    public TMP_Text levelText;
    public TMP_Text xpText;
    public TMP_Text nextLevelText;
    public TMP_Text skillPointsText;
    public TMP_Text shardsText;
    public TMP_Text runsText;
    public TMP_Text playStateText;
    public Button playDungeonButton;

    [Header("Behavior")]
    public bool enforceDailyLivesOnHome = true;

    public int CurrentLevel { get; private set; }
    public int CurrentXp { get; private set; }
    public int CurrentSkillPoints { get; private set; }
    public int CurrentShards { get; private set; }
    public int CurrentRunsUsed { get; private set; }
    public int CurrentRunsRemaining { get; private set; }
    public string CurrentClass { get; private set; }

    private void Start()
    {
        Refresh();
    }

    private void OnEnable()
    {
        Refresh();
    }

    public async void Refresh()
    {
        SetHomeState("Loading profile...");

        if (BootstrapController.Db == null || BootstrapController.User == null)
        {
            Debug.LogError("HomeController: Firebase not ready.");
            SetHomeState("Home unavailable. Firebase not ready.");
            return;
        }

        try
        {
            var uid = BootstrapController.User.UserId;
            var doc = BootstrapController.Db.Collection("users").Document(uid);
            var snap = await doc.GetSnapshotAsync();

            if (!snap.Exists)
            {
                Debug.LogWarning("HomeController: user doc missing.");
                SetHomeState("Profile missing. Restart from onboarding.");
                return;
            }

            var data = snap.ToDictionary();
            CurrentClass = ReadString(data, "class", "warrior");
            CurrentLevel = ReadInt(data, "level", 1);
            CurrentXp = ReadInt(data, "xp", 0);
            CurrentSkillPoints = ReadInt(data, "skillPoints", 0);
            CurrentShards = ReadInt(data, "shards", 0);

            var dayKey = DateTime.Now.ToString("yyyy_MM_dd");
            var dailyRef = BootstrapController.Db.Collection("users").Document(uid).Collection("daily_state").Document(dayKey);
            var dailySnap = await dailyRef.GetSnapshotAsync();
            var dailyData = dailySnap.Exists ? dailySnap.ToDictionary() : new Dictionary<string, object>();
            CurrentRunsUsed = ReadInt(dailyData, "runsUsed", 0);
            CurrentRunsRemaining = Mathf.Max(0, MaxRunsPerDay - CurrentRunsUsed);

            BindUi();
        }
        catch (Exception ex)
        {
            Debug.LogError("HomeController.Refresh failed: " + ex);
            SetHomeState("Failed to load home data.");
        }
    }

    public void GoToTaskScene()
    {
        SceneManager.LoadScene("TaskScene");
    }

    public void GoToDungeonScene()
    {
        if (enforceDailyLivesOnHome && CurrentRunsRemaining <= 0)
        {
            SetHomeState("Dungeon: No lives remaining today");
            return;
        }

        SceneManager.LoadScene("DungeonScene");
    }

    public void GoToUpgradeScene()
    {
        SceneManager.LoadScene("UpgradeScene");
    }

    public bool CanStartDungeonFromHome()
    {
        return !enforceDailyLivesOnHome || CurrentRunsRemaining > 0;
    }

    private void BindUi()
    {
        if (classText != null) classText.text = "Class: " + CurrentClass;
        if (levelText != null) levelText.text = "Level: " + CurrentLevel;
        if (xpText != null)
        {
            xpText.text = CurrentLevel >= XpProgressionService.LevelCap
                ? "XP: MAX"
                : "XP: " + CurrentXp + " / " + XpProgressionService.GetXpRequired(CurrentLevel);
        }

        if (nextLevelText != null)
        {
            nextLevelText.text = CurrentLevel >= XpProgressionService.LevelCap
                ? "Next Level: MAX"
                : "Next Level XP Needed: " + Mathf.Max(0, XpProgressionService.GetXpRequired(CurrentLevel) - CurrentXp);
        }

        if (skillPointsText != null) skillPointsText.text = "Skill Points: " + CurrentSkillPoints;
        if (shardsText != null) shardsText.text = "Shards: " + CurrentShards;
        if (runsText != null) runsText.text = "Runs: " + CurrentRunsUsed + " / " + MaxRunsPerDay + " used";
        var canPlay = !enforceDailyLivesOnHome || CurrentRunsRemaining > 0;
        if (playStateText != null) playStateText.text = canPlay ? "Dungeon: Ready" : "Dungeon: No lives remaining today";
        if (playDungeonButton != null) playDungeonButton.interactable = canPlay;
    }

    private void SetHomeState(string status)
    {
        if (playStateText != null)
        {
            playStateText.text = status;
        }

        if (playDungeonButton != null)
        {
            playDungeonButton.interactable = false;
        }
    }

    private static int ReadInt(Dictionary<string, object> data, string key, int fallback)
    {
        if (!data.TryGetValue(key, out var raw) || raw == null)
        {
            return fallback;
        }

        if (raw is long longVal) return (int)longVal;
        if (raw is int intVal) return intVal;
        if (raw is double doubleVal) return Mathf.RoundToInt((float)doubleVal);

        return fallback;
    }

    private static string ReadString(Dictionary<string, object> data, string key, string fallback)
    {
        if (!data.TryGetValue(key, out var raw) || raw == null)
        {
            return fallback;
        }

        return raw.ToString();
    }
}
