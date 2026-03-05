using System.Threading.Tasks;
using Firebase;
using Firebase.Auth;
using Firebase.Firestore;
using UnityEngine;
using UnityEngine.SceneManagement;

public class BootstrapController : MonoBehaviour
{
    public static FirebaseAuth Auth { get; private set; }
    public static FirebaseFirestore Db { get; private set; }
    public static FirebaseUser User { get; private set; }

    private async void Start()
    {
        await InitializeFirebaseAsync();
        await SignInAnonymouslyIfNeededAsync();
        await RouteAsync();
    }

    private async Task InitializeFirebaseAsync()
    {
        var status = await FirebaseApp.CheckAndFixDependenciesAsync();
        if (status != DependencyStatus.Available)
        {
            Debug.LogError("Firebase dependencies not available: " + status);
            return;
        }

        Auth = FirebaseAuth.DefaultInstance;
        Db = FirebaseFirestore.DefaultInstance;
    }

    private async Task SignInAnonymouslyIfNeededAsync()
    {
        if (Auth == null) return;

        if (Auth.CurrentUser != null)
        {
            User = Auth.CurrentUser;
            Debug.Log("Already signed in: " + User.UserId);
            return;
        }

        var result = await Auth.SignInAnonymouslyAsync();
        User = result.User;
        Debug.Log("Signed in anonymously: " + User.UserId);
    }

    private async Task RouteAsync()
    {
        if (Db == null || User == null) return;

        var userDoc = Db.Collection("users").Document(User.UserId);
        var snap = await userDoc.GetSnapshotAsync();

        SceneManager.LoadScene(snap.Exists ? "HomeScene" : "OnboardingScene");
    }
}
