using UnityEngine;

[RequireComponent(typeof(SpriteRenderer))]
public sealed class PlayerController : MonoBehaviour
{
    private const int PLAYER_SORTING_ORDER = 10;
    private const float CHARACTER_Z_POSITION = 0.2f;

    private static Sprite sharedDefaultPlayerSprite;

    [SerializeField] private float renderScale = 0.8f;

    private SpriteRenderer spriteRenderer;

    public Vector2Int GridPosition { get; private set; }
    public int CarriedItemCount { get; private set; }
    public int BurnedItemCount { get; private set; }
    public bool HasItem => CarriedItemCount > 0;

    private void Awake()
    {
        spriteRenderer = GetComponent<SpriteRenderer>();
        spriteRenderer.sprite = GetDefaultPlayerSprite();
        spriteRenderer.color = Color.white;
        spriteRenderer.sortingOrder = PLAYER_SORTING_ORDER;
    }

    public void Initialize(GameVisualConfiguration configuration)
    {
        if (configuration == null)
        {
            return;
        }

        if (configuration.PlayerSprite != null)
        {
            spriteRenderer.sprite = configuration.PlayerSprite;
        }
    }

    public void SetInitialPosition(Vector2Int gridPosition, Vector3 worldPosition, float tileSize)
    {
        GridPosition = gridPosition;
        transform.position = new Vector3(worldPosition.x, worldPosition.y, CHARACTER_Z_POSITION);
        transform.localScale = Vector3.one * (tileSize * renderScale);
    }

    public void MoveTo(Vector2Int gridPosition, Vector3 worldPosition)
    {
        GridPosition = gridPosition;
        transform.position = new Vector3(worldPosition.x, worldPosition.y, CHARACTER_Z_POSITION);
    }

    public bool TryPickUpItem()
    {
        if (CarriedItemCount >= 1)
        {
            return false;
        }

        CarriedItemCount++;
        return true;
    }

    public bool TryBurnOneItem()
    {
        if (CarriedItemCount <= 0)
        {
            return false;
        }

        CarriedItemCount--;
        BurnedItemCount++;
        return true;
    }

    private static Sprite GetDefaultPlayerSprite()
    {
        if (sharedDefaultPlayerSprite != null)
        {
            return sharedDefaultPlayerSprite;
        }

        Texture2D texture = Texture2D.whiteTexture;
        sharedDefaultPlayerSprite = Sprite.Create(texture, new Rect(0.0f, 0.0f, texture.width, texture.height), new Vector2(0.5f, 0.5f), texture.width);
        return sharedDefaultPlayerSprite;
    }
}
