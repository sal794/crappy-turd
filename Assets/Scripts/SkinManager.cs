using UnityEngine;
using LootLocker.Requests;
using System.Collections.Generic;

public class SkinManager : MonoBehaviour
{
    public static SkinManager Instance { get; private set; }

    private const string ActiveSkinPrefKey = "CT2000_ActiveSkin";
    private const string GuestSkinsPrefKey  = "CT2000_GuestSkins"; // earned as guest; survives logout
    private const string WLSkinsPrefKey     = "CT2000_WLSkins";    // WL account cache; cleared on logout

    public class SkinDefinition
    {
        public string Id;
        public string DisplayName;
        public string ResourcePath;
        public int UnlockScore;
        public string PowerDescription;
    }

    public static readonly SkinDefinition[] AllSkins =
    {
        new SkinDefinition { Id = "mr_crappy",        DisplayName = "Mr. Crappy",      ResourcePath = "Skins/turd2000",        UnlockScore = 0,   PowerDescription = "Your basic turd"    },
        new SkinDefinition { Id = "the_kernel",       DisplayName = "The Kernel",       ResourcePath = "Skins/goldturd2000",    UnlockScore = 20,  PowerDescription = "Now with Corn"       },
        new SkinDefinition { Id = "the_dook",         DisplayName = "The Dook",         ResourcePath = "Skins/turdhat2000",     UnlockScore = 50,  PowerDescription = "One Free Pass"       },
        new SkinDefinition { Id = "craptain_america", DisplayName = "Craptain America", ResourcePath = "Skins/craptainamerica", UnlockScore = 150, PowerDescription = "Shield Regenerates"  },
    };

    // Skins earned while in any guest session. Never cleared on logout so they can be migrated
    // to a WL account on next login.
    private readonly HashSet<string> _guestUnlocked = new HashSet<string>();

    // Combined set of everything currently available (mr_crappy + guest + WL).
    private readonly HashSet<string> _unlocked = new HashSet<string> { "mr_crappy" };

    public string ActiveSkinId { get; private set; } = "mr_crappy";

    public event System.Action OnSkinChanged;
    public event System.Action OnUnlockChanged;
    public event System.Action<string> OnSkinUnlocked;

    private void Awake()
    {
        if (Instance != null) { Destroy(gameObject); return; }
        Instance = this;
        DontDestroyOnLoad(gameObject);

        // One-time migration from the old unified key (pre-split builds).
        // We don't know if those skins came from a guest or WL session, so treat them as guest
        // so they survive logout and get migrated to the WL account on next login.
        string legacy = PlayerPrefs.GetString("CT2000_UnlockedSkins", "");
        if (!string.IsNullOrEmpty(legacy))
        {
            foreach (string id in legacy.Split(','))
                if (!string.IsNullOrEmpty(id) && id != "mr_crappy")
                    _guestUnlocked.Add(id);
            PlayerPrefs.SetString(GuestSkinsPrefKey, string.Join(",", _guestUnlocked));
            PlayerPrefs.DeleteKey("CT2000_UnlockedSkins");
            PlayerPrefs.Save();
        }

        // Load guest skins (persist across logout/login cycles)
        string guestSaved = PlayerPrefs.GetString(GuestSkinsPrefKey, "");
        if (!string.IsNullOrEmpty(guestSaved))
            foreach (string id in guestSaved.Split(','))
                if (!string.IsNullOrEmpty(id)) { _guestUnlocked.Add(id); _unlocked.Add(id); }

        // Load cached WL skins from last session (empty if we logged out cleanly)
        string wlSaved = PlayerPrefs.GetString(WLSkinsPrefKey, "");
        if (!string.IsNullOrEmpty(wlSaved))
            foreach (string id in wlSaved.Split(','))
                if (!string.IsNullOrEmpty(id)) _unlocked.Add(id);

        ActiveSkinId = PlayerPrefs.GetString(ActiveSkinPrefKey, "mr_crappy");
        if (!IsUnlocked(ActiveSkinId)) ActiveSkinId = "mr_crappy";
    }

    // Called on logout. Strips WL skins from the active set, keeps guest-earned skins.
    public void ResetUnlocks()
    {
        _unlocked.Clear();
        _unlocked.Add("mr_crappy");
        foreach (string id in _guestUnlocked) _unlocked.Add(id);

        // Wipe WL cache so next app start doesn't show stale account skins
        PlayerPrefs.SetString(WLSkinsPrefKey, "");

        if (!_unlocked.Contains(ActiveSkinId))
        {
            ActiveSkinId = "mr_crappy";
            PlayerPrefs.SetString(ActiveSkinPrefKey, "mr_crappy");
        }
        PlayerPrefs.Save();

        OnSkinChanged?.Invoke();
        OnUnlockChanged?.Invoke();
    }

    // Called when a WL session becomes active. Fetches the authoritative skin list from LootLocker,
    // migrates any guest-earned skins into the account, then refreshes the UI.
    public void LoadUnlocksFromStorage(System.Action onComplete = null)
    {
        if (LeaderboardManager.Instance == null || LeaderboardManager.Instance.IsGuest)
        {
            onComplete?.Invoke();
            return;
        }

        LootLockerSDKManager.GetEntirePersistentStorage((response) =>
        {
            // Rebuild _unlocked from scratch inside the callback to avoid a flash of locked
            // state while waiting for the async response.
            _unlocked.Clear();
            _unlocked.Add("mr_crappy");
            foreach (string id in _guestUnlocked) _unlocked.Add(id);

            var remoteKeys = new HashSet<string>();

            if (response.success && response.payload != null)
            {
                foreach (var item in response.payload)
                {
                    if (item.key.StartsWith("skin_") && item.value == "true")
                    {
                        string id = item.key.Substring(5);
                        remoteKeys.Add(id);
                        _unlocked.Add(id);
                    }
                }
            }

            // Migrate guest-earned skins to this WL account (write any not already there)
            foreach (string id in _guestUnlocked)
            {
                if (id != "mr_crappy" && !remoteKeys.Contains(id))
                {
                    LootLockerSDKManager.UpdateOrCreateKeyValue("skin_" + id, "true", (_) => { });
                    remoteKeys.Add(id);
                    _unlocked.Add(id);
                }
            }
            // Guest skins are now owned by the WL account — clear guest tracking
            _guestUnlocked.Clear();
            PlayerPrefs.SetString(GuestSkinsPrefKey, "");

            // Cache WL skins locally so next startup can show them before LootLocker responds
            PlayerPrefs.SetString(WLSkinsPrefKey, string.Join(",", remoteKeys));
            PlayerPrefs.Save();

            OnUnlockChanged?.Invoke();
            onComplete?.Invoke();
        });
    }

    public bool IsUnlocked(string skinId) => skinId == "mr_crappy" || _unlocked.Contains(skinId);

    public void Unlock(string skinId, System.Action onComplete = null)
    {
        if (_unlocked.Contains(skinId)) { onComplete?.Invoke(); return; }
        _unlocked.Add(skinId);
        OnUnlockChanged?.Invoke();
        OnSkinUnlocked?.Invoke(skinId);

        if (LeaderboardManager.Instance != null && !LeaderboardManager.Instance.IsGuest)
        {
            // WL session: persist to LootLocker and append to local WL cache
            LootLockerSDKManager.UpdateOrCreateKeyValue("skin_" + skinId, "true", (response) =>
            {
                if (!response.success)
                {
                    Debug.LogWarning("SkinManager: failed to persist unlock for " + skinId);
                }
                else
                {
                    string existing = PlayerPrefs.GetString(WLSkinsPrefKey, "");
                    PlayerPrefs.SetString(WLSkinsPrefKey,
                        string.IsNullOrEmpty(existing) ? skinId : existing + "," + skinId);
                    PlayerPrefs.Save();
                }
                onComplete?.Invoke();
            });
        }
        else
        {
            // Guest session: persist to guest prefs only; no LootLocker write
            _guestUnlocked.Add(skinId);
            PlayerPrefs.SetString(GuestSkinsPrefKey, string.Join(",", _guestUnlocked));
            PlayerPrefs.Save();
            onComplete?.Invoke();
        }
    }

    public void SetActiveSkin(string skinId)
    {
        if (!IsUnlocked(skinId)) return;
        ActiveSkinId = skinId;
        PlayerPrefs.SetString(ActiveSkinPrefKey, skinId);
        PlayerPrefs.Save();
        OnSkinChanged?.Invoke();
    }

    public void CheckScoreUnlock(int score)
    {
        foreach (var skin in AllSkins)
            if (skin.UnlockScore > 0 && score >= skin.UnlockScore && !IsUnlocked(skin.Id))
                Unlock(skin.Id);
    }

    public Sprite LoadActiveSkinSprite()
    {
        foreach (var skin in AllSkins)
            if (skin.Id == ActiveSkinId)
                return Resources.Load<Sprite>(skin.ResourcePath);
        return Resources.Load<Sprite>(AllSkins[0].ResourcePath);
    }

    public SkinDefinition GetSkinDef(string skinId)
    {
        foreach (var skin in AllSkins)
            if (skin.Id == skinId) return skin;
        return AllSkins[0];
    }
}
