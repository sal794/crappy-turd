using UnityEngine;
using LootLocker;
using LootLocker.Requests;

public class LeaderboardManager : MonoBehaviour
{
    public static LeaderboardManager Instance { get; private set; }

    private const string LeaderboardKey = "ct2000_highscores";
    private const string PlayerNamePrefKey = "CT2000_PlayerName";

    public bool SessionActive { get; private set; }
    public string PlayerName { get; private set; }
    public event System.Action OnSessionReady;

    private void Awake()
    {
        if (Instance != null) { Destroy(gameObject); return; }
        Instance = this;
        DontDestroyOnLoad(gameObject);

        if (!PlayerPrefs.HasKey(PlayerNamePrefKey))
            PlayerPrefs.SetString(PlayerNamePrefKey, GenerateRandomName());

        PlayerName = PlayerPrefs.GetString(PlayerNamePrefKey);
    }

    private void Start()
    {
        LootLockerConfig.current.apiKey = "dev_b813ee9f33a54e3a995d3b787b334c9e";
        LootLockerConfig.current.game_version = "1.0.0";

        LootLockerSDKManager.StartGuestSession((response) =>
        {
            if (!response.success)
            {
                Debug.LogWarning("LootLocker session failed: " + response.text);
                return;
            }
            SessionActive = true;
            // Verify the initial auto-generated name is unique, regenerate if not
            FindUniqueName(PlayerName, 8, (uniqueName) =>
            {
                if (uniqueName != PlayerName)
                {
                    PlayerName = uniqueName;
                    PlayerPrefs.SetString(PlayerNamePrefKey, PlayerName);
                    PlayerPrefs.Save();
                }
                LootLockerSDKManager.SetPlayerName(PlayerName, (_) => { });
                OnSessionReady?.Invoke();
            });
        });
    }

    public void SubmitScore(int score, System.Action onComplete = null)
    {
        if (!SessionActive || score <= 0) { onComplete?.Invoke(); return; }
        LootLockerSDKManager.SubmitScore(null, score, LeaderboardKey, (response) =>
        {
            if (!response.success) Debug.LogWarning("Score submit failed: " + response.text);
            onComplete?.Invoke();
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

    public void RegenerateName(System.Action<string> onComplete = null)
    {
        if (!SessionActive)
        {
            PlayerName = GenerateRandomName();
            PlayerPrefs.SetString(PlayerNamePrefKey, PlayerName);
            PlayerPrefs.Save();
            onComplete?.Invoke(PlayerName);
            return;
        }

        FindUniqueName(null, 10, (uniqueName) =>
        {
            PlayerName = uniqueName;
            PlayerPrefs.SetString(PlayerNamePrefKey, PlayerName);
            PlayerPrefs.Save();
            LootLockerSDKManager.SetPlayerName(PlayerName, (_) => onComplete?.Invoke(PlayerName));
        });
    }

    private void FindUniqueName(string initialCandidate, int attemptsLeft, System.Action<string> onFound)
    {
        string candidate = initialCandidate ?? GenerateRandomName();

        if (attemptsLeft <= 0)
        {
            // All retries exhausted — append extra digits to force uniqueness
            onFound?.Invoke(candidate + Random.Range(100, 999));
            return;
        }

        LootLockerSDKManager.LookupPlayerNamesByPlayerNames(new[] { candidate }, (response) =>
        {
            bool taken = response.success && response.players != null && response.players.Length > 0;
            if (taken)
                FindUniqueName(null, attemptsLeft - 1, onFound);
            else
                onFound?.Invoke(candidate);
        });
    }

    private string GenerateRandomName()
    {
        string[] adj = { "Smelly", "Chunky", "Soggy", "Crusty", "Lumpy", "Sloppy", "Gooey", "Runny", "Rotten", "Stinky", "Mushy", "Gnarly" };
        string[] noun = { "Turd", "Plop", "Nugget", "Log", "Dookie", "Poop", "Splash", "Brown" };
        return adj[Random.Range(0, adj.Length)] + noun[Random.Range(0, noun.Length)] + Random.Range(10, 99);
    }
}
