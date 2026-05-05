using UnityEngine;

[RequireComponent(typeof(SpriteRenderer))]
public sealed class GhostController : MonoBehaviour
{
    private const int GHOST_SORTING_ORDER = 11;
    private const float CHARACTER_Z_POSITION = 0.2f;

    private static readonly Vector2Int[] CardinalDirections =
    {
        new Vector2Int(1, 0),
        new Vector2Int(-1, 0),
        new Vector2Int(0, 1),
        new Vector2Int(0, -1)
    };

    [SerializeField] private Color ghostColor = Color.red;
    [SerializeField] private float renderScale = 0.8f;

    private SpriteRenderer spriteRenderer;

    public Vector2Int GridPosition { get; private set; }

    private void Awake()
    {
        spriteRenderer = GetComponent<SpriteRenderer>();
        spriteRenderer.sprite = CreateSharedSquareSprite();
        spriteRenderer.color = ghostColor;
        spriteRenderer.sortingOrder = GHOST_SORTING_ORDER;
    }

    public void SetInitialPosition(Vector2Int gridPosition, Vector3 worldPosition, float tileSize)
    {
        GridPosition = gridPosition;
        transform.position = new Vector3(worldPosition.x, worldPosition.y, CHARACTER_Z_POSITION);
        transform.localScale = Vector3.one * (tileSize * renderScale);
    }

    public void TakeTurn(Vector2Int playerPosition, GridManager gridManager)
    {
        Vector2Int bestPosition = GridPosition;
        int bestDistance = ManhattanDistance(GridPosition, playerPosition);

        for (int index = 0; index < CardinalDirections.Length; index++)
        {
            Vector2Int candidate = GridPosition + CardinalDirections[index];
            if (!gridManager.IsWalkableForGhost(candidate))
            {
                continue;
            }

            int candidateDistance = ManhattanDistance(candidate, playerPosition);
            if (candidateDistance < bestDistance)
            {
                bestDistance = candidateDistance;
                bestPosition = candidate;
            }
        }

        GridPosition = bestPosition;
        Vector3 worldPosition = gridManager.GetWorldPosition(GridPosition);
        transform.position = new Vector3(worldPosition.x, worldPosition.y, CHARACTER_Z_POSITION);
    }

    private static int ManhattanDistance(Vector2Int a, Vector2Int b)
    {
        return Mathf.Abs(a.x - b.x) + Mathf.Abs(a.y - b.y);
    }

    private static Sprite CreateSharedSquareSprite()
    {
        Texture2D texture = Texture2D.whiteTexture;
        return Sprite.Create(texture, new Rect(0.0f, 0.0f, texture.width, texture.height), new Vector2(0.5f, 0.5f), texture.width);
    }
}
