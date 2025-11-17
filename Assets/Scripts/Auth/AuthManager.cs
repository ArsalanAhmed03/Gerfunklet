using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.SceneManagement;

using Unity.Services.Core;
using Unity.Services.Authentication;
using Unity.Services.CloudSave;
using Unity.Services.CloudSave.Models; // for IDeserializable.GetAsString()
using Unity.Services.Leaderboards;

[Serializable]
public class PlayerProfile
{
    public string handle = "Player";
    public int mmr = 1000;
    public int level = 1;
    public int gold = 0;
    public long createdAtUnix;
}

public class AuthManager : MonoBehaviour
{
    [Header("Navigation")]
    public string sceneAfterLogin = "GameScene";
    public UnityEvent onLoginSuccess;
    public UnityEvent onSignUpSuccess;
    public UnityEvent onLogout;
    public UnityEvent alreadyLoggedIn;
    public UnityEvent<string> onAuthError;

    public PlayerProfile CurrentProfile { get; private set; }
    public string UgsPlayerId => AuthenticationService.Instance.PlayerId;

    const string ProfileKey = "profile";
    const string LeaderboardId = "player_level";

    // singleton guard to avoid double subscription / double init
    static AuthManager _instance;

    // profile init re-entry guard
    readonly SemaphoreSlim _profileInitLock = new(1, 1);
    bool _profileInitialized;

    async void Awake()
    {
        // ensure single instance across scene loads
        if (_instance != null && _instance != this)
        {
            Destroy(gameObject);
            return;
        }
        _instance = this;
        DontDestroyOnLoad(gameObject);

        try
        {
            await UnityServices.InitializeAsync();

            // listen once
            AuthenticationService.Instance.SignedIn += OnPlayerSignedIn;
            // await AuthenticationService.Instance.SignInAnonymouslyAsync();

            // if you want to auto-resume existing sessions, uncomment below
            if (AuthenticationService.Instance.SessionTokenExists)
                alreadyLoggedIn?.Invoke();
            else
                Debug.Log("No cached session found. Waiting for manual login.");
        }
        catch (Exception e)
        {
            onAuthError?.Invoke($"UGS Initialization Failed: {e.Message}");
            Debug.LogException(e);
        }
    }

    void OnDestroy()
    {
        // defensive unhook (service can be disposed on app quit)
        try { AuthenticationService.Instance.SignedIn -= OnPlayerSignedIn; }
        catch { /* ignore */ }
    }

    private async Task UpsertLeaderboardLevelAsync(int level)
    {
        try
        {
            await LeaderboardsService.Instance.AddPlayerScoreAsync(LeaderboardId, level);
            Debug.Log($"[LB] Upsert level={level} to '{LeaderboardId}'");
        }
        catch (Exception e)
        {
            Debug.LogError($"[LB] Upsert failed: {e.Message}");
        }
    }

    void OnPlayerSignedIn()
    {
        Debug.Log($"UGS Signed In. Player ID: {UgsPlayerId}");
        // single source of truth: profile boot happens here
        // EnsureProfileExistsAsync(AuthenticationService.Instance.PlayerName);
    }

    public async void trySilentSignIn()
    {
        try
        {
            if (AuthenticationService.Instance.SessionTokenExists)
            {
                await AuthenticationService.Instance.SignInAnonymouslyAsync();
                EnsureProfileExistsAsync(AuthenticationService.Instance.PlayerName);
                Debug.Log("Silent sign-in successful.");
            }
            else
            {
                Debug.Log("No cached session; skipping silent sign-in.");
            }
        }
        catch (Exception e)
        {
            Debug.LogError($"Silent sign-in failed: {e.Message}");
        }
    }

    // sign up with username/password; do not create profile here
    public async void SignUp(string email, string password, string nickname)
    {
        if (string.IsNullOrWhiteSpace(email) ||
            string.IsNullOrWhiteSpace(password) ||
            string.IsNullOrWhiteSpace(nickname))
        {
            onAuthError?.Invoke("All fields are required for sign up.");
            return;
        }

        try
        {

            await AuthenticationService.Instance.SignUpWithUsernamePasswordAsync(email.Trim(), password);
            await AuthenticationService.Instance.UpdatePlayerNameAsync(nickname.Trim());
            EnsureProfileExistsAsync(AuthenticationService.Instance.PlayerName);

            // do not call EnsureProfileExists here; SignedIn event will handle it once
            onSignUpSuccess?.Invoke();

        }
        catch (AuthenticationException ex)
        {
            onAuthError?.Invoke($"Sign Up Failed: {ex.Message}");
            Debug.LogException(ex);
        }
        catch (Exception e)
        {
            onAuthError?.Invoke($"Sign Up failed: {e.Message}");
            Debug.LogException(e);
        }
    }

    // login with username/password; rely on SignedIn to complete boot
    public async void Login(string usernameOrEmail, string password)
    {
        if (string.IsNullOrWhiteSpace(usernameOrEmail) || string.IsNullOrWhiteSpace(password))
        {
            onAuthError?.Invoke("Username/Email and password are required.");
            return;
        }

        try
        {
            await AuthenticationService.Instance.SignInWithUsernamePasswordAsync(usernameOrEmail.Trim(), password);
            EnsureProfileExistsAsync(AuthenticationService.Instance.PlayerName);
            // Do not call EnsureProfileExists here; the SignedIn event path will run.
        }
        catch (AuthenticationException ex)
        {
            onAuthError?.Invoke($"Login Failed: {ex.Message}");
            Debug.LogException(ex);
        }
        catch (Exception e)
        {
            onAuthError?.Invoke($"Login failed: {e.Message}");
            Debug.LogException(e);
        }
    }

    public async void SignInAnonymously()
    {
        Debug.Log("Attempting anonymous sign-in...");
        try
        {
            if (AuthenticationService.Instance.IsSignedIn)
            {
                if (!string.IsNullOrEmpty(sceneAfterLogin))
                    SceneManager.LoadScene(sceneAfterLogin);
                return;
            }

            await AuthenticationService.Instance.SignInAnonymouslyAsync();
            EnsureProfileExistsAsync(AuthenticationService.Instance.PlayerName);
            // profile init continues in SignedIn handler
        }
        catch (Exception e)
        {
            onAuthError?.Invoke(e.Message);
            Debug.LogException(e);
        }
    }

    //     public void SignOut()
    //     {
    //         try
    //         {
    //             if (AuthenticationService.Instance.IsSignedIn)
    //             {

    // #if UNITY_SERVICES_AUTHENTICATION_3_OR_NEWER
    //                 Debug.Log("Signing out with clearCredentials=true");
    //                 AuthenticationService.Instance.SignOut(clearCredentials: true);
    // #else
    //                 Debug.Log("Signing out Old");
    //                 AuthenticationService.Instance.SignOut();
    //                 AuthenticationService.Instance.ClearSessionToken();
    // #endif
    //             }

    //             Debug.Log("User signed out.");

    //             CurrentProfile = null;
    //             _profileInitialized = false; // allow re-init on next sign-in
    //             onLogout?.Invoke();
    //         }
    //         catch (Exception e)
    //         {
    //             Debug.LogException(e);
    //         }
    //     }

    public void SignOut()
    {
        try
        {
            if (AuthenticationService.Instance.SessionTokenExists)
            {

#if UNITY_SERVICES_AUTHENTICATION_3_OR_NEWER
                Debug.Log("Signing out with clearCredentials=true");
                AuthenticationService.Instance.SignOut(clearCredentials: true);
#else
                Debug.Log("Signing out Old");
                AuthenticationService.Instance.SignOut();
                AuthenticationService.Instance.ClearSessionToken();
#endif
            }

            Debug.Log("User signed out.");

            CurrentProfile = null;
            _profileInitialized = false; // allow re-init on next sign-in
            onLogout?.Invoke();
        }
        catch (Exception e)
        {
            Debug.LogException(e);
        }
    }

    // profile bootstrap guarded against duplicates
    private async void EnsureProfileExistsAsync(string defaultNickname)
    {
        try
        {
            await _profileInitLock.WaitAsync();

            if (_profileInitialized)
                return;

            Debug.Log("Loading or creating player profile after sign-in...");
            await EnsureProfileExists(defaultNickname);
            _profileInitialized = true;

            if (CurrentProfile != null)
                onLoginSuccess?.Invoke();

            await UpsertLeaderboardLevelAsync(CurrentProfile.level);

            if (!string.IsNullOrEmpty(sceneAfterLogin) &&
                SceneManager.GetActiveScene().name != sceneAfterLogin)
            {
                SceneManager.LoadScene(sceneAfterLogin);
            }
        }
        catch (Exception e)
        {
            onAuthError?.Invoke($"Profile Load/Create Failed: {e.Message}");
            Debug.LogException(e);
        }
        finally
        {
            _profileInitLock.Release();
        }
    }

    async Task EnsureProfileExists(string defaultNickname = null)
    {
        Debug.Log("Ensuring player profile exists...");
        var loaded = await LoadProfileAsync();
        if (loaded != null)
        {
            CurrentProfile = loaded;

            var playerName = AuthenticationService.Instance.PlayerName;
            if (!string.IsNullOrEmpty(playerName) && CurrentProfile.handle != playerName)
            {
                CurrentProfile.handle = playerName;
                await SaveProfileAsync(CurrentProfile);
                await UpsertLeaderboardLevelAsync(CurrentProfile.level);
            }
            return;
        }

        Debug.Log("No existing profile found. Creating a new profile.");

        string initialHandle = string.IsNullOrWhiteSpace(defaultNickname)
            ? $"Player_{UnityEngine.Random.Range(1000, 9999)}"
            : defaultNickname.Trim();

        CurrentProfile = new PlayerProfile
        {
            handle = initialHandle,
            mmr = 1000,
            level = 1,
            gold = 0,
            createdAtUnix = DateTimeOffset.UtcNow.ToUnixTimeSeconds()
        };

        await SaveProfileAsync(CurrentProfile);
    }

    public async Task SaveProfileAsync(PlayerProfile profile)
    {
        try
        {
            var json = JsonUtility.ToJson(profile);
            await CloudSaveService.Instance.Data.Player.SaveAsync(
                new Dictionary<string, object> { { ProfileKey, json } }
            );
            Debug.Log($"Profile for {profile.handle} saved.");
        }
        catch (Exception e)
        {
            onAuthError?.Invoke($"Save failed: {e.Message}");
            Debug.LogException(e);
        }
    }

    public async Task<PlayerProfile> LoadProfileAsync()
    {
        try
        {
            var res = await CloudSaveService.Instance.Data.Player.LoadAsync(new HashSet<string> { ProfileKey });
            if (res.TryGetValue(ProfileKey, out var item))
            {
                var json = item.Value.GetAsString();
                if (!string.IsNullOrEmpty(json))
                {
                    Debug.Log("Profile loaded from Cloud Save.");
                    return JsonUtility.FromJson<PlayerProfile>(json);
                }
            }
        }
        catch (Exception e)
        {
            Debug.LogWarning($"[UGS] Load profile failed (first run/new device?): {e.Message}");
        }
        return null;
    }
}
