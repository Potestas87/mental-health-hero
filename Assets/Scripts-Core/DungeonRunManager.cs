using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Firebase;
using Firebase.Auth;
using Firebase.Firestore;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;

public class DungeonRunManager : MonoBehaviour
{
    [Header("Optional HUD")]
    public TMP_Text runStatusText;
    public TMP_Text floorText;
    public TMP_Text hpText;

    [Header("Run Settings")]
    public int totalFloors = 3;
    public int maxRunsPerDay = 2;
    public bool enforceDailyLives = true;
    public bool useAuthoritativeFunctions;

    [Header("Player Reference")]
    public HeroController2D player;
    public DungeonSpawner spawner;
    public CloudFunctionClient functionClient;

    private bool _runActive;
    private bool _bossDefeated;
    private bool _resultSaved;
    private int _floorsCleared;
    private Timestamp _runStartedAt;
    private string _activeRunId;

    private FirebaseAuth _auth;
    private FirebaseFirestore _db;
    private FirebaseUser _user;

    private void Start()
    {
        UpdateFloorHud();
        UpdateHpHud();
    }

    public async void StartRun()
    {
        if (_runActive)
        {
            SetStatus("Run already active.");
            return;
        }

        if (!await EnsureFirebaseReadyAsync())
        {
            SetStatus("Firebase unavailable. Try again.");
            return;
        }

        try
        {
            if (useAuthoritativeFunctions)
            {
                if (functionClient == null)
                {
                    SetStatus("CloudFunctionClient is not assigned.");
                    return;
                }

                var response = await functionClient.StartRunAsync();
                _activeRunId = response.TryGetValue("runId", out var runIdObj) ? runIdObj?.ToString() : null;
                if (string.IsNullOrEmpty(_activeRunId))
                {
                    SetStatus("startRun response missing runId.");
                    return;
                }
            }
            else if (enforceDailyLives)
            {
                var uid = _user.UserId;
                var userRef = _db.Collection("users").Document(uid);
                var dayKey = DateTime.Now.ToString("yyyy_MM_dd");
                var dailyRef = userRef.Collection("daily_state").Document(dayKey);
                var dailySnap = await dailyRef.GetSnapshotAsync();
                var dailyData = dailySnap.Exists ? dailySnap.ToDictionary() : new Dictionary<string, object>();
                var runsUsed = ReadInt(dailyData, "runsUsed", 0);
                if (runsUsed >= maxRunsPerDay)
                {
                    SetStatus("No lives remaining today.");
                    return;
                }

                await dailyRef.SetAsync(new Dictionary<string, object>
                {
                    { "runsUsed", runsUsed + 1 },
                    { "serverDayKey", dayKey },
                    { "updatedAt", Timestamp.GetCurrentTimestamp() }
                }, SetOptions.MergeAll);
            }

            _runActive = true;
            _bossDefeated = false;
            _resultSaved = false;
            _floorsCleared = 0;
            _runStartedAt = Timestamp.GetCurrentTimestamp();

            var activeClass = await LoadPlayerClassAsync();
            if (player != null)
            {
                player.ApplyClassProfile(activeClass);
                player.ResetHealthToFull();
                player.enabled = true;
            }

            UpdateFloorHud();
            UpdateHpHud();
            SetStatus("Run started. Class: " + activeClass);

            if (spawner != null)
            {
                spawner.DespawnActiveEnemy();
                spawner.SpawnFloorEnemy(1);
                SetStatus("Run started. Floor 1 enemy spawned.");
            }
        }
        catch (Exception ex)
        {
            Debug.LogError("StartRun failed: " + ex);
            SetStatus("Start run failed.");
        }
    }

    public void OnPlayerHpChanged(int hp, int maxHp)
    {
        if (hpText != null)
        {
            hpText.text = "HP: " + hp + " / " + maxHp;
        }
    }

    public void ClearFloor()
    {
        if (!_runActive)
        {
            SetStatus("Start run first.");
            return;
        }

        HandleNormalFloorCleared("manual");
    }

    public void DefeatBoss()
    {
        if (!_runActive)
        {
            SetStatus("Start run first.");
            return;
        }

        _bossDefeated = true;
        EndRun("win");
    }

    public void PlayerDied()
    {
        if (!_runActive)
        {
            return;
        }

        EndRun("loss");
    }

    public void QuitRun()
    {
        if (!_runActive)
        {
            return;
        }

        EndRun("quit");
    }

    public void ReturnHome()
    {
        SceneManager.LoadScene("HomeScene");
    }

    public void OnEnemyDefeated(bool wasBoss)
    {
        if (!_runActive)
        {
            return;
        }

        if (wasBoss)
        {
            _bossDefeated = true;
            EndRun("win");
            return;
        }

        HandleNormalFloorCleared("combat");
    }

    private async void EndRun(string result)
    {
        if (!_runActive || _resultSaved)
        {
            return;
        }

        _runActive = false;
        _resultSaved = true;

        if (spawner != null)
        {
            spawner.DespawnActiveEnemy();
        }

        if (!await EnsureFirebaseReadyAsync())
        {
            SetStatus("Run ended locally; Firebase unavailable.");
            return;
        }

        try
        {
            if (useAuthoritativeFunctions)
            {
                if (functionClient == null)
                {
                    SetStatus("CloudFunctionClient is not assigned.");
                    return;
                }

                if (string.IsNullOrEmpty(_activeRunId))
                {
                    SetStatus("No active runId for endRun.");
                    return;
                }

                var response = await functionClient.EndRunAsync(_activeRunId, result, _floorsCleared, _bossDefeated);
                var xpAwardedFromServer = CloudFunctionClient.ReadInt(response, "xpAwarded", 0);
                var shardsAwardedFromServer = CloudFunctionClient.ReadInt(response, "shardsAwarded", 0);
                var alreadyEnded = CloudFunctionClient.ReadBool(response, "alreadyEnded", false);
                SetStatus("Run " + result + (alreadyEnded ? " (already ended)" : "") + " | +" + xpAwardedFromServer + " XP, +" + shardsAwardedFromServer + " shards");
                _activeRunId = null;
                return;
            }

            var uid = _user.UserId;
            var userRef = _db.Collection("users").Document(uid);
            var dayKey = DateTime.Now.ToString("yyyy_MM_dd");
            var dailyRef = userRef.Collection("daily_state").Document(dayKey);
            var runLogsRef = userRef.Collection("run_logs");

            var userSnap = await userRef.GetSnapshotAsync();
            var dailySnap = await dailyRef.GetSnapshotAsync();
            if (!userSnap.Exists)
            {
                SetStatus("Run saved failed: user doc missing.");
                return;
            }

            var userData = userSnap.ToDictionary();
            var dailyData = dailySnap.Exists ? dailySnap.ToDictionary() : new Dictionary<string, object>();

            var currentLevel = ReadInt(userData, "level", 1);
            var currentXp = ReadInt(userData, "xp", 0);
            var currentSkillPoints = ReadInt(userData, "skillPoints", 0);
            var currentShards = ReadInt(userData, "shards", 0);
            var bossClearCount = ReadInt(dailyData, "bossClearCount", 0);

            var floorXp = _floorsCleared * 8;
            var bossXp = _bossDefeated ? 20 : 0;
            var xpAwarded = floorXp + bossXp;

            var bossShards = _bossDefeated ? 5 : 0;
            var firstBossBonus = (_bossDefeated && bossClearCount == 0) ? 3 : 0;
            var shardsAwarded = bossShards + firstBossBonus;

            var xpResult = XpProgressionService.ApplyXp(currentLevel, currentXp, currentSkillPoints, xpAwarded);

            await userRef.SetAsync(new Dictionary<string, object>
            {
                { "level", xpResult.Level },
                { "xp", xpResult.Xp },
                { "skillPoints", xpResult.SkillPoints },
                { "shards", currentShards + shardsAwarded },
                { "updatedAt", Timestamp.GetCurrentTimestamp() }
            }, SetOptions.MergeAll);

            var dailyUpdates = new Dictionary<string, object>
            {
                { "serverDayKey", dayKey },
                { "updatedAt", Timestamp.GetCurrentTimestamp() }
            };

            if (_bossDefeated)
            {
                dailyUpdates["bossClearCount"] = bossClearCount + 1;
            }

            await dailyRef.SetAsync(dailyUpdates, SetOptions.MergeAll);

            await runLogsRef.AddAsync(new Dictionary<string, object>
            {
                { "startedAt", _runStartedAt },
                { "endedAt", Timestamp.GetCurrentTimestamp() },
                { "result", result },
                { "floorsCleared", _floorsCleared },
                { "bossDefeated", _bossDefeated },
                { "xpAwarded", xpAwarded },
                { "shardsAwarded", shardsAwarded }
            });

            SetStatus("Run " + result + " | +" + xpAwarded + " XP, +" + shardsAwarded + " shards");
            _activeRunId = null;
        }
        catch (Exception ex)
        {
            Debug.LogError("EndRun failed: " + ex);
            SetStatus("Run save failed.");
        }
    }

    private void UpdateFloorHud()
    {
        if (floorText != null)
        {
            floorText.text = "Floors: " + _floorsCleared + " / " + totalFloors;
        }
    }

    private void UpdateHpHud()
    {
        if (player == null)
        {
            return;
        }

        OnPlayerHpChanged(player.CurrentHp, player.MaxHp);
    }

    private void SetStatus(string message)
    {
        Debug.Log(message);
        if (runStatusText != null)
        {
            runStatusText.text = message;
        }
    }

    private void HandleNormalFloorCleared(string source)
    {
        if (_floorsCleared >= totalFloors)
        {
            SetStatus("All normal floors already cleared.");
            return;
        }

        _floorsCleared += 1;
        UpdateFloorHud();

        if (_floorsCleared >= totalFloors)
        {
            if (spawner != null)
            {
                spawner.SpawnBoss();
                SetStatus("Floor " + _floorsCleared + "/" + totalFloors + " cleared via " + source + ". Boss spawned.");
            }
            else
            {
                SetStatus("Floor " + _floorsCleared + "/" + totalFloors + " cleared via " + source + ". Spawn boss manually.");
            }

            return;
        }

        var nextFloor = _floorsCleared + 1;
        if (spawner != null)
        {
            spawner.SpawnFloorEnemy(nextFloor);
        }

        SetStatus("Floor cleared: " + _floorsCleared + "/" + totalFloors + " via " + source + ". Floor " + nextFloor + " started.");
    }

    private async Task<string> LoadPlayerClassAsync()
    {
        if (_db == null || _user == null)
        {
            return "warrior";
        }

        try
        {
            var userSnap = await _db.Collection("users").Document(_user.UserId).GetSnapshotAsync();
            if (!userSnap.Exists)
            {
                return "warrior";
            }

            if (userSnap.TryGetValue<string>("class", out var classId) && !string.IsNullOrWhiteSpace(classId))
            {
                return classId.Trim().ToLowerInvariant();
            }
        }
        catch (Exception ex)
        {
            Debug.LogWarning("LoadPlayerClassAsync failed, defaulting to warrior: " + ex.Message);
        }

        return "warrior";
    }

    private async Task<bool> EnsureFirebaseReadyAsync()
    {
        if (_db != null && _user != null)
        {
            return true;
        }

        try
        {
            var status = await FirebaseApp.CheckAndFixDependenciesAsync();
            if (status != DependencyStatus.Available)
            {
                Debug.LogError("DungeonRunManager: Firebase dependencies unavailable: " + status);
                return false;
            }

            _auth = FirebaseAuth.DefaultInstance;
            _db = FirebaseFirestore.DefaultInstance;

            if (_auth.CurrentUser != null)
            {
                _user = _auth.CurrentUser;
                return true;
            }

            var signInResult = await _auth.SignInAnonymouslyAsync();
            _user = signInResult.User;
            return _user != null;
        }
        catch (Exception ex)
        {
            Debug.LogError("DungeonRunManager: Firebase init/sign-in failed: " + ex);
            return false;
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
}
