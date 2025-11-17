using UnityEngine;
using TMPro;
using UnityEngine.UI;

public enum FriendRelationKind { Friend, OutgoingRequest, IncomingRequest }

public class FriendEntryView : MonoBehaviour
{
    [Header("Text Tags")]
    public TMP_Text nameText;   // player display name
    public TMP_Text levelText;  // e.g., "Lv. 12"

    [Header("Type-Specific UI Roots")]
    public GameObject friendPlayGroup;      // shown for Friend
    public GameObject incomingDecisionGroup; // shown for IncomingRequest (accept+reject+text inside)
    public GameObject outgoingRequestTextObj; // shown for OutgoingRequest ("Request Sent")

    [Header("Buttons (optional wiring)")]
    public Button playButton;    // inside friendPlayGroup
    public Button acceptButton;  // inside incomingDecisionGroup
    public Button rejectButton;  // inside incomingDecisionGroup

    string _playerId;
    FriendRelationKind _kind;

    public void Setup(string playerId, string displayName, string levelDisplay, FriendRelationKind kind)
    {
        _playerId = playerId;
        _kind = kind;

        if (nameText)  nameText.text  = string.IsNullOrWhiteSpace(displayName) ? "(unknown)" : displayName;
        if (levelText) levelText.text = string.IsNullOrWhiteSpace(levelDisplay) ? "Lv. --" : levelDisplay;

        // default hide all groups
        if (friendPlayGroup)        friendPlayGroup.SetActive(false);
        if (incomingDecisionGroup)  incomingDecisionGroup.SetActive(false);
        if (outgoingRequestTextObj) outgoingRequestTextObj.SetActive(false);

        switch (kind)
        {
            case FriendRelationKind.Friend:
                if (friendPlayGroup) friendPlayGroup.SetActive(true);
                break;

            case FriendRelationKind.OutgoingRequest:
                if (outgoingRequestTextObj) outgoingRequestTextObj.SetActive(true);
                break;

            case FriendRelationKind.IncomingRequest:
                if (incomingDecisionGroup) incomingDecisionGroup.SetActive(true);
                break;
        }
    }

    public string PlayerId => _playerId;
    public FriendRelationKind Kind => _kind;
}
