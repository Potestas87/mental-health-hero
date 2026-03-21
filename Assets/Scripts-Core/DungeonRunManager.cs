using System;
using System.Collections;
using System.Collections.Generic;
using System.Threading.Tasks;
using Firebase;
using Firebase.Auth;
using Firebase.Firestore;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class DungeonRunManager : MonoBehaviour
{
    [Header("Optional HUD")]
    public TMP_Text runStatusText;
    public TMP_Text floorText;
    public TMP_Text hpText;
    public Button startRunButton;
    public Button quitRunButton;
    public Button backHomeButton;
    public Button clearFloorButton;

    [Header("Result Panel (Optional)")]
    public GameObject resultPanel;
    public TMP_Text resultTitleText;
    public TMP_Text resultFloorsText;
    public TMP_Text resultXpText;
    public TMP_Text resultShardsText;

    [Header("Run Settings")]
    public int totalFloors = 3;
    public int maxRunsPerDay = 2;
    public bool enforceDailyLives = true;
    public bool useAuthoritativeFunctions;
    public bool allowManualFloorClearForDebug;
    public bool useWaveSpawning;
    public int wavesToSpawn = 3;
    public int enemiesPerWave = 3;
    public float secondsBetweenWaves = 20f;

    [Header("Player Reference")]
    public HeroController2D player;
    public Transform playerSpawnPoint;
    public DungeonSpawner spawner;
    public CloudFunctionClient functionClient;

    private bool _runActive;
    private bool _bossDefeated;
    private bool _resultSaved;
    private int _floorsCleared;
    private Timestamp _runStartedAt;
    private DateTime _runStartedUtc;
    private string _activeClassId = "warrior";
    private string _activeRunId;
    private string _activeRunDayKey;

    private FirebaseAuth _auth;
    private FirebaseFirestore _db;
    private FirebaseUser _user;

    private void Start()
    {
        ResolveFunctionClientIfNeeded();
        ShowResultPanel(false, string.Empty, 0, 0);
        UpdateFloorHud();
        UpdateHpHud();
        ApplyHudControlState();
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
                ResolveFunctionClientIfNeeded();
                if (functionClient == null)
                {
                    SetStatus("CloudFunctionClient is not assigned.");
                    return;
                }

                var response = await functionClient.StartRunAsync();
                _activeRunId = response.TryGetValue("runId", out var runIdObj) ? runIdObj?.ToString() : null;
                _activeRunDayKey = response.TryGetValue("dayKey", out var dayKeyObj) ? dayKeyObj?.ToString() : DateTime.Now.ToString("yyyy_MM_dd");
                if (string.IsNullOrEmpty(_activeRunId))
                {
                    SetStatus("startRun response missing runId.");
                    return;
                }
            }
            else
            {
                var uid = _user.UserId;
                var userRef = _db.Collection("users").Document(uid);
                var dayKey = DateTime.Now.ToString("yyyy_MM_dd");
                var dailyRef = userRef.Collection("daily_state").Document(dayKey);
                var generatedRunId = Guid.NewGuid().ToString("N");
                _activeRunDayKey = dayKey;

                if (enforceDailyLives)
                {
                    var txResult = await _db.RunTransactionAsync(async tx =>
                    {
                        var userSnap = await tx.GetSnapshotAsync(userRef);
                        var dailySnap = await tx.GetSnapshotAsync(dailyRef);
                        var userData = userSnap.Exists ? userSnap.ToDictionary() : new Dictionary<string, object>();
                        var dailyData = dailySnap.Exists ? dailySnap.ToDictionary() : new Dictionary<string, object>();

                        var existingRunId = ReadString(userData, "activeRunId", string.Empty);
                        if (!string.IsNullOrEmpty(existingRunId))
                        {
                            throw new InvalidOperationException("An active run is already in progress.");
                        }

                        var runsUsed = ReadInt(dailyData, "runsUsed", 0);
                        if (runsUsed >= maxRunsPerDay)
                        {
                            throw new InvalidOperationException("No lives remaining today.");
                        }

                        tx.Set(dailyRef, new Dictionary<string, object>
                        {
                            { "runsUsed", runsUsed + 1 },
                            { "serverDayKey", dayKey },
                            { "updatedAt", Timestamp.GetCurrentTimestamp() }
                        }, SetOptions.MergeAll);

                        tx.Set(userRef, new Dictionary<string, object>
                        {
                            { "activeRunId", generatedRunId },
                            { "activeRunDayKey", dayKey },
                            { "updatedAt", Timestamp.GetCurrentTimestamp() }
                        }, SetOptions.MergeAll);

                        return generatedRunId;
                    });

                    _activeRunId = txResult;
                }
                else
                {
                    _activeRunId = generatedRunId;
                    await userRef.SetAsync(new Dictionary<string, object>
                    {
                        { "activeRunId", generatedRunId },
                        { "activeRunDayKey", dayKey },
                        { "updatedAt", Timestamp.GetCurrentTimestamp() }
                    }, SetOptions.MergeAll);
                }
            }

            _runActive = true;
            _bossDefeated = false;
            _resultSaved = false;
            _floorsCleared = 0;
            _runStartedAt = Timestamp.GetCurrentTimestamp();
            ShowResultPanel(false, string.Empty, 0, 0);
            ApplyHudControlState();

            var profile = await LoadPlayerProfileAsync();
            _activeClassId = profile.ClassId;
            _runStartedUtc = DateTime.UtcNow;
            if (player != null)
            {
                if (playerSpawnPoint != null)
                {
                    player.transform.position = playerSpawnPoint.position;
                    player.transform.rotation = playerSpawnPoint.rotation;
                }

                player.ApplyClassProfile(profile.ClassId);
                player.ApplyUpgradeBonuses(profile.PurchasedNodeIds);
                player.ApplyCosmeticTint(profile.EquippedTintId);
                player.ResetHealthToFull();
                player.enabled = true;
            }

            UpdateFloorHud();
            UpdateHpHud();
            SetStatus("Run started. Class: " + profile.ClassId + " | Upgrades: " + profile.PurchasedNodeIds.Count);
            AnalyticsService.LogDungeonRunStart(_activeClassId);

            if (spawner != null)
            {
                spawner.StopWaveSpawning();
                spawner.DespawnAllSpawnedEnemies();
                spawner.DespawnActiveEnemy();
                if (useWaveSpawning)
                {
                    spawner.StartWaveSpawning(wavesToSpawn, enemiesPerWave, secondsBetweenWaves);
                    SetStatus("Run started. Wave mode active: " + wavesToSpawn + " waves, " + enemiesPerWave + " enemies every " + secondsBetweenWaves + "s.");
                }
                else
                {
                    spawner.SpawnFloorEnemy(1);
                    SetStatus("Run started. Floor 1 enemy spawned.");
                }
            }
        }
        catch (Exception ex)
        {
            Debug.LogError("StartRun failed: " + ex);
            SetStatus("Start run failed: " + ex.Message);
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
        if (!allowManualFloorClearForDebug)
        {
            SetStatus("Manual floor clear is disabled. Defeat enemies to progress.");
            return;
        }

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
        ApplyHudControlState();
        var runDurationSec = _runStartedUtc == default ? 0 : Mathf.Max(0, Mathf.RoundToInt((float)(DateTime.UtcNow - _runStartedUtc).TotalSeconds));

        if (spawner != null)
        {
            spawner.StopWaveSpawning();
            spawner.DespawnAllSpawnedEnemies();
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
                ResolveFunctionClientIfNeeded();
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
                ShowResultPanel(true, result, xpAwardedFromServer, shardsAwardedFromServer);
                AnalyticsService.LogDungeonRunEnd(_activeClassId, _floorsCleared, result, xpAwardedFromServer, shardsAwardedFromServer, runDurationSec);
                _activeRunId = null;
                _activeRunDayKey = null;
                ApplyHudControlState();
                return;
            }

            if (string.IsNullOrEmpty(_activeRunId))
            {
                SetStatus("No active runId for local endRun.");
                ShowResultPanel(true, "error", 0, 0);
                return;
            }

            var uid = _user.UserId;
            var userRef = _db.Collection("users").Document(uid);
            var dayKey = string.IsNullOrEmpty(_activeRunDayKey) ? DateTime.Now.ToString("yyyy_MM_dd") : _activeRunDayKey;
            var dailyRef = userRef.Collection("daily_state").Document(dayKey);
            var runLogRef = userRef.Collection("run_logs").Document(_activeRunId);

            var txResult = await _db.RunTransactionAsync(async tx =>
            {
                var userSnap = await tx.GetSnapshotAsync(userRef);
                var dailySnap = await tx.GetSnapshotAsync(dailyRef);
                var runLogSnap = await tx.GetSnapshotAsync(runLogRef);

                if (!userSnap.Exists)
                {
                    throw new InvalidOperationException("Run saved failed: user doc missing.");
                }

                if (runLogSnap.Exists)
                {
                    var existing = runLogSnap.ToDictionary();
                    var existingXp = ReadInt(existing, "xpAwarded", 0);
                    var existingShards = ReadInt(existing, "shardsAwarded", 0);

                    tx.Set(userRef, new Dictionary<string, object>
                    {
                        { "activeRunId", FieldValue.Delete },
                        { "activeRunDayKey", FieldValue.Delete },
                        { "updatedAt", Timestamp.GetCurrentTimestamp() }
                    }, SetOptions.MergeAll);

                    return new LocalRunTxResult(existingXp, existingShards, true);
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

                tx.Set(userRef, new Dictionary<string, object>
                {
                    { "level", xpResult.Level },
                    { "xp", xpResult.Xp },
                    { "skillPoints", xpResult.SkillPoints },
                    { "shards", currentShards + shardsAwarded },
                    { "activeRunId", FieldValue.Delete },
                    { "activeRunDayKey", FieldValue.Delete },
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

                tx.Set(dailyRef, dailyUpdates, SetOptions.MergeAll);
                tx.Set(runLogRef, new Dictionary<string, object>
                {
                    { "runId", _activeRunId },
                    { "idempotencyKey", _activeRunId },
                    { "startedAt", _runStartedAt },
                    { "endedAt", Timestamp.GetCurrentTimestamp() },
                    { "result", result },
                    { "floorsCleared", _floorsCleared },
                    { "bossDefeated", _bossDefeated },
                    { "xpAwarded", xpAwarded },
                    { "shardsAwarded", shardsAwarded },
                    { "dayKey", dayKey }
                }, SetOptions.MergeAll);

                return new LocalRunTxResult(xpAwarded, shardsAwarded, false);
            });

            var txSuffix = txResult.AlreadyEnded ? " (already ended)" : "";
            SetStatus("Run " + result + txSuffix + " | +" + txResult.XpAwarded + " XP, +" + txResult.ShardsAwarded + " shards");
            ShowResultPanel(true, result, txResult.XpAwarded, txResult.ShardsAwarded);
            AnalyticsService.LogDungeonRunEnd(_activeClassId, _floorsCleared, result, txResult.XpAwarded, txResult.ShardsAwarded, runDurationSec);
            _activeRunId = null;
            _activeRunDayKey = null;
            ApplyHudControlState();
        }
        catch (Exception ex)
        {
            Debug.LogError("EndRun failed: " + ex);
            SetStatus("Run save failed.");
            ShowResultPanel(true, "error", 0, 0);
            ApplyHudControlState();
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

    public void SetExternalStatus(string message)
    {
        SetStatus(message);
    }

    private void ApplyHudControlState()
    {
        if (startRunButton != null)
        {
            startRunButton.interactable = !_runActive;
        }

        if (quitRunButton != null)
        {
            quitRunButton.interactable = _runActive;
        }

        if (backHomeButton != null)
        {
            backHomeButton.interactable = !_runActive;
        }

        if (clearFloorButton != null)
        {
            clearFloorButton.interactable = _runActive && allowManualFloorClearForDebug;
        }
    }

    private void ShowResultPanel(bool show, string result, int xpAwarded, int shardsAwarded)
    {
        if (resultPanel != null)
        {
            resultPanel.SetActive(show);
        }

        if (!show)
        {
            return;
        }

        if (resultTitleText != null)
        {
            resultTitleText.text = "Result: " + result.ToUpperInvariant();
        }

        if (resultFloorsText != null)
        {
            resultFloorsText.text = "Floors Cleared: " + _floorsCleared + " / " + totalFloors;
        }

        if (resultXpText != null)
        {
            resultXpText.text = "XP Gained: +" + xpAwarded;
        }

        if (resultShardsText != null)
        {
            resultShardsText.text = "Shards Gained: +" + shardsAwarded;
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

    private sealed class PlayerProfile
    {
        public string ClassId = "warrior";
        public List<string> PurchasedNodeIds = new List<string>();
        public string EquippedTintId = string.Empty;
    }

    private sealed class LocalRunTxResult
    {
        public int XpAwarded { get; }
        public int ShardsAwarded { get; }
        public bool AlreadyEnded { get; }

        public LocalRunTxResult(int xpAwarded, int shardsAwarded, bool alreadyEnded)
        {
            XpAwarded = xpAwarded;
            ShardsAwarded = shardsAwarded;
            AlreadyEnded = alreadyEnded;
        }
    }

    private async Task<PlayerProfile> LoadPlayerProfileAsync()
    {
        var profile = new PlayerProfile();
        if (_db == null || _user == null)
        {
            return profile;
        }

        try
        {
            var userSnap = await _db.Collection("users").Document(_user.UserId).GetSnapshotAsync();
            if (!userSnap.Exists)
            {
                return profile;
            }

            if (userSnap.TryGetValue<string>("class", out var classId) && !string.IsNullOrWhiteSpace(classId))
            {
                profile.ClassId = classId.Trim().ToLowerInvariant();
            }

            if (userSnap.TryGetValue<List<string>>("purchasedUpgrades", out var purchasedList) && purchasedList != null)
            {
                profile.PurchasedNodeIds = purchasedList;
            }
            else if (userSnap.TryGetValue<ArrayList>("purchasedUpgrades", out var purchasedArrayList) && purchasedArrayList != null)
            {
                var parsed = new List<string>();
                for (var i = 0; i < purchasedArrayList.Count; i++)
                {
                    var value = purchasedArrayList[i]?.ToString();
                    if (!string.IsNullOrWhiteSpace(value))
                    {
                        parsed.Add(value.Trim().ToLowerInvariant());
                    }
                }

                profile.PurchasedNodeIds = parsed;
            }

            if (userSnap.TryGetValue<string>("equippedTintId", out var equippedTintId) && !string.IsNullOrWhiteSpace(equippedTintId))
            {
                profile.EquippedTintId = equippedTintId.Trim().ToLowerInvariant();
            }
        }
        catch (Exception ex)
        {
            Debug.LogWarning("LoadPlayerProfileAsync failed, using defaults: " + ex.Message);
        }

        return profile;
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

    private void ResolveFunctionClientIfNeeded()
    {
        if (functionClient != null)
        {
            return;
        }

        functionClient = FindFirstObjectByType<CloudFunctionClient>();
        if (useAuthoritativeFunctions && functionClient == null)
        {
            Debug.LogWarning("DungeonRunManager: useAuthoritativeFunctions is enabled, but no CloudFunctionClient exists in scene.");
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
