using System;
using System.Collections.Generic;
using Firebase.Firestore;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;

public class HomeController : MonoBehaviour
{
    [Header("Optional UI Text Fields")]
    public TMP_Text classText;
    public TMP_Text levelText;
    public TMP_Text xpText;
    public TMP_Text skillPointsText;
    public TMP_Text shardsText;

    public int CurrentLevel { get; private set; }
    public int CurrentXp { get; private set; }
    public int CurrentSkillPoints { get; private set; }
    public int CurrentShards { get; private set; }
    public string CurrentClass { get; private set; }

    private void Start()
    {
        Refresh();
    }

    public async void Refresh()
    {
        if (BootstrapController.Db == null || BootstrapController.User == null)
        {
            Debug.LogError("HomeController: Firebase not ready.");
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
                return;
            }

            var data = snap.ToDictionary();
            CurrentClass = ReadString(data, "class", "warrior");
            CurrentLevel = ReadInt(data, "level", 1);
            CurrentXp = ReadInt(data, "xp", 0);
            CurrentSkillPoints = ReadInt(data, "skillPoints", 0);
            CurrentShards = ReadInt(data, "shards", 0);

            BindUi();
        }
        catch (Exception ex)
        {
            Debug.LogError("HomeController.Refresh failed: " + ex);
        }
    }

    public void GoToTaskScene()
    {
        SceneManager.LoadScene("TaskScene");
    }

    private void BindUi()
    {
        if (classText != null) classText.text = "Class: " + CurrentClass;
        if (levelText != null) levelText.text = "Level: " + CurrentLevel;
        if (xpText != null) xpText.text = "XP: " + CurrentXp + " / " + XpProgressionService.GetXpRequired(CurrentLevel);
        if (skillPointsText != null) skillPointsText.text = "Skill Points: " + CurrentSkillPoints;
        if (shardsText != null) shardsText.text = "Shards: " + CurrentShards;
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
