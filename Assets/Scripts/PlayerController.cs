using UnityEngine;
using UnityEngine.InputSystem;

public class PlayerController : MonoBehaviour
{
    [SerializeField] private float jumpForce = 7f;
    [SerializeField] private float maxLeanAngle = 25f;
    [SerializeField] private float leanSpeed = 8f;

    private Rigidbody2D _rb;
    private float _halfPlayerHeight;
    private float _bottomBound;
    private float _topBound;
    private float _normalGravityScale;

    private void Awake()
    {
        _rb = GetComponent<Rigidbody2D>();
    }

    private void Start()
    {
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
                _rb.gravityScale = _normalGravityScale;
                GameManager.Instance.StartGame();
                Jump();
            }
            else
            {
                Jump();
            }
        }

        if (GameManager.Instance.IsStarted && !GameManager.Instance.IsGameOver)
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

    private void OnCollisionEnter2D(Collision2D collision)
    {
        if (collision.gameObject.CompareTag("Obstacle"))
            GameManager.Instance.TriggerGameOver();
    }
}
