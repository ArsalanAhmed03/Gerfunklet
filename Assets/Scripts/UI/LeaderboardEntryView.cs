using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Unity.Services.Friends;
using Unity.Services.Friends.Exceptions;
using System.Threading.Tasks;
using Unity.Services.Friends.Models;

public class LeaderboardEntryView : MonoBehaviour
{
    public TMP_Text rankText;
    public TMP_Text nameText;
    public TMP_Text levelText;
    public Button addFriendButton;

    public void Bind(int rankZeroBased, string displayName, long level, string playerId)
    {
        rankText.text = (rankZeroBased + 1).ToString();
        nameText.text = displayName;
        levelText.text = "Level " + level;
        if (addFriendButton != null)
        {
            addFriendButton.onClick.RemoveAllListeners();
            addFriendButton.onClick.AddListener(() =>
            {
                FindFirstObjectByType<FriendsManager>().OnRequestSentByID(playerId);
            });
        }
    }
}
