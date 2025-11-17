using System;
using System.Linq;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.UI;
using Unity.Services.Core;
using Unity.Services.Authentication;
using Unity.Services.Leaderboards;
using Unity.Services.Leaderboards.Models;

public class LeaderboardUIManager : MonoBehaviour
{

    [Header("LeaderBoard UI")]

    [SerializeField] GameObject leaderboardPanel;

    [SerializeField] Button openButton;

    [SerializeField] Button closeButton;


    [Header("UGS")]
    [SerializeField] string leaderboardId = "player_level"; // dashboard ID

    [Header("UI")]
    [SerializeField] Transform content; // the VerticalLayoutGroup container
    [SerializeField] GameObject firstPrefab;
    [SerializeField] GameObject secondPrefab;
    [SerializeField] GameObject thirdPrefab;
    [SerializeField] GameObject normalPrefab;
    [SerializeField] int pageSize = 20;

    [Header("Options")]
    [SerializeField] bool showMyRowIfOutsideTop = true;

    bool ready;

    async void Awake()
    {
        await InitAndAuth();

        if (openButton) openButton.onClick.AddListener(() => leaderboardPanel.SetActive(true));
        if (closeButton) closeButton.onClick.AddListener(() => leaderboardPanel.SetActive(false));
    }

    async Task InitAndAuth()
    {
        if (UnityServices.State != ServicesInitializationState.Initialized)
            await UnityServices.InitializeAsync();

        if (!AuthenticationService.Instance.IsSignedIn)
            await AuthenticationService.Instance.SignInAnonymouslyAsync();

        ready = true;
        Debug.Log("Signed in as " + AuthenticationService.Instance.PlayerId);

        await RefreshTop();
    }

    // Call after sign-up or when player sets their display name
    public async Task OnSignupSetNameAndAddToBoard(string playerDisplayName, int startingLevel = 0)
    {
        if (!ready) await InitAndAuth();

        try
        {
            if (!string.IsNullOrWhiteSpace(playerDisplayName))
                await AuthenticationService.Instance.UpdatePlayerNameAsync(playerDisplayName);

            // Add them to the board with their current/highest level (0 or 1 is fine).
            await LeaderboardsService.Instance.AddPlayerScoreAsync(leaderboardId, startingLevel);
            Debug.Log("Player added to leaderboard with level " + startingLevel);
        }
        catch (Exception e)
        {
            Debug.LogError("OnSignupSetNameAndAddToBoard failed: " + e);
        }
    }

    // Call this when you open the leaderboard screen, or after posting a new level
    public async Task RefreshTop()
    {
        if (!ready) await InitAndAuth();

        // clear old entries
        for (int i = content.childCount - 1; i >= 0; i--)
            Destroy(content.GetChild(i).gameObject);

        try
        {
            var page = await LeaderboardsService.Instance.GetScoresAsync(
                leaderboardId,
                new GetScoresOptions { Limit = pageSize, Offset = 0 });

            // top N
            for (int i = 0; i < page.Results.Count; i++)
            {
                var entry = page.Results[i];
                var prefab = i == 0 ? firstPrefab : i == 1 ? secondPrefab : i == 2 ? thirdPrefab : normalPrefab;
                CreateRow(prefab, i, SafeName(entry.PlayerName, entry.PlayerId), (long) entry.Score, entry.PlayerId);
            }

            // if requested, also show "you" even if outside top
            if (showMyRowIfOutsideTop)
            {
                var me = await LeaderboardsService.Instance.GetPlayerScoreAsync(leaderboardId);
                if (me != null && (me.Rank < 0 || me.Rank >= pageSize))
                {
                    // optional: a subtle separator row (use a thin UI element or skip)
                    // Instantiate(separatorPrefab, content); // if you have one

                    CreateRow(normalPrefab, me.Rank, SafeName(me.PlayerName, AuthenticationService.Instance.PlayerId), (long) me.Score, me.PlayerId);
                }
            }
        }
        catch (Exception e)
        {
            Debug.LogError("RefreshTop failed: " + e);
        }
    }

    // Call when the player reaches a new personal max level
    // public async Task SubmitHighestLevel(int level)
    // {
    //     if (!ready) await InitAndAuth();

    //     try
    //     {
    //         var res = await LeaderboardsService.Instance.AddPlayerScoreAsync(leaderboardId, level);
    //         Debug.Log($"Posted level {level}. Rank: {res.Rank}, Best: {res.BestScore}");
    //     }
    //     catch (Exception e)
    //     {
    //         Debug.LogError("SubmitHighestLevel failed: " + e);
    //     }
    // }

    void CreateRow(GameObject prefab, int rankZeroBased, string name, long level, string playerId)
    {
        var go = Instantiate(prefab, content, false);
        var view = go.GetComponent<LeaderboardEntryView>();
        if (view == null)
        {
            Debug.LogError("Prefab missing LeaderboardEntryView: " + prefab.name);
            return;
        }
        view.Bind(rankZeroBased, name, level, playerId);
    }

    static string SafeName(string playerName, string playerId)
    {
        var name = string.IsNullOrWhiteSpace(playerName) ? ShortId(playerId) : playerName.Trim();
        if (name.Length > 16) name = name.Substring(0, 16) + "…";
        return name;
    }

    static string ShortId(string id)
    {
        if (string.IsNullOrEmpty(id)) return "Player";
        return id.Length <= 8 ? id : id.Substring(0, 4) + "…" + id.Substring(id.Length - 4);
    }
}
