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
    private GameObject _pauseButton;
    private GameObject _pausePanel;
    private LegacyText _scoreLabel;
    private LegacyText _highScoreLabel;
    private LegacyText _restartHintLabel;
    private LegacyText _startSubtitleLabel;
    private LegacyText _leaderboardTitle;
    private LegacyText[] _leaderboardRows;
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
            LeaderboardManager.Instance.OnSessionReady += RefreshLeaderboard;

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

        MakeLabel(_startPanel.transform, "TitleText", "Crappy Turd 2000", 96, new Vector2(0f, 280f), new Vector2(1000f, 150f));
        _startSubtitleLabel = MakeLabel(_startPanel.transform, "SubtitleText", GetStartHint(), 36, new Vector2(0f, -295f), new Vector2(900f, 80f));

        CreateLeaderboardUI();
        CreateNamePickerUI();
        CreateSettingsButton();
        CreateSettingsPanel();
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
        rowRt.anchoredPosition = new Vector2(0f, -200f);
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
        GameObject btnGo = new GameObject("RerollButton");
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
        rt.sizeDelta = new Vector2(300f, 60f);
        rt.anchoredPosition = new Vector2(0f, -385f);

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
        boardRt.anchoredPosition = new Vector2(0f, 20f);
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
