using UnityEngine;

public class Obstacle : MonoBehaviour
{
    [SerializeField] private float speed = 5f;

    private float _despawnX;
    private bool _scored;

    private void Start()
    {
        Camera cam = Camera.main;
        float halfWidth = cam.orthographicSize * cam.aspect;
        _despawnX = cam.transform.position.x - halfWidth - 1f;
    }

    private void Update()
    {
        float currentSpeed = speed * (GameManager.Instance != null ? GameManager.Instance.SpeedMultiplier : 1f);
        transform.Translate(Vector2.left * currentSpeed * Time.deltaTime);

        if (!_scored && transform.position.x < 0f)
        {
            _scored = true;
            GameManager.Instance.AddScore();
        }

        if (transform.position.x < _despawnX)
            Destroy(gameObject);
    }
}
