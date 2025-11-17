using System.Threading.Tasks;
using UnityEngine;
using Unity.Services.Core;
using Unity.Services.Authentication;
using Firebase.Auth;

public class UgsAuthBootstrap : MonoBehaviour
{
    [Tooltip("If true, will sign in to UGS anonymously only when there is no Firebase user or UGS session")]
    public bool fallbackToAnonIfNoSession = true;

    async void Awake()
    {
        // 1) Initialize UGS once
        await UnityServices.InitializeAsync();

        // 2) If already signed in to UGS, we're done
        if (AuthenticationService.Instance.IsSignedIn)
        {
            Debug.Log("[UGS] Already signed in.");
            return;
        }

        // 3) Try to bind UGS to your Firebase session (optional, best practice)
        var fb = FirebaseAuth.DefaultInstance;
        var user = fb.CurrentUser;

        if (user != null && !user.IsAnonymous)
        {
            // OPTIONAL: If you configure a Firebase OIDC provider in Unity Dashboard,
            // you can sign in UGS with Firebase IdToken so identities match:
            // await AuthenticationService.Instance.SignInWithOpenIdConnectAsync("firebase", await user.TokenAsync(false));

            // If you have NOT set up OIDC, just proceed to anon or skip sign-in here.
            Debug.Log("[UGS] Firebase user present; skipping UGS anon. (Consider OIDC to link identities.)");
            return;
        }

        // 4) Fallback: anonymous UGS sign-in only if you want it
        if (fallbackToAnonIfNoSession)
        {
            await AuthenticationService.Instance.SignInAnonymouslyAsync();
            Debug.Log($"[UGS] Signed in anonymously. PlayerId={AuthenticationService.Instance.PlayerId}");
        }
        else
        {
            Debug.Log("[UGS] Not signed in (waiting for explicit sign-in).");
        }
    }
}
