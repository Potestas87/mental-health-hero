using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;
using Firebase.Firestore;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;

public class UpgradeTreeController : MonoBehaviour
{
    [Header("Optional UI")]
    public TMP_Text summaryText;
    public TMP_Text nodesText;
    public TMP_InputField nodeIdInput;

    private FirebaseFirestore _db;
    private string _uid;

    private string _playerClass = "warrior";
    private int _skillPoints;
    private HashSet<string> _purchasedNodeIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

    private void Start()
    {
        Refresh();
    }

    public async void Refresh()
    {
        if (BootstrapController.Db == null || BootstrapController.User == null)
        {
            SetSummary("Firebase not ready.");
            return;
        }

        _db = BootstrapController.Db;
        _uid = BootstrapController.User.UserId;

        try
        {
            var userRef = _db.Collection("users").Document(_uid);
            var snap = await userRef.GetSnapshotAsync();

            if (!snap.Exists)
            {
                SetSummary("User profile missing.");
                return;
            }

            var data = snap.ToDictionary();
            _playerClass = ReadString(data, "class", "warrior").ToLowerInvariant();
            _skillPoints = ReadInt(data, "skillPoints", 0);
            _purchasedNodeIds = ReadStringSet(data, "purchasedUpgrades");

            RenderUi();
        }
        catch (Exception ex)
        {
            Debug.LogError("UpgradeTreeController.Refresh failed: " + ex);
            SetSummary("Failed to load upgrade tree.");
        }
    }

    public async void BuyNodeFromInput()
    {
        if (nodeIdInput == null)
        {
            SetSummary("Node input is not assigned.");
            return;
        }

        await BuyNodeByIdInternalAsync(nodeIdInput.text);
    }

    public async void BuyNodeById(string nodeId)
    {
        await BuyNodeByIdInternalAsync(nodeId);
    }

    public void ReturnHome()
    {
        SceneManager.LoadScene("HomeScene");
    }

    private async System.Threading.Tasks.Task BuyNodeByIdInternalAsync(string nodeIdRaw)
    {
        var nodeId = (nodeIdRaw ?? string.Empty).Trim().ToLowerInvariant();
        if (string.IsNullOrEmpty(nodeId))
        {
            SetSummary("Enter a node id.");
            return;
        }

        if (BootstrapController.Db == null || BootstrapController.User == null)
        {
            SetSummary("Firebase not ready.");
            return;
        }

        if (!UpgradeCatalog.TryGetNode(nodeId, out var node))
        {
            SetSummary("Unknown node id: " + nodeId);
            return;
        }

        var userRef = BootstrapController.Db.Collection("users").Document(BootstrapController.User.UserId);

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
                var classId = ReadString(data, "class", "warrior").ToLowerInvariant();
                var skillPoints = ReadInt(data, "skillPoints", 0);
                var purchased = ReadStringSet(data, "purchasedUpgrades");

                if (node.ClassId != classId)
                {
                    throw new InvalidOperationException("Node does not match your class.");
                }

                if (purchased.Contains(node.Id))
                {
                    throw new InvalidOperationException("Node already purchased.");
                }

                for (var i = 0; i < node.RequiresNodeIds.Length; i++)
                {
                    if (!purchased.Contains(node.RequiresNodeIds[i]))
                    {
                        throw new InvalidOperationException("Missing prerequisite: " + node.RequiresNodeIds[i]);
                    }
                }

                if (skillPoints < node.CostSkillPoints)
                {
                    throw new InvalidOperationException("Not enough skill points.");
                }

                purchased.Add(node.Id);
                var purchasedList = new List<string>(purchased);

                tx.Update(userRef, new Dictionary<string, object>
                {
                    { "skillPoints", skillPoints - node.CostSkillPoints },
                    { "purchasedUpgrades", purchasedList },
                    { "updatedAt", Timestamp.GetCurrentTimestamp() }
                });
            });

            SetSummary("Purchased: " + node.Name);
            Refresh();
        }
        catch (Exception ex)
        {
            var message = ex.InnerException != null ? ex.InnerException.Message : ex.Message;
            SetSummary("Purchase failed: " + message);
            Debug.LogError("BuyNodeByIdInternalAsync failed: " + ex);
        }
    }

    private void RenderUi()
    {
        SetSummary("Class: " + _playerClass + " | Skill Points: " + _skillPoints + " | Purchased: " + _purchasedNodeIds.Count);

        if (nodesText == null)
        {
            return;
        }

        var nodes = UpgradeCatalog.GetNodesForClass(_playerClass);
        var sb = new StringBuilder();

        for (var i = 0; i < nodes.Count; i++)
        {
            var node = nodes[i];
            var owned = _purchasedNodeIds.Contains(node.Id);
            var unlocked = IsUnlocked(node, _purchasedNodeIds);
            var state = owned ? "OWNED" : (unlocked ? "AVAILABLE" : "LOCKED");

            sb.Append("[").Append(state).Append("] ").Append(node.Id).Append(" - ").Append(node.Name)
              .Append(" (Cost ").Append(node.CostSkillPoints).Append(")");

            if (node.RequiresNodeIds.Length > 0)
            {
                sb.Append(" req:");
                for (var r = 0; r < node.RequiresNodeIds.Length; r++)
                {
                    sb.Append(' ').Append(node.RequiresNodeIds[r]);
                }
            }

            sb.AppendLine();
        }

        nodesText.text = sb.ToString();
    }

    private static bool IsUnlocked(UpgradeNodeDefinition node, HashSet<string> purchased)
    {
        for (var i = 0; i < node.RequiresNodeIds.Length; i++)
        {
            if (!purchased.Contains(node.RequiresNodeIds[i]))
            {
                return false;
            }
        }

        return true;
    }

    private void SetSummary(string message)
    {
        Debug.Log(message);
        if (summaryText != null)
        {
            summaryText.text = message;
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
