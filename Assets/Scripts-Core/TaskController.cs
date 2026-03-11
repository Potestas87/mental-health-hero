using System;
using System.Collections.Generic;
using Firebase.Firestore;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class TaskController : MonoBehaviour
{
    [Header("Optional Debug Output")]
    public TMP_Text statusText;

    [Header("Optional Controls")]
    public Button seedTasksButton;
    public Button loadTasksButton;
    public Button completeEasyButton;
    public Button completeMediumButton;
    public Button completeHardButton;
    public Button backHomeButton;

    private const string CategoryMind = "mind";
    private const string CategoryBody = "body";
    private const string CategorySocial = "social";
    private const string CategoryRoutine = "routine";

    private const string DifficultyEasy = "easy";
    private const string DifficultyMedium = "medium";
    private const string DifficultyHard = "hard";

    private string _selectedTaskId;
    private string _selectedDifficulty = DifficultyEasy;
    private bool _isBusy;

    private void Start()
    {
        SetStatus("Task screen ready.");
        UpdateControlState();
    }

    public async void SeedStarterTasks()
    {
        if (!IsReady()) return;
        SetBusy(true, "Seeding starter tasks...");

        var uid = BootstrapController.User.UserId;
        var tasks = BootstrapController.Db.Collection("users").Document(uid).Collection("tasks");

        var starter = new List<Dictionary<string, object>>
        {
            BuildTask("5-minute breathing", CategoryMind, DifficultyEasy),
            BuildTask("10-minute walk", CategoryBody, DifficultyMedium),
            BuildTask("Message one friend", CategorySocial, DifficultyEasy),
            BuildTask("Make your bed", CategoryRoutine, DifficultyEasy),
            BuildTask("Journal for 10 minutes", CategoryMind, DifficultyHard)
        };

        try
        {
            foreach (var task in starter)
            {
                await tasks.AddAsync(task);
            }

            SetStatus("Seeded starter tasks.");
        }
        catch (Exception ex)
        {
            Debug.LogError("SeedStarterTasks failed: " + ex);
            SetStatus("Seed tasks failed: " + ex.Message);
        }
        finally
        {
            SetBusy(false);
        }
    }

    public async void LoadActiveTasks()
    {
        if (!IsReady()) return;
        SetBusy(true, "Loading active tasks...");

        var uid = BootstrapController.User.UserId;
        var tasks = BootstrapController.Db.Collection("users").Document(uid).Collection("tasks");

        try
        {
            var query = tasks.WhereEqualTo("active", true);
            var snap = await query.GetSnapshotAsync();

            if (snap.Count == 0)
            {
                SetStatus("No active tasks found.");
                return;
            }

            // For MVP speed, auto-select first active task and let buttons choose difficulty.
            DocumentSnapshot first = null;
            foreach (var doc in snap.Documents)
            {
                first = doc;
                break;
            }

            if (first == null)
            {
                SetStatus("No active tasks found.");
                return;
            }
            _selectedTaskId = first.Id;

            if (first.TryGetValue<string>("difficulty", out var loadedDifficulty))
            {
                _selectedDifficulty = loadedDifficulty;
            }

            SetStatus("Selected task: " + first.GetValue<string>("title") + " (" + _selectedDifficulty + ")");
        }
        catch (Exception ex)
        {
            Debug.LogError("LoadActiveTasks failed: " + ex);
            SetStatus("Load tasks failed: " + ex.Message);
        }
        finally
        {
            SetBusy(false);
        }
    }

    public void CompleteSelectedAsEasy() => CompleteSelectedTask(DifficultyEasy);
    public void CompleteSelectedAsMedium() => CompleteSelectedTask(DifficultyMedium);
    public void CompleteSelectedAsHard() => CompleteSelectedTask(DifficultyHard);

    public async void CompleteSelectedTask(string difficultyOverride = null)
    {
        if (!IsReady()) return;

        if (string.IsNullOrEmpty(_selectedTaskId))
        {
            SetStatus("No task selected. Run LoadActiveTasks first.");
            return;
        }

        var uid = BootstrapController.User.UserId;
        var db = BootstrapController.Db;
        var dayKey = DateTime.UtcNow.ToString("yyyy_MM_dd");
        SetBusy(true, "Completing task...");

        try
        {
            var userRef = db.Collection("users").Document(uid);
            var dailyRef = userRef.Collection("daily_state").Document(dayKey);
            var taskLogsRef = userRef.Collection("task_logs");

            var userSnap = await userRef.GetSnapshotAsync();
            if (!userSnap.Exists)
            {
                SetStatus("User doc missing.");
                return;
            }

            var dailySnap = await dailyRef.GetSnapshotAsync();
            var userData = userSnap.ToDictionary();
            var dailyData = dailySnap.Exists ? dailySnap.ToDictionary() : new Dictionary<string, object>();

            var currentLevel = ReadInt(userData, "level", 1);
            var currentXp = ReadInt(userData, "xp", 0);
            var currentSkillPoints = ReadInt(userData, "skillPoints", 0);

            var tasksCompletedCount = ReadInt(dailyData, "tasksCompletedCount", 0);
            var taskXpGrantedCount = ReadInt(dailyData, "taskXpGrantedCount", 0);
            var firstTaskCompleted = ReadBool(dailyData, "firstTaskCompleted", false);
            var streakCount = ReadInt(userData, "streakCount", 0);

            var difficulty = string.IsNullOrEmpty(difficultyOverride) ? _selectedDifficulty : difficultyOverride;
            var baseXp = DifficultyToBaseXp(difficulty);

            var bonusXp = 0;
            var bonusesApplied = new List<string>();

            // Bonus applies to first 3 completed tasks of the day.
            if (tasksCompletedCount < 3)
            {
                bonusXp += 8;
                bonusesApplied.Add("first_three_tasks_bonus");
            }

            // Streak trigger bonus on first completed task of day.
            if (!firstTaskCompleted)
            {
                bonusXp += 12;
                bonusesApplied.Add("first_task_streak_bonus");
                streakCount += 1;
            }

            var totalXpBeforeCap = baseXp + bonusXp;

            // XP granted only for first 6 completed tasks/day.
            var xpAwarded = taskXpGrantedCount < 6 ? totalXpBeforeCap : 0;
            if (xpAwarded == 0)
            {
                bonusesApplied.Add("daily_xp_cap_reached");
            }

            var xpResult = XpProgressionService.ApplyXp(currentLevel, currentXp, currentSkillPoints, xpAwarded);

            var userUpdates = new Dictionary<string, object>
            {
                { "level", xpResult.Level },
                { "xp", xpResult.Xp },
                { "skillPoints", xpResult.SkillPoints },
                { "streakCount", streakCount },
                { "updatedAt", Timestamp.GetCurrentTimestamp() }
            };

            var dailyUpdates = new Dictionary<string, object>
            {
                { "tasksCompletedCount", tasksCompletedCount + 1 },
                { "taskXpGrantedCount", taskXpGrantedCount + (xpAwarded > 0 ? 1 : 0) },
                { "firstTaskCompleted", true },
                { "firstTaskCompletedAt", firstTaskCompleted ? dailyData.GetValueOrDefault("firstTaskCompletedAt", null) : Timestamp.GetCurrentTimestamp() },
                { "serverDayKey", dayKey },
                { "updatedAt", Timestamp.GetCurrentTimestamp() }
            };

            var logData = new Dictionary<string, object>
            {
                { "taskId", _selectedTaskId },
                { "completedAt", Timestamp.GetCurrentTimestamp() },
                { "difficulty", difficulty },
                { "baseXp", baseXp },
                { "bonusXp", bonusXp },
                { "xpAwarded", xpAwarded },
                { "bonusesApplied", bonusesApplied.ToArray() },
                { "serverDayKey", dayKey }
            };

            await userRef.SetAsync(userUpdates, SetOptions.MergeAll);
            await dailyRef.SetAsync(dailyUpdates, SetOptions.MergeAll);
            await taskLogsRef.AddAsync(logData);

            SetStatus($"Task complete. +{xpAwarded} XP | L{xpResult.Level} XP {xpResult.Xp}/{XpProgressionService.GetXpRequired(xpResult.Level)}");
            Debug.Log($"task_completed difficulty={difficulty} xp_awarded={xpAwarded} levels_gained={xpResult.LevelsGained}");
        }
        catch (Exception ex)
        {
            Debug.LogError("CompleteSelectedTask failed: " + ex);
            SetStatus("Complete task failed: " + ex.Message);
        }
        finally
        {
            SetBusy(false);
        }
    }

    public void ReturnHome()
    {
        SceneManager.LoadScene("HomeScene");
    }

    private bool IsReady()
    {
        if (_isBusy)
        {
            SetStatus("Please wait...");
            return false;
        }

        if (BootstrapController.Db == null || BootstrapController.User == null)
        {
            SetStatus("Firebase not ready.");
            Debug.LogError("TaskController: Firebase not ready.");
            return false;
        }

        return true;
    }

    private static Dictionary<string, object> BuildTask(string title, string category, string difficulty)
    {
        return new Dictionary<string, object>
        {
            { "title", title },
            { "category", category },
            { "difficulty", difficulty },
            { "active", true }
        };
    }

    private static int DifficultyToBaseXp(string difficulty)
    {
        switch (difficulty)
        {
            case DifficultyHard:
                return 35;
            case DifficultyMedium:
                return 22;
            default:
                return 12;
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

    private static bool ReadBool(Dictionary<string, object> data, string key, bool fallback)
    {
        if (!data.TryGetValue(key, out var raw) || raw == null)
        {
            return fallback;
        }

        if (raw is bool boolVal) return boolVal;

        return fallback;
    }

    private void SetStatus(string message)
    {
        if (statusText != null)
        {
            statusText.text = message;
        }

        Debug.Log(message);
    }

    private void SetBusy(bool busy, string status = null)
    {
        _isBusy = busy;
        UpdateControlState();

        if (!string.IsNullOrWhiteSpace(status))
        {
            SetStatus(status);
        }
    }

    private void UpdateControlState()
    {
        var canInteract = !_isBusy;

        if (seedTasksButton != null) seedTasksButton.interactable = canInteract;
        if (loadTasksButton != null) loadTasksButton.interactable = canInteract;
        if (completeEasyButton != null) completeEasyButton.interactable = canInteract;
        if (completeMediumButton != null) completeMediumButton.interactable = canInteract;
        if (completeHardButton != null) completeHardButton.interactable = canInteract;
        if (backHomeButton != null) backHomeButton.interactable = canInteract;
    }
}
