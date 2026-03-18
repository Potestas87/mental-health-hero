using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;
using Firebase.Firestore;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class CosmeticsController : MonoBehaviour
{
    [Header("Optional UI")]
    public TMP_Text shardsText;
    public TMP_Text equippedText;
    public TMP_Text catalogText;
    public TMP_Text statusText;
    public TMP_InputField cosmeticIdInput;
    public Button buyButton;
    public Button equipButton;

    private const string EquippedByCategoryField = "equippedCosmeticIdsByCategory";
    private const string OwnedCosmeticsField = "ownedCosmeticIds";
    private const string ShardsField = "shards";
    private const string UpdatedAtField = "updatedAt";
    private const string DefaultNoTintId = "tint_default";
    private const string DefaultTestingTintId = "tint_crimson";

    private FirebaseFirestore _db;
    private string _uid;
    private bool _isBusy;

    private int _currentShards;
    private HashSet<string> _owned = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, string> _equippedByCategory = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

    private void Start()
    {
        Refresh();
    }

    public async void Refresh()
    {
        SetBusy(true, "Loading cosmetics...");

        if (BootstrapController.Db == null || BootstrapController.User == null)
        {
            SetBusy(false, "Firebase not ready.");
            return;
        }

        _db = BootstrapController.Db;
        _uid = BootstrapController.User.UserId;

        try
        {
            var snap = await _db.Collection("users").Document(_uid).GetSnapshotAsync();
            if (!snap.Exists)
            {
                SetBusy(false, "User profile missing.");
                return;
            }

            var data = snap.ToDictionary();
            _currentShards = ReadInt(data, ShardsField, 0);
            _owned = ReadStringSet(data, OwnedCosmeticsField);

            _equippedByCategory.Clear();
            var loadedMap = ReadStringMap(data, EquippedByCategoryField);
            foreach (var kv in loadedMap)
            {
                _equippedByCategory[kv.Key] = kv.Value;
            }

            // Backward compatibility for existing user docs.
            var legacyTint = ReadString(data, "equippedTintId", string.Empty);
            if (!_equippedByCategory.ContainsKey(CosmeticsCatalog.TintCategoryKey) && !string.IsNullOrWhiteSpace(legacyTint))
            {
                _equippedByCategory[CosmeticsCatalog.TintCategoryKey] = legacyTint.Trim().ToLowerInvariant();
            }

            var grantedDefault = await EnsureDefaultTintUnlockedAsync();

            RenderUi();
            SetBusy(false, grantedDefault ? "Cosmetics loaded. Granted default no-tint + crimson tint." : "Cosmetics loaded.");
        }
        catch (Exception ex)
        {
            Debug.LogError("CosmeticsController.Refresh failed: " + ex);
            SetBusy(false, "Failed to load cosmetics.");
        }
    }

    public async void BuyFromInput()
    {
        if (_isBusy)
        {
            return;
        }

        var cosmeticId = (cosmeticIdInput != null ? cosmeticIdInput.text : string.Empty).Trim().ToLowerInvariant();
        if (string.IsNullOrEmpty(cosmeticId))
        {
            SetStatus("Enter a cosmetic id.");
            return;
        }

        if (!CosmeticsCatalog.TryGet(cosmeticId, out var cosmetic))
        {
            SetStatus("Unknown cosmetic id: " + cosmeticId);
            return;
        }

        if (!CosmeticsCatalog.IsEnabledInMvp(cosmetic) || cosmetic.Category != CosmeticCategory.Tint)
        {
            SetStatus("Only tint cosmetics are active right now.");
            return;
        }

        if (BootstrapController.Db == null || BootstrapController.User == null)
        {
            SetStatus("Firebase not ready.");
            return;
        }

        var userRef = BootstrapController.Db.Collection("users").Document(BootstrapController.User.UserId);
        SetBusy(true, "Purchasing " + cosmetic.Name + "...");

        try
        {
            await BootstrapController.Db.RunTransactionAsync(async tx =>
            {
                var snap = await tx.GetSnapshotAsync(userRef);
                if (!snap.Exists)
                {
                    throw new InvalidOperationException("User profile missing.");
                }

                var data = snap.ToDictionary();
                var shards = ReadInt(data, ShardsField, 0);
                var owned = ReadStringSet(data, OwnedCosmeticsField);

                if (owned.Contains(cosmetic.Id))
                {
                    throw new InvalidOperationException("Already owned.");
                }

                if (shards < cosmetic.ShardCost)
                {
                    throw new InvalidOperationException("Not enough shards.");
                }

                owned.Add(cosmetic.Id);
                tx.Update(userRef, new Dictionary<string, object>
                {
                    { ShardsField, shards - cosmetic.ShardCost },
                    { OwnedCosmeticsField, new List<string>(owned) },
                    { UpdatedAtField, Timestamp.GetCurrentTimestamp() }
                });
            });

            SetStatus("Purchased: " + cosmetic.Name);
            Refresh();
        }
        catch (Exception ex)
        {
            var message = ex.InnerException != null ? ex.InnerException.Message : ex.Message;
            SetBusy(false, "Purchase failed: " + message);
        }
    }

    public async void EquipFromInput()
    {
        if (_isBusy)
        {
            return;
        }

        var cosmeticId = (cosmeticIdInput != null ? cosmeticIdInput.text : string.Empty).Trim().ToLowerInvariant();
        if (string.IsNullOrEmpty(cosmeticId))
        {
            SetStatus("Enter a cosmetic id.");
            return;
        }

        if (!CosmeticsCatalog.TryGet(cosmeticId, out var cosmetic))
        {
            SetStatus("Unknown cosmetic id: " + cosmeticId);
            return;
        }

        if (!CosmeticsCatalog.IsEnabledInMvp(cosmetic) || cosmetic.Category != CosmeticCategory.Tint)
        {
            SetStatus("Only tint cosmetics are active right now.");
            return;
        }

        if (!_owned.Contains(cosmetic.Id))
        {
            SetStatus("Cosmetic not owned.");
            return;
        }

        if (BootstrapController.Db == null || BootstrapController.User == null)
        {
            SetStatus("Firebase not ready.");
            return;
        }

        var categoryKey = CosmeticsCatalog.GetCategoryKey(cosmetic.Category);
        var mapToSave = new Dictionary<string, object>();
        foreach (var kv in _equippedByCategory)
        {
            mapToSave[kv.Key] = kv.Value;
        }
        mapToSave[categoryKey] = cosmetic.Id;

        var updates = new Dictionary<string, object>
        {
            { EquippedByCategoryField, mapToSave },
            { "equippedTintId", cosmetic.Id }, // legacy bridge for existing tint application paths
            { UpdatedAtField, Timestamp.GetCurrentTimestamp() }
        };

        SetBusy(true, "Equipping " + cosmetic.Name + "...");

        try
        {
            await BootstrapController.Db.Collection("users").Document(BootstrapController.User.UserId)
                .SetAsync(updates, SetOptions.MergeAll);

            SetStatus("Equipped: " + cosmetic.Name);
            Refresh();
        }
        catch (Exception ex)
        {
            SetBusy(false, "Equip failed: " + ex.Message);
        }
    }

    public void ReturnHome()
    {
        SceneManager.LoadScene("HomeScene");
    }

    private async System.Threading.Tasks.Task<bool> EnsureDefaultTintUnlockedAsync()
    {
        var grantedSomething = false;
        if (!_owned.Contains(DefaultNoTintId))
        {
            _owned.Add(DefaultNoTintId);
            grantedSomething = true;
        }

        if (!_owned.Contains(DefaultTestingTintId))
        {
            _owned.Add(DefaultTestingTintId);
            grantedSomething = true;
        }

        var currentTint = GetEquippedId(CosmeticsCatalog.TintCategoryKey);
        var needsEquip = string.IsNullOrWhiteSpace(currentTint);
        if (needsEquip)
        {
            _equippedByCategory[CosmeticsCatalog.TintCategoryKey] = DefaultNoTintId;
        }

        if (!grantedSomething && !needsEquip)
        {
            return false;
        }

        var mapToSave = new Dictionary<string, object>();
        foreach (var kv in _equippedByCategory)
        {
            mapToSave[kv.Key] = kv.Value;
        }

        var updates = new Dictionary<string, object>
        {
            { OwnedCosmeticsField, new List<string>(_owned) },
            { EquippedByCategoryField, mapToSave },
            { "equippedTintId", GetEquippedId(CosmeticsCatalog.TintCategoryKey) },
            { UpdatedAtField, Timestamp.GetCurrentTimestamp() }
        };

        await _db.Collection("users").Document(_uid).SetAsync(updates, SetOptions.MergeAll);
        return true;
    }

    private void RenderUi()
    {
        if (shardsText != null)
        {
            shardsText.text = "Shards: " + _currentShards;
        }

        if (equippedText != null)
        {
            var tintId = GetEquippedId(CosmeticsCatalog.TintCategoryKey);
            equippedText.text = "Equipped -> Tint: " + SafeOrNone(tintId) + " (MVP active category)";
        }

        if (catalogText != null)
        {
            var sb = new StringBuilder();
            var active = CosmeticsCatalog.GetEnabledInMvp();
            sb.AppendLine("Active category now: Tint");
            sb.AppendLine("Planned categories: Armor, Weapon, Attack FX, Companion Pet, Dungeon Music");
            sb.AppendLine();

            for (var i = 0; i < active.Count; i++)
            {
                var c = active[i];
                var ownedTag = _owned.Contains(c.Id) ? "OWNED" : "LOCKED";
                sb.Append('[').Append(ownedTag).Append("] ")
                    .Append(c.Id).Append(" - ").Append(c.Name)
                    .Append(" (Category: ").Append(c.Category).Append(", Cost: ").Append(c.ShardCost).Append(")")
                    .AppendLine();
            }

            catalogText.text = sb.ToString();
        }
    }

    private string GetEquippedId(string categoryKey)
    {
        if (string.IsNullOrWhiteSpace(categoryKey))
        {
            return string.Empty;
        }

        if (_equippedByCategory.TryGetValue(categoryKey, out var value) && !string.IsNullOrWhiteSpace(value))
        {
            return value;
        }

        return string.Empty;
    }

    private void SetBusy(bool busy, string status)
    {
        _isBusy = busy;
        if (buyButton != null) buyButton.interactable = !busy;
        if (equipButton != null) equipButton.interactable = !busy;
        SetStatus(status);
    }

    private void SetStatus(string message)
    {
        if (statusText != null)
        {
            statusText.text = message;
        }

        Debug.Log(message);
    }

    private static string SafeOrNone(string value)
    {
        return string.IsNullOrWhiteSpace(value) ? "none" : value;
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

    private static HashSet<string> ReadStringSet(Dictionary<string, object> data, string key)
    {
        var set = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        if (!data.TryGetValue(key, out var raw) || raw == null)
        {
            return set;
        }

        if (raw is IEnumerable enumerable)
        {
            foreach (var item in enumerable)
            {
                if (item == null)
                {
                    continue;
                }

                var value = item.ToString().Trim().ToLowerInvariant();
                if (!string.IsNullOrEmpty(value))
                {
                    set.Add(value);
                }
            }
        }

        return set;
    }

    private static Dictionary<string, string> ReadStringMap(Dictionary<string, object> data, string key)
    {
        var map = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        if (!data.TryGetValue(key, out var raw) || raw == null)
        {
            return map;
        }

        if (raw is IDictionary dict)
        {
            foreach (DictionaryEntry entry in dict)
            {
                if (entry.Key == null || entry.Value == null)
                {
                    continue;
                }

                var mapKey = entry.Key.ToString().Trim().ToLowerInvariant();
                var mapValue = entry.Value.ToString().Trim().ToLowerInvariant();
                if (!string.IsNullOrWhiteSpace(mapKey))
                {
                    map[mapKey] = mapValue;
                }
            }

            return map;
        }

        if (raw is Dictionary<string, object> objMap)
        {
            foreach (var kv in objMap)
            {
                if (string.IsNullOrWhiteSpace(kv.Key) || kv.Value == null)
                {
                    continue;
                }

                map[kv.Key.Trim().ToLowerInvariant()] = kv.Value.ToString().Trim().ToLowerInvariant();
            }
        }

        return map;
    }
}
