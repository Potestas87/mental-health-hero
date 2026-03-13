using System;
using System.Collections.Generic;
using Firebase.Firestore;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using System.Threading.Tasks;

public class TaskController : MonoBehaviour
{
    [Header("Optional Debug Output")]
    public TMP_Text statusText;

    [Header("Optional Controls")]
    public Button seedTasksButton;
    public Button seedMvpTasksButton;
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
    private string _selectedEasyTaskId;
    private string _selectedMediumTaskId;
    private string _selectedHardTaskId;
    private string _selectedEasyTaskTitle = "Easy";
    private string _selectedMediumTaskTitle = "Medium";
    private string _selectedHardTaskTitle = "Hard";
    private bool _easyCompletedToday;
    private bool _mediumCompletedToday;
    private bool _hardCompletedToday;

    private sealed class TaskTemplate
    {
        public string Title;
        public string Category;
        public string Difficulty;

        public TaskTemplate(string title, string category, string difficulty)
        {
            Title = title;
            Category = category;
            Difficulty = difficulty;
        }
    }

    private void Start()
    {
        SetStatus("Task screen ready. Loading daily tasks...");
        UpdateControlState();
        _ = InitializeDailyTaskStateAsync();
    }

    public async void SeedStarterTasks()
    {
        var starter = new List<TaskTemplate>
        {
            new TaskTemplate("5-minute breathing", CategoryMind, DifficultyEasy),
            new TaskTemplate("10-minute walk", CategoryBody, DifficultyMedium),
            new TaskTemplate("Message one friend", CategorySocial, DifficultyEasy),
            new TaskTemplate("Make your bed", CategoryRoutine, DifficultyEasy),
            new TaskTemplate("Journal for 10 minutes", CategoryMind, DifficultyHard)
        };

        await SeedTemplatesAsync(starter, "starter");
    }

    public async void SeedMvpTaskTemplates()
    {
        var templates = new List<TaskTemplate>
        {
            // Mind (5)
            new TaskTemplate("5-minute breathing", CategoryMind, DifficultyEasy),
            new TaskTemplate("10-minute guided meditation", CategoryMind, DifficultyMedium),
            new TaskTemplate("Write 3 gratitudes", CategoryMind, DifficultyEasy),
            new TaskTemplate("Journal for 10 minutes", CategoryMind, DifficultyHard),
            new TaskTemplate("5-minute thought reframing", CategoryMind, DifficultyMedium),

            // Body (5)
            new TaskTemplate("10-minute walk", CategoryBody, DifficultyMedium),
            new TaskTemplate("Drink one full bottle of water", CategoryBody, DifficultyEasy),
            new TaskTemplate("15-minute stretch routine", CategoryBody, DifficultyMedium),
            new TaskTemplate("20-minute workout", CategoryBody, DifficultyHard),
            new TaskTemplate("Go to bed on schedule", CategoryBody, DifficultyHard),

            // Social (5)
            new TaskTemplate("Message one friend", CategorySocial, DifficultyEasy),
            new TaskTemplate("Call a family member", CategorySocial, DifficultyMedium),
            new TaskTemplate("Thank someone directly", CategorySocial, DifficultyEasy),
            new TaskTemplate("Have a 10-minute check-in chat", CategorySocial, DifficultyMedium),
            new TaskTemplate("Plan one social activity", CategorySocial, DifficultyHard),

            // Routine (5)
            new TaskTemplate("Make your bed", CategoryRoutine, DifficultyEasy),
            new TaskTemplate("Tidy one small area", CategoryRoutine, DifficultyEasy),
            new TaskTemplate("Review tomorrow's top 3 tasks", CategoryRoutine, DifficultyMedium),
            new TaskTemplate("No phone for first 30 minutes after waking", CategoryRoutine, DifficultyHard),
            new TaskTemplate("Prepare meals/snacks for tomorrow", CategoryRoutine, DifficultyHard)
        };

        await SeedTemplatesAsync(templates, "MVP");
    }

    private async System.Threading.Tasks.Task SeedTemplatesAsync(List<TaskTemplate> templates, string label)
    {
        if (!IsReady()) return;
        SetBusy(true, "Seeding " + label + " templates...");

        var uid = BootstrapController.User.UserId;
        var tasksRef = BootstrapController.Db.Collection("users").Document(uid).Collection("tasks");

        try
        {
            var existingSnap = await tasksRef.GetSnapshotAsync();
            var existingTitles = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var doc in existingSnap.Documents)
            {
                if (doc.TryGetValue<string>("title", out var title) && !string.IsNullOrWhiteSpace(title))
                {
                    existingTitles.Add(title.Trim());
                }
            }

            var createdCount = 0;
            for (var i = 0; i < templates.Count; i++)
            {
                var template = templates[i];
                if (existingTitles.Contains(template.Title))
                {
                    continue;
                }

                await tasksRef.AddAsync(BuildTask(template.Title, template.Category, template.Difficulty));
                existingTitles.Add(template.Title);
                createdCount += 1;
            }

            SetStatus("Seeded " + createdCount + " " + label + " templates (deduped).");
        }
        catch (Exception ex)
        {
            Debug.LogError("SeedTemplatesAsync failed: " + ex);
            SetStatus("Seed templates failed: " + ex.Message);
        }
        finally
        {
            SetBusy(false);
        }
    }

    public async void LoadActiveTasks()
    {
        await InitializeDailyTaskStateAsync();
    }

    private async Task InitializeDailyTaskStateAsync()
    {
        if (!IsReady()) return;
        SetBusy(true, "Loading today's task set...");

        var uid = BootstrapController.User.UserId;
        var userRef = BootstrapController.Db.Collection("users").Document(uid);
        var tasks = userRef.Collection("tasks");
        var dayKey = DateTime.UtcNow.ToString("yyyy_MM_dd");
        var dailyRef = userRef.Collection("daily_state").Document(dayKey);

        try
        {
            var dailySnap = await dailyRef.GetSnapshotAsync();
            var dailyData = dailySnap.Exists ? dailySnap.ToDictionary() : new Dictionary<string, object>();
            _easyCompletedToday = ReadBool(dailyData, "completed_easy", false);
            _mediumCompletedToday = ReadBool(dailyData, "completed_medium", false);
            _hardCompletedToday = ReadBool(dailyData, "completed_hard", false);

            var activeSnap = await tasks.WhereEqualTo("active", true).GetSnapshotAsync();
            var easyTasks = new List<DocumentSnapshot>();
            var mediumTasks = new List<DocumentSnapshot>();
            var hardTasks = new List<DocumentSnapshot>();

            foreach (var doc in activeSnap.Documents)
            {
                if (!doc.TryGetValue<string>("difficulty", out var difficulty))
                {
                    continue;
                }

                var normalized = (difficulty ?? string.Empty).Trim().ToLowerInvariant();
                switch (normalized)
                {
                    case DifficultyEasy:
                        easyTasks.Add(doc);
                        break;
                    case DifficultyMedium:
                        mediumTasks.Add(doc);
                        break;
                    case DifficultyHard:
                        hardTasks.Add(doc);
                        break;
                }
            }

            _selectedEasyTaskId = PickTaskIdForDay(easyTasks, dayKey, out _selectedEasyTaskTitle, "Easy");
            _selectedMediumTaskId = PickTaskIdForDay(mediumTasks, dayKey, out _selectedMediumTaskTitle, "Medium");
            _selectedHardTaskId = PickTaskIdForDay(hardTasks, dayKey, out _selectedHardTaskTitle, "Hard");

            UpdateDifficultyButtonLabels();
            SetStatus("Today's tasks loaded.");
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

    public void CompleteSelectedAsEasy() => CompleteTaskForDifficulty(DifficultyEasy, _selectedEasyTaskId);
    public void CompleteSelectedAsMedium() => CompleteTaskForDifficulty(DifficultyMedium, _selectedMediumTaskId);
    public void CompleteSelectedAsHard() => CompleteTaskForDifficulty(DifficultyHard, _selectedHardTaskId);

    public async void CompleteSelectedTask(string difficultyOverride = null)
    {
        if (!IsReady()) return;

        if (string.IsNullOrEmpty(_selectedTaskId))
        {
            SetStatus("No task selected for " + _selectedDifficulty + ". Run Load Tasks first.");
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

            if (tasksCompletedCount < 3)
            {
                bonusXp += 8;
                bonusesApplied.Add("first_three_tasks_bonus");
            }

            if (!firstTaskCompleted)
            {
                bonusXp += 12;
                bonusesApplied.Add("first_task_streak_bonus");
                streakCount += 1;
            }

            var totalXpBeforeCap = baseXp + bonusXp;

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

            if (difficulty == DifficultyEasy)
            {
                dailyUpdates["completed_easy"] = true;
                dailyUpdates["completed_easy_task_id"] = _selectedTaskId;
            }
            else if (difficulty == DifficultyMedium)
            {
                dailyUpdates["completed_medium"] = true;
                dailyUpdates["completed_medium_task_id"] = _selectedTaskId;
            }
            else if (difficulty == DifficultyHard)
            {
                dailyUpdates["completed_hard"] = true;
                dailyUpdates["completed_hard_task_id"] = _selectedTaskId;
            }

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

            if (difficulty == DifficultyEasy) _easyCompletedToday = true;
            if (difficulty == DifficultyMedium) _mediumCompletedToday = true;
            if (difficulty == DifficultyHard) _hardCompletedToday = true;
            UpdateDifficultyButtonLabels();

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
        if (seedMvpTasksButton != null) seedMvpTasksButton.interactable = canInteract;
        if (loadTasksButton != null) loadTasksButton.interactable = canInteract;
        if (completeEasyButton != null) completeEasyButton.interactable = canInteract && !_easyCompletedToday && !string.IsNullOrEmpty(_selectedEasyTaskId);
        if (completeMediumButton != null) completeMediumButton.interactable = canInteract && !_mediumCompletedToday && !string.IsNullOrEmpty(_selectedMediumTaskId);
        if (completeHardButton != null) completeHardButton.interactable = canInteract && !_hardCompletedToday && !string.IsNullOrEmpty(_selectedHardTaskId);
        if (backHomeButton != null) backHomeButton.interactable = canInteract;
    }

    private void CompleteTaskForDifficulty(string difficulty, string taskId)
    {
        if (difficulty == DifficultyEasy && _easyCompletedToday)
        {
            SetStatus("Easy task already completed today.");
            return;
        }

        if (difficulty == DifficultyMedium && _mediumCompletedToday)
        {
            SetStatus("Medium task already completed today.");
            return;
        }

        if (difficulty == DifficultyHard && _hardCompletedToday)
        {
            SetStatus("Hard task already completed today.");
            return;
        }

        if (string.IsNullOrEmpty(taskId))
        {
            SetStatus("No " + difficulty + " task available for today.");
            return;
        }

        _selectedTaskId = taskId;
        _selectedDifficulty = difficulty;
        CompleteSelectedTask(difficulty);
    }

    private static string PickTaskIdForDay(List<DocumentSnapshot> docs, string dayKey, out string title, string fallbackTitle)
    {
        title = fallbackTitle;
        if (docs == null || docs.Count == 0)
        {
            return null;
        }

        docs.Sort((a, b) => string.CompareOrdinal(a.Id, b.Id));
        var hash = 17;
        for (var i = 0; i < dayKey.Length; i++)
        {
            hash = hash * 31 + dayKey[i];
        }

        var index = Mathf.Abs(hash) % docs.Count;
        var picked = docs[index];
        if (picked.TryGetValue<string>("title", out var loadedTitle) && !string.IsNullOrWhiteSpace(loadedTitle))
        {
            title = loadedTitle.Trim();
        }

        return picked.Id;
    }

    private void UpdateDifficultyButtonLabels()
    {
        SetButtonText(completeEasyButton, _easyCompletedToday ? "Easy Complete" : "Easy: " + _selectedEasyTaskTitle);
        SetButtonText(completeMediumButton, _mediumCompletedToday ? "Medium Complete" : "Medium: " + _selectedMediumTaskTitle);
        SetButtonText(completeHardButton, _hardCompletedToday ? "Hard Complete" : "Hard: " + _selectedHardTaskTitle);
        UpdateControlState();
    }

    private static void SetButtonText(Button button, string value)
    {
        if (button == null)
        {
            return;
        }

        var tmp = button.GetComponentInChildren<TMP_Text>(true);
        if (tmp != null)
        {
            tmp.text = value;
        }
    }
}
