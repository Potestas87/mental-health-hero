using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;
using Firebase.Firestore;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class UpgradeTreeController : MonoBehaviour
{
    [Header("Optional UI")]
    public TMP_Text summaryText;
    public TMP_Text skillPointsHeadlineText;
    public TMP_Text inputHintText;
    public TMP_Text feedbackText;
    public TMP_Text nodesText;
    public TMP_InputField nodeIdInput;
    public Button buyButton;

    private FirebaseFirestore _db;
    private string _uid;

    private string _playerClass = "warrior";
    private int _skillPoints;
    private HashSet<string> _purchasedNodeIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
    private bool _isBusy;

    private void Start()
    {
        if (nodeIdInput != null)
        {
            nodeIdInput.onValueChanged.AddListener(_ => UpdateBuyButtonState());
        }

        SetInputHint();
        Refresh();
    }

    public async void Refresh()
    {
        _isBusy = true;
        UpdateBuyButtonState();

        if (BootstrapController.Db == null || BootstrapController.User == null)
        {
            SetSummary("Firebase not ready.");
            _isBusy = false;
            UpdateBuyButtonState();
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
                _isBusy = false;
                UpdateBuyButtonState();
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
        finally
        {
            _isBusy = false;
            UpdateBuyButtonState();
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
        _isBusy = true;
        UpdateBuyButtonState();

        var nodeId = (nodeIdRaw ?? string.Empty).Trim().ToLowerInvariant();
        if (string.IsNullOrEmpty(nodeId))
        {
            SetSummary("Enter a node id.");
            _isBusy = false;
            UpdateBuyButtonState();
            return;
        }

        if (BootstrapController.Db == null || BootstrapController.User == null)
        {
            SetSummary("Firebase not ready.");
            _isBusy = false;
            UpdateBuyButtonState();
            return;
        }

        if (!UpgradeCatalog.TryGetNode(nodeId, out var node))
        {
            SetSummary("Unknown node id: " + nodeId);
            _isBusy = false;
            UpdateBuyButtonState();
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
            _isBusy = false;
            UpdateBuyButtonState();
        }
    }

    private void RenderUi()
    {
        SetSummary("Class: " + _playerClass + " | Skill Points: " + _skillPoints + " | Purchased: " + _purchasedNodeIds.Count);
        SetInputHint();
        UpdateBuyButtonState();

        if (skillPointsHeadlineText != null)
        {
            skillPointsHeadlineText.text = "Skill Points Available: " + _skillPoints;
        }

        if (nodesText == null)
        {
            return;
        }

        var nodes = UpgradeCatalog.GetNodesForClass(_playerClass);
        var available = new List<UpgradeNodeDefinition>();
        var locked = new List<UpgradeNodeDefinition>();
        var ownedNodes = new List<UpgradeNodeDefinition>();

        for (var i = 0; i < nodes.Count; i++)
        {
            var node = nodes[i];
            var isOwned = _purchasedNodeIds.Contains(node.Id);
            var unlocked = IsUnlocked(node, _purchasedNodeIds);
            if (isOwned)
            {
                ownedNodes.Add(node);
            }
            else if (unlocked)
            {
                available.Add(node);
            }
            else
            {
                locked.Add(node);
            }
        }

        var sb = new StringBuilder();
        sb.AppendLine("AVAILABLE NODES");
        AppendNodeList(sb, available, "AVAILABLE");
        sb.AppendLine();

        sb.AppendLine("LOCKED NODES");
        AppendNodeList(sb, locked, "LOCKED");
        sb.AppendLine();

        sb.AppendLine("OWNED NODES");
        AppendNodeList(sb, ownedNodes, "OWNED");

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

        if (feedbackText != null)
        {
            feedbackText.text = message;
        }
    }

    private void SetInputHint()
    {
        if (inputHintText == null)
        {
            return;
        }

        inputHintText.text = "Enter node id then Buy (example: war_hp_1, rng_speed_1, mag_range_1).";
    }

    private void UpdateBuyButtonState()
    {
        if (buyButton == null)
        {
            return;
        }

        var hasInput = nodeIdInput != null && !string.IsNullOrWhiteSpace(nodeIdInput.text);
        buyButton.interactable = !_isBusy && hasInput && _skillPoints > 0;
    }

    private static void AppendNodeList(StringBuilder sb, List<UpgradeNodeDefinition> nodes, string state)
    {
        if (nodes.Count == 0)
        {
            sb.AppendLine("- none");
            return;
        }

        for (var i = 0; i < nodes.Count; i++)
        {
            var node = nodes[i];
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
