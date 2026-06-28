using UnityEngine;
using LootLocker.Requests;
using System.Collections.Generic;
using System;

public class SkinManager : MonoBehaviour
{
    public static SkinManager Instance { get; private set; }

    private const string ActiveSkinPrefKey = "CT2000_ActiveSkin";
    private const string UnlockedSkinsPrefKey = "CT2000_UnlockedSkins";

    public class SkinDefinition
    {
        public string Id;
        public string DisplayName;
        public string ResourcePath; // relative to Resources/
        public int UnlockScore;     // 0 = always unlocked
        public string PowerDescription;
    }

    public static readonly SkinDefinition[] AllSkins =
    {
        new SkinDefinition { Id = "mr_crappy",  DisplayName = "Mr. Crappy", ResourcePath = "Skins/turd2000",     UnlockScore = 0,  PowerDescription = "Your basic turd"  },
        new SkinDefinition { Id = "the_kernel", DisplayName = "The Kernel", ResourcePath = "Skins/goldturd2000", UnlockScore = 20, PowerDescription = "Now with Corn"     },
        new SkinDefinition { Id = "the_dook",   DisplayName = "The Dook",   ResourcePath = "Skins/turdhat2000",  UnlockScore = 50, PowerDescription = "One Free Pass"     },
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

        // Restore unlocks from local cache immediately so the skin is available before LootLocker responds
        string saved = PlayerPrefs.GetString(UnlockedSkinsPrefKey, "");
        if (!string.IsNullOrEmpty(saved))
            foreach (string id in saved.Split(','))
                if (!string.IsNullOrEmpty(id)) _unlocked.Add(id);

        ActiveSkinId = PlayerPrefs.GetString(ActiveSkinPrefKey, "mr_crappy");
        if (!IsUnlocked(ActiveSkinId)) ActiveSkinId = "mr_crappy";
    }

    public void LoadUnlocksFromStorage()
    {
        LootLockerSDKManager.GetEntirePersistentStorage((response) =>
        {
            if (!response.success || response.payload == null) return;
            bool changed = false;
            foreach (var item in response.payload)
            {
                if (item.key.StartsWith("skin_") && item.value == "true")
                {
                    string id = item.key.Substring(5);
                    if (_unlocked.Add(id)) changed = true;
                }
            }
            if (changed) OnUnlockChanged?.Invoke();
        });
    }

    public bool IsUnlocked(string skinId) => skinId == "mr_crappy" || _unlocked.Contains(skinId);

    public void Unlock(string skinId, System.Action onComplete = null)
    {
        if (_unlocked.Contains(skinId)) { onComplete?.Invoke(); return; }
        _unlocked.Add(skinId);
        SaveUnlocksToPrefs();
        OnUnlockChanged?.Invoke();
        LootLockerSDKManager.UpdateOrCreateKeyValue("skin_" + skinId, "true", (response) =>
        {
            if (!response.success)
                Debug.LogWarning("SkinManager: failed to persist unlock for " + skinId);
            onComplete?.Invoke();
        });
    }

    private void SaveUnlocksToPrefs()
    {
        PlayerPrefs.SetString(UnlockedSkinsPrefKey, string.Join(",", _unlocked));
        PlayerPrefs.Save();
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
