using UnityEngine;
using UnityEngine.Advertisements;
public class AdsIntialization : MonoBehaviour, IUnityAdsInitializationListener
{

    [SerializeField] private string androidGameId = "5986585";
    [SerializeField] private string iosGameId = "5986584";
    [SerializeField] bool testMode = true;

    private string gameId;

    private void Awake()
    {
#if UNITY_IOS
            gameId = iosGameId;
#elif UNITY_ANDROID
        gameId = androidGameId;
#elif UNITY_EDITOR
            gameId = androidGameId;
#endif
        if (!Advertisement.isInitialized && Advertisement.isSupported)
        {
            Advertisement.Initialize(gameId, testMode, this);
        }
    }

    public void OnInitializationComplete()
    {
        Debug.Log("Unity Ads initialization complete.");
    }

    public void OnInitializationFailed(UnityAdsInitializationError error, string message)
    {
        Debug.Log($"Unity Ads Initialization Failed: {error.ToString()} - {message}");
    }
}
