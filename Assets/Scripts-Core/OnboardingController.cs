using System;
using System.Collections.Generic;
using Firebase.Firestore;
using UnityEngine;
using UnityEngine.SceneManagement;

public class OnboardingController : MonoBehaviour
{
    public async void ContinueAs(string selectedClass)
    {
        Debug.Log($"ContinueAs clicked: {selectedClass}");

        try
        {
            if (BootstrapController.Db == null || BootstrapController.User == null)
            {
                Debug.LogError("Firebase not ready. Db or User is null.");
                return;
            }

            var uid = BootstrapController.User.UserId;
            var doc = BootstrapController.Db.Collection("users").Document(uid);

            var data = new Dictionary<string, object>
            {
                { "class", selectedClass },
                { "level", 1 },
                { "xp", 0 },
                { "skillPoints", 0 },
                { "shards", 0 },
                { "createdAt", Timestamp.GetCurrentTimestamp() },
                { "updatedAt", Timestamp.GetCurrentTimestamp() }
            };

            await doc.SetAsync(data, SetOptions.MergeAll);
            Debug.Log("User doc saved. Loading HomeScene...");
            SceneManager.LoadScene("HomeScene");
        }
        catch (Exception ex)
        {
            Debug.LogError("ContinueAs failed: " + ex);
        }
    }

    public void ContinueAsWarrior() { ContinueAs("warrior"); }
    public void ContinueAsRanger() { ContinueAs("ranger"); }
    public void ContinueAsMage() { ContinueAs("mage"); }
}
