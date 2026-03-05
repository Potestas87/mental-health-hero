using System;
using System.Collections.Generic;
using Firebase.Firestore;
using UnityEngine;

public class HomeDebugData : MonoBehaviour
{
    public async void SaveTestData()
    {
        if (BootstrapController.Db == null || BootstrapController.User == null)
        {
            Debug.LogError("Firebase not ready.");
            return;
        }

        var uid = BootstrapController.User.UserId;
        var doc = BootstrapController.Db.Collection("users").Document(uid);

        var data = new Dictionary<string, object>
        {
            { "level", 1 },
            { "xp", 0 },
            { "skillPoints", 0 },
            { "shards", 0 },
            { "class", "warrior" }, // temp default for day 1
            { "createdAt", Timestamp.GetCurrentTimestamp() },
            { "updatedAt", Timestamp.GetCurrentTimestamp() }
        };

        await doc.SetAsync(data, SetOptions.MergeAll);
        Debug.Log("Saved test user document.");
    }

    public async void LoadTestData()
    {
        if (BootstrapController.Db == null || BootstrapController.User == null)
        {
            Debug.LogError("Firebase not ready.");
            return;
        }

        var uid = BootstrapController.User.UserId;
        var doc = BootstrapController.Db.Collection("users").Document(uid);
        var snap = await doc.GetSnapshotAsync();

        if (!snap.Exists)
        {
            Debug.Log("No user document found.");
            return;
        }

        Debug.Log("Loaded user data:");
        foreach (var kv in snap.ToDictionary())
        {
            Debug.Log($"{kv.Key}: {kv.Value}");
        }
    }
}
