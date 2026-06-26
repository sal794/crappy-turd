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
            FindUniqueName(PlayerName, 8, (uniqueName) =>
            {
                if (uniqueName != PlayerName)
                {
                    PlayerName = uniqueName;
                    PlayerPrefs.SetString(GuestNamePrefKey, PlayerName);
                    PlayerPrefs.Save();
                }
                LootLockerSDKManager.SetPlayerName(PlayerName, (_) => { });
                OnSessionReady?.Invoke();
            });
        });
    }

    private void AttemptAutoLogin(string email, string pass)
    {
        LootLockerSDKManager.WhiteLabelLogin(email, pass, true, (loginResp) =>
        {
            if (!loginResp.success) { StartGuestSession(); return; }
            LootLockerSDKManager.StartWhiteLabelSession((sessionResp) =>
            {
                if (!sessionResp.success) { StartGuestSession(); return; }
                IsGuest = false;
                PlayerName = PlayerPrefs.GetString(AccountDisplayNamePrefKey, "Player");
                SessionActive = true;
                OnSessionReady?.Invoke();
            });
        });
    }

    public void CreateAccount(string username, string pin, System.Action<bool, string> onComplete)
    {
        string email = BuildEmail(username);
        string password = BuildPassword(pin);

        LootLockerSDKManager.WhiteLabelSignUp(email, password, (signUpResp) =>
        {
            if (!signUpResp.success) { onComplete?.Invoke(false, signUpResp.text); return; }

            LootLockerSDKManager.WhiteLabelLogin(email, password, true, (loginResp) =>
            {
                if (!loginResp.success) { onComplete?.Invoke(false, "Login failed after signup."); return; }

                LootLockerSDKManager.StartWhiteLabelSession((sessionResp) =>
                {
                    if (!sessionResp.success) { onComplete?.Invoke(false, "Session failed."); return; }

                    IsGuest = false;
                    PlayerName = username;
                    SaveAccountPrefs(email, password, username);
                    SessionActive = true;
                    LootLockerSDKManager.SetPlayerName(username, (_) => { });
                    OnAccountStateChanged?.Invoke();
                    OnSessionReady?.Invoke();
                    onComplete?.Invoke(true, "");
                });
            });
        });
    }

    public void Login(string username, string pin, System.Action<bool, string> onComplete)
    {
        string email = BuildEmail(username);
        string password = BuildPassword(pin);

        LootLockerSDKManager.WhiteLabelLogin(email, password, true, (loginResp) =>
        {
            if (!loginResp.success) { onComplete?.Invoke(false, "Wrong username or PIN. (" + loginResp.text + ")"); return; }

            LootLockerSDKManager.StartWhiteLabelSession((sessionResp) =>
            {
                if (!sessionResp.success) { onComplete?.Invoke(false, "Session failed."); return; }

                IsGuest = false;
                PlayerName = username;
                SaveAccountPrefs(email, password, username);
                SessionActive = true;
                LootLockerSDKManager.SetPlayerName(username, (_) => { });
                OnAccountStateChanged?.Invoke();
                OnSessionReady?.Invoke();
                onComplete?.Invoke(true, "");
            });
        });
    }

    public void Logout()
    {
        PlayerPrefs.DeleteKey(AccountEmailPrefKey);
        PlayerPrefs.DeleteKey(AccountPassPrefKey);
        PlayerPrefs.DeleteKey(AccountDisplayNamePrefKey);
        PlayerPrefs.Save();
        SessionActive = false;
        IsGuest = true;
        PlayerName = PlayerPrefs.GetString(GuestNamePrefKey);
        OnAccountStateChanged?.Invoke();
        StartGuestSession();
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
        if (!SessionActive)
        {
            PlayerName = GenerateRandomName();
            PlayerPrefs.SetString(GuestNamePrefKey, PlayerName);
            PlayerPrefs.Save();
            onComplete?.Invoke(PlayerName);
            return;
        }
        FindUniqueName(null, 10, (uniqueName) =>
        {
            PlayerName = uniqueName;
            PlayerPrefs.SetString(GuestNamePrefKey, PlayerName);
            PlayerPrefs.Save();
            LootLockerSDKManager.SetPlayerName(PlayerName, (_) => onComplete?.Invoke(PlayerName));
        });
    }

    private void SaveAccountPrefs(string email, string password, string displayName)
    {
        PlayerPrefs.SetString(AccountEmailPrefKey, email);
        PlayerPrefs.SetString(AccountPassPrefKey, password);
        PlayerPrefs.SetString(AccountDisplayNamePrefKey, displayName);
        PlayerPrefs.Save();
    }

    private void FindUniqueName(string initialCandidate, int attemptsLeft, System.Action<string> onFound)
    {
        string candidate = initialCandidate ?? GenerateRandomName();
        if (attemptsLeft <= 0) { onFound?.Invoke(candidate + Random.Range(100, 999)); return; }
        LootLockerSDKManager.LookupPlayerNamesByPlayerNames(new[] { candidate }, (response) =>
        {
            bool taken = response.success && response.players != null && response.players.Length > 0;
            if (taken) FindUniqueName(null, attemptsLeft - 1, onFound);
            else onFound?.Invoke(candidate);
        });
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
