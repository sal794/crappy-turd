using UnityEngine;
using LootLocker;
using LootLocker.Requests;
using System.Text.RegularExpressions;

public class LeaderboardManager : MonoBehaviour
{
    public static LeaderboardManager Instance { get; private set; }

    private const string LeaderboardKey = "ct2000_highscores";
    private const string DailyLeaderboardKey = "ct2000_daily";
    private const string GuestNamePrefKey = "CT2000_PlayerName";
    private const string AccountEmailPrefKey = "CT2000_AccountEmail";
    private const string AccountPassPrefKey = "CT2000_AccountPass";
    private const string AccountDisplayNamePrefKey = "CT2000_AccountDisplayName";

    public bool SessionActive { get; private set; }
    public bool IsGuest { get; private set; } = true;
    public string PlayerName { get; private set; }
    public string PlayerId { get; private set; }

    public event System.Action OnSessionReady;
    public event System.Action OnAccountStateChanged;

    private void Awake()
    {
        if (Instance != null) { Destroy(gameObject); return; }
        Instance = this;
        DontDestroyOnLoad(gameObject);

        if (!PlayerPrefs.HasKey(GuestNamePrefKey))
            PlayerPrefs.SetString(GuestNamePrefKey, GenerateRandomName());

        // Set PlayerName immediately so GameManager.Start() can read it during UI creation.
        // It will be overwritten once the async session confirms the real name.
        string savedAccount = PlayerPrefs.GetString(AccountDisplayNamePrefKey, "");
        PlayerName = !string.IsNullOrEmpty(savedAccount)
            ? savedAccount
            : PlayerPrefs.GetString(GuestNamePrefKey);
    }

    private void Start()
    {
        LootLockerConfig.current.apiKey = "dev_b813ee9f33a54e3a995d3b787b334c9e";
        LootLockerConfig.current.domainKey = "skereuxp";
        LootLockerConfig.current.game_version = "1.0.0";

        string savedEmail = PlayerPrefs.GetString(AccountEmailPrefKey, "");
        string savedPass = PlayerPrefs.GetString(AccountPassPrefKey, "");

        if (!string.IsNullOrEmpty(savedEmail) && !string.IsNullOrEmpty(savedPass))
            AttemptAutoLogin(savedEmail, savedPass);
        else
            StartGuestSession();
    }

    private void StartGuestSession()
    {
        IsGuest = true;
        PlayerName = PlayerPrefs.GetString(GuestNamePrefKey);
        LootLockerSDKManager.StartGuestSession((response) =>
        {
            if (!response.success) { Debug.LogWarning("Guest session failed: " + response.text); return; }
            SessionActive = true;
            PlayerId = response.player_id.ToString();
            LootLockerSDKManager.SetPlayerName(PlayerName, (_) => OnSessionReady?.Invoke());
        });
    }

    private void AttemptAutoLogin(string email, string pass)
    {
        LootLockerSDKManager.WhiteLabelLogin(email, pass, true, (loginResp) =>
        {
            if (!loginResp.success)
            {
                Debug.LogWarning("Auto-login failed, falling back to guest: " + loginResp.text);
                ClearAccountPrefsAndStartGuest();
                return;
            }
            LootLockerSDKManager.StartWhiteLabelSession(email, (sessionResp) =>
            {
                if (!sessionResp.success)
                {
                    Debug.LogWarning("White label session failed, falling back to guest: " + sessionResp.text);
                    ClearAccountPrefsAndStartGuest();
                    return;
                }
                IsGuest = false;
                PlayerName = PlayerPrefs.GetString(AccountDisplayNamePrefKey, "Player");
                SessionActive = true;
                PlayerId = sessionResp.player_id.ToString();
                LootLockerSDKManager.SetPlayerName(PlayerName, (_) => OnSessionReady?.Invoke());
            });
        });
    }

    private void ClearAccountPrefsAndStartGuest()
    {
        // Wipe saved credentials so the guest session can never inherit the account display name.
        // If credentials are genuinely bad the user will need to log in again manually.
        PlayerPrefs.DeleteKey(AccountEmailPrefKey);
        PlayerPrefs.DeleteKey(AccountPassPrefKey);
        PlayerPrefs.DeleteKey(AccountDisplayNamePrefKey);
        PlayerPrefs.Save();
        StartGuestSession();
    }

    public void CreateAccount(string username, string pin, System.Action<bool, string> onComplete)
    {
        string email = BuildEmail(username);
        string password = BuildPassword(pin);

        EndGuestSessionThen(() =>
        {
            LootLockerSDKManager.WhiteLabelSignUp(email, password, (signUpResp) =>
            {
                if (!signUpResp.success) { onComplete?.Invoke(false, signUpResp.text); return; }

                StartWhiteLabelLoginAndSession(email, password, username, onComplete);
            });
        });
    }

    public void Login(string username, string pin, System.Action<bool, string> onComplete)
    {
        string email = BuildEmail(username);
        string password = BuildPassword(pin);

        EndGuestSessionThen(() => StartWhiteLabelLoginAndSession(email, password, username, onComplete));
    }

    // Ends the active guest session on LootLocker's servers before proceeding, so that WL login
    // is not associated with the guest player context (which would create a new player UID).
    private void EndGuestSessionThen(System.Action next)
    {
        if (IsGuest && SessionActive)
        {
            SessionActive = false;
            LootLockerSDKManager.EndSession((_) => next(), true);
        }
        else
        {
            next();
        }
    }

    private void StartWhiteLabelLoginAndSession(string email, string password, string username, System.Action<bool, string> onComplete)
    {
        LootLockerSDKManager.WhiteLabelLogin(email, password, true, (loginResp) =>
        {
            if (!loginResp.success) { onComplete?.Invoke(false, "Wrong username or PIN. (" + loginResp.text + ")"); return; }

            LootLockerSDKManager.StartWhiteLabelSession(email, (sessionResp) =>
            {
                if (!sessionResp.success) { onComplete?.Invoke(false, "Session failed."); return; }

                IsGuest = false;
                PlayerName = username;
                SaveAccountPrefs(email, password, username);
                SessionActive = true;
                PlayerId = sessionResp.player_id.ToString();
                OnAccountStateChanged?.Invoke();
                LootLockerSDKManager.SetPlayerName(username, (_) => OnSessionReady?.Invoke());
                onComplete?.Invoke(true, "");
            });
        });
    }

    public void Logout()
    {
        PlayerPrefs.DeleteKey(AccountEmailPrefKey);
        PlayerPrefs.DeleteKey(AccountPassPrefKey);
        PlayerPrefs.DeleteKey(AccountDisplayNamePrefKey);
        string newGuestName = GenerateRandomName();
        PlayerPrefs.SetString(GuestNamePrefKey, newGuestName);
        PlayerPrefs.Save();
        SessionActive = false;
        IsGuest = true;
        PlayerName = newGuestName;
        OnAccountStateChanged?.Invoke();
        StartGuestSession();
    }

    public void GetPlayerHighScore(System.Action<int> onComplete)
    {
        if (!SessionActive || string.IsNullOrEmpty(PlayerId)) { onComplete?.Invoke(0); return; }
        LootLockerSDKManager.GetMemberRank(LeaderboardKey, PlayerId, (response) =>
        {
            onComplete?.Invoke(response.success ? response.score : 0);
        });
    }

    public void SubmitScore(int score, System.Action onComplete = null)
    {
        if (!SessionActive || score <= 0) { onComplete?.Invoke(); return; }
        LootLockerSDKManager.SubmitScore(null, score, LeaderboardKey, (response) =>
        {
            if (!response.success) Debug.LogWarning("Score submit failed: " + response.text);
            LootLockerSDKManager.SubmitScore(null, score, DailyLeaderboardKey, (dailyResponse) =>
            {
                if (!dailyResponse.success) Debug.LogWarning("Daily score submit failed: " + dailyResponse.text);
                onComplete?.Invoke();
            });
        });
    }

    public void GetTopScores(int count, System.Action<LootLockerLeaderboardMember[]> onComplete)
    {
        if (!SessionActive) { onComplete?.Invoke(null); return; }
        LootLockerSDKManager.GetScoreList(LeaderboardKey, count, (response) =>
        {
            onComplete?.Invoke(response.success ? response.items : null);
        });
    }

    public void GetDailyTopScores(int count, System.Action<LootLockerLeaderboardMember[]> onComplete)
    {
        if (!SessionActive) { onComplete?.Invoke(null); return; }
        LootLockerSDKManager.GetScoreList(DailyLeaderboardKey, count, (response) =>
        {
            onComplete?.Invoke(response.success ? response.items : null);
        });
    }

    public void RegenerateName(System.Action<string> onComplete = null)
    {
        if (!IsGuest) return;
        PlayerName = GenerateRandomName();
        PlayerPrefs.SetString(GuestNamePrefKey, PlayerName);
        PlayerPrefs.Save();
        onComplete?.Invoke(PlayerName);
        if (!SessionActive) return;
        LootLockerSDKManager.SetPlayerName(PlayerName, (_) => { });
    }

    private void SaveAccountPrefs(string email, string password, string displayName)
    {
        PlayerPrefs.SetString(AccountEmailPrefKey, email);
        PlayerPrefs.SetString(AccountPassPrefKey, password);
        PlayerPrefs.SetString(AccountDisplayNamePrefKey, displayName);
        PlayerPrefs.Save();
    }

    private string BuildEmail(string username) =>
        SanitizeForEmail(username) + "@play.ct2000.game";

    private string BuildPassword(string pin) =>
        "CT2K" + pin + "!Trd";

    private string SanitizeForEmail(string username) =>
        Regex.Replace(username.ToLower().Trim(), @"[^a-z0-9_]", "");

    private string GenerateRandomName()
    {
        string[] adj = { "Smelly", "Chunky", "Soggy", "Crusty", "Lumpy", "Sloppy", "Gooey", "Runny", "Rotten", "Stinky", "Mushy", "Gnarly" };
        string[] noun = { "Turd", "Plop", "Nugget", "Log", "Dookie", "Poop", "Splash", "Brown" };
        return adj[Random.Range(0, adj.Length)] + noun[Random.Range(0, noun.Length)] + Random.Range(10, 99);
    }
}
