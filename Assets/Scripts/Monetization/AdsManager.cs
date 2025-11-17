using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class AdsManager : MonoBehaviour
{

    public AdsIntialization adsIntialization;
    public InterstitialAds interstitialAds;
    public RewardedAds rewardedAds;
    public static AdsManager Instance;

    [SerializeField] private Button rewardedAdButton;

    [SerializeField] private Button interstitialAdButton;


    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);

            interstitialAds.LoadInterstitialAd();
            rewardedAds.LoadRewardedAds();

            rewardedAdButton.onClick.AddListener(() => {
                rewardedAds.ShowRewardedAd();
            });

            interstitialAdButton.onClick.AddListener(() => {
                interstitialAds.ShowInterstitialAd();
            });

        }
        else
        {
            Destroy(gameObject);
        }
    }
}