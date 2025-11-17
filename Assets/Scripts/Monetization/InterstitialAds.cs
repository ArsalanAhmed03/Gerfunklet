using UnityEngine;
using UnityEngine.Advertisements;

public class InterstitialAds : MonoBehaviour, IUnityAdsLoadListener, IUnityAdsShowListener
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

    public void LoadInterstitialAd()
    {
        Advertisement.Load(adUnitId, this);
    }

    public void ShowInterstitialAd()
    {
        if (Advertisement.isSupported)
        {
            Advertisement.Show(adUnitId, this);
            LoadInterstitialAd();
        }
    }

    public void OnUnityAdsAdLoaded(string placementId)
    {
        Debug.Log("Interstitial ad loaded successfully.");
    }

    public void OnUnityAdsFailedToLoad(string placementId, UnityAdsLoadError error, string message)
    {
        Debug.Log($"Failed to load interstitial ad: {error.ToString()} - {message}");
    }

    public void OnUnityAdsShowFailure(string placementId, UnityAdsShowError error, string message)
    {
        Debug.Log($"Failed to show interstitial ad: {error.ToString()} - {message}");
    }

    public void OnUnityAdsShowStart(string placementId)
    {
        Debug.Log("Interstitial ad started.");
    }

    public void OnUnityAdsShowClick(string placementId)
    {
        Debug.Log("Interstitial ad clicked.");
    }

    public void OnUnityAdsShowComplete(string placementId, UnityAdsShowCompletionState showCompletionState)
    {
        Debug.Log("Interstitial ad completed.");
    }
}
