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

public class PomodoroController : MonoBehaviour
{
    [Header("UI")]
    public TMP_Text timerText;
    public TMP_Text statusText;
    public TMP_Text rewardPreviewText;
    public Button start5Button;
    public Button start10Button;
    public Button start15Button;
    public Button cancelButton;
    public Button backButton;

    [Header("Rewards (small XP)")]
    public int xpFor5Min = 8;
    public int xpFor10Min = 16;
    public int xpFor15Min = 26;

    [Header("Behavior")]
    public bool cancelIfAppLosesFocus = true;
    public string homeSceneName = "HomeScene";

    private FirebaseAuth _auth;
    private FirebaseFirestore _db;
    private FirebaseUser _user;

    private Coroutine _timerRoutine;
    private bool _isRunning;
    private bool _eligibleForReward;
    private int _selectedMinutes;
    private int _selectedXp;
    private float _remainingSeconds;

    private void Start()
    {
        ResetTimerLabel();
        SetStatus("Pick a timer length.");
        RefreshRewardPreview();
        UpdateUiState();
    }

    public void StartPomodoro5() => StartPomodoro(5, xpFor5Min);
    public void StartPomodoro10() => StartPomodoro(10, xpFor10Min);
    public void StartPomodoro15() => StartPomodoro(15, xpFor15Min);

    public async void StartPomodoro(int minutes, int xpReward)
    {
        if (_isRunning)
        {
            SetStatus("Timer already running.");
            return;
        }

        if (minutes <= 0 || xpReward < 0)
        {
            SetStatus("Invalid timer configuration.");
            return;
        }

        if (!await EnsureFirebaseReadyAsync())
        {
            SetStatus("Firebase unavailable. Try again.");
            return;
        }

        _selectedMinutes = minutes;
        _selectedXp = xpReward;
        _remainingSeconds = minutes * 60f;
        _isRunning = true;
        _eligibleForReward = true;
        UpdateUiState();
        SetStatus("Focus mode started: " + minutes + " min");

        if (_timerRoutine != null)
        {
            StopCoroutine(_timerRoutine);
        }

        _timerRoutine = StartCoroutine(TimerRoutine());
    }

    public void CancelPomodoro()
    {
        if (!_isRunning)
        {
            SetStatus("No active timer.");
            return;
        }

        InvalidateAndStop("Timer canceled. No XP awarded.");
    }

    public void ReturnHome()
    {
        if (_isRunning)
        {
            InvalidateAndStop("You left focus mode. No XP awarded.");
        }

        SceneManager.LoadScene(homeSceneName);
    }

    private IEnumerator TimerRoutine()
    {
        while (_isRunning && _remainingSeconds > 0f)
        {
            _remainingSeconds -= Time.unscaledDeltaTime;
            if (_remainingSeconds < 0f)
            {
                _remainingSeconds = 0f;
            }

            UpdateTimerLabel(_remainingSeconds);
            yield return null;
        }

        _timerRoutine = null;

        if (!_isRunning)
        {
            yield break;
        }

        _isRunning = false;
        UpdateUiState();
        UpdateTimerLabel(0f);

        if (!_eligibleForReward)
        {
            SetStatus("Timer ended with no reward.");
            yield break;
        }

        _ = AwardXpAsync(_selectedXp, _selectedMinutes);
    }

    private async Task AwardXpAsync(int xpReward, int minutes)
    {
        if (_db == null || _user == null)
        {
            SetStatus("Reward failed: Firebase missing.");
            return;
        }

        try
        {
            var uid = _user.UserId;
            var userRef = _db.Collection("users").Document(uid);
            var logRef = userRef.Collection("pomodoro_logs").Document();

            var userSnap = await userRef.GetSnapshotAsync();
            if (!userSnap.Exists)
            {
                SetStatus("Reward failed: user profile missing.");
                return;
            }

            var data = userSnap.ToDictionary();
            var currentLevel = ReadInt(data, "level", 1);
            var currentXp = ReadInt(data, "xp", 0);
            var currentSkillPoints = ReadInt(data, "skillPoints", 0);

            var xpResult = XpProgressionService.ApplyXp(currentLevel, currentXp, currentSkillPoints, xpReward);

            await userRef.SetAsync(new Dictionary<string, object>
            {
                { "level", xpResult.Level },
                { "xp", xpResult.Xp },
                { "skillPoints", xpResult.SkillPoints },
                { "updatedAt", Timestamp.GetCurrentTimestamp() }
            }, SetOptions.MergeAll);

            await logRef.SetAsync(new Dictionary<string, object>
            {
                { "minutes", minutes },
                { "xpAwarded", xpReward },
                { "completedAt", Timestamp.GetCurrentTimestamp() }
            }, SetOptions.MergeAll);

            SetStatus("Pomodoro complete. +" + xpReward + " XP.");
        }
        catch (Exception ex)
        {
            Debug.LogError("Pomodoro award failed: " + ex);
            SetStatus("Reward failed: " + ex.Message);
        }
    }

    private void OnDisable()
    {
        if (_isRunning)
        {
            InvalidateAndStop("Timer interrupted. No XP awarded.");
        }
    }

    private void OnApplicationPause(bool paused)
    {
        if (paused && cancelIfAppLosesFocus)
        {
            InvalidateAndStop("App left focus. No XP awarded.");
        }
    }

    private void OnApplicationFocus(bool hasFocus)
    {
        if (!hasFocus && cancelIfAppLosesFocus)
        {
            InvalidateAndStop("App left focus. No XP awarded.");
        }
    }

    private void InvalidateAndStop(string message)
    {
        _eligibleForReward = false;
        _isRunning = false;

        if (_timerRoutine != null)
        {
            StopCoroutine(_timerRoutine);
            _timerRoutine = null;
        }

        ResetTimerLabel();
        UpdateUiState();
        SetStatus(message);
    }

    private async Task<bool> EnsureFirebaseReadyAsync()
    {
        if (_db != null && _user != null)
        {
            return true;
        }

        if (BootstrapController.Db != null && BootstrapController.User != null)
        {
            _db = BootstrapController.Db;
            _user = BootstrapController.User;
            return true;
        }

        try
        {
            var status = await FirebaseApp.CheckAndFixDependenciesAsync();
            if (status != DependencyStatus.Available)
            {
                Debug.LogError("PomodoroController: Firebase dependencies unavailable: " + status);
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
            Debug.LogError("PomodoroController: Firebase init/sign-in failed: " + ex);
            return false;
        }
    }

    private void UpdateUiState()
    {
        if (start5Button != null) start5Button.interactable = !_isRunning;
        if (start10Button != null) start10Button.interactable = !_isRunning;
        if (start15Button != null) start15Button.interactable = !_isRunning;
        if (cancelButton != null) cancelButton.interactable = _isRunning;
        if (backButton != null) backButton.interactable = !_isRunning;
    }

    private void RefreshRewardPreview()
    {
        if (rewardPreviewText == null)
        {
            return;
        }

        rewardPreviewText.text =
            "5m: +" + xpFor5Min + " XP\n" +
            "10m: +" + xpFor10Min + " XP\n" +
            "15m: +" + xpFor15Min + " XP\n" +
            "Leaving this screen cancels reward.";
    }

    private void UpdateTimerLabel(float totalSeconds)
    {
        if (timerText == null)
        {
            return;
        }

        var seconds = Mathf.CeilToInt(Mathf.Max(0f, totalSeconds));
        var minutesPart = seconds / 60;
        var secondsPart = seconds % 60;
        timerText.text = minutesPart.ToString("00") + ":" + secondsPart.ToString("00");
    }

    private void ResetTimerLabel()
    {
        if (_selectedMinutes <= 0)
        {
            UpdateTimerLabel(0f);
            return;
        }

        UpdateTimerLabel(_selectedMinutes * 60f);
    }

    private void SetStatus(string message)
    {
        Debug.Log(message);
        if (statusText != null)
        {
            statusText.text = message;
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
