using UnityEngine;
using TMPro;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

using Unity.Services.Core;
using Unity.Services.Authentication;
using Unity.Services.CloudSave;
using Unity.Services.CloudSave.Models; 

// NOTE: You must copy the PlayerProfile class into this script 
// or ensure it is accessible via a shared namespace/script.
// [Serializable]
// public class PlayerProfile
// {
//     public string handle = "Player";
//     public int mmr = 1000;
//     public int level = 1;
//     public int gold = 0;
//     public long createdAtUnix;
// }

public class PlayerStatsLoader : MonoBehaviour
{
    [Header("UI Elements")]
    public TMP_Text handleText;
    public TMP_Text levelText;
    public TMP_Text mmrText;
    public TMP_Text goldText;
    public TMP_Text playerIdText;
    public TMP_Text createdAtText;

    const string ProfileKey = "profile";

    private void Start()
    {
        // Start the async process
        LoadAndDisplayStats();
    }

    async void LoadAndDisplayStats()
    {
        try
        {
            // 1. Ensure UGS is initialized (safe to call multiple times)
            if (UnityServices.State != ServicesInitializationState.Initialized)
            {
                await UnityServices.InitializeAsync();
            }

            // 2. Check Authentication Status
            if (!AuthenticationService.Instance.IsSignedIn)
            {
                // This scenario should NOT happen if the AuthManager logic is correct,
                // but it's a necessary check.
                DisplayError("Error: Player is not signed in to UGS.");
                return;
            }
            
            // 3. Load the data directly using the static CloudSaveService instance
            PlayerProfile profile = await LoadProfileAsync();

            if (profile != null)
            {
                DisplayStats(profile);
            }
            else
            {
                DisplayError("Error: Profile data not found in Cloud Save.");
            }
        }
        catch (Exception e)
        {
            Debug.LogError($"Fatal Error during stats load: {e.Message}");
            DisplayError("CONNECTION FAILED");
        }
    }

    private async Task<PlayerProfile> LoadProfileAsync()
    {
        try
        {
            var res = await CloudSaveService.Instance.Data.Player.LoadAsync(new HashSet<string> { ProfileKey });

            if (res.TryGetValue(ProfileKey, out var item))
            {
                // Correctly extract the JSON string using GetAsString()
                string json = item.Value.GetAsString(); 

                if (!string.IsNullOrEmpty(json))
                {
                    return JsonUtility.FromJson<PlayerProfile>(json);
                }
            }
        }
        catch (Exception e)
        {
            // Cloud Save can throw exceptions for network errors or if the key truly doesn't exist
            Debug.LogWarning($"[Cloud Save] Load failed for key '{ProfileKey}': {e.Message}");
        }
        return null;
    }

    private void DisplayStats(PlayerProfile profile)
    {
        if (profile == null)
        {
            DisplayError("Error: No profile");
            return;
        }

        if (handleText != null) handleText.text = $"{profile.handle ?? "Unknown"}";
        if (levelText != null) levelText.text = $"{profile.level}";
        if (mmrText != null) mmrText.text = $"{profile.mmr}";
        if (goldText != null) goldText.text = $"{profile.gold}";

        string playerId = null;
        if (AuthenticationService.Instance != null && AuthenticationService.Instance.IsSignedIn)
        {
            playerId = AuthenticationService.Instance.PlayerId;
        }
        if (playerIdText != null) playerIdText.text = $"{playerId ?? "N/A"}";

        if (createdAtText != null)
        {
            if (profile.createdAtUnix > 0)
            {
                try
                {
                    DateTimeOffset dto = DateTimeOffset.FromUnixTimeSeconds(profile.createdAtUnix);
                    createdAtText.text = $"{dto.ToLocalTime():yyyy-MM-dd HH:mm}";
                }
                catch (ArgumentOutOfRangeException)
                {
                    createdAtText.text = "Created: Invalid timestamp";
                }
            }
            else
            {
                createdAtText.text = "Created: N/A";
            }
        }
    }

    private void DisplayError(string message)
    {
        if (handleText != null) handleText.text = message;
        if (levelText != null) levelText.text = "";
        if (mmrText != null) mmrText.text = "";
        if (goldText != null) goldText.text = "";
        if (playerIdText != null) playerIdText.text = "";
        if (createdAtText != null) createdAtText.text = "";
    }
}