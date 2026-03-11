using System.Collections.Generic;
using TMPro;
using UnityEngine;

public class SceneReliabilityValidator : MonoBehaviour
{
    [Header("Optional Output")]
    public TMP_Text statusText;

    [Header("Optional References")]
    public HomeController homeController;
    public TaskController taskController;
    public DungeonRunManager dungeonRunManager;
    public UpgradeTreeController upgradeTreeController;
    public CosmeticsController cosmeticsController;

    [Header("Behavior")]
    public bool validateOnStart = true;

    private void Start()
    {
        if (validateOnStart)
        {
            ValidateScene();
        }
    }

    public void ValidateScene()
    {
        var issues = new List<string>();

        if (homeController != null)
        {
            ValidateHome(homeController, issues);
        }

        if (taskController != null)
        {
            ValidateTask(taskController, issues);
        }

        if (dungeonRunManager != null)
        {
            ValidateDungeon(dungeonRunManager, issues);
        }

        if (upgradeTreeController != null)
        {
            ValidateUpgrade(upgradeTreeController, issues);
        }

        if (cosmeticsController != null)
        {
            ValidateCosmetics(cosmeticsController, issues);
        }

        if (issues.Count == 0)
        {
            SetStatus("Scene validation: OK");
            return;
        }

        var message = "Scene validation found " + issues.Count + " issue(s):\n- " + string.Join("\n- ", issues);
        SetStatus(message);
        Debug.LogWarning(message);
    }

    private static void ValidateHome(HomeController controller, List<string> issues)
    {
        if (controller.playStateText == null) issues.Add("HomeController.playStateText is not assigned.");
        if (controller.playDungeonButton == null) issues.Add("HomeController.playDungeonButton is not assigned.");
    }

    private static void ValidateTask(TaskController controller, List<string> issues)
    {
        if (controller.statusText == null) issues.Add("TaskController.statusText is not assigned.");
    }

    private static void ValidateDungeon(DungeonRunManager controller, List<string> issues)
    {
        if (controller.player == null) issues.Add("DungeonRunManager.player is not assigned.");
        if (controller.spawner == null) issues.Add("DungeonRunManager.spawner is not assigned.");
        if (controller.runStatusText == null) issues.Add("DungeonRunManager.runStatusText is not assigned.");
        if (controller.floorText == null) issues.Add("DungeonRunManager.floorText is not assigned.");
        if (controller.hpText == null) issues.Add("DungeonRunManager.hpText is not assigned.");

        if (controller.useAuthoritativeFunctions && controller.functionClient == null)
        {
            issues.Add("DungeonRunManager.useAuthoritativeFunctions is enabled but functionClient is not assigned.");
        }
    }

    private static void ValidateUpgrade(UpgradeTreeController controller, List<string> issues)
    {
        if (controller.summaryText == null) issues.Add("UpgradeTreeController.summaryText is not assigned.");
        if (controller.nodesText == null) issues.Add("UpgradeTreeController.nodesText is not assigned.");
        if (controller.nodeIdInput == null) issues.Add("UpgradeTreeController.nodeIdInput is not assigned.");
    }

    private static void ValidateCosmetics(CosmeticsController controller, List<string> issues)
    {
        if (controller.shardsText == null) issues.Add("CosmeticsController.shardsText is not assigned.");
        if (controller.equippedText == null) issues.Add("CosmeticsController.equippedText is not assigned.");
        if (controller.catalogText == null) issues.Add("CosmeticsController.catalogText is not assigned.");
        if (controller.cosmeticIdInput == null) issues.Add("CosmeticsController.cosmeticIdInput is not assigned.");
    }

    private void SetStatus(string message)
    {
        if (statusText != null)
        {
            statusText.text = message;
        }

        Debug.Log(message);
    }
}
