using UnityEngine;
using LootLocker.Requests;
using System.Collections.Generic;

public class SkinManager : MonoBehaviour
{
    public static SkinManager Instance { get; private set; }

    private const string ActiveSkinPrefKey = "CT2000_ActiveSkin";
    private const int ScoreUnlockThreshold = 20;

    public class SkinDefinition
    {
        public string Id;
        public string DisplayName;
        public string ResourcePath; // relative to Resources/
    }

    public static readonly SkinDefinition[] AllSkins =
    {
        new SkinDefinition { Id = "mr_crappy",  DisplayName = "Mr. Crappy",  ResourcePath = "Skins/turd2000"     },
        new SkinDefinition { Id = "the_kernel", DisplayName = "The Kernel",  ResourcePath = "Skins/goldturd2000" },
    };

    private readonly HashSet<string> _unlocked = new HashSet<string> { "mr_crappy" };

    public string ActiveSkinId { get; private set; } = "mr_crappy";

    public event System.Action OnSkinChanged;
    public event System.Action OnUnlockChanged;

    private void Awake()
    {
        if (Instance != null) { Destroy(gameObject); return; }
        Instance = this;
        DontDestroyOnLoad(gameObject);
        ActiveSkinId = PlayerPrefs.GetString(ActiveSkinPrefKey, "mr_crappy");
    }

    public void LoadUnlocksFromStorage()
    {
        LootLockerSDKManager.GetEntirePersistentStorage((Action<LootLockerGetPersistentStorageResponseDictionary>) ((response) =>
        {
            if (!response.success || response.payload == null) return;
            bool changed = false;
            foreach (var kvp in response.payload)
            {
                if (kvp.Key.StartsWith("skin_") && kvp.Value == "true")
                {
                    string id = kvp.Key.Substring(5);
                    if (_unlocked.Add(id)) changed = true;
                }
            }
            if (changed) OnUnlockChanged?.Invoke();
        }));
    }

    public bool IsUnlocked(string skinId) => skinId == "mr_crappy" || _unlocked.Contains(skinId);

    public void Unlock(string skinId, System.Action onComplete = null)
    {
        if (_unlocked.Contains(skinId)) { onComplete?.Invoke(); return; }
        _unlocked.Add(skinId);
        OnUnlockChanged?.Invoke();
        LootLockerSDKManager.UpdateOrCreateKeyValue("skin_" + skinId, "true", (response) =>
        {
            if (!response.success)
                Debug.LogWarning("SkinManager: failed to persist unlock for " + skinId);
            onComplete?.Invoke();
        });
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
        if (score > ScoreUnlockThreshold && !IsUnlocked("the_kernel"))
            Unlock("the_kernel", () => SetActiveSkin("the_kernel"));
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
