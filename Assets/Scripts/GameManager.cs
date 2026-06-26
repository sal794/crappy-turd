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
    private AudioSource _audioSource;
    private AudioSource _musicSource;

    private const string MusicVolumePrefKey = "CT2000_MusicVolume";
    private const string SFXVolumePrefKey = "CT2000_SFXVolume";

    private const int LeaderboardDisplayCount = 8;

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

    private void Start()
    {
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
            LeaderboardManager.Instance.OnAccountStateChanged += RefreshAccountUI;
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

        if (!IsStarted && _startSubtitleLabel != null)
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
            InputDevice.PlayStation => "Press Cross to Start Crappin",
            InputDevice.Xbox => "Press A to Start Crappin",
            InputDevice.Touch => "Tap to Start Crappin",
            _ => "Press Space to Start Crappin",
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

    public void StartGame()
    {
        IsStarted = true;
        _startPanel.SetActive(false);
        if (_settingsPanel != null) _settingsPanel.SetActive(false);
        _scoreLabel.gameObject.SetActive(true);
        _highScoreLabel.gameObject.SetActive(true);
        if (_pauseButton != null) _pauseButton.SetActive(true);
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
            _highScoreLabel.text = $"High Score: {HighScore}";
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
        txt.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");

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

        // Resume button — same dark-box style
        GameObject resumeGo = new GameObject("ResumeButton");
        resumeGo.transform.SetParent(_pausePanel.transform, false);
        RectTransform resumeRt = resumeGo.AddComponent<RectTransform>();
        resumeRt.anchorMin = new Vector2(0.5f, 0.5f);
        resumeRt.anchorMax = new Vector2(0.5f, 0.5f);
        resumeRt.pivot = new Vector2(0.5f, 0.5f);
        resumeRt.sizeDelta = new Vector2(360f, 80f);
        resumeRt.anchoredPosition = new Vector2(0f, -60f);
        Image resumeImg = resumeGo.AddComponent<Image>();
        resumeImg.color = new Color(0f, 0f, 0f, 0.92f);
        Button resumeBtn = resumeGo.AddComponent<Button>();
        resumeBtn.targetGraphic = resumeImg;
        ColorBlock resumeColors = resumeBtn.colors;
        resumeColors.highlightedColor = new Color(0.15f, 0.15f, 0.15f, 1f);
        resumeColors.pressedColor = new Color(0.3f, 0.3f, 0.3f, 1f);
        resumeBtn.colors = resumeColors;
        resumeBtn.onClick.AddListener(TogglePause);

        GameObject resumeTxtGo = new GameObject("Label");
        resumeTxtGo.transform.SetParent(resumeGo.transform, false);
        RectTransform resumeTxtRt = resumeTxtGo.AddComponent<RectTransform>();
        resumeTxtRt.anchorMin = Vector2.zero;
        resumeTxtRt.anchorMax = Vector2.one;
        resumeTxtRt.offsetMin = Vector2.zero;
        resumeTxtRt.offsetMax = Vector2.zero;
        LegacyText resumeTxt = resumeTxtGo.AddComponent<LegacyText>();
        resumeTxt.text = "Resume";
        resumeTxt.fontSize = 40;
        resumeTxt.color = Color.white;
        resumeTxt.fontStyle = FontStyle.Bold;
        resumeTxt.alignment = TextAnchor.MiddleCenter;
        resumeTxt.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");

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
            txt.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
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
        _startSubtitleLabel = MakeLabel(_startPanel.transform, "SubtitleText", GetStartHint(), 36, new Vector2(0f, -365f), new Vector2(900f, 80f));

        CreateLeaderboardUI();
        CreateDailyLeaderboardUI();
        CreateNamePickerUI();
        CreateSettingsButton();
        CreateSettingsPanel();
        CreateSkinsButton();
        CreateSkinsPanel();
        CreateAccountButton();
        CreateAccountPanel();
    }

    private void CreateNamePickerUI()
    {
        string currentName = LeaderboardManager.Instance != null ? LeaderboardManager.Instance.PlayerName : "...";

        // Outer box — same dark style as start hint, centered, just above it
        GameObject row = new GameObject("NamePicker");
        row.transform.SetParent(_startPanel.transform, false);
        RectTransform rowRt = row.AddComponent<RectTransform>();
        rowRt.anchorMin = new Vector2(0.5f, 0.5f);
        rowRt.anchorMax = new Vector2(0.5f, 0.5f);
        rowRt.pivot = new Vector2(0.5f, 0.5f);
        rowRt.sizeDelta = new Vector2(900f, 80f);
        rowRt.anchoredPosition = new Vector2(0f, -260f);
        Image bg = row.AddComponent<Image>();
        bg.color = new Color(0f, 0f, 0f, 0.92f);

        // "Playing as: Name" — left 68%
        GameObject labelGo = new GameObject("NameLabel");
        labelGo.transform.SetParent(row.transform, false);
        RectTransform labelRt = labelGo.AddComponent<RectTransform>();
        labelRt.anchorMin = new Vector2(0f, 0f);
        labelRt.anchorMax = new Vector2(0.68f, 1f);
        labelRt.offsetMin = new Vector2(20f, 0f);
        labelRt.offsetMax = Vector2.zero;
        _playerNameLabel = labelGo.AddComponent<LegacyText>();
        _playerNameLabel.text = "Playing as: " + currentName;
        _playerNameLabel.fontSize = 32;
        _playerNameLabel.color = Color.white;
        _playerNameLabel.fontStyle = FontStyle.Bold;
        _playerNameLabel.alignment = TextAnchor.MiddleLeft;
        _playerNameLabel.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        _playerNameLabel.resizeTextForBestFit = false;

        // Divider line
        GameObject divGo = new GameObject("Divider");
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
                if (_playerNameLabel != null)
                    _playerNameLabel.text = "Playing as: " + newName;
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
        btnText.color = new Color(0.7f, 0.88f, 1f, 1f);
        btnText.alignment = TextAnchor.MiddleCenter;
        btnText.fontStyle = FontStyle.Bold;
        btnText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
    }

    private void CreateSettingsButton()
    {
        // Same dark-box style as start hint, centered below it
        GameObject btnGo = new GameObject("SettingsButton");
        btnGo.transform.SetParent(_startPanel.transform, false);
        RectTransform rt = btnGo.AddComponent<RectTransform>();
        rt.anchorMin = new Vector2(0.5f, 0.5f);
        rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.sizeDelta = new Vector2(220f, 60f);
        rt.anchoredPosition = new Vector2(-320f, -455f);

        Image img = btnGo.AddComponent<Image>();
        img.color = new Color(0f, 0f, 0f, 0.92f);
        Button btn = btnGo.AddComponent<Button>();
        btn.targetGraphic = img;
        ColorBlock colors = btn.colors;
        colors.normalColor = Color.white;
        colors.highlightedColor = new Color(0.85f, 0.85f, 0.85f, 1f);
        colors.pressedColor = new Color(0.7f, 0.7f, 0.7f, 1f);
        btn.colors = colors;
        btn.onClick.AddListener(() => _settingsPanel.SetActive(true));

        GameObject textGo = new GameObject("Label");
        textGo.transform.SetParent(btnGo.transform, false);
        RectTransform textRt = textGo.AddComponent<RectTransform>();
        textRt.anchorMin = Vector2.zero;
        textRt.anchorMax = Vector2.one;
        textRt.offsetMin = Vector2.zero;
        textRt.offsetMax = Vector2.zero;
        LegacyText txt = textGo.AddComponent<LegacyText>();
        txt.text = "Settings";
        txt.fontSize = 30;
        txt.color = Color.white;
        txt.fontStyle = FontStyle.Bold;
        txt.alignment = TextAnchor.MiddleCenter;
        txt.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
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
        titleTxt.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");

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
        volLabelTxt.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");

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
        sfxLabelTxt.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");

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
        GameObject closeBtnGo = new GameObject("CloseButton");
        closeBtnGo.transform.SetParent(box.transform, false);
        RectTransform closeBtnRt = closeBtnGo.AddComponent<RectTransform>();
        closeBtnRt.anchorMin = new Vector2(0.5f, 0f);
        closeBtnRt.anchorMax = new Vector2(0.5f, 0f);
        closeBtnRt.pivot = new Vector2(0.5f, 0f);
        closeBtnRt.sizeDelta = new Vector2(200f, 60f);
        closeBtnRt.anchoredPosition = new Vector2(0f, 20f);
        Image closeBtnImg = closeBtnGo.AddComponent<Image>();
        closeBtnImg.color = new Color(0.2f, 0.6f, 1f, 1f);
        Button closeBtn = closeBtnGo.AddComponent<Button>();
        closeBtn.targetGraphic = closeBtnImg;
        closeBtn.onClick.AddListener(() => _settingsPanel.SetActive(false));

        GameObject closeTxtGo = new GameObject("Label");
        closeTxtGo.transform.SetParent(closeBtnGo.transform, false);
        RectTransform closeTxtRt = closeTxtGo.AddComponent<RectTransform>();
        closeTxtRt.anchorMin = Vector2.zero;
        closeTxtRt.anchorMax = Vector2.one;
        closeTxtRt.offsetMin = Vector2.zero;
        closeTxtRt.offsetMax = Vector2.zero;
        LegacyText closeTxt = closeTxtGo.AddComponent<LegacyText>();
        closeTxt.text = "Close";
        closeTxt.fontSize = 30;
        closeTxt.color = Color.white;
        closeTxt.alignment = TextAnchor.MiddleCenter;
        closeTxt.fontStyle = FontStyle.Bold;
        closeTxt.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");

        _settingsPanel.SetActive(false);
    }

    private void CreateSkinsButton()
    {
        GameObject btnGo = new GameObject("SkinsButton");
        btnGo.transform.SetParent(_startPanel.transform, false);
        RectTransform rt = btnGo.AddComponent<RectTransform>();
        rt.anchorMin = new Vector2(0.5f, 0.5f);
        rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.sizeDelta = new Vector2(220f, 60f);
        rt.anchoredPosition = new Vector2(0f, -455f);

        Image img = btnGo.AddComponent<Image>();
        img.color = new Color(0f, 0f, 0f, 0.92f);
        Button btn = btnGo.AddComponent<Button>();
        btn.targetGraphic = img;
        ColorBlock colors = btn.colors;
        colors.normalColor = Color.white;
        colors.highlightedColor = new Color(0.85f, 0.85f, 0.85f, 1f);
        colors.pressedColor = new Color(0.7f, 0.7f, 0.7f, 1f);
        btn.colors = colors;
        btn.onClick.AddListener(() => { RefreshSkinsPanel(); _skinsPanel.SetActive(true); });

        GameObject textGo = new GameObject("Label");
        textGo.transform.SetParent(btnGo.transform, false);
        RectTransform textRt = textGo.AddComponent<RectTransform>();
        textRt.anchorMin = Vector2.zero;
        textRt.anchorMax = Vector2.one;
        textRt.offsetMin = Vector2.zero;
        textRt.offsetMax = Vector2.zero;
        LegacyText txt = textGo.AddComponent<LegacyText>();
        txt.text = "Skins";
        txt.fontSize = 30;
        txt.color = Color.white;
        txt.fontStyle = FontStyle.Bold;
        txt.alignment = TextAnchor.MiddleCenter;
        txt.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
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
        boxRt.sizeDelta = new Vector2(800f, 580f);
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
        titleTxt.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");

        // Build one card per skin
        int skinCount = SkinManager.AllSkins.Length;
        _skinEquipButtons = new Button[skinCount];
        _skinStatusLabels = new LegacyText[skinCount];
        _skinCardBorders = new Image[skinCount];

        float cardW = 280f;
        float cardH = 380f;
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
                sprRt.sizeDelta = new Vector2(160f, 200f);
                sprRt.anchoredPosition = new Vector2(0f, -20f);
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
            nameRt.sizeDelta = new Vector2(0f, 50f);
            nameRt.anchoredPosition = new Vector2(0f, -235f);
            LegacyText nameTxt = nameGo.AddComponent<LegacyText>();
            nameTxt.text = skin.DisplayName;
            nameTxt.fontSize = 28;
            nameTxt.color = Color.white;
            nameTxt.fontStyle = FontStyle.Bold;
            nameTxt.alignment = TextAnchor.MiddleCenter;
            nameTxt.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");

            // Status label (e.g. unlock hint for locked skins)
            GameObject hintGo = new GameObject("Hint");
            hintGo.transform.SetParent(card.transform, false);
            RectTransform hintRt = hintGo.AddComponent<RectTransform>();
            hintRt.anchorMin = new Vector2(0f, 1f);
            hintRt.anchorMax = new Vector2(1f, 1f);
            hintRt.pivot = new Vector2(0.5f, 1f);
            hintRt.sizeDelta = new Vector2(0f, 36f);
            hintRt.anchoredPosition = new Vector2(0f, -285f);
            LegacyText hintTxt = hintGo.AddComponent<LegacyText>();
            hintTxt.fontSize = 20;
            hintTxt.color = new Color(0.7f, 0.7f, 0.7f, 1f);
            hintTxt.alignment = TextAnchor.MiddleCenter;
            hintTxt.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            _skinStatusLabels[i] = hintTxt;

            // Equip button
            int capturedIndex = i;
            string capturedId = skin.Id;
            GameObject btnGo = new GameObject("EquipBtn");
            btnGo.transform.SetParent(card.transform, false);
            RectTransform btnRt = btnGo.AddComponent<RectTransform>();
            btnRt.anchorMin = new Vector2(0.5f, 1f);
            btnRt.anchorMax = new Vector2(0.5f, 1f);
            btnRt.pivot = new Vector2(0.5f, 1f);
            btnRt.sizeDelta = new Vector2(200f, 56f);
            btnRt.anchoredPosition = new Vector2(0f, -326f);
            Image btnImg = btnGo.AddComponent<Image>();
            Button btn = btnGo.AddComponent<Button>();
            btn.targetGraphic = btnImg;
            btn.onClick.AddListener(() => { SkinManager.Instance?.SetActiveSkin(capturedId); RefreshSkinsPanel(); });
            _skinEquipButtons[i] = btn;

            GameObject btnLabel = new GameObject("Label");
            btnLabel.transform.SetParent(btnGo.transform, false);
            RectTransform btnLabelRt = btnLabel.AddComponent<RectTransform>();
            btnLabelRt.anchorMin = Vector2.zero;
            btnLabelRt.anchorMax = Vector2.one;
            btnLabelRt.offsetMin = Vector2.zero;
            btnLabelRt.offsetMax = Vector2.zero;
            LegacyText btnTxt = btnLabel.AddComponent<LegacyText>();
            btnTxt.fontSize = 26;
            btnTxt.fontStyle = FontStyle.Bold;
            btnTxt.alignment = TextAnchor.MiddleCenter;
            btnTxt.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            _skinStatusLabels[i] = btnTxt;
        }

        // Close button
        GameObject closeGo = new GameObject("CloseBtn");
        closeGo.transform.SetParent(box.transform, false);
        RectTransform closeRt = closeGo.AddComponent<RectTransform>();
        closeRt.anchorMin = new Vector2(0.5f, 0f);
        closeRt.anchorMax = new Vector2(0.5f, 0f);
        closeRt.pivot = new Vector2(0.5f, 0f);
        closeRt.sizeDelta = new Vector2(200f, 56f);
        closeRt.anchoredPosition = new Vector2(0f, 20f);
        Image closeImg = closeGo.AddComponent<Image>();
        closeImg.color = new Color(0.25f, 0.25f, 0.25f, 1f);
        Button closeBtn = closeGo.AddComponent<Button>();
        closeBtn.targetGraphic = closeImg;
        ColorBlock cc = closeBtn.colors;
        cc.normalColor = Color.white;
        cc.highlightedColor = new Color(0.85f, 0.85f, 0.85f, 1f);
        cc.pressedColor = new Color(0.6f, 0.6f, 0.6f, 1f);
        closeBtn.colors = cc;
        closeBtn.onClick.AddListener(() => _skinsPanel.SetActive(false));

        GameObject closeLbl = new GameObject("Label");
        closeLbl.transform.SetParent(closeGo.transform, false);
        RectTransform closeLblRt = closeLbl.AddComponent<RectTransform>();
        closeLblRt.anchorMin = Vector2.zero;
        closeLblRt.anchorMax = Vector2.one;
        closeLblRt.offsetMin = Vector2.zero;
        closeLblRt.offsetMax = Vector2.zero;
        LegacyText closeTxt = closeLbl.AddComponent<LegacyText>();
        closeTxt.text = "Close";
        closeTxt.fontSize = 28;
        closeTxt.color = Color.white;
        closeTxt.fontStyle = FontStyle.Bold;
        closeTxt.alignment = TextAnchor.MiddleCenter;
        closeTxt.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");

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
            LegacyText lbl = _skinStatusLabels[i];
            Image border = _skinCardBorders[i];

            border.color = equipped ? new Color(0.25f, 0.22f, 0.05f, 1f) : darkBg;

            if (equipped)
            {
                btn.interactable = false;
                btn.GetComponent<Image>().color = gold;
                lbl.text = "Equipped";
                lbl.color = new Color(0.1f, 0.1f, 0.1f, 1f);
            }
            else if (unlocked)
            {
                btn.interactable = true;
                btn.GetComponent<Image>().color = new Color(0.25f, 0.25f, 0.25f, 1f);
                lbl.text = "Equip";
                lbl.color = Color.white;
            }
            else
            {
                btn.interactable = false;
                btn.GetComponent<Image>().color = new Color(0.15f, 0.15f, 0.15f, 1f);
                lbl.text = "Score 20+ to unlock";
                lbl.color = new Color(0.55f, 0.55f, 0.55f, 1f);
            }
        }
    }

    private void CreateAccountButton()
    {
        GameObject btnGo = new GameObject("AccountButton");
        btnGo.transform.SetParent(_startPanel.transform, false);
        RectTransform rt = btnGo.AddComponent<RectTransform>();
        rt.anchorMin = new Vector2(0.5f, 0.5f);
        rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.sizeDelta = new Vector2(220f, 60f);
        rt.anchoredPosition = new Vector2(320f, -455f);

        Image img = btnGo.AddComponent<Image>();
        img.color = new Color(0f, 0f, 0f, 0.92f);
        Button btn = btnGo.AddComponent<Button>();
        btn.targetGraphic = img;
        ColorBlock colors = btn.colors;
        colors.normalColor = Color.white;
        colors.highlightedColor = new Color(0.85f, 0.85f, 0.85f, 1f);
        colors.pressedColor = new Color(0.7f, 0.7f, 0.7f, 1f);
        btn.colors = colors;
        btn.onClick.AddListener(() => _accountPanel.SetActive(true));

        GameObject textGo = new GameObject("Label");
        textGo.transform.SetParent(btnGo.transform, false);
        RectTransform textRt = textGo.AddComponent<RectTransform>();
        textRt.anchorMin = Vector2.zero;
        textRt.anchorMax = Vector2.one;
        textRt.offsetMin = Vector2.zero;
        textRt.offsetMax = Vector2.zero;
        _accountButtonLabel = textGo.AddComponent<LegacyText>();
        _accountButtonLabel.text = "Save Progress";
        _accountButtonLabel.fontSize = 28;
        _accountButtonLabel.color = Color.white;
        _accountButtonLabel.fontStyle = FontStyle.Bold;
        _accountButtonLabel.alignment = TextAnchor.MiddleCenter;
        _accountButtonLabel.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
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
        _accountPanelTitle.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");

        // Close button (always visible)
        MakeSmallButton(box.transform, "Close", new Vector2(0f, 16f), new Vector2(240f, 65f),
            new Color(0f, 0f, 0f, 0.92f), anchorBottom: true,
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
        descTxt.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");

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
        statusRt.sizeDelta = new Vector2(0f, 32f);
        statusRt.anchoredPosition = new Vector2(0f, -228f);
        _accountStatusText = statusGo.AddComponent<LegacyText>();
        _accountStatusText.fontSize = 22;
        _accountStatusText.color = new Color(1f, 0.4f, 0.4f, 1f);
        _accountStatusText.alignment = TextAnchor.MiddleCenter;
        _accountStatusText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");

        // Create Account button
        MakeSmallButton(_accountGuestPanel.transform, "Create Account",
            new Vector2(-150f, -270f), new Vector2(270f, 70f),
            new Color(0.2f, 0.6f, 1f, 1f), anchorBottom: false,
            onClick: () =>
            {
                string username = usernameField.text.Trim();
                string pin = pinField.text.Trim();
                if (username.Length < 3) { _accountStatusText.text = "Username must be at least 3 characters."; return; }
                if (username.Length > 20) { _accountStatusText.text = "Username must be 20 characters or fewer."; return; }
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
            new Vector2(150f, -270f), new Vector2(270f, 70f),
            new Color(0.15f, 0.15f, 0.15f, 1f), anchorBottom: false,
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
        liLabelTxt.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");

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
        _accountLoggedInNameLabel.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");

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
        liSubTxt.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");

        // Log Out button
        MakeSmallButton(_accountLoggedInPanel.transform, "Log Out",
            new Vector2(0f, -205f), new Vector2(280f, 70f),
            new Color(0.65f, 0.18f, 0.18f, 1f), anchorBottom: false,
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
            _playerNameLabel.text = "Playing as: " + name;
        if (_newNameButton != null)
            _newNameButton.SetActive(isGuest);
        if (_accountButtonLabel != null)
            _accountButtonLabel.text = isGuest ? "Save Progress" : "Account";
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

    private void MakeSmallButton(Transform parent, string label, Vector2 anchoredPos, Vector2 size,
        Color color, bool anchorBottom, System.Action onClick)
    {
        GameObject go = new GameObject(label + "Btn");
        go.transform.SetParent(parent, false);
        RectTransform rt = go.AddComponent<RectTransform>();
        if (anchorBottom)
        {
            rt.anchorMin = new Vector2(0.5f, 0f);
            rt.anchorMax = new Vector2(0.5f, 0f);
            rt.pivot = new Vector2(0.5f, 0f);
        }
        else
        {
            rt.anchorMin = new Vector2(0.5f, 1f);
            rt.anchorMax = new Vector2(0.5f, 1f);
            rt.pivot = new Vector2(0.5f, 1f);
        }
        rt.sizeDelta = size;
        rt.anchoredPosition = anchoredPos;

        Image img = go.AddComponent<Image>();
        img.color = color;
        Button btn = go.AddComponent<Button>();
        btn.targetGraphic = img;
        ColorBlock colors = btn.colors;
        colors.highlightedColor = new Color(
            Mathf.Min(color.r + 0.15f, 1f),
            Mathf.Min(color.g + 0.15f, 1f),
            Mathf.Min(color.b + 0.15f, 1f), color.a);
        colors.pressedColor = new Color(
            Mathf.Max(color.r - 0.1f, 0f),
            Mathf.Max(color.g - 0.1f, 0f),
            Mathf.Max(color.b - 0.1f, 0f), color.a);
        btn.colors = colors;
        btn.onClick.AddListener(() => onClick?.Invoke());

        GameObject textGo = new GameObject("Label");
        textGo.transform.SetParent(go.transform, false);
        RectTransform textRt = textGo.AddComponent<RectTransform>();
        textRt.anchorMin = Vector2.zero;
        textRt.anchorMax = Vector2.one;
        textRt.offsetMin = Vector2.zero;
        textRt.offsetMax = Vector2.zero;
        LegacyText txt = textGo.AddComponent<LegacyText>();
        txt.text = label;
        txt.fontSize = 30;
        txt.color = Color.white;
        txt.fontStyle = FontStyle.Bold;
        txt.alignment = TextAnchor.MiddleCenter;
        txt.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
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
        _highScoreLabel.text = "High Score: 0";
        _highScoreLabel.fontSize = 36;
        _highScoreLabel.color = Color.white;
        _highScoreLabel.alignment = TextAnchor.UpperRight;
        _highScoreLabel.fontStyle = FontStyle.Bold;
        _highScoreLabel.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        _highScoreLabel.resizeTextForBestFit = false;

        hgo.SetActive(false);
    }

    private LegacyText MakeLabel(Transform parent, string name, string content, int fontSize, Vector2 anchoredPos, Vector2 size)
    {
        // Container holds the dark background box
        GameObject go = new GameObject(name);
        go.transform.SetParent(parent, false);
        RectTransform rt = go.AddComponent<RectTransform>();
        rt.anchorMin = new Vector2(0.5f, 0.5f);
        rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.sizeDelta = size;
        rt.anchoredPosition = anchoredPos;
        Image bg = go.AddComponent<Image>();
        bg.color = new Color(0f, 0f, 0f, 0.92f);

        // Text is a child so it renders on top of the background
        GameObject textGo = new GameObject("Label");
        textGo.transform.SetParent(go.transform, false);
        RectTransform textRt = textGo.AddComponent<RectTransform>();
        textRt.anchorMin = Vector2.zero;
        textRt.anchorMax = Vector2.one;
        textRt.offsetMin = Vector2.zero;
        textRt.offsetMax = Vector2.zero;
        LegacyText txt = textGo.AddComponent<LegacyText>();
        txt.text = content;
        txt.fontSize = fontSize;
        txt.color = Color.white;
        txt.alignment = TextAnchor.MiddleCenter;
        txt.fontStyle = FontStyle.Bold;
        txt.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        txt.resizeTextForBestFit = false;
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
        LayoutElement hintLE = _restartHintLabel.transform.parent.gameObject.AddComponent<LayoutElement>();
        hintLE.ignoreLayout = true;
        RectTransform hintRt = _restartHintLabel.transform.parent.GetComponent<RectTransform>();
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
