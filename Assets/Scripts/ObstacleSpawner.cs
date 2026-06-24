using UnityEngine;

public class ObstacleSpawner : MonoBehaviour
{
    [SerializeField] private Sprite obstacleSprite;
    [SerializeField] private float spawnInterval = 2f;
    [SerializeField] private float gapSize = 3f;
    [SerializeField] private float pieceWidth = 1.5f;
    [SerializeField] private float pieceHeight = 14f;
    [SerializeField] private float colliderWidthFraction = 0.5f;

    private float _spawnX;
    private float _screenHalfHeight;
    private float _timer;

    public void ResetTimer() => _timer = 0f;

    private void Start()
    {
        Camera cam = Camera.main;
        _spawnX = cam.transform.position.x + cam.orthographicSize * cam.aspect + 2f;
        _screenHalfHeight = cam.orthographicSize;
    }

    private void Update()
    {
        if (GameManager.Instance == null || !GameManager.Instance.IsStarted || GameManager.Instance.IsGameOver) return;
        _timer += Time.deltaTime;
        float currentInterval = spawnInterval / (GameManager.Instance != null ? GameManager.Instance.SpeedMultiplier : 1f);
        if (_timer >= currentInterval)
        {
            _timer = 0f;
            SpawnPair();
        }
    }

    private void SpawnPair()
    {
        float gapCenterY = Random.Range(-_screenHalfHeight + gapSize, _screenHalfHeight - gapSize);

        GameObject parent = new GameObject("ObstaclePair");
        parent.transform.position = new Vector3(_spawnX, 0f, 0f);
        parent.AddComponent<Obstacle>();

        CreatePiece(parent, "TopPiece", gapCenterY + gapSize / 2f + pieceHeight / 2f);
        CreatePiece(parent, "BottomPiece", gapCenterY - gapSize / 2f - pieceHeight / 2f);
    }

    private void CreatePiece(GameObject parent, string pieceName, float localY)
    {
        GameObject piece = new GameObject(pieceName);
        piece.transform.SetParent(parent.transform);
        piece.transform.localPosition = new Vector3(0f, localY, 0f);

        SpriteRenderer sr = piece.AddComponent<SpriteRenderer>();
        sr.sprite = obstacleSprite;
        sr.color = Color.white;

        float spriteWorldW = obstacleSprite.rect.width / obstacleSprite.pixelsPerUnit;
        float spriteWorldH = obstacleSprite.rect.height / obstacleSprite.pixelsPerUnit;
        piece.transform.localScale = new Vector3(pieceWidth / spriteWorldW, pieceHeight / spriteWorldH, 1f);

        BoxCollider2D col = piece.AddComponent<BoxCollider2D>();
        // Size in local space: world target = pieceWidth*fraction x pieceHeight
        // local width = (pieceWidth * fraction) / sx = fraction * spriteWorldW
        col.size = new Vector2(colliderWidthFraction * spriteWorldW, spriteWorldH);
        piece.tag = "Obstacle";
    }
}
