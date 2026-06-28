using UnityEngine;
using UnityEngine.InputSystem;
using System.Collections;

public class PlayerController : MonoBehaviour
{
    [SerializeField] private float jumpForce = 7f;
    [SerializeField] private float maxLeanAngle = 25f;
    [SerializeField] private float leanSpeed = 8f;

    private Rigidbody2D _rb;
    private SpriteRenderer _sr;
    private Vector3 _originalScale;
    private Vector2 _baseSpriteSize;
    private float _halfPlayerHeight;
    private float _bottomBound;
    private float _topBound;
    private float _normalGravityScale;
    private bool _ghostHitUsed = false;
    private bool _wasActive = false;

    private void Awake()
    {
        _rb = GetComponent<Rigidbody2D>();
        _sr = GetComponent<SpriteRenderer>();

        // Capture original transform scale (set in scene for Mr. Crappy) before any skin override
        _originalScale = transform.localScale;

        Sprite baseSpr = Resources.Load<Sprite>("Skins/turd2000");
        if (baseSpr != null)
            _baseSpriteSize = baseSpr.bounds.size;
    }

    private void Start()
    {
        ApplyActiveSkin();
        if (SkinManager.Instance != null)
            SkinManager.Instance.OnSkinChanged += ApplyActiveSkin;

        _halfPlayerHeight = GetComponent<Collider2D>().bounds.extents.y;
        Camera cam = Camera.main;
        _bottomBound = cam.transform.position.y - cam.orthographicSize - _halfPlayerHeight;
        _topBound = cam.transform.position.y + cam.orthographicSize + _halfPlayerHeight;

        _normalGravityScale = _rb.gravityScale;
        _rb.gravityScale = 0f;
    }

    private void Update()
    {
        if (GameManager.Instance == null) return;

        bool spacePressed = Keyboard.current != null && Keyboard.current.spaceKey.wasPressedThisFrame;
        bool tapped = Touchscreen.current != null && Touchscreen.current.primaryTouch.press.wasPressedThisFrame
            && !IsTouchOverUI(Touchscreen.current.primaryTouch.position.ReadValue());
        bool gamepadJump = Gamepad.current != null && Gamepad.current.buttonSouth.wasPressedThisFrame;

        if (spacePressed || tapped || gamepadJump)
        {
            if (!GameManager.Instance.IsStarted)
            {
                if (!GameManager.Instance.StartHintDismissed)
                    GameManager.Instance.DismissStartHint();
                else
                {
                    _rb.gravityScale = _normalGravityScale;
                    GameManager.Instance.StartGame();
                    Jump();
                }
            }
            else
            {
                Jump();
            }
        }

        bool nowActive = GameManager.Instance.IsStarted && !GameManager.Instance.IsGameOver;
        if (nowActive && !_wasActive)
            _ghostHitUsed = false;
        _wasActive = nowActive;

        if (nowActive)
            UpdateLean();
    }

    private void UpdateLean()
    {
        float targetAngle = Mathf.Clamp(_rb.linearVelocity.y * (maxLeanAngle / jumpForce), -maxLeanAngle, maxLeanAngle);
        float currentAngle = transform.eulerAngles.z > 180f ? transform.eulerAngles.z - 360f : transform.eulerAngles.z;
        float smoothAngle = Mathf.Lerp(currentAngle, targetAngle, Time.deltaTime * leanSpeed);
        transform.rotation = Quaternion.Euler(0f, 0f, smoothAngle);
    }

    private void FixedUpdate()
    {
        _rb.angularVelocity = 0f;

        if (GameManager.Instance != null && GameManager.Instance.IsStarted && !GameManager.Instance.IsGameOver)
        {
            if (transform.position.y < _bottomBound || transform.position.y > _topBound)
                GameManager.Instance.TriggerGameOver();
        }
    }

    private static bool IsTouchOverUI(Vector2 screenPos)
    {
        var es = UnityEngine.EventSystems.EventSystem.current;
        if (es == null) return false;
        var ptr = new UnityEngine.EventSystems.PointerEventData(es) { position = screenPos };
        var results = new System.Collections.Generic.List<UnityEngine.EventSystems.RaycastResult>();
        es.RaycastAll(ptr, results);
        return results.Count > 0;
    }

    private void Jump()
    {
        if (GameManager.Instance.IsGameOver) return;
        // Reset vertical velocity so each tap feels consistent regardless of fall speed
        _rb.linearVelocity = new Vector2(0f, 0f);
        _rb.AddForce(Vector2.up * jumpForce, ForceMode2D.Impulse);
    }

    private void OnDestroy()
    {
        if (SkinManager.Instance != null)
            SkinManager.Instance.OnSkinChanged -= ApplyActiveSkin;
    }

    private static Texture2D CreateSoftCircleTexture()
    {
        const int size = 32;
        float center = size / 2f;
        Texture2D tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float t = 1f - Mathf.Clamp01(Vector2.Distance(new Vector2(x, y), new Vector2(center, center)) / center);
                tex.SetPixel(x, y, new Color(1f, 1f, 1f, t * t));
            }
        }
        tex.Apply();
        return tex;
    }

    private void ApplyActiveSkin()
    {
        if (_sr == null || SkinManager.Instance == null) return;
        Sprite s = SkinManager.Instance.LoadActiveSkinSprite();
        if (s == null) return;
        _sr.sprite = s;

        if (_baseSpriteSize.x > 0 && s.bounds.size.x > 0)
        {
            transform.localScale = new Vector3(
                _originalScale.x * (_baseSpriteSize.x / s.bounds.size.x),
                _originalScale.y * (_baseSpriteSize.y / s.bounds.size.y),
                _originalScale.z);
        }
    }

    private void OnCollisionEnter2D(Collision2D collision)
    {
        if (!collision.gameObject.CompareTag("Obstacle")) return;

        if (!_ghostHitUsed && SkinManager.Instance?.ActiveSkinId == "the_dook")
        {
            _ghostHitUsed = true;
            StartCoroutine(GhostHit());
            return;
        }

        GameManager.Instance.TriggerGameOver();
    }

    private IEnumerator GhostHit()
    {
        Collider2D col = GetComponent<Collider2D>();
        col.isTrigger = true;

        GameObject psGo = new GameObject("GhostParticles");
        psGo.transform.SetParent(transform, false);
        psGo.transform.localPosition = Vector3.zero;
        ParticleSystem ps = psGo.AddComponent<ParticleSystem>();

        var psr = ps.GetComponent<ParticleSystemRenderer>();
        var mat = new Material(Shader.Find("Sprites/Default"));
        mat.mainTexture = CreateSoftCircleTexture();
        psr.material = mat;
        psr.sortingLayerName = _sr.sortingLayerName;
        psr.sortingOrder = _sr.sortingOrder - 1;

        var main = ps.main;
        main.duration = 1.0f;
        main.loop = false;
        main.startLifetime = 0.5f;
        main.startSpeed = new ParticleSystem.MinMaxCurve(0.3f, 0.8f);
        main.startSize = new ParticleSystem.MinMaxCurve(0.3f, 0.7f);
        main.startColor = new Color(1f, 1f, 1f, 1f);
        main.maxParticles = 40;
        main.simulationSpace = ParticleSystemSimulationSpace.World;

        var emission = ps.emission;
        emission.rateOverTime = 60f;

        var shape = ps.shape;
        shape.shapeType = ParticleSystemShapeType.Circle;
        shape.radius = 0.25f;

        var colorOverLifetime = ps.colorOverLifetime;
        colorOverLifetime.enabled = false;

        ps.Play();

        float elapsed = 0f;
        while (elapsed < 1.0f)
        {
            float alpha = Mathf.Lerp(0.15f, 0.6f, (Mathf.Sin(elapsed * 20f) + 1f) * 0.5f);
            _sr.color = new Color(1f, 1f, 1f, alpha);
            elapsed += Time.deltaTime;
            yield return null;
        }

        _sr.color = Color.white;
        col.isTrigger = false;
        Destroy(psGo, 0.5f);
    }
}
