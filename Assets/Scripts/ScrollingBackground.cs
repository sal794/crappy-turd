using UnityEngine;

public class ScrollingBackground : MonoBehaviour
{
    [SerializeField] private float scrollSpeed = 2f;

    private SpriteRenderer _sr;
    private float _spriteWidth;
    private float _leftEdge;
    private Transform _other;

    public void Init(Transform other) { _other = other; }

    private void Start()
    {
        _sr = GetComponent<SpriteRenderer>();
        _spriteWidth = _sr.bounds.size.x;

        Camera cam = Camera.main;
        _leftEdge = cam.transform.position.x - cam.orthographicSize * cam.aspect;

        // Find sibling panel at runtime — don't rely on Init() persisting across play mode
        if (transform.parent != null)
        {
            foreach (Transform child in transform.parent)
                if (child != transform) { _other = child; break; }
        }
    }

    private void Update()
    {
        if (_other == null) return;
        if (GameManager.Instance == null || !GameManager.Instance.IsStarted || GameManager.Instance.IsGameOver)
            return;

        transform.Translate(Vector2.left * scrollSpeed * Time.deltaTime);

        if (transform.position.x + _spriteWidth * 0.5f < _leftEdge)
            transform.position = new Vector3(_other.position.x + _spriteWidth, transform.position.y, transform.position.z);
    }
}
