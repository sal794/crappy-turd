using UnityEngine;
using UnityEngine.UI;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.DualShock;
using UnityEngine.InputSystem.XInput;
using TMPro;
using LegacyText = UnityEngine.UI.Text;
using Slider = UnityEngine.UI.Slider;

public class GameManager : MonoBehaviour
{
    public static GameManager Instance { get; private set; }

    [SerializeField] private GameObject gameOverPanel;
    [SerializeField] private AudioClip[] scoreSounds;
    [SerializeField] private AudioClip[] gameOverSounds;
    [SerializeField] private AudioClip backgroundMusic;

    public bool IsGameOver { get; private set; }
    public bool IsStarted { get; private set; }
    public bool IsPaused { get; private set; }
    public int Score { get; private set; }
    public int HighScore { get; private set; }
    private const string HighScorePrefKey = "CT2000_HighScore";
    public float SpeedMultiplier { get; private set; } = 1f;

    private GameObject _startPanel;
    private GameObject _settingsPanel;
    private GameObject _skinsPanel;
    private Button[] _skinEquipButtons;
    private LegacyText[] _skinStatusLabels;
    private Image[] _skinCardBorders;
    private GameObject _pauseButton;
    private GameObject _pausePanel;
    private GameObject _accountPanel;
    private GameObject _accountGuestPanel;
    private GameObject _accountLoggedInPanel;
    private GameObject _newNameButton;
    private GameObject _namePickerDivider;
    private RectTransform _playerNameLabelRt;
    private LegacyText _accountPanelTitle;
    private LegacyText _accountLoggedInNameLabel;
    private LegacyText _accountButtonLabel;
    private LegacyText _accountStatusText;
    private LegacyText _scoreLabel;
    private LegacyText _highScoreLabel;
    private LegacyText _restartHintLabel;
    private LegacyText _startSubtitleLabel;
    private LegacyText _leaderboardTitle;
    private LegacyText[] _leaderboardRows;
    private LegacyText _dailyLeaderboardTitle;
    private LegacyText[] _dailyLeaderboardRows;
    private LegacyText _playerNameLabel;
    private LegacyText _playerNameValueLabel;
    private RectTransform _playerNameValueLabelRt;
    private AudioSource _audioSource;
    private AudioSource _musicSource;
    private Font _gameFont;
    private Font _frukturFont;

    private const string MusicVolumePrefKey = "CT2000_MusicVolume";
    private const string SFXVolumePrefKey = "CT2000_SFXVolume";
    private const string HeadstartPrefKey = "CT2000_Headstart";

    private bool _headstartEnabled;
    private Button _headstartToggleBtn;
    private LegacyText _headstartToggleLbl;
    public bool StartHintDismissed => _startHintDismissed;
    private bool _startHintDismissed;

    private const int LeaderboardDisplayCount = 8;

    // Button color palette
    private static readonly Color BtnBlue         = new Color(0.20f, 0.55f, 0.92f);
    private static readonly Color BtnBlueBorder    = new Color(0.10f, 0.30f, 0.62f);
    private static readonly Color BtnPurple        = new Color(0.62f, 0.22f, 0.85f);
    private static readonly Color BtnPurpleBorder  = new Color(0.36f, 0.08f, 0.52f);
    private static readonly Color BtnGreen         = new Color(0.18f, 0.72f, 0.30f);
    private static readonly Color BtnGreenBorder   = new Color(0.08f, 0.44f, 0.16f);
    private static readonly Color BtnOrange        = new Color(0.98f, 0.55f, 0.08f);
    private static readonly Color BtnOrangeBorder  = new Color(0.66f, 0.34f, 0.02f);
    private static readonly Color BtnRed           = new Color(0.88f, 0.20f, 0.16f);
    private static readonly Color BtnRedBorder     = new Color(0.56f, 0.08f, 0.06f);
    private static readonly Color BtnGold          = new Color(0.98f, 0.78f, 0.05f);
    private static readonly Color BtnGoldBorder    = new Color(0.66f, 0.50f, 0.02f);
    private static readonly Color BtnGray          = new Color(0.30f, 0.30f, 0.30f);
    private static readonly Color BtnGrayBorder    = new Color(0.15f, 0.15f, 0.15f);
    private static readonly Color BtnPink          = new Color(0.92f, 0.18f, 0.60f);
    private static readonly Color BtnPinkBorder    = new Color(0.58f, 0.06f, 0.36f);
    private static readonly Color BtnMagenta       = new Color(0.80f, 0.12f, 0.75f);
    private static readonly Color BtnMagentaBorder = new Color(0.48f, 0.04f, 0.44f);

    private enum InputDevice { Keyboard, Xbox, PlayStation, Touch }
    private InputDevice _lastInputDevice = InputDevice.Keyboard;

    private void Awake()
    {
        if (Instance != null) { Destroy(gameObject); return; }
        Instance = this;
        Debug.unityLogger.logHandler = new DeviceWarningFilter(Debug.unityLogger.logHandler);
        _audioSource = gameObject.AddComponent<AudioSource>();
        _audioSource.playOnAwake = false;

        _musicSource = gameObject.AddComponent<AudioSource>();
        _musicSource.playOnAwake = false;
        _musicSource.loop = true;
        _musicSource.volume = 0.35f;
    }

    private Font GetGameFont() =>
        _gameFont != null ? _gameFont : Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");

    private void Start()
    {
        _gameFont = Resources.Load<Font>("Fonts/Nosifer-Regular");
        _frukturFont = Resources.Load<Font>("Fonts/Fruktur-Regular");
        HighScore = PlayerPrefs.GetInt(HighScorePrefKey, 0);
        _headstartEnabled = PlayerPrefs.GetInt(HeadstartPrefKey, 0) == 1;
        StyleGameOverUI();

        Transform btnT = gameOverPanel.transform.Find("RestartButton");
        if (btnT != null)
        {
            Button btn = btnT.GetComponent<Button>();
            if (btn != null) { btn.onClick.RemoveAllListeners(); btn.onClick.AddListener(Restart); }
        }

        gameOverPanel.SetActive(false);
        CreateStartUI();
        CreateScoreUI();
        CreatePauseButton();
        CreatePausePanel();

        if (LeaderboardManager.Instance != null)
        {
            LeaderboardManager.Instance.OnSessionReady += RefreshLeaderboard;
            LeaderboardManager.Instance.OnSessionReady += RefreshDailyLeaderboard;
            LeaderboardManager.Instance.OnSessionReady += () => SkinManager.Instance?.LoadUnlocksFromStorage();
            LeaderboardManager.Instance.OnSessionReady += RefreshAccountUI;
            LeaderboardManager.Instance.OnSessionReady += FetchLootLockerHighScore;
            LeaderboardManager.Instance.OnAccountStateChanged += RefreshAccountUI;
            LeaderboardManager.Instance.OnAccountStateChanged += FetchLootLockerHighScore;
            LeaderboardManager.Instance.OnAccountStateChanged += () => SkinManager.Instance?.LoadUnlocksFromStorage();
            RefreshAccountUI();
        }
        if (SkinManager.Instance != null)
        {
            SkinManager.Instance.OnUnlockChanged += RefreshSkinsPanel;
            SkinManager.Instance.OnSkinChanged += RefreshSkinsPanel;
        }

        _musicSource.volume = PlayerPrefs.GetFloat(MusicVolumePrefKey, 0.35f);
        _audioSource.volume = PlayerPrefs.GetFloat(SFXVolumePrefKey, 1f);

        if (backgroundMusic == null)
            backgroundMusic = Resources.Load<AudioClip>("Farting-Pokemon");
        if (backgroundMusic != null)
        {
            _musicSource.clip = backgroundMusic;
            _musicSource.Play();
        }
    }

    private void Update()
    {
        bool restartKeyboard = Keyboard.current != null && Keyboard.current.rKey.wasPressedThisFrame;
        bool restartGamepad = Gamepad.current != null && Gamepad.current.buttonNorth.wasPressedThisFrame;
        if (IsGameOver && (restartKeyboard || restartGamepad))
            Restart();

        if (IsStarted && !IsGameOver && Keyboard.current != null && Keyboard.current.pKey.wasPressedThisFrame)
            TogglePause();

        UpdateLastInputDevice();

        if (IsGameOver && _restartHintLabel != null)
            _restartHintLabel.text = GetRestartHint();

        if (!IsStarted && !_startHintDismissed && _startSubtitleLabel != null)
            _startSubtitleLabel.text = GetStartHint();
    }

    private void UpdateLastInputDevice()
    {
        if (Keyboard.current != null && Keyboard.current.anyKey.wasPressedThisFrame)
        {
            _lastInputDevice = InputDevice.Keyboard;
        }
        else if (Touchscreen.current != null && Touchscreen.current.primaryTouch.press.wasPressedThisFrame)
        {
            _lastInputDevice = InputDevice.Touch;
        }
        else if (Gamepad.current != null && Gamepad.current.wasUpdatedThisFrame)
        {
            if (Gamepad.current is DualShockGamepad ||
                Gamepad.current.description.product.IndexOf("DualSense", System.StringComparison.OrdinalIgnoreCase) >= 0)
                _lastInputDevice = InputDevice.PlayStation;
            else
                _lastInputDevice = InputDevice.Xbox;
        }
    }

    private string GetStartHint()
    {
        return _lastInputDevice switch
        {
            InputDevice.PlayStation => "Press Cross to Start Crappin'",
            InputDevice.Xbox => "Press A to Start Crappin'",
            InputDevice.Touch => "Tap to Start Crappin'",
            _ => "Press Space to Start Crappin'",
        };
    }

    private string GetRestartHint()
    {
        return _lastInputDevice switch
        {
            InputDevice.PlayStation => "Press △ to Restart",
            InputDevice.Xbox => "Press Y to Restart",
            InputDevice.Touch => "Tap the Restart button",
            _ => "Press R to Restart",
        };
    }

    public void DismissStartHint()
    {
        _startHintDismissed = true;
        if (_startSubtitleLabel != null)
            _startSubtitleLabel.transform.parent.parent.gameObject.SetActive(false);
    }

    public void StartGame()
    {
        IsStarted = true;
        _startPanel.SetActive(false);
        if (_settingsPanel != null) _settingsPanel.SetActive(false);


        if (_headstartEnabled && HighScore >= 2)
        {
            Score = HighScore / 2;
            SpeedMultiplier = 1f + Score * 0.01f;
            _scoreLabel.text = $"Current Score: {Score}";
        }

        _scoreLabel.gameObject.SetActive(true);
        _highScoreLabel.gameObject.SetActive(true);
        if (_pauseButton != null) _pauseButton.SetActive(true);
    }

    private void FetchLootLockerHighScore()
    {
        LeaderboardManager.Instance?.GetPlayerHighScore((remoteScore) =>
        {
            if (remoteScore > HighScore)
            {
                HighScore = remoteScore;
                PlayerPrefs.SetInt(HighScorePrefKey, HighScore);
                PlayerPrefs.Save();
                if (_highScoreLabel != null)
                    _highScoreLabel.text = $"High Score: {HighScore}";
                if (_headstartToggleBtn != null)
                {
                    _headstartToggleBtn.transform.parent.gameObject.SetActive(HighScore >= 2);
                    UpdateHeadstartToggleLabel();
                }
            }
        });
    }

    public void AddScore()
    {
        if (!IsStarted || IsGameOver) return;
        Score++;
        SpeedMultiplier += 0.01f;
        _scoreLabel.text = $"Current Score: {Score}";
        if (Score > HighScore)
        {
            HighScore = Score;
            PlayerPrefs.SetInt(HighScorePrefKey, HighScore);
            PlayerPrefs.Save();
            _highScoreLabel.text = $"High Score: {HighScore}";
            if (_headstartToggleBtn != null)
            {
                _headstartToggleBtn.transform.parent.gameObject.SetActive(HighScore >= 2);
                UpdateHeadstartToggleLabel();
            }
        }
        SkinManager.Instance?.CheckScoreUnlock(Score);
        PlayRandomScoreSound();
    }

    private void PlayRandomScoreSound()
    {
        if (scoreSounds == null || scoreSounds.Length == 0) return;
        AudioClip clip = scoreSounds[Random.Range(0, scoreSounds.Length)];
        if (clip != null) _audioSource.PlayOneShot(clip);
    }

    public void TriggerGameOver()
    {
        if (IsGameOver) return;
        if (IsPaused) TogglePause();
        IsGameOver = true;
        if (gameOverSounds != null && gameOverSounds.Length > 0)
        {
            AudioClip clip = gameOverSounds[Random.Range(0, gameOverSounds.Length)];
            if (clip != null) _audioSource.PlayOneShot(clip);
        }
        Time.timeScale = 0f;
        gameOverPanel.SetActive(true);
        if (_pauseButton != null) _pauseButton.SetActive(false);
        if (LeaderboardManager.Instance != null)
            LeaderboardManager.Instance.SubmitScore(Score);
    }

    public void Restart()
    {
        Time.timeScale = 1f;
        IsGameOver = false;
        IsStarted = false;
        Score = 0;
        SpeedMultiplier = 1f;
        _scoreLabel.text = "Current Score: 0";
        gameOverPanel.SetActive(false);

        GameObject player = GameObject.Find("Player");
        if (player != null)
        {
            player.transform.position = Vector3.zero;
            Rigidbody2D rb = player.GetComponent<Rigidbody2D>();
            if (rb != null) { rb.linearVelocity = Vector2.zero; rb.gravityScale = 0f; }
        }

        foreach (GameObject go in GameObject.FindObjectsByType<GameObject>(FindObjectsSortMode.None))
            if (go.name == "ObstaclePair") Destroy(go);

        ObstacleSpawner spawner = FindFirstObjectByType<ObstacleSpawner>();
        if (spawner != null) spawner.ResetTimer();

        _startPanel.SetActive(true);
        if (_pauseButton != null) _pauseButton.SetActive(false);
        RefreshLeaderboard();
        RefreshDailyLeaderboard();
    }

    public void TogglePause()
    {
        IsPaused = !IsPaused;
        Time.timeScale = IsPaused ? 0f : 1f;
        if (IsPaused) _musicSource.Pause(); else _musicSource.UnPause();
        if (_pausePanel != null) _pausePanel.SetActive(IsPaused);
    }

    private void CreatePauseButton()
    {
        Canvas canvas = FindFirstObjectByType<Canvas>();
        _pauseButton = new GameObject("PauseButton");
        _pauseButton.transform.SetParent(canvas.transform, false);

        RectTransform rt = _pauseButton.AddComponent<RectTransform>();
        rt.anchorMin = new Vector2(0f, 1f);
        rt.anchorMax = new Vector2(0f, 1f);
        rt.pivot = new Vector2(0f, 1f);
        rt.sizeDelta = new Vector2(100f, 70f);
        rt.anchoredPosition = new Vector2(20f, -20f);

        Image img = _pauseButton.AddComponent<Image>();
        img.color = new Color(0f, 0f, 0f, 0.75f);
        Button btn = _pauseButton.AddComponent<Button>();
        btn.targetGraphic = img;
        ColorBlock colors = btn.colors;
        colors.highlightedColor = new Color(0.2f, 0.2f, 0.2f, 0.9f);
        colors.pressedColor = new Color(0.35f, 0.35f, 0.35f, 0.9f);
        btn.colors = colors;
        btn.onClick.AddListener(TogglePause);

        GameObject textGo = new GameObject("Label");
        textGo.transform.SetParent(_pauseButton.transform, false);
        RectTransform textRt = textGo.AddComponent<RectTransform>();
        textRt.anchorMin = Vector2.zero;
        textRt.anchorMax = Vector2.one;
        textRt.offsetMin = Vector2.zero;
        textRt.offsetMax = Vector2.zero;
        LegacyText txt = textGo.AddComponent<LegacyText>();
        txt.text = "II";
        txt.fontSize = 32;
        txt.color = Color.white;
        txt.fontStyle = FontStyle.Bold;
        txt.alignment = TextAnchor.MiddleCenter;
        txt.font = GetGameFont();

        _pauseButton.SetActive(false);
    }

    private void CreatePausePanel()
    {
        Canvas canvas = FindFirstObjectByType<Canvas>();
        _pausePanel = new GameObject("PausePanel");
        _pausePanel.transform.SetParent(canvas.transform, false);

        RectTransform overlayRt = _pausePanel.AddComponent<RectTransform>();
        overlayRt.anchorMin = Vector2.zero;
        overlayRt.anchorMax = Vector2.one;
        overlayRt.offsetMin = Vector2.zero;
        overlayRt.offsetMax = Vector2.zero;
        Image overlayImg = _pausePanel.AddComponent<Image>();
        overlayImg.color = new Color(0f, 0f, 0f, 0.6f);

        // Title
        LegacyText titleTxt = MakeLabel(_pausePanel.transform, "PauseTitle", "PAUSED", 72, new Vector2(0f, 80f), new Vector2(600f, 120f));
        titleTxt.color = new Color(1f, 0.85f, 0.2f, 1f);

        // Resume button
        MakeFancyButton(_pausePanel.transform, "Resume", new Vector2(0f, -60f),
            new Vector2(360f, 80f), BtnOrange, BtnOrangeBorder, TogglePause, fontSize: 40);

        _pausePanel.SetActive(false);
    }

    private void CreateTitleImage()
    {
        Sprite spr = Resources.Load<Sprite>("titlebetter");

        GameObject go = new GameObject("TitleImage");
        go.transform.SetParent(_startPanel.transform, false);
        RectTransform rt = go.AddComponent<RectTransform>();
        rt.anchorMin = new Vector2(0.5f, 0.5f);
        rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = new Vector2(0f, 294f);

        if (spr != null)
        {
            float maxW = 1430f;
            float maxH = 437f;
            float aspect = spr.rect.width / spr.rect.height;
            float w = maxW;
            float h = w / aspect;
            if (h > maxH) { h = maxH; w = h * aspect; }
            rt.sizeDelta = new Vector2(w, h);
            Image img = go.AddComponent<Image>();
            img.sprite = spr;
            img.color = Color.white;
            img.preserveAspect = true;
        }
        else
        {
            rt.sizeDelta = new Vector2(1000f, 150f);
            LegacyText txt = go.AddComponent<LegacyText>();
            txt.text = "Crappy Turd 2000";
            txt.fontSize = 96;
            txt.color = Color.white;
            txt.fontStyle = FontStyle.Bold;
            txt.alignment = TextAnchor.MiddleCenter;
            txt.font = GetGameFont();
        }
    }

    private void CreateStartUI()
    {
        Canvas canvas = FindFirstObjectByType<Canvas>();
        if (canvas == null) return;

        _startPanel = new GameObject("StartPanel");
        _startPanel.transform.SetParent(canvas.transform, false);

        RectTransform panelRt = _startPanel.AddComponent<RectTransform>();
        panelRt.anchorMin = Vector2.zero;
        panelRt.anchorMax = Vector2.one;
        panelRt.offsetMin = Vector2.zero;
        panelRt.offsetMax = Vector2.zero;

        CreateTitleImage();
        _startSubtitleLabel = MakeLabel(_startPanel.transform, "SubtitleText", GetStartHint(), 52, new Vector2(0f, -100f), new Vector2(1300f, 110f));
        _startSubtitleLabel.horizontalOverflow = HorizontalWrapMode.Overflow;

        CreateLeaderboardUI();
        CreateDailyLeaderboardUI();
        CreateNamePickerUI();
        CreateSettingsButton();
        CreateSettingsPanel();
        CreateHeadstartToggle();
        CreateSkinsButton();
        CreateSkinsPanel();
        CreateAccountButton();
        CreateAccountPanel();

        // Ensure the start hint renders on top of leaderboards and other elements
        if (_startSubtitleLabel != null)
            _startSubtitleLabel.transform.parent.parent.SetAsLastSibling();
    }

    private void CreateNamePickerUI()
    {
        string currentName = LeaderboardManager.Instance != null ? LeaderboardManager.Instance.PlayerName : "...";

        // Border layer
        GameObject border = new GameObject("NamePickerBorder");
        border.transform.SetParent(_startPanel.transform, false);
        RectTransform borderRt = border.AddComponent<RectTransform>();
        borderRt.anchorMin = new Vector2(0.5f, 0.5f);
        borderRt.anchorMax = new Vector2(0.5f, 0.5f);
        borderRt.pivot = new Vector2(0.5f, 0.5f);
        borderRt.sizeDelta = new Vector2(910f, 90f);
        borderRt.anchoredPosition = new Vector2(0f, -260f);
        Image borderImg = border.AddComponent<Image>();
        borderImg.color = BtnMagentaBorder;

        // Face layer
        GameObject row = new GameObject("NamePicker");
        row.transform.SetParent(border.transform, false);
        RectTransform rowRt = row.AddComponent<RectTransform>();
        rowRt.anchorMin = Vector2.zero;
        rowRt.anchorMax = Vector2.one;
        rowRt.offsetMin = new Vector2(5f, 5f);
        rowRt.offsetMax = new Vector2(-5f, -5f);
        Image bg = row.AddComponent<Image>();
        bg.color = BtnMagenta;

        // "Playing as:" label — Nosifer, fixed left portion
        GameObject labelGo = new GameObject("NameLabel");
        labelGo.transform.SetParent(row.transform, false);
        _playerNameLabelRt = labelGo.AddComponent<RectTransform>();
        _playerNameLabelRt.anchorMin = new Vector2(0f, 0f);
        _playerNameLabelRt.anchorMax = new Vector2(0.4f, 1f);
        _playerNameLabelRt.offsetMin = new Vector2(20f, 0f);
        _playerNameLabelRt.offsetMax = Vector2.zero;
        _playerNameLabel = labelGo.AddComponent<LegacyText>();
        _playerNameLabel.text = "Playing as:";
        _playerNameLabel.fontSize = 32;
        _playerNameLabel.color = new Color(0.92f, 0.65f, 0.25f);
        _playerNameLabel.fontStyle = FontStyle.Bold;
        _playerNameLabel.alignment = TextAnchor.MiddleLeft;
        _playerNameLabel.font = _gameFont != null ? _gameFont : Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        _playerNameLabel.resizeTextForBestFit = false;

        // Player name value — Fruktur
        GameObject nameValGo = new GameObject("NameValue");
        nameValGo.transform.SetParent(row.transform, false);
        _playerNameValueLabelRt = nameValGo.AddComponent<RectTransform>();
        _playerNameValueLabelRt.anchorMin = new Vector2(0.4f, 0f);
        _playerNameValueLabelRt.anchorMax = new Vector2(0.68f, 1f);
        _playerNameValueLabelRt.offsetMin = Vector2.zero;
        _playerNameValueLabelRt.offsetMax = Vector2.zero;
        _playerNameValueLabel = nameValGo.AddComponent<LegacyText>();
        _playerNameValueLabel.text = currentName;
        _playerNameValueLabel.fontSize = 32;
        _playerNameValueLabel.color = new Color(0.92f, 0.65f, 0.25f);
        _playerNameValueLabel.fontStyle = FontStyle.Bold;
        _playerNameValueLabel.alignment = TextAnchor.MiddleLeft;
        _playerNameValueLabel.font = _frukturFont != null ? _frukturFont : Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        _playerNameValueLabel.resizeTextForBestFit = false;
        Outline nameValOutline = nameValGo.AddComponent<Outline>();
        nameValOutline.effectColor    = new Color(0f, 0f, 0f, 1f);
        nameValOutline.effectDistance = new Vector2(2f, -2f);
        Outline nameOutline = labelGo.AddComponent<Outline>();
        nameOutline.effectColor    = new Color(0f, 0f, 0f, 0.6f);
        nameOutline.effectDistance = new Vector2(2f, -2f);

        // Divider line
        _namePickerDivider = new GameObject("Divider");
        GameObject divGo = _namePickerDivider;
        divGo.transform.SetParent(row.transform, false);
        RectTransform divRt = divGo.AddComponent<RectTransform>();
        divRt.anchorMin = new Vector2(0.68f, 0.15f);
        divRt.anchorMax = new Vector2(0.68f, 0.85f);
        divRt.sizeDelta = new Vector2(2f, 0f);
        Image divImg = divGo.AddComponent<Image>();
        divImg.color = new Color(1f, 1f, 1f, 0.2f);

        // "New Name" button — right 32%, same dark family but slightly lighter on hover
        _newNameButton = new GameObject("RerollButton");
        GameObject btnGo = _newNameButton;
        btnGo.transform.SetParent(row.transform, false);
        RectTransform btnRt = btnGo.AddComponent<RectTransform>();
        btnRt.anchorMin = new Vector2(0.68f, 0f);
        btnRt.anchorMax = new Vector2(1f, 1f);
        btnRt.offsetMin = Vector2.zero;
        btnRt.offsetMax = Vector2.zero;
        Image btnImg = btnGo.AddComponent<Image>();
        btnImg.color = new Color(0f, 0f, 0f, 0f);
        Button btn = btnGo.AddComponent<Button>();
        btn.targetGraphic = btnImg;
        ColorBlock btnColors = btn.colors;
        btnColors.normalColor = new Color(1f, 1f, 1f, 0f);
        btnColors.highlightedColor = new Color(1f, 1f, 1f, 0.1f);
        btnColors.pressedColor = new Color(1f, 1f, 1f, 0.2f);
        btn.colors = btnColors;
        btn.onClick.AddListener(() =>
        {
            if (LeaderboardManager.Instance == null) return;
            LeaderboardManager.Instance.RegenerateName((newName) =>
            {
                if (_playerNameValueLabel != null)
                    _playerNameValueLabel.text = newName;
            });
        });

        GameObject btnTextGo = new GameObject("Label");
        btnTextGo.transform.SetParent(btnGo.transform, false);
        RectTransform btnTextRt = btnTextGo.AddComponent<RectTransform>();
        btnTextRt.anchorMin = Vector2.zero;
        btnTextRt.anchorMax = Vector2.one;
        btnTextRt.offsetMin = Vector2.zero;
        btnTextRt.offsetMax = Vector2.zero;
        LegacyText btnText = btnTextGo.AddComponent<LegacyText>();
        btnText.text = "New Name";
        btnText.fontSize = 32;
        btnText.color = new Color(0.92f, 0.65f, 0.25f);
        btnText.alignment = TextAnchor.MiddleCenter;
        btnText.fontStyle = FontStyle.Bold;
        btnText.font = GetGameFont();
        Outline newNameOutline = btnTextGo.AddComponent<Outline>();
        newNameOutline.effectColor    = new Color(0f, 0f, 0f, 0.6f);
        newNameOutline.effectDistance = new Vector2(2f, -2f);
    }

    private void CreateHeadstartToggle()
    {
        _headstartToggleBtn = MakeFancyButton(_startPanel.transform, "Headstart",
            new Vector2(0f, -365f), new Vector2(420f, 60f),
            _headstartEnabled ? BtnGold : BtnGray,
            _headstartEnabled ? BtnGoldBorder : BtnGrayBorder,
            ToggleHeadstart, fontSize: 30);
        _headstartToggleLbl = _headstartToggleBtn.transform.Find("Label")?.GetComponent<LegacyText>();
        UpdateHeadstartToggleLabel();
        _headstartToggleBtn.transform.parent.gameObject.SetActive(HighScore >= 2);
    }

    private void ToggleHeadstart()
    {
        _headstartEnabled = !_headstartEnabled;
        PlayerPrefs.SetInt(HeadstartPrefKey, _headstartEnabled ? 1 : 0);
        PlayerPrefs.Save();
        Image face = _headstartToggleBtn.GetComponent<Image>();
        Image border = _headstartToggleBtn.transform.parent?.GetComponent<Image>();
        if (face != null) face.color = _headstartEnabled ? BtnGold : BtnGray;
        if (border != null) border.color = _headstartEnabled ? BtnGoldBorder : BtnGrayBorder;
        UpdateHeadstartToggleLabel();
    }

    private void UpdateHeadstartToggleLabel()
    {
        if (_headstartToggleLbl == null) return;
        int headstart = HighScore / 2;
        _headstartToggleLbl.text = _headstartEnabled
            ? $"Headstart: ON  (start at {headstart})"
            : "Headstart: OFF";
    }

    private void CreateSettingsButton()
    {
        MakeFancyButton(_startPanel.transform, "Settings", new Vector2(-320f, -460f),
            new Vector2(210f, 60f), BtnBlue, BtnBlueBorder,
            () => _settingsPanel.SetActive(true), fontSize: 30);
    }

    private void CreateSettingsPanel()
    {
        Canvas canvas = FindFirstObjectByType<Canvas>();
        _settingsPanel = new GameObject("SettingsPanel");
        _settingsPanel.transform.SetParent(canvas.transform, false);

        RectTransform overlayRt = _settingsPanel.AddComponent<RectTransform>();
        overlayRt.anchorMin = Vector2.zero;
        overlayRt.anchorMax = Vector2.one;
        overlayRt.offsetMin = Vector2.zero;
        overlayRt.offsetMax = Vector2.zero;
        Image overlayImg = _settingsPanel.AddComponent<Image>();
        overlayImg.color = new Color(0f, 0f, 0f, 0.82f);

        // Content box
        GameObject box = new GameObject("Box");
        box.transform.SetParent(_settingsPanel.transform, false);
        RectTransform boxRt = box.AddComponent<RectTransform>();
        boxRt.anchorMin = new Vector2(0.5f, 0.5f);
        boxRt.anchorMax = new Vector2(0.5f, 0.5f);
        boxRt.pivot = new Vector2(0.5f, 0.5f);
        boxRt.sizeDelta = new Vector2(600f, 420f);
        boxRt.anchoredPosition = Vector2.zero;
        Image boxImg = box.AddComponent<Image>();
        boxImg.color = new Color(0.1f, 0.1f, 0.1f, 1f);

        // Title
        GameObject titleGo = new GameObject("Title");
        titleGo.transform.SetParent(box.transform, false);
        RectTransform titleRt = titleGo.AddComponent<RectTransform>();
        titleRt.anchorMin = new Vector2(0f, 1f);
        titleRt.anchorMax = new Vector2(1f, 1f);
        titleRt.pivot = new Vector2(0.5f, 1f);
        titleRt.sizeDelta = new Vector2(0f, 60f);
        titleRt.anchoredPosition = new Vector2(0f, 0f);
        LegacyText titleTxt = titleGo.AddComponent<LegacyText>();
        titleTxt.text = "Settings";
        titleTxt.fontSize = 38;
        titleTxt.color = new Color(1f, 0.85f, 0.2f, 1f);
        titleTxt.alignment = TextAnchor.MiddleCenter;
        titleTxt.fontStyle = FontStyle.Bold;
        titleTxt.font = GetGameFont();

        // Music Volume label
        GameObject volLabelGo = new GameObject("VolumeLabel");
        volLabelGo.transform.SetParent(box.transform, false);
        RectTransform volLabelRt = volLabelGo.AddComponent<RectTransform>();
        volLabelRt.anchorMin = new Vector2(0f, 1f);
        volLabelRt.anchorMax = new Vector2(1f, 1f);
        volLabelRt.pivot = new Vector2(0.5f, 1f);
        volLabelRt.sizeDelta = new Vector2(0f, 50f);
        volLabelRt.anchoredPosition = new Vector2(0f, -70f);
        volLabelRt.offsetMin = new Vector2(30f, volLabelRt.offsetMin.y);
        volLabelRt.offsetMax = new Vector2(-30f, volLabelRt.offsetMax.y);
        LegacyText volLabelTxt = volLabelGo.AddComponent<LegacyText>();
        volLabelTxt.fontSize = 28;
        volLabelTxt.color = Color.white;
        volLabelTxt.alignment = TextAnchor.MiddleLeft;
        volLabelTxt.font = GetGameFont();

        // Slider
        int savedStep = Mathf.RoundToInt(PlayerPrefs.GetFloat(MusicVolumePrefKey, 0.35f) * 10f);
        volLabelTxt.text = $"Music Volume:  {savedStep * 10}%";

        GameObject sliderGo = new GameObject("VolumeSlider");
        sliderGo.transform.SetParent(box.transform, false);
        RectTransform sliderRt = sliderGo.AddComponent<RectTransform>();
        sliderRt.anchorMin = new Vector2(0f, 1f);
        sliderRt.anchorMax = new Vector2(1f, 1f);
        sliderRt.pivot = new Vector2(0.5f, 1f);
        sliderRt.sizeDelta = new Vector2(0f, 50f);
        sliderRt.anchoredPosition = new Vector2(0f, -128f);
        sliderRt.offsetMin = new Vector2(30f, sliderRt.offsetMin.y);
        sliderRt.offsetMax = new Vector2(-30f, sliderRt.offsetMax.y);

        // Slider background track
        GameObject bgGo = new GameObject("Background");
        bgGo.transform.SetParent(sliderGo.transform, false);
        RectTransform bgRt = bgGo.AddComponent<RectTransform>();
        bgRt.anchorMin = new Vector2(0f, 0.25f);
        bgRt.anchorMax = new Vector2(1f, 0.75f);
        bgRt.offsetMin = Vector2.zero;
        bgRt.offsetMax = Vector2.zero;
        Image bgImg = bgGo.AddComponent<Image>();
        bgImg.color = new Color(0.3f, 0.3f, 0.3f, 1f);

        // Fill area
        GameObject fillAreaGo = new GameObject("Fill Area");
        fillAreaGo.transform.SetParent(sliderGo.transform, false);
        RectTransform fillAreaRt = fillAreaGo.AddComponent<RectTransform>();
        fillAreaRt.anchorMin = new Vector2(0f, 0.25f);
        fillAreaRt.anchorMax = new Vector2(1f, 0.75f);
        fillAreaRt.offsetMin = new Vector2(5f, 0f);
        fillAreaRt.offsetMax = new Vector2(-15f, 0f);

        GameObject fillGo = new GameObject("Fill");
        fillGo.transform.SetParent(fillAreaGo.transform, false);
        RectTransform fillRt = fillGo.AddComponent<RectTransform>();
        fillRt.anchorMin = Vector2.zero;
        fillRt.anchorMax = new Vector2(0f, 1f);
        fillRt.offsetMin = Vector2.zero;
        fillRt.offsetMax = new Vector2(10f, 0f);
        Image fillImg = fillGo.AddComponent<Image>();
        fillImg.color = new Color(0.2f, 0.6f, 1f, 1f);

        // Handle area
        GameObject handleAreaGo = new GameObject("Handle Slide Area");
        handleAreaGo.transform.SetParent(sliderGo.transform, false);
        RectTransform handleAreaRt = handleAreaGo.AddComponent<RectTransform>();
        handleAreaRt.anchorMin = Vector2.zero;
        handleAreaRt.anchorMax = Vector2.one;
        handleAreaRt.offsetMin = new Vector2(10f, 0f);
        handleAreaRt.offsetMax = new Vector2(-10f, 0f);

        GameObject handleGo = new GameObject("Handle");
        handleGo.transform.SetParent(handleAreaGo.transform, false);
        RectTransform handleRt = handleGo.AddComponent<RectTransform>();
        handleRt.anchorMin = new Vector2(0f, 0f);
        handleRt.anchorMax = new Vector2(0f, 1f);
        handleRt.sizeDelta = new Vector2(30f, 0f);
        Image handleImg = handleGo.AddComponent<Image>();
        handleImg.color = Color.white;

        Slider slider = sliderGo.AddComponent<Slider>();
        slider.fillRect = fillRt;
        slider.handleRect = handleRt;
        slider.direction = Slider.Direction.LeftToRight;
        slider.minValue = 0;
        slider.maxValue = 10;
        slider.wholeNumbers = true;
        slider.value = savedStep;
        slider.onValueChanged.AddListener((val) =>
        {
            float volume = val / 10f;
            _musicSource.volume = volume;
            PlayerPrefs.SetFloat(MusicVolumePrefKey, volume);
            PlayerPrefs.Save();
            volLabelTxt.text = $"Music Volume:  {(int)(val * 10)}%";
        });

        // Sound Effects label
        int savedSFXStep = Mathf.RoundToInt(PlayerPrefs.GetFloat(SFXVolumePrefKey, 1f) * 10f);
        GameObject sfxLabelGo = new GameObject("SFXLabel");
        sfxLabelGo.transform.SetParent(box.transform, false);
        RectTransform sfxLabelRt = sfxLabelGo.AddComponent<RectTransform>();
        sfxLabelRt.anchorMin = new Vector2(0f, 1f);
        sfxLabelRt.anchorMax = new Vector2(1f, 1f);
        sfxLabelRt.pivot = new Vector2(0.5f, 1f);
        sfxLabelRt.sizeDelta = new Vector2(0f, 50f);
        sfxLabelRt.anchoredPosition = new Vector2(0f, -210f);
        sfxLabelRt.offsetMin = new Vector2(30f, sfxLabelRt.offsetMin.y);
        sfxLabelRt.offsetMax = new Vector2(-30f, sfxLabelRt.offsetMax.y);
        LegacyText sfxLabelTxt = sfxLabelGo.AddComponent<LegacyText>();
        sfxLabelTxt.text = $"Sound Effects:  {savedSFXStep * 10}%";
        sfxLabelTxt.fontSize = 28;
        sfxLabelTxt.color = Color.white;
        sfxLabelTxt.alignment = TextAnchor.MiddleLeft;
        sfxLabelTxt.font = GetGameFont();

        // Sound Effects slider
        GameObject sfxSliderGo = new GameObject("SFXSlider");
        sfxSliderGo.transform.SetParent(box.transform, false);
        RectTransform sfxSliderRt = sfxSliderGo.AddComponent<RectTransform>();
        sfxSliderRt.anchorMin = new Vector2(0f, 1f);
        sfxSliderRt.anchorMax = new Vector2(1f, 1f);
        sfxSliderRt.pivot = new Vector2(0.5f, 1f);
        sfxSliderRt.sizeDelta = new Vector2(0f, 50f);
        sfxSliderRt.anchoredPosition = new Vector2(0f, -268f);
        sfxSliderRt.offsetMin = new Vector2(30f, sfxSliderRt.offsetMin.y);
        sfxSliderRt.offsetMax = new Vector2(-30f, sfxSliderRt.offsetMax.y);

        GameObject sfxBgGo = new GameObject("Background");
        sfxBgGo.transform.SetParent(sfxSliderGo.transform, false);
        RectTransform sfxBgRt = sfxBgGo.AddComponent<RectTransform>();
        sfxBgRt.anchorMin = new Vector2(0f, 0.25f);
        sfxBgRt.anchorMax = new Vector2(1f, 0.75f);
        sfxBgRt.offsetMin = Vector2.zero;
        sfxBgRt.offsetMax = Vector2.zero;
        Image sfxBgImg = sfxBgGo.AddComponent<Image>();
        sfxBgImg.color = new Color(0.3f, 0.3f, 0.3f, 1f);

        GameObject sfxFillAreaGo = new GameObject("Fill Area");
        sfxFillAreaGo.transform.SetParent(sfxSliderGo.transform, false);
        RectTransform sfxFillAreaRt = sfxFillAreaGo.AddComponent<RectTransform>();
        sfxFillAreaRt.anchorMin = new Vector2(0f, 0.25f);
        sfxFillAreaRt.anchorMax = new Vector2(1f, 0.75f);
        sfxFillAreaRt.offsetMin = new Vector2(5f, 0f);
        sfxFillAreaRt.offsetMax = new Vector2(-15f, 0f);

        GameObject sfxFillGo = new GameObject("Fill");
        sfxFillGo.transform.SetParent(sfxFillAreaGo.transform, false);
        RectTransform sfxFillRt = sfxFillGo.AddComponent<RectTransform>();
        sfxFillRt.anchorMin = Vector2.zero;
        sfxFillRt.anchorMax = new Vector2(0f, 1f);
        sfxFillRt.offsetMin = Vector2.zero;
        sfxFillRt.offsetMax = new Vector2(10f, 0f);
        Image sfxFillImg = sfxFillGo.AddComponent<Image>();
        sfxFillImg.color = new Color(0.2f, 0.6f, 1f, 1f);

        GameObject sfxHandleAreaGo = new GameObject("Handle Slide Area");
        sfxHandleAreaGo.transform.SetParent(sfxSliderGo.transform, false);
        RectTransform sfxHandleAreaRt = sfxHandleAreaGo.AddComponent<RectTransform>();
        sfxHandleAreaRt.anchorMin = Vector2.zero;
        sfxHandleAreaRt.anchorMax = Vector2.one;
        sfxHandleAreaRt.offsetMin = new Vector2(10f, 0f);
        sfxHandleAreaRt.offsetMax = new Vector2(-10f, 0f);

        GameObject sfxHandleGo = new GameObject("Handle");
        sfxHandleGo.transform.SetParent(sfxHandleAreaGo.transform, false);
        RectTransform sfxHandleRt = sfxHandleGo.AddComponent<RectTransform>();
        sfxHandleRt.anchorMin = new Vector2(0f, 0f);
        sfxHandleRt.anchorMax = new Vector2(0f, 1f);
        sfxHandleRt.sizeDelta = new Vector2(30f, 0f);
        Image sfxHandleImg = sfxHandleGo.AddComponent<Image>();
        sfxHandleImg.color = Color.white;

        Slider sfxSlider = sfxSliderGo.AddComponent<Slider>();
        sfxSlider.fillRect = sfxFillRt;
        sfxSlider.handleRect = sfxHandleRt;
        sfxSlider.direction = Slider.Direction.LeftToRight;
        sfxSlider.minValue = 0;
        sfxSlider.maxValue = 10;
        sfxSlider.wholeNumbers = true;
        sfxSlider.value = savedSFXStep;
        sfxSlider.onValueChanged.AddListener((val) =>
        {
            float volume = val / 10f;
            _audioSource.volume = volume;
            PlayerPrefs.SetFloat(SFXVolumePrefKey, volume);
            PlayerPrefs.Save();
            sfxLabelTxt.text = $"Sound Effects:  {(int)(val * 10)}%";
        });

        // Close button
        MakeFancyButton(box.transform, "Close", new Vector2(0f, 20f),
            new Vector2(200f, 55f), BtnRed, BtnRedBorder,
            () => _settingsPanel.SetActive(false),
            new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), 28);

        _settingsPanel.SetActive(false);
    }

    private void CreateSkinsButton()
    {
        MakeFancyButton(_startPanel.transform, "Skins", new Vector2(0f, -460f),
            new Vector2(210f, 60f), BtnPurple, BtnPurpleBorder,
            () => { RefreshSkinsPanel(); _skinsPanel.SetActive(true); }, fontSize: 30);
    }

    private void CreateSkinsPanel()
    {
        Canvas canvas = FindFirstObjectByType<Canvas>();
        _skinsPanel = new GameObject("SkinsPanel");
        _skinsPanel.transform.SetParent(canvas.transform, false);

        RectTransform overlayRt = _skinsPanel.AddComponent<RectTransform>();
        overlayRt.anchorMin = Vector2.zero;
        overlayRt.anchorMax = Vector2.one;
        overlayRt.offsetMin = Vector2.zero;
        overlayRt.offsetMax = Vector2.zero;
        Image overlayImg = _skinsPanel.AddComponent<Image>();
        overlayImg.color = new Color(0f, 0f, 0f, 0.85f);

        // Content box
        GameObject box = new GameObject("Box");
        box.transform.SetParent(_skinsPanel.transform, false);
        RectTransform boxRt = box.AddComponent<RectTransform>();
        boxRt.anchorMin = new Vector2(0.5f, 0.5f);
        boxRt.anchorMax = new Vector2(0.5f, 0.5f);
        boxRt.pivot = new Vector2(0.5f, 0.5f);
        boxRt.sizeDelta = new Vector2(800f, 660f);
        boxRt.anchoredPosition = Vector2.zero;
        Image boxImg = box.AddComponent<Image>();
        boxImg.color = new Color(0.1f, 0.1f, 0.1f, 1f);

        // Title
        GameObject titleGo = new GameObject("Title");
        titleGo.transform.SetParent(box.transform, false);
        RectTransform titleRt = titleGo.AddComponent<RectTransform>();
        titleRt.anchorMin = new Vector2(0f, 1f);
        titleRt.anchorMax = new Vector2(1f, 1f);
        titleRt.pivot = new Vector2(0.5f, 1f);
        titleRt.sizeDelta = new Vector2(0f, 70f);
        titleRt.anchoredPosition = Vector2.zero;
        LegacyText titleTxt = titleGo.AddComponent<LegacyText>();
        titleTxt.text = "Skins";
        titleTxt.fontSize = 44;
        titleTxt.color = new Color(1f, 0.85f, 0.2f, 1f);
        titleTxt.fontStyle = FontStyle.Bold;
        titleTxt.alignment = TextAnchor.MiddleCenter;
        titleTxt.font = GetGameFont();

        // Build one card per skin
        int skinCount = SkinManager.AllSkins.Length;
        _skinEquipButtons = new Button[skinCount];
        _skinStatusLabels = new LegacyText[skinCount];
        _skinCardBorders = new Image[skinCount];

        float cardW = 280f;
        float cardH = 420f;
        float spacing = 40f;
        float totalW = skinCount * cardW + (skinCount - 1) * spacing;
        float startX = -totalW / 2f + cardW / 2f;

        for (int i = 0; i < skinCount; i++)
        {
            SkinManager.SkinDefinition skin = SkinManager.AllSkins[i];
            float cardX = startX + i * (cardW + spacing);

            // Card background
            GameObject card = new GameObject("Card_" + skin.Id);
            card.transform.SetParent(box.transform, false);
            RectTransform cardRt = card.AddComponent<RectTransform>();
            cardRt.anchorMin = new Vector2(0.5f, 0.5f);
            cardRt.anchorMax = new Vector2(0.5f, 0.5f);
            cardRt.pivot = new Vector2(0.5f, 0.5f);
            cardRt.sizeDelta = new Vector2(cardW, cardH);
            cardRt.anchoredPosition = new Vector2(cardX, -10f);
            Image cardImg = card.AddComponent<Image>();
            cardImg.color = new Color(0.18f, 0.18f, 0.18f, 1f);
            _skinCardBorders[i] = cardImg;

            // Power description — top of card
            GameObject descGo = new GameObject("Desc");
            descGo.transform.SetParent(card.transform, false);
            RectTransform descRt = descGo.AddComponent<RectTransform>();
            descRt.anchorMin = new Vector2(0f, 1f);
            descRt.anchorMax = new Vector2(1f, 1f);
            descRt.pivot = new Vector2(0.5f, 1f);
            descRt.sizeDelta = new Vector2(0f, 40f);
            descRt.anchoredPosition = new Vector2(0f, -8f);
            LegacyText descTxt = descGo.AddComponent<LegacyText>();
            descTxt.text = skin.PowerDescription;
            descTxt.fontSize = 24;
            descTxt.fontStyle = FontStyle.Italic;
            descTxt.color = new Color(0.6f, 0.85f, 1f, 1f);
            descTxt.alignment = TextAnchor.MiddleCenter;
            descTxt.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");

            // Sprite preview
            Sprite spr = Resources.Load<Sprite>(skin.ResourcePath);
            if (spr != null)
            {
                GameObject sprGo = new GameObject("Preview");
                sprGo.transform.SetParent(card.transform, false);
                RectTransform sprRt = sprGo.AddComponent<RectTransform>();
                sprRt.anchorMin = new Vector2(0.5f, 1f);
                sprRt.anchorMax = new Vector2(0.5f, 1f);
                sprRt.pivot = new Vector2(0.5f, 1f);
                sprRt.sizeDelta = new Vector2(160f, 190f);
                sprRt.anchoredPosition = new Vector2(0f, -55f);
                Image sprImg = sprGo.AddComponent<Image>();
                sprImg.sprite = spr;
                sprImg.preserveAspect = true;
                sprImg.color = Color.white;
            }

            // Skin name
            GameObject nameGo = new GameObject("Name");
            nameGo.transform.SetParent(card.transform, false);
            RectTransform nameRt = nameGo.AddComponent<RectTransform>();
            nameRt.anchorMin = new Vector2(0f, 1f);
            nameRt.anchorMax = new Vector2(1f, 1f);
            nameRt.pivot = new Vector2(0.5f, 1f);
            nameRt.sizeDelta = new Vector2(0f, 60f);
            nameRt.anchoredPosition = new Vector2(0f, -252f);
            LegacyText nameTxt = nameGo.AddComponent<LegacyText>();
            nameTxt.text = skin.DisplayName;
            nameTxt.fontSize = 28;
            nameTxt.color = Color.white;
            nameTxt.fontStyle = FontStyle.Bold;
            nameTxt.alignment = TextAnchor.MiddleCenter;
            nameTxt.font = GetGameFont();

            // Status label (e.g. unlock hint for locked skins)
            GameObject hintGo = new GameObject("Hint");
            hintGo.transform.SetParent(card.transform, false);
            RectTransform hintRt = hintGo.AddComponent<RectTransform>();
            hintRt.anchorMin = new Vector2(0f, 1f);
            hintRt.anchorMax = new Vector2(1f, 1f);
            hintRt.pivot = new Vector2(0.5f, 1f);
            hintRt.sizeDelta = new Vector2(0f, 36f);
            hintRt.anchoredPosition = new Vector2(0f, -318f);
            LegacyText hintTxt = hintGo.AddComponent<LegacyText>();
            hintTxt.fontSize = 20;
            hintTxt.color = new Color(0.7f, 0.7f, 0.7f, 1f);
            hintTxt.alignment = TextAnchor.MiddleCenter;
            hintTxt.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            _skinStatusLabels[i] = hintTxt;

            // Equip button
            string capturedId = skin.Id;
            Button btn = MakeFancyButton(card.transform, "Equip",
                new Vector2(0f, -360f), new Vector2(190f, 50f),
                BtnGreen, BtnGreenBorder,
                () => { SkinManager.Instance?.SetActiveSkin(capturedId); RefreshSkinsPanel(); },
                new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), 24);
            _skinEquipButtons[i] = btn;
        }

        // Close button
        MakeFancyButton(box.transform, "Close", new Vector2(0f, 20f),
            new Vector2(200f, 52f), BtnRed, BtnRedBorder,
            () => _skinsPanel.SetActive(false),
            new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), 26);

        _skinsPanel.SetActive(false);
    }

    private void RefreshSkinsPanel()
    {
        if (_skinEquipButtons == null) return;
        string activeSkinId = SkinManager.Instance?.ActiveSkinId ?? "mr_crappy";

        Color gold = new Color(1f, 0.85f, 0.2f, 1f);
        Color darkBg = new Color(0.18f, 0.18f, 0.18f, 1f);

        for (int i = 0; i < SkinManager.AllSkins.Length; i++)
        {
            if (i >= _skinEquipButtons.Length) break;
            SkinManager.SkinDefinition skin = SkinManager.AllSkins[i];
            bool unlocked = SkinManager.Instance?.IsUnlocked(skin.Id) ?? (skin.Id == "mr_crappy");
            bool equipped = skin.Id == activeSkinId;

            Button btn = _skinEquipButtons[i];
            LegacyText hint = _skinStatusLabels[i];
            LegacyText btnLbl = btn.transform.Find("Label")?.GetComponent<LegacyText>();
            Image border = _skinCardBorders[i];

            border.color = equipped ? new Color(0.25f, 0.22f, 0.05f, 1f) : darkBg;

            Image face = btn.GetComponent<Image>();
            Image borderImg = btn.transform.parent != null ? btn.transform.parent.GetComponent<Image>() : null;

            if (equipped)
            {
                btn.interactable = false;
                if (face != null) face.color = BtnGold;
                if (borderImg != null) borderImg.color = BtnGoldBorder;
                if (btnLbl != null) { btnLbl.text = "Equipped"; btnLbl.color = Color.white; }
                if (hint != null) hint.text = "";
            }
            else if (unlocked)
            {
                btn.interactable = true;
                if (face != null) face.color = BtnGreen;
                if (borderImg != null) borderImg.color = BtnGreenBorder;
                if (btnLbl != null) { btnLbl.text = "Equip"; btnLbl.color = Color.white; }
                if (hint != null) hint.text = "";
            }
            else
            {
                btn.interactable = false;
                if (face != null) face.color = BtnGray;
                if (borderImg != null) borderImg.color = BtnGrayBorder;
                if (btnLbl != null) { btnLbl.text = "Equip"; btnLbl.color = new Color(0.75f, 0.75f, 0.75f, 1f); }
                if (hint != null) { hint.text = $"Score {skin.UnlockScore}+ to unlock"; hint.color = new Color(0.75f, 0.75f, 0.75f, 1f); }
            }
        }
    }

    private void CreateAccountButton()
    {
        Button btn = MakeFancyButton(_startPanel.transform, "Login", new Vector2(320f, -460f),
            new Vector2(210f, 60f), BtnGreen, BtnGreenBorder,
            () => _accountPanel.SetActive(true), fontSize: 26);
        _accountButtonLabel = btn.transform.Find("Label")?.GetComponent<LegacyText>();
    }

    private void CreateAccountPanel()
    {
        Canvas canvas = FindFirstObjectByType<Canvas>();
        _accountPanel = new GameObject("AccountPanel");
        _accountPanel.transform.SetParent(canvas.transform, false);

        RectTransform overlayRt = _accountPanel.AddComponent<RectTransform>();
        overlayRt.anchorMin = Vector2.zero;
        overlayRt.anchorMax = Vector2.one;
        overlayRt.offsetMin = Vector2.zero;
        overlayRt.offsetMax = Vector2.zero;
        Image overlayImg = _accountPanel.AddComponent<Image>();
        overlayImg.color = new Color(0f, 0f, 0f, 0.82f);

        // Center box — large enough to be comfortably tappable on mobile
        GameObject box = new GameObject("Box");
        box.transform.SetParent(_accountPanel.transform, false);
        RectTransform boxRt = box.AddComponent<RectTransform>();
        boxRt.anchorMin = new Vector2(0.5f, 0.5f);
        boxRt.anchorMax = new Vector2(0.5f, 0.5f);
        boxRt.pivot = new Vector2(0.5f, 0.5f);
        boxRt.sizeDelta = new Vector2(820f, 560f);
        boxRt.anchoredPosition = Vector2.zero;
        Image boxImg = box.AddComponent<Image>();
        boxImg.color = new Color(0.1f, 0.1f, 0.1f, 1f);

        // Title
        GameObject titleGo = new GameObject("Title");
        titleGo.transform.SetParent(box.transform, false);
        RectTransform titleRt = titleGo.AddComponent<RectTransform>();
        titleRt.anchorMin = new Vector2(0f, 1f);
        titleRt.anchorMax = new Vector2(1f, 1f);
        titleRt.pivot = new Vector2(0.5f, 1f);
        titleRt.sizeDelta = new Vector2(0f, 72f);
        titleRt.anchoredPosition = Vector2.zero;
        _accountPanelTitle = titleGo.AddComponent<LegacyText>();
        _accountPanelTitle.fontSize = 42;
        _accountPanelTitle.color = new Color(1f, 0.85f, 0.2f, 1f);
        _accountPanelTitle.alignment = TextAnchor.MiddleCenter;
        _accountPanelTitle.fontStyle = FontStyle.Bold;
        _accountPanelTitle.font = GetGameFont();

        // Close button (always visible)
        MakeSmallButton(box.transform, "Close", new Vector2(0f, 16f), new Vector2(240f, 65f),
            BtnRed, anchorBottom: true,
            onClick: () => _accountPanel.SetActive(false));

        // ---- Guest sub-panel ----
        _accountGuestPanel = new GameObject("GuestPanel");
        _accountGuestPanel.transform.SetParent(box.transform, false);
        RectTransform guestRt = _accountGuestPanel.AddComponent<RectTransform>();
        guestRt.anchorMin = Vector2.zero;
        guestRt.anchorMax = Vector2.one;
        guestRt.offsetMin = new Vector2(40f, 88f);
        guestRt.offsetMax = new Vector2(-40f, -76f);

        // Description
        GameObject descGo = new GameObject("Desc");
        descGo.transform.SetParent(_accountGuestPanel.transform, false);
        RectTransform descRt = descGo.AddComponent<RectTransform>();
        descRt.anchorMin = new Vector2(0f, 1f);
        descRt.anchorMax = new Vector2(1f, 1f);
        descRt.pivot = new Vector2(0.5f, 1f);
        descRt.sizeDelta = new Vector2(0f, 60f);
        descRt.anchoredPosition = Vector2.zero;
        LegacyText descTxt = descGo.AddComponent<LegacyText>();
        descTxt.text = "A username + PIN saves your scores across devices.\nGuests can still play — scores only save on this device.";
        descTxt.fontSize = 22;
        descTxt.color = new Color(0.72f, 0.72f, 0.72f, 1f);
        descTxt.alignment = TextAnchor.MiddleCenter;
        descTxt.font = GetGameFont();

        // Username field
        InputField usernameField = MakeInputField(_accountGuestPanel.transform, "Username (3–20 chars)", new Vector2(0f, -70f), new Vector2(0f, 70f));

        // PIN field
        InputField pinField = MakeInputField(_accountGuestPanel.transform, "4-digit PIN", new Vector2(0f, -150f), new Vector2(0f, 70f));
        pinField.contentType = InputField.ContentType.Pin;
        pinField.characterLimit = 4;

        // Status / error text
        GameObject statusGo = new GameObject("StatusText");
        statusGo.transform.SetParent(_accountGuestPanel.transform, false);
        RectTransform statusRt = statusGo.AddComponent<RectTransform>();
        statusRt.anchorMin = new Vector2(0f, 1f);
        statusRt.anchorMax = new Vector2(1f, 1f);
        statusRt.pivot = new Vector2(0.5f, 1f);
        statusRt.sizeDelta = new Vector2(0f, 52f);
        statusRt.anchoredPosition = new Vector2(0f, -228f);
        _accountStatusText = statusGo.AddComponent<LegacyText>();
        _accountStatusText.fontSize = 28;
        _accountStatusText.color = new Color(1f, 0.4f, 0.4f, 1f);
        _accountStatusText.alignment = TextAnchor.MiddleCenter;
        _accountStatusText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");

        // Create Account button
        MakeSmallButton(_accountGuestPanel.transform, "Create Account",
            new Vector2(-150f, -298f), new Vector2(270f, 70f),
            BtnBlue, anchorBottom: false,
            onClick: () =>
            {
                string username = usernameField.text.Trim();
                string pin = pinField.text.Trim();
                if (username.Length < 3) { _accountStatusText.text = "Username must be at least 3 characters."; return; }
                if (username.Length > 20) { _accountStatusText.text = "Username must be 20 characters or fewer."; return; }
                if (ContainsProfanity(username)) { _accountStatusText.text = "That username isn't allowed. Try another."; return; }
                if (pin.Length != 4 || !int.TryParse(pin, out _)) { _accountStatusText.text = "PIN must be exactly 4 digits."; return; }
                _accountStatusText.text = "Creating account...";
                _accountStatusText.color = new Color(0.72f, 0.72f, 0.72f, 1f);
                LeaderboardManager.Instance?.CreateAccount(username, pin, (success, error) =>
                {
                    if (success) { _accountPanel.SetActive(false); RefreshAccountUI(); RefreshLeaderboard(); RefreshDailyLeaderboard(); }
                    else { _accountStatusText.text = error; _accountStatusText.color = new Color(1f, 0.4f, 0.4f, 1f); }
                });
            });

        // Log In button
        MakeSmallButton(_accountGuestPanel.transform, "Log In",
            new Vector2(150f, -298f), new Vector2(270f, 70f),
            BtnOrange, anchorBottom: false,
            onClick: () =>
            {
                string username = usernameField.text.Trim();
                string pin = pinField.text.Trim();
                if (string.IsNullOrEmpty(username)) { _accountStatusText.text = "Enter your username."; return; }
                if (pin.Length != 4 || !int.TryParse(pin, out _)) { _accountStatusText.text = "PIN must be exactly 4 digits."; return; }
                _accountStatusText.text = "Logging in...";
                _accountStatusText.color = new Color(0.72f, 0.72f, 0.72f, 1f);
                LeaderboardManager.Instance?.Login(username, pin, (success, error) =>
                {
                    if (success) { _accountPanel.SetActive(false); RefreshAccountUI(); RefreshLeaderboard(); RefreshDailyLeaderboard(); }
                    else { _accountStatusText.text = error; _accountStatusText.color = new Color(1f, 0.4f, 0.4f, 1f); }
                });
            });

        // ---- Logged-in sub-panel ----
        _accountLoggedInPanel = new GameObject("LoggedInPanel");
        _accountLoggedInPanel.transform.SetParent(box.transform, false);
        RectTransform loggedInRt = _accountLoggedInPanel.AddComponent<RectTransform>();
        loggedInRt.anchorMin = Vector2.zero;
        loggedInRt.anchorMax = Vector2.one;
        loggedInRt.offsetMin = new Vector2(40f, 88f);
        loggedInRt.offsetMax = new Vector2(-40f, -76f);

        // "Logged in as:" label
        GameObject liLabelGo = new GameObject("LoggedInAs");
        liLabelGo.transform.SetParent(_accountLoggedInPanel.transform, false);
        RectTransform liLabelRt = liLabelGo.AddComponent<RectTransform>();
        liLabelRt.anchorMin = new Vector2(0f, 1f);
        liLabelRt.anchorMax = new Vector2(1f, 1f);
        liLabelRt.pivot = new Vector2(0.5f, 1f);
        liLabelRt.sizeDelta = new Vector2(0f, 44f);
        liLabelRt.anchoredPosition = new Vector2(0f, -15f);
        LegacyText liLabelTxt = liLabelGo.AddComponent<LegacyText>();
        liLabelTxt.text = "Logged in as:";
        liLabelTxt.fontSize = 30;
        liLabelTxt.color = new Color(0.72f, 0.72f, 0.72f, 1f);
        liLabelTxt.alignment = TextAnchor.MiddleCenter;
        liLabelTxt.font = GetGameFont();

        // Username in gold
        GameObject liNameGo = new GameObject("LoggedInName");
        liNameGo.transform.SetParent(_accountLoggedInPanel.transform, false);
        RectTransform liNameRt = liNameGo.AddComponent<RectTransform>();
        liNameRt.anchorMin = new Vector2(0f, 1f);
        liNameRt.anchorMax = new Vector2(1f, 1f);
        liNameRt.pivot = new Vector2(0.5f, 1f);
        liNameRt.sizeDelta = new Vector2(0f, 80f);
        liNameRt.anchoredPosition = new Vector2(0f, -64f);
        _accountLoggedInNameLabel = liNameGo.AddComponent<LegacyText>();
        _accountLoggedInNameLabel.fontSize = 56;
        _accountLoggedInNameLabel.color = new Color(1f, 0.85f, 0.2f, 1f);
        _accountLoggedInNameLabel.fontStyle = FontStyle.Bold;
        _accountLoggedInNameLabel.alignment = TextAnchor.MiddleCenter;
        _accountLoggedInNameLabel.font = GetGameFont();

        // Subtitle
        GameObject liSubGo = new GameObject("LoggedInSub");
        liSubGo.transform.SetParent(_accountLoggedInPanel.transform, false);
        RectTransform liSubRt = liSubGo.AddComponent<RectTransform>();
        liSubRt.anchorMin = new Vector2(0f, 1f);
        liSubRt.anchorMax = new Vector2(1f, 1f);
        liSubRt.pivot = new Vector2(0.5f, 1f);
        liSubRt.sizeDelta = new Vector2(0f, 40f);
        liSubRt.anchoredPosition = new Vector2(0f, -150f);
        LegacyText liSubTxt = liSubGo.AddComponent<LegacyText>();
        liSubTxt.text = "Your scores save across all devices.";
        liSubTxt.fontSize = 26;
        liSubTxt.color = new Color(0.72f, 0.72f, 0.72f, 1f);
        liSubTxt.alignment = TextAnchor.MiddleCenter;
        liSubTxt.font = GetGameFont();

        // Log Out button
        MakeSmallButton(_accountLoggedInPanel.transform, "Log Out",
            new Vector2(0f, -205f), new Vector2(280f, 70f),
            BtnRed, anchorBottom: false,
            onClick: () =>
            {
                LeaderboardManager.Instance?.Logout();
                _accountPanel.SetActive(false);
            });

        RefreshAccountUI();
        _accountPanel.SetActive(false);
    }

    private void RefreshAccountUI()
    {
        bool isGuest = LeaderboardManager.Instance == null || LeaderboardManager.Instance.IsGuest;
        string name = LeaderboardManager.Instance != null ? LeaderboardManager.Instance.PlayerName : "...";

        if (_playerNameLabel != null)
        {
            _playerNameLabel.alignment = isGuest ? TextAnchor.MiddleLeft : TextAnchor.MiddleCenter;
            _playerNameLabel.SetAllDirty();
        }
        if (_playerNameValueLabel != null)
        {
            _playerNameValueLabel.text = name;
            _playerNameValueLabel.alignment = isGuest ? TextAnchor.MiddleLeft : TextAnchor.MiddleCenter;
            _playerNameValueLabel.SetAllDirty();
        }
        if (_newNameButton != null)
            _newNameButton.SetActive(isGuest);
        if (_namePickerDivider != null)
            _namePickerDivider.SetActive(isGuest);
        if (_playerNameLabelRt != null)
        {
            _playerNameLabelRt.anchorMin = new Vector2(0f, 0f);
            _playerNameLabelRt.anchorMax = isGuest ? new Vector2(0.4f, 1f) : new Vector2(0.4f, 1f);
            _playerNameLabelRt.offsetMin = new Vector2(20f, 0f);
            _playerNameLabelRt.offsetMax = Vector2.zero;
            LayoutRebuilder.ForceRebuildLayoutImmediate(_playerNameLabelRt);
        }
        if (_playerNameValueLabelRt != null)
        {
            _playerNameValueLabelRt.anchorMin = new Vector2(0.4f, 0f);
            _playerNameValueLabelRt.anchorMax = isGuest ? new Vector2(0.68f, 1f) : new Vector2(1f, 1f);
            _playerNameValueLabelRt.offsetMin = Vector2.zero;
            _playerNameValueLabelRt.offsetMax = Vector2.zero;
            LayoutRebuilder.ForceRebuildLayoutImmediate(_playerNameValueLabelRt);
        }
        if (_accountButtonLabel != null)
            _accountButtonLabel.text = isGuest ? "Login" : "Account";
        if (_accountPanelTitle != null)
            _accountPanelTitle.text = isGuest ? "Save Your Progress" : "Account";
        if (_accountGuestPanel != null)
            _accountGuestPanel.SetActive(isGuest);
        if (_accountLoggedInPanel != null)
            _accountLoggedInPanel.SetActive(!isGuest);
        if (_accountLoggedInNameLabel != null && !isGuest)
            _accountLoggedInNameLabel.text = name;
        if (_accountStatusText != null)
            _accountStatusText.text = "";
    }

    private static readonly string[] _blockedWords =
    {
        "nigger","nigga","faggot","faget","fagot","chink","spic","spick","kike","gook","wetback",
        "tranny","retard","cunt","beaner","cracker","raghead","towelhead","sandnigger","zipperhead",
        "dyke","troon","shemale","cripple","tard","mongoloid",
        "hitler","nazi","kkk","nonce","pedo","pedophile","rapist",
        "cum","cums","cumshot","cumslut","jizz","cock","cocks","cocksucker","dick","dicks","dickhead",
        "pussy","pussies","tits","titties","boobs","boner","erection","penis","vagina","vulva",
        "blowjob","handjob","rimjob","deepthroat","gangbang","orgy","threesome","creampie",
        "hentai","pornhub","onlyfans","dildo","vibrator","fleshlight","buttplug","anal","anus",
        "masturbat","foreskin","testicl","ballsack","nutsack","scrotum","clitoris","labia",
        "slutty","whore","hooker","escort","stripper","camgirl","sexting","nudes","nude",
    };

    private static bool ContainsProfanity(string text)
    {
        string lower = text.ToLower();
        foreach (string word in _blockedWords)
            if (lower.Contains(word)) return true;
        return false;
    }

    private InputField MakeInputField(Transform parent, string placeholder, Vector2 anchoredPos, Vector2 sizeDelta)
    {
        GameObject go = new GameObject("InputField_" + placeholder);
        go.transform.SetParent(parent, false);
        RectTransform rt = go.AddComponent<RectTransform>();
        rt.anchorMin = new Vector2(0f, 1f);
        rt.anchorMax = new Vector2(1f, 1f);
        rt.pivot = new Vector2(0.5f, 1f);
        rt.sizeDelta = sizeDelta;
        rt.anchoredPosition = anchoredPos;
        Image bg = go.AddComponent<Image>();
        bg.color = new Color(0.18f, 0.18f, 0.18f, 1f);

        GameObject phGo = new GameObject("Placeholder");
        phGo.transform.SetParent(go.transform, false);
        RectTransform phRt = phGo.AddComponent<RectTransform>();
        phRt.anchorMin = Vector2.zero;
        phRt.anchorMax = Vector2.one;
        phRt.offsetMin = new Vector2(12f, 4f);
        phRt.offsetMax = new Vector2(-12f, -4f);
        LegacyText phTxt = phGo.AddComponent<LegacyText>();
        phTxt.text = placeholder;
        phTxt.fontSize = 28;
        phTxt.color = new Color(0.45f, 0.45f, 0.45f, 1f);
        phTxt.fontStyle = FontStyle.Italic;
        phTxt.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");

        GameObject textGo = new GameObject("Text");
        textGo.transform.SetParent(go.transform, false);
        RectTransform textRt = textGo.AddComponent<RectTransform>();
        textRt.anchorMin = Vector2.zero;
        textRt.anchorMax = Vector2.one;
        textRt.offsetMin = new Vector2(12f, 4f);
        textRt.offsetMax = new Vector2(-12f, -4f);
        LegacyText txt = textGo.AddComponent<LegacyText>();
        txt.fontSize = 28;
        txt.color = Color.white;
        txt.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        txt.supportRichText = false;

        InputField field = go.AddComponent<InputField>();
        field.textComponent = txt;
        field.placeholder = phTxt;
        return field;
    }

    // Creates a colorful bordered button. Returns the face Image so callers can store a ref for color changes.
    private Button MakeFancyButton(Transform parent, string label, Vector2 anchoredPos, Vector2 size,
        Color faceColor, Color borderColor, System.Action onClick,
        Vector2 anchorMin = default, Vector2 anchorMax = default, Vector2 pivot = default,
        int fontSize = 32)
    {
        // Default anchor = centre/centre
        if (anchorMin == default) anchorMin = new Vector2(0.5f, 0.5f);
        if (anchorMax == default) anchorMax = new Vector2(0.5f, 0.5f);
        if (pivot    == default) pivot     = new Vector2(0.5f, 0.5f);

        const float border = 5f;

        // --- Border layer (slightly larger, darker) ---
        GameObject borderGo = new GameObject(label + "Border");
        borderGo.transform.SetParent(parent, false);
        RectTransform borderRt = borderGo.AddComponent<RectTransform>();
        borderRt.anchorMin = anchorMin;
        borderRt.anchorMax = anchorMax;
        borderRt.pivot     = pivot;
        borderRt.sizeDelta = new Vector2(size.x + border * 2f, size.y + border * 2f);
        borderRt.anchoredPosition = anchoredPos;
        Image borderImg = borderGo.AddComponent<Image>();
        borderImg.color = borderColor;

        // --- Face layer ---
        GameObject faceGo = new GameObject(label + "Face");
        faceGo.transform.SetParent(borderGo.transform, false);
        RectTransform faceRt = faceGo.AddComponent<RectTransform>();
        faceRt.anchorMin = Vector2.zero;
        faceRt.anchorMax = Vector2.one;
        faceRt.offsetMin = new Vector2(border, border);
        faceRt.offsetMax = new Vector2(-border, -border);
        Image faceImg = faceGo.AddComponent<Image>();
        faceImg.color = faceColor;

        Button btn = faceGo.AddComponent<Button>();
        btn.targetGraphic = faceImg;
        ColorBlock cb = btn.colors;
        cb.normalColor      = Color.white;
        cb.highlightedColor = new Color(1f, 1f, 1f, 0.85f);
        cb.pressedColor     = new Color(0.75f, 0.75f, 0.75f, 1f);
        cb.selectedColor    = Color.white;
        btn.colors = cb;
        btn.onClick.AddListener(() => onClick?.Invoke());

        // --- Label ---
        GameObject textGo = new GameObject("Label");
        textGo.transform.SetParent(faceGo.transform, false);
        RectTransform textRt = textGo.AddComponent<RectTransform>();
        textRt.anchorMin = Vector2.zero;
        textRt.anchorMax = Vector2.one;
        textRt.offsetMin = Vector2.zero;
        textRt.offsetMax = Vector2.zero;
        LegacyText txt = textGo.AddComponent<LegacyText>();
        txt.text      = label;
        txt.fontSize  = fontSize;
        txt.color     = new Color(0.92f, 0.65f, 0.25f);
        txt.fontStyle = FontStyle.Bold;
        txt.alignment = TextAnchor.MiddleCenter;
        txt.font      = GetGameFont();

        Outline outline = textGo.AddComponent<Outline>();
        outline.effectColor    = new Color(0f, 0f, 0f, 0.6f);
        outline.effectDistance = new Vector2(2f, -2f);

        return btn;
    }

    private void MakeSmallButton(Transform parent, string label, Vector2 anchoredPos, Vector2 size,
        Color faceColor, bool anchorBottom, System.Action onClick)
    {
        Vector2 aMin, aMax, piv;
        if (anchorBottom)
        {
            aMin = new Vector2(0.5f, 0f);
            aMax = new Vector2(0.5f, 0f);
            piv  = new Vector2(0.5f, 0f);
        }
        else
        {
            aMin = new Vector2(0.5f, 1f);
            aMax = new Vector2(0.5f, 1f);
            piv  = new Vector2(0.5f, 1f);
        }

        Color borderColor = new Color(
            Mathf.Max(faceColor.r - 0.25f, 0f),
            Mathf.Max(faceColor.g - 0.25f, 0f),
            Mathf.Max(faceColor.b - 0.25f, 0f));
        MakeFancyButton(parent, label, anchoredPos, size, faceColor, borderColor, onClick,
            aMin, aMax, piv, 28);
    }

    private void CreateLeaderboardUI()
    {
        float rowHeight = 36f;
        float padding = 16f;
        float titleHeight = 44f;
        float panelH = titleHeight + padding + LeaderboardDisplayCount * rowHeight + padding;
        float panelW = 520f;

        // Container box
        GameObject board = new GameObject("LeaderboardPanel");
        board.transform.SetParent(_startPanel.transform, false);
        RectTransform boardRt = board.AddComponent<RectTransform>();
        boardRt.anchorMin = new Vector2(0.5f, 0.5f);
        boardRt.anchorMax = new Vector2(0.5f, 0.5f);
        boardRt.pivot = new Vector2(0.5f, 0.5f);
        boardRt.sizeDelta = new Vector2(panelW, panelH);
        boardRt.anchoredPosition = new Vector2(667f, 100f);
        Image bg = board.AddComponent<Image>();
        bg.color = new Color(0f, 0f, 0f, 0.88f);

        // Title
        GameObject titleGo = new GameObject("LeaderboardTitle");
        titleGo.transform.SetParent(board.transform, false);
        RectTransform titleRt = titleGo.AddComponent<RectTransform>();
        titleRt.anchorMin = new Vector2(0f, 1f);
        titleRt.anchorMax = new Vector2(1f, 1f);
        titleRt.pivot = new Vector2(0.5f, 1f);
        titleRt.sizeDelta = new Vector2(0f, titleHeight);
        titleRt.anchoredPosition = new Vector2(0f, 0f);
        _leaderboardTitle = titleGo.AddComponent<LegacyText>();
        _leaderboardTitle.text = "Top Scores";
        _leaderboardTitle.fontSize = 28;
        _leaderboardTitle.color = new Color(1f, 0.85f, 0.2f, 1f);
        _leaderboardTitle.alignment = TextAnchor.MiddleCenter;
        _leaderboardTitle.fontStyle = FontStyle.Bold;
        _leaderboardTitle.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");

        // Rows
        _leaderboardRows = new LegacyText[LeaderboardDisplayCount];
        for (int i = 0; i < LeaderboardDisplayCount; i++)
        {
            GameObject rowGo = new GameObject("Row" + i);
            rowGo.transform.SetParent(board.transform, false);
            RectTransform rowRt = rowGo.AddComponent<RectTransform>();
            rowRt.anchorMin = new Vector2(0f, 1f);
            rowRt.anchorMax = new Vector2(1f, 1f);
            rowRt.pivot = new Vector2(0.5f, 1f);
            rowRt.sizeDelta = new Vector2(0f, rowHeight);
            rowRt.anchoredPosition = new Vector2(0f, -(titleHeight + padding + i * rowHeight));

            LegacyText row = rowGo.AddComponent<LegacyText>();
            row.text = "";
            row.fontSize = 22;
            row.color = Color.white;
            row.alignment = TextAnchor.MiddleLeft;
            row.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            // Left-pad text by setting rect left inset
            rowRt.offsetMin = new Vector2(20f, rowRt.offsetMin.y);
            rowRt.offsetMax = new Vector2(-20f, rowRt.offsetMax.y);
            _leaderboardRows[i] = row;
        }
    }

    private void RefreshLeaderboard()
    {
        if (_leaderboardRows == null) return;
        for (int i = 0; i < _leaderboardRows.Length; i++)
            _leaderboardRows[i].text = i == 0 ? "Loading..." : "";

        if (LeaderboardManager.Instance == null) return;
        LeaderboardManager.Instance.GetTopScores(LeaderboardDisplayCount, (items) =>
        {
            if (items == null || items.Length == 0)
            {
                _leaderboardRows[0].text = "No scores yet — be the first!";
                for (int i = 1; i < _leaderboardRows.Length; i++)
                    _leaderboardRows[i].text = "";
                return;
            }
            for (int i = 0; i < _leaderboardRows.Length; i++)
            {
                if (i < items.Length)
                {
                    var entry = items[i];
                    string name = (entry.player != null && !string.IsNullOrEmpty(entry.player.name))
                        ? entry.player.name : "Unknown";
                    _leaderboardRows[i].text = $"#{entry.rank}  {name}  —  {entry.score}";
                }
                else
                {
                    _leaderboardRows[i].text = "";
                }
            }
        });
    }

    private void CreateDailyLeaderboardUI()
    {
        float rowHeight = 36f;
        float padding = 16f;
        float titleHeight = 44f;
        float panelH = titleHeight + padding + LeaderboardDisplayCount * rowHeight + padding;
        float panelW = 520f;

        GameObject board = new GameObject("DailyLeaderboardPanel");
        board.transform.SetParent(_startPanel.transform, false);
        RectTransform boardRt = board.AddComponent<RectTransform>();
        boardRt.anchorMin = new Vector2(0.5f, 0.5f);
        boardRt.anchorMax = new Vector2(0.5f, 0.5f);
        boardRt.pivot = new Vector2(0.5f, 0.5f);
        boardRt.sizeDelta = new Vector2(panelW, panelH);
        boardRt.anchoredPosition = new Vector2(-667f, 100f);
        Image bg = board.AddComponent<Image>();
        bg.color = new Color(0f, 0f, 0f, 0.88f);

        GameObject titleGo = new GameObject("DailyLeaderboardTitle");
        titleGo.transform.SetParent(board.transform, false);
        RectTransform titleRt = titleGo.AddComponent<RectTransform>();
        titleRt.anchorMin = new Vector2(0f, 1f);
        titleRt.anchorMax = new Vector2(1f, 1f);
        titleRt.pivot = new Vector2(0.5f, 1f);
        titleRt.sizeDelta = new Vector2(0f, titleHeight);
        titleRt.anchoredPosition = new Vector2(0f, 0f);
        _dailyLeaderboardTitle = titleGo.AddComponent<LegacyText>();
        _dailyLeaderboardTitle.text = "Today's Scores";
        _dailyLeaderboardTitle.fontSize = 28;
        _dailyLeaderboardTitle.color = new Color(1f, 0.85f, 0.2f, 1f);
        _dailyLeaderboardTitle.alignment = TextAnchor.MiddleCenter;
        _dailyLeaderboardTitle.fontStyle = FontStyle.Bold;
        _dailyLeaderboardTitle.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");

        _dailyLeaderboardRows = new LegacyText[LeaderboardDisplayCount];
        for (int i = 0; i < LeaderboardDisplayCount; i++)
        {
            GameObject rowGo = new GameObject("Row" + i);
            rowGo.transform.SetParent(board.transform, false);
            RectTransform rowRt = rowGo.AddComponent<RectTransform>();
            rowRt.anchorMin = new Vector2(0f, 1f);
            rowRt.anchorMax = new Vector2(1f, 1f);
            rowRt.pivot = new Vector2(0.5f, 1f);
            rowRt.sizeDelta = new Vector2(0f, rowHeight);
            rowRt.anchoredPosition = new Vector2(0f, -(titleHeight + padding + i * rowHeight));

            LegacyText row = rowGo.AddComponent<LegacyText>();
            row.text = "";
            row.fontSize = 22;
            row.color = Color.white;
            row.alignment = TextAnchor.MiddleLeft;
            row.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            rowRt.offsetMin = new Vector2(20f, rowRt.offsetMin.y);
            rowRt.offsetMax = new Vector2(-20f, rowRt.offsetMax.y);
            _dailyLeaderboardRows[i] = row;
        }
    }

    private void RefreshDailyLeaderboard()
    {
        if (_dailyLeaderboardRows == null) return;
        for (int i = 0; i < _dailyLeaderboardRows.Length; i++)
            _dailyLeaderboardRows[i].text = i == 0 ? "Loading..." : "";

        if (LeaderboardManager.Instance == null) return;
        LeaderboardManager.Instance.GetDailyTopScores(LeaderboardDisplayCount, (items) =>
        {
            if (items == null || items.Length == 0)
            {
                _dailyLeaderboardRows[0].text = "No scores yet — be the first!";
                for (int i = 1; i < _dailyLeaderboardRows.Length; i++)
                    _dailyLeaderboardRows[i].text = "";
                return;
            }
            for (int i = 0; i < _dailyLeaderboardRows.Length; i++)
            {
                if (i < items.Length)
                {
                    var entry = items[i];
                    string name = (entry.player != null && !string.IsNullOrEmpty(entry.player.name))
                        ? entry.player.name : "Unknown";
                    _dailyLeaderboardRows[i].text = $"#{entry.rank}  {name}  —  {entry.score}";
                }
                else
                {
                    _dailyLeaderboardRows[i].text = "";
                }
            }
        });
    }

    private void CreateScoreUI()
    {
        Canvas canvas = FindFirstObjectByType<Canvas>();
        if (canvas == null) return;

        GameObject go = new GameObject("ScoreLabel");
        go.transform.SetParent(canvas.transform, false);

        RectTransform rt = go.AddComponent<RectTransform>();
        rt.anchorMin = new Vector2(1f, 1f);
        rt.anchorMax = new Vector2(1f, 1f);
        rt.pivot = new Vector2(1f, 1f);
        rt.sizeDelta = new Vector2(320f, 60f);
        rt.anchoredPosition = new Vector2(-30f, -20f);

        _scoreLabel = go.AddComponent<LegacyText>();
        _scoreLabel.text = "Current Score: 0";
        _scoreLabel.fontSize = 36;
        _scoreLabel.color = Color.white;
        _scoreLabel.alignment = TextAnchor.UpperRight;
        _scoreLabel.fontStyle = FontStyle.Bold;
        _scoreLabel.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        _scoreLabel.resizeTextForBestFit = false;

        go.SetActive(false);

        GameObject hgo = new GameObject("HighScoreLabel");
        hgo.transform.SetParent(canvas.transform, false);

        RectTransform hrt = hgo.AddComponent<RectTransform>();
        hrt.anchorMin = new Vector2(1f, 1f);
        hrt.anchorMax = new Vector2(1f, 1f);
        hrt.pivot = new Vector2(1f, 1f);
        hrt.sizeDelta = new Vector2(320f, 60f);
        hrt.anchoredPosition = new Vector2(-30f, -60f);

        _highScoreLabel = hgo.AddComponent<LegacyText>();
        _highScoreLabel.text = $"High Score: {HighScore}";
        _highScoreLabel.fontSize = 36;
        _highScoreLabel.color = Color.white;
        _highScoreLabel.alignment = TextAnchor.UpperRight;
        _highScoreLabel.fontStyle = FontStyle.Bold;
        _highScoreLabel.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        _highScoreLabel.resizeTextForBestFit = false;

        hgo.SetActive(false);
    }

    private LegacyText MakeLabel(Transform parent, string name, string content, int fontSize,
        Vector2 anchoredPos, Vector2 size)
    {
        const float border = 5f;
        Color faceColor   = BtnPink;
        Color borderColor = BtnPinkBorder;

        // Border layer
        GameObject go = new GameObject(name);
        go.transform.SetParent(parent, false);
        RectTransform rt = go.AddComponent<RectTransform>();
        rt.anchorMin = new Vector2(0.5f, 0.5f);
        rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.sizeDelta = new Vector2(size.x + border * 2f, size.y + border * 2f);
        rt.anchoredPosition = anchoredPos;
        Image borderImg = go.AddComponent<Image>();
        borderImg.color = borderColor;

        // Face layer
        GameObject faceGo = new GameObject("Face");
        faceGo.transform.SetParent(go.transform, false);
        RectTransform faceRt = faceGo.AddComponent<RectTransform>();
        faceRt.anchorMin = Vector2.zero;
        faceRt.anchorMax = Vector2.one;
        faceRt.offsetMin = new Vector2(border, border);
        faceRt.offsetMax = new Vector2(-border, -border);
        Image faceImg = faceGo.AddComponent<Image>();
        faceImg.color = faceColor;

        // Text on top of face
        GameObject textGo = new GameObject("Label");
        textGo.transform.SetParent(faceGo.transform, false);
        RectTransform textRt = textGo.AddComponent<RectTransform>();
        textRt.anchorMin = Vector2.zero;
        textRt.anchorMax = Vector2.one;
        textRt.offsetMin = Vector2.zero;
        textRt.offsetMax = Vector2.zero;
        LegacyText txt = textGo.AddComponent<LegacyText>();
        txt.text = content;
        txt.fontSize = fontSize;
        txt.color = new Color(0.92f, 0.65f, 0.25f);
        txt.alignment = TextAnchor.MiddleCenter;
        txt.fontStyle = FontStyle.Bold;
        txt.font = GetGameFont();
        txt.resizeTextForBestFit = false;
        Outline lblOutline = textGo.AddComponent<Outline>();
        lblOutline.effectColor    = new Color(0f, 0f, 0f, 0.6f);
        lblOutline.effectDistance = new Vector2(2f, -2f);
        return txt;
    }

    private void StyleGameOverUI()
    {
        // Stretch panel to fill the entire screen
        RectTransform panelRt = gameOverPanel.GetComponent<RectTransform>();
        panelRt.anchorMin = Vector2.zero;
        panelRt.anchorMax = Vector2.one;
        panelRt.offsetMin = Vector2.zero;
        panelRt.offsetMax = Vector2.zero;

        Image panelImg = gameOverPanel.GetComponent<Image>();
        if (panelImg != null) panelImg.color = new Color(0f, 0f, 0f, 0.75f);

        VerticalLayoutGroup vlg = gameOverPanel.GetComponent<VerticalLayoutGroup>();
        if (vlg == null) vlg = gameOverPanel.AddComponent<VerticalLayoutGroup>();
        vlg.childAlignment = TextAnchor.MiddleCenter;
        vlg.spacing = 60f;
        vlg.childControlWidth = false;
        vlg.childControlHeight = false;
        vlg.childForceExpandWidth = false;
        vlg.childForceExpandHeight = false;

        // Game Over text — try TMP first, fall back to legacy Text
        Transform textT = gameOverPanel.transform.Find("GameOverText");
        if (textT != null)
        {
            textT.GetComponent<RectTransform>().sizeDelta = new Vector2(900f, 180f);
            var tmp = textT.GetComponentInChildren<TextMeshProUGUI>();
            var leg = textT.GetComponentInChildren<LegacyText>();
            if (tmp != null)
            {
                tmp.text = "GAME OVER"; tmp.fontSize = 80; tmp.color = Color.white;
                tmp.fontStyle = FontStyles.Bold; tmp.alignment = TextAlignmentOptions.Center;
            }
            else if (leg != null)
            {
                leg.text = "GAME OVER"; leg.fontSize = 80; leg.color = Color.white;
                leg.fontStyle = FontStyle.Bold; leg.alignment = TextAnchor.MiddleCenter;
                leg.resizeTextForBestFit = false;
            }
        }

        // Restart button
        Transform btnT = gameOverPanel.transform.Find("RestartButton");
        if (btnT != null)
        {
            btnT.GetComponent<RectTransform>().sizeDelta = new Vector2(500f, 130f);
            Image btnImg = btnT.GetComponent<Image>();
            if (btnImg != null) btnImg.color = new Color(0.2f, 0.6f, 1f, 1f);
            var btnTmp = btnT.GetComponentInChildren<TextMeshProUGUI>();
            var btnLeg = btnT.GetComponentInChildren<LegacyText>();
            if (btnTmp != null)
            {
                btnTmp.text = "RESTART"; btnTmp.fontSize = 50; btnTmp.color = Color.white;
                btnTmp.fontStyle = FontStyles.Bold; btnTmp.alignment = TextAlignmentOptions.Center;
            }
            else if (btnLeg != null)
            {
                btnLeg.text = "RESTART"; btnLeg.fontSize = 50; btnLeg.color = Color.white;
                btnLeg.fontStyle = FontStyle.Bold; btnLeg.alignment = TextAnchor.MiddleCenter;
                btnLeg.resizeTextForBestFit = false;
            }
        }

        // Restart hint — same dark-box style as start screen subtitle
        _restartHintLabel = MakeLabel(gameOverPanel.transform, "RestartHint", GetRestartHint(), 28, new Vector2(0f, 20f), new Vector2(900f, 60f));
        // Pin to bottom of panel, outside the VLG stack
        // MakeLabel hierarchy: border (direct child of panel) → face → textGo; go up two levels
        GameObject hintBorder = _restartHintLabel.transform.parent.parent.gameObject;
        LayoutElement hintLE = hintBorder.AddComponent<LayoutElement>();
        hintLE.ignoreLayout = true;
        RectTransform hintRt = hintBorder.GetComponent<RectTransform>();
        hintRt.anchorMin = new Vector2(0.5f, 0f);
        hintRt.anchorMax = new Vector2(0.5f, 0f);
        hintRt.pivot = new Vector2(0.5f, 0f);
        hintRt.anchoredPosition = new Vector2(0f, 20f);
    }
}

internal class DeviceWarningFilter : ILogHandler
{
    private readonly ILogHandler _inner;

    internal DeviceWarningFilter(ILogHandler inner) { _inner = inner; }

    public void LogFormat(LogType logType, UnityEngine.Object context, string format, params object[] args)
    {
        string msg = args.Length > 0 ? string.Format(format, args) : format;
        if (msg.Contains("Could not create a device") || msg.Contains("has no matching layout"))
            return;
        _inner.LogFormat(logType, context, format, args);
    }

    public void LogException(System.Exception exception, UnityEngine.Object context)
        => _inner.LogException(exception, context);
}
