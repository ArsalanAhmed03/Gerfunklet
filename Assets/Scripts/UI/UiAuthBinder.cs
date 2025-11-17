using TMPro;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
using Unity.Services.Authentication;
using Unity.VisualScripting;

public class UiAuthBinder : MonoBehaviour
{
    public AuthManager auth;

    [Header("Start")]
    public Button startButton;
    public Button startLoginButton;

    public Button startLogoutButton;


    // --- Login Panel Fields ---
    [Header("Login")]
    // This input will now strictly be used for the UGS 'Username'
    public TMP_InputField loginUsername;
    // This input is the password for the UGS login
    public TMP_InputField loginPassword;
    public Button buttonLogin;
    public Button buttonLoginAnon;
    public Button needAccountButton;
    public Button closeLoginButton;


    // --- Signup Panel Fields ---
    [Header("Signup")]
    // This input is for the UGS 'Username'
    public TMP_InputField signupUsername;
    // This input is the password for the UGS signup
    public TMP_InputField signupPassword;
    // This input is for the in-game display name (PlayerProfile.handle)
    public TMP_InputField signupNickname;
    public Button buttonSignup;
    public Button alreadyHaveAccountButton;

    public Button closeSignupButton;

    // --- UI State Fields ---
    [Header("Panels")]
    public GameObject panelLogin;
    public GameObject panelSignup;

    [Header("Feedback")]
    public TMP_Text feedback;

    void Awake()
    {
        // --- Event Subscriptions ---
        if (auth != null)
        {
            auth.onAuthError.AddListener(SetFeedback);
            auth.onLoginSuccess.AddListener(() => SetFeedback("Signed in."));
            auth.onSignUpSuccess.AddListener(() => { SetFeedback("Account created! You can now log in."); ShowLogin(true); });
            auth.onLogout.AddListener(() => SetFeedback("Signed out."));
            auth.alreadyLoggedIn.AddListener(userLoggedInAlready);
        }

        // --- Button Listeners ---
        if (buttonLogin) buttonLogin.onClick.AddListener(ClickLogin);
        if (buttonLoginAnon) buttonLoginAnon.onClick.AddListener(ClickAnon);
        if (needAccountButton) needAccountButton.onClick.AddListener(ShowSignup);
        if (buttonSignup) buttonSignup.onClick.AddListener(ClickSignup);
        if (alreadyHaveAccountButton) alreadyHaveAccountButton.onClick.AddListener(BackToLogin);
        if (startLoginButton) startLoginButton.onClick.AddListener(() => { ShowLogin(true); });
        if (closeLoginButton) closeLoginButton.onClick.AddListener(() => { panelLogin.SetActive(false); });
        if (closeSignupButton) closeSignupButton.onClick.AddListener(() => { panelSignup.SetActive(false); });
        if (startLogoutButton) startLogoutButton.onClick.AddListener(() =>
        {
            Debug.Log("Signing out Button Clicked.");
            auth.SignOut();
            startButton.gameObject.SetActive(false);
            startLogoutButton.gameObject.SetActive(false);
            startLoginButton.gameObject.SetActive(true);
        });

        if (startButton) startButton.onClick.AddListener(() =>
        {
            auth.trySilentSignIn();
        });

        // startButton.gameObject.SetActive(true);
        // startLogoutButton.gameObject.SetActive(true);
        // startLoginButton.gameObject.SetActive(false);

        panelLogin.SetActive(false);
        panelSignup.SetActive(false);
    }

    public void userLoggedInAlready()
    {
        Debug.Log("User already logged in.");
        startButton.gameObject.SetActive(true);
        startLogoutButton.gameObject.SetActive(true);
        startLoginButton.gameObject.SetActive(false);
    }

    // -------------------------------------------------------------------
    // Button Click Handlers
    // -------------------------------------------------------------------

    public void ClickLogin()
    {
        if (auth == null) return;

        string username = loginUsername ? loginUsername.text : string.Empty;
        string password = loginPassword ? loginPassword.text : string.Empty;

        // Calls AuthManager.Login(string usernameOrEmail, string password)
        Debug.Log($"Logging in with username: {username}, Password: {password}");
        auth.Login(username, password);
    }

    public void ClickSignup()
    {
        if (auth == null) return;

        string username = signupUsername ? signupUsername.text : string.Empty;
        string password = signupPassword ? signupPassword.text : string.Empty;
        string nickname = signupNickname ? signupNickname.text : string.Empty;

        // Calls AuthManager.SignUp(string username, string password, string nickname)
        // The 'username' is the unique identifier for UGS Auth.
        // The 'nickname' is the display name saved to Cloud Save.
        Debug.Log($"Signing up with username: {username}, nickname: {nickname}, Password: {password}");
        auth.SignUp(username, password, nickname);
    }

    public void ClickAnon()
    {
        if (auth == null) return;
        auth.SignInAnonymously();
    }

    // -------------------------------------------------------------------
    // UI Logic
    // -------------------------------------------------------------------

    public void ShowLogin(bool show)
    {
        if (panelLogin) panelLogin.SetActive(show);
        if (panelSignup) panelSignup.SetActive(!show);
        SetFeedback(string.Empty); // Clear feedback when switching panels
    }

    public void ShowSignup() => ShowLogin(false);
    public void BackToLogin() => ShowLogin(true);

    void SetFeedback(string msg)
    {
        if (feedback) feedback.text = msg;
    }
}