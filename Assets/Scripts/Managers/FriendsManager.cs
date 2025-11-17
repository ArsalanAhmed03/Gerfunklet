using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;
using Unity.Services.Core;
using Unity.Services.Authentication;
using Unity.Services.Friends;
using Unity.Services.Friends.Models;
using UnityEngine.UI;
using Unity.Services.Friends.Exceptions;


public class FriendsManager : MonoBehaviour
{

    [Header("Friend UI")]

    public GameObject friendsPanel;

    public Button openFriendsButton;
    public Button closeFriendsButton;


    [Header("Content Root (Vertical Layout Group parent)")]
    public Transform listContent;

    [Header("Three Alternating Row Prefabs (each has FriendEntryView)")]
    public GameObject prefab1;
    public GameObject prefab2;
    public GameObject prefab3;

    [Header("Optional Controls")]
    public TMPro.TMP_InputField addByIdInput;       // for manual “send request by ID”
    public UnityEngine.UI.Button sendRequestButton; // wires to FriendsService.AddFriendAsync
    public UnityEngine.UI.Button refreshButton;
    public float autoRefreshSeconds = 10f;

    // If you know friends' levels from your backend, fill this before RefreshNow()
    public Dictionary<string, int> KnownLevels = new();

    readonly List<GameObject> _spawned = new();
    int _rowIndex;

    async void Awake()
    {

        if (UnityServices.State != ServicesInitializationState.Initialized)
            await UnityServices.InitializeAsync();

        if (!AuthenticationService.Instance.IsSignedIn)
            await AuthenticationService.Instance.SignInAnonymouslyAsync();

        await FriendsService.Instance.InitializeAsync();

        if (sendRequestButton) sendRequestButton.onClick.AddListener(OnSendRequestClicked);
        if (refreshButton) refreshButton.onClick.AddListener(() => _ = RefreshAsync());

        await RefreshAsync();

        if (autoRefreshSeconds > 0f)
            InvokeRepeating(nameof(RefreshNow), autoRefreshSeconds, autoRefreshSeconds);



        if (closeFriendsButton) closeFriendsButton.onClick.AddListener(() => friendsPanel.SetActive(false));
        if (openFriendsButton) openFriendsButton.onClick.AddListener(() => friendsPanel.SetActive(true));
    }

    void OnEnable()
    {
        AuthenticationService.Instance.SignedIn += OnSignedIn;
    }

    void OnDisable()
    {
        AuthenticationService.Instance.SignedIn -= OnSignedIn;
    }

    async void OnSignedIn()
    {
        await RefreshAsync();
    }

    public async void RefreshNow() => await RefreshAsync();

    async Task RefreshAsync()
    {
        try
        {
            var friends = FriendsService.Instance.Friends;
            var outgoing = FriendsService.Instance.OutgoingFriendRequests;
            var incoming = FriendsService.Instance.IncomingFriendRequests;

            Rebuild(friends, outgoing, incoming);
        }
        catch (Exception e)
        {
            Debug.LogError("[Friends] Refresh failed: " + e.Message);
        }
    }

    void Rebuild(IReadOnlyList<Relationship> friends, IReadOnlyList<Relationship> outgoing, IReadOnlyList<Relationship> incoming)
    {
        foreach (var go in _spawned) Destroy(go);
        _spawned.Clear();
        _rowIndex = 0;

        // Order: friends → outgoing → incoming (change if you prefer)
        foreach (var r in friends)
        {
            Debug.Log("Friend: " + r.Member.Profile.Name);
            _ = SpawnRowAsync(r.Member, FriendRelationKind.Friend);
        }

        foreach (var r in outgoing)
        {
            Debug.Log("Outgoing Request: " + r.Member.Profile.Name);
            _ = SpawnRowAsync(r.Member, FriendRelationKind.OutgoingRequest);
        }

        foreach (var r in incoming)
        {
            Debug.Log("Incoming Request: " + r.Member.Profile.Name);
            _ = SpawnRowAsync(r.Member, FriendRelationKind.IncomingRequest);
        }
    }

    async Task SpawnRowAsync(Member other, FriendRelationKind kind)
    {
        if (!listContent) return;

        var prefab = PickCycledPrefab();
        if (!prefab) return;

        var go = Instantiate(prefab, listContent);
        _spawned.Add(go);

        var view = go.GetComponent<FriendEntryView>();
        if (!view)
        {
            Debug.LogWarning("Row prefab missing FriendEntryView.");
            return;
        }

        string displayName = other.Profile?.Name;
        string levelStr = "Lv. --";
        if (KnownLevels != null && KnownLevels.TryGetValue(other.Id, out var lvl))
            levelStr = $"Lv. {lvl}";

        view.Setup(other.Id, displayName, levelStr, kind);

        // wire buttons for incoming accept/reject
        if (kind == FriendRelationKind.IncomingRequest)
        {
            view.friendPlayGroup.SetActive(false);
            view.outgoingRequestTextObj.SetActive(false);
            view.incomingDecisionGroup.SetActive(true);
            if (view.acceptButton)
                view.acceptButton.onClick.AddListener(() => _ = AcceptAsync(other.Id));

            if (view.rejectButton)
                view.rejectButton.onClick.AddListener(() => _ = RejectAsync(other.Id));
        }

        // optional play button for friends – currently no lobby, just a log or your callback later
        if (kind == FriendRelationKind.Friend && view.playButton)
        {
            view.friendPlayGroup.SetActive(true);
            view.outgoingRequestTextObj.SetActive(false);
            view.incomingDecisionGroup.SetActive(false);
            view.playButton.onClick.AddListener(() =>
            {
                Debug.Log($"Play clicked for friend {displayName} ({other.Id})");
                // plug any local action here (no lobby calls as requested)
            });
        }

        if (kind == FriendRelationKind.OutgoingRequest)
        {
            view.friendPlayGroup.SetActive(false);
            view.outgoingRequestTextObj.SetActive(true);
            view.incomingDecisionGroup.SetActive(false);
        }

        _rowIndex++;
        await Task.Yield();
    }

    GameObject PickCycledPrefab()
    {
        int mod = _rowIndex % 3;
        return mod switch
        {
            0 => prefab1 ? prefab1 : (prefab2 ? prefab2 : prefab3),
            1 => prefab2 ? prefab2 : (prefab3 ? prefab3 : prefab1),
            _ => prefab3 ? prefab3 : (prefab1 ? prefab1 : prefab2),
        };
    }

    async Task AcceptAsync(string fromPlayerId)
    {
        try { await FriendsService.Instance.AddFriendAsync(fromPlayerId); }
        catch (Exception e) { Debug.LogError("[Friends] Accept failed: " + e.Message); }
        finally { await RefreshAsync(); }
    }

    async Task RejectAsync(string fromPlayerId)
    {
        try { await FriendsService.Instance.DeleteFriendAsync(fromPlayerId); }
        catch (Exception e) { Debug.LogError("[Friends] Reject failed: " + e.Message); }
        finally { await RefreshAsync(); }
    }

    public void OnSendRequestClicked()
    {
        var id = addByIdInput ? addByIdInput.text?.Trim() : null;
        if (string.IsNullOrEmpty(id)) return;
        _ = SendFriendRequest(id);
    }

    public void OnRequestSentByID(string targetPlayerId)
    {
        _ = SendRequestAsync(targetPlayerId);
    }

    async Task SendRequestAsync(string targetPlayerId)
    {
        try { await FriendsService.Instance.AddFriendAsync(targetPlayerId); }
        catch (Exception e) { Debug.LogError("[Friends] Send request failed: " + e.Message); }
        finally { await RefreshAsync(); }
    }

    async Task<bool> SendFriendRequest(string playerName)
    {
        try
        {
            //We add the friend by name in this sample but you can also add a friend by ID using AddFriendAsync
            var relationship = await FriendsService.Instance.AddFriendByNameAsync(playerName);
            Debug.Log($"Friend request sent to {playerName}.");
            //If both players send friend request to each other, their relationship is changed to Friend.
            return relationship.Type is RelationshipType.FriendRequest or RelationshipType.Friend;
        }
        catch (FriendsServiceException e)
        {
            Debug.Log($"Failed to Request {playerName} - {e}.");
            return false;
        }
    }
}
