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

    private FirebaseFirestore _db;
    private string _uid;
    private bool _isBusy;

    private int _currentShards;
    private HashSet<string> _owned = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
    private string _equippedTint = string.Empty;
    private string _equippedWeapon = string.Empty;
    private string _equippedArmor = string.Empty;

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
            _currentShards = ReadInt(data, "shards", 0);
            _owned = ReadStringSet(data, "ownedCosmeticIds");
            _equippedTint = ReadString(data, "equippedTintId", string.Empty);
            _equippedWeapon = ReadString(data, "equippedWeaponId", string.Empty);
            _equippedArmor = ReadString(data, "equippedArmorId", string.Empty);

            RenderUi();
            SetBusy(false, "Cosmetics loaded.");
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
                var shards = ReadInt(data, "shards", 0);
                var owned = ReadStringSet(data, "ownedCosmeticIds");

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
                    { "shards", shards - cosmetic.ShardCost },
                    { "ownedCosmeticIds", new List<string>(owned) },
                    { "updatedAt", Timestamp.GetCurrentTimestamp() }
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

        var updates = new Dictionary<string, object>
        {
            { "updatedAt", Timestamp.GetCurrentTimestamp() }
        };

        switch (cosmetic.Slot)
        {
            case CosmeticSlot.Tint:
                updates["equippedTintId"] = cosmetic.Id;
                break;
            case CosmeticSlot.Weapon:
                updates["equippedWeaponId"] = cosmetic.Id;
                break;
            case CosmeticSlot.Armor:
                updates["equippedArmorId"] = cosmetic.Id;
                break;
        }

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

    private void RenderUi()
    {
        if (shardsText != null)
        {
            shardsText.text = "Shards: " + _currentShards;
        }

        if (equippedText != null)
        {
            equippedText.text = "Equipped -> Tint: " + SafeOrNone(_equippedTint) + " | Weapon: " + SafeOrNone(_equippedWeapon) + " | Armor: " + SafeOrNone(_equippedArmor);
        }

        if (catalogText != null)
        {
            var sb = new StringBuilder();
            var all = CosmeticsCatalog.GetAll();
            for (var i = 0; i < all.Count; i++)
            {
                var c = all[i];
                var ownedTag = _owned.Contains(c.Id) ? "OWNED" : "LOCKED";
                sb.Append('[').Append(ownedTag).Append("] ")
                  .Append(c.Id).Append(" - ").Append(c.Name)
                  .Append(" (Slot: ").Append(c.Slot).Append(", Cost: ").Append(c.ShardCost).Append(")")
                  .AppendLine();
            }

            catalogText.text = sb.ToString();
        }
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
}
