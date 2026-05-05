using UnityEngine;

[RequireComponent(typeof(SpriteRenderer))]
public sealed class GhostController : MonoBehaviour
{
    private const int GHOST_SORTING_ORDER = 11;
    private const float CHARACTER_Z_POSITION = 0.2f;

    private static Sprite sharedDefaultGhostSprite;

    private static readonly Vector2Int[] CardinalDirections =
    {
        new Vector2Int(1, 0),
        new Vector2Int(-1, 0),
        new Vector2Int(0, 1),
        new Vector2Int(0, -1)
    };

    [SerializeField] private float renderScale = 0.8f;
    [SerializeField] private int stickyAggroTurns = 3;

    private SpriteRenderer spriteRenderer;
    private int aggroTimer;
    private int stunnedTurns;

    public Vector2Int GridPosition { get; private set; }
    public GhostState State { get; private set; }
    public int AggroTimer => aggroTimer;
    public int StunnedTurns => stunnedTurns;

    private void Awake()
    {
        spriteRenderer = GetComponent<SpriteRenderer>();
        spriteRenderer.sprite = GetDefaultGhostSprite();
        spriteRenderer.color = Color.white;
        spriteRenderer.sortingOrder = GHOST_SORTING_ORDER;
        State = GhostState.Passive;
    }

    public void Initialize(GameVisualConfiguration configuration)
    {
        if (configuration == null)
        {
            return;
        }

        if (configuration.GhostSprite != null)
        {
            spriteRenderer.sprite = configuration.GhostSprite;
        }
    }

    public void SetInitialPosition(Vector2Int gridPosition, Vector3 worldPosition, float tileSize)
    {
        GridPosition = gridPosition;
        transform.position = new Vector3(worldPosition.x, worldPosition.y, CHARACTER_Z_POSITION);
        transform.localScale = Vector3.one * (tileSize * renderScale);
    }

    public void SetVisible(bool visible)
    {
        spriteRenderer.enabled = visible;
    }

    public void UpdateState(Vector2Int playerPosition, bool playerHasItem, int chaseRadius)
    {
        if (stunnedTurns > 0)
        {
            State = GhostState.Stunned;
            return;
        }

        int distanceToPlayer = ManhattanDistance(GridPosition, playerPosition);
        bool playerInsideChaseRadius = distanceToPlayer <= chaseRadius;
        if (playerHasItem || playerInsideChaseRadius)
        {
            aggroTimer = stickyAggroTurns;
        }
        else if (aggroTimer > 0)
        {
            aggroTimer--;
        }

        State = aggroTimer > 0 ? GhostState.Aggro : GhostState.Passive;
    }

    public void ApplyStun(int turns)
    {
        stunnedTurns = Mathf.Max(stunnedTurns, turns);
        State = GhostState.Stunned;
    }

    public void TakeTurn(Vector2Int playerPosition, GridManager gridManager, bool canTakeBonusMove)
    {
        if (stunnedTurns > 0)
        {
            stunnedTurns--;
            State = GhostState.Stunned;
            return;
        }

        if (State == GhostState.Passive)
        {
            TryMoveRandom(gridManager);
            return;
        }

        TryMoveTowardsPlayer(playerPosition, gridManager);
        if (canTakeBonusMove)
        {
            TryMoveTowardsPlayer(playerPosition, gridManager);
        }
    }

    private void TryMoveTowardsPlayer(Vector2Int playerPosition, GridManager gridManager)
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

    private void TryMoveRandom(GridManager gridManager)
    {
        Vector2Int[] shuffledDirections = (Vector2Int[])CardinalDirections.Clone();
        for (int index = 0; index < shuffledDirections.Length; index++)
        {
            int randomIndex = Random.Range(index, shuffledDirections.Length);
            Vector2Int cachedDirection = shuffledDirections[index];
            shuffledDirections[index] = shuffledDirections[randomIndex];
            shuffledDirections[randomIndex] = cachedDirection;
        }

        for (int index = 0; index < shuffledDirections.Length; index++)
        {
            Vector2Int candidate = GridPosition + shuffledDirections[index];
            if (!gridManager.IsWalkableForGhost(candidate))
            {
                continue;
            }

            GridPosition = candidate;
            Vector3 worldPosition = gridManager.GetWorldPosition(GridPosition);
            transform.position = new Vector3(worldPosition.x, worldPosition.y, CHARACTER_Z_POSITION);
            return;
        }
    }

    private static int ManhattanDistance(Vector2Int a, Vector2Int b)
    {
        return Mathf.Abs(a.x - b.x) + Mathf.Abs(a.y - b.y);
    }

    private static Sprite GetDefaultGhostSprite()
    {
        if (sharedDefaultGhostSprite != null)
        {
            return sharedDefaultGhostSprite;
        }

        Texture2D texture = Texture2D.whiteTexture;
        sharedDefaultGhostSprite = Sprite.Create(texture, new Rect(0.0f, 0.0f, texture.width, texture.height), new Vector2(0.5f, 0.5f), texture.width);
        return sharedDefaultGhostSprite;
    }
}
