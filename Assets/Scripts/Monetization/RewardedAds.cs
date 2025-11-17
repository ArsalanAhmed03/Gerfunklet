using UnityEngine;
using UnityEngine.Advertisements;

public class RewardedAds : MonoBehaviour, IUnityAdsLoadListener, IUnityAdsShowListener
{
    [SerializeField] private string androidAdUnitId;
    [SerializeField] private string iosAdUnitId;
    private string adUnitId;

    private void Awake()
    {
#if UNITY_IOS
        adUnitId = iosAdUnitId;
#elif UNITY_ANDROID
        adUnitId = androidAdUnitId;
#elif UNITY_EDITOR
        adUnitId = androidAdUnitId;
#endif

    }

    public void LoadRewardedAds()
    {
        Advertisement.Load(adUnitId, this);
    }

    public void ShowRewardedAd()
    {
        if (Advertisement.isSupported)
        {
            Advertisement.Show(adUnitId, this);
            LoadRewardedAds();
        }
    }

    public void OnUnityAdsAdLoaded(string placementId)
    {
        Debug.Log("Rewareded ad loaded successfully.");
    }

    public void OnUnityAdsFailedToLoad(string placementId, UnityAdsLoadError error, string message)
    {
        Debug.Log($"Failed to load rewarded ad: {error.ToString()} - {message}");
    }

    public void OnUnityAdsShowFailure(string placementId, UnityAdsShowError error, string message)
    {
        Debug.Log($"Failed to show rewarded ad: {error.ToString()} - {message}");
    }

    public void OnUnityAdsShowStart(string placementId)
    {
        Debug.Log("Rewarded ad started.");
    }

    public void OnUnityAdsShowClick(string placementId)
    {
        Debug.Log("Rewarded ad clicked.");
    }

    public void OnUnityAdsShowComplete(string placementId, UnityAdsShowCompletionState showCompletionState)
    {
        if(placementId == adUnitId && showCompletionState == UnityAdsShowCompletionState.COMPLETED)
        {
            Debug.Log("Reward the user for watching the ad.");
        }
    }
}
