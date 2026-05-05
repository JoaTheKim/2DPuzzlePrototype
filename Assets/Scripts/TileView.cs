using UnityEngine;

[RequireComponent(typeof(SpriteRenderer))]
[RequireComponent(typeof(BoxCollider2D))]
public sealed class TileView : MonoBehaviour
{
    private static Sprite sharedTileSprite;

    private SpriteRenderer spriteRenderer;
    private BoxCollider2D boxCollider2D;
    private Color baseColor;

    public Vector2Int GridPosition { get; private set; }
    public TileType TileType { get; private set; }

    private void Awake()
    {
        spriteRenderer = GetComponent<SpriteRenderer>();
        boxCollider2D = GetComponent<BoxCollider2D>();
    }

    public void Initialize(Vector2Int gridPosition, TileType tileType, Vector3 worldPosition, float tileSize, Color color)
    {
        EnsureSprite();

        GridPosition = gridPosition;
        TileType = tileType;
        baseColor = color;

        transform.position = worldPosition;
        transform.localScale = Vector3.one * tileSize;
        gameObject.name = $"Tile_{gridPosition.x}_{gridPosition.y}_{tileType}";

        spriteRenderer.sprite = sharedTileSprite;
        spriteRenderer.color = color;

        boxCollider2D.size = Vector2.one;
        boxCollider2D.isTrigger = true;
    }

    public void SetType(TileType tileType, Color color)
    {
        TileType = tileType;
        baseColor = color;
        spriteRenderer.color = color;
        gameObject.name = $"Tile_{GridPosition.x}_{GridPosition.y}_{tileType}";
    }

    public void SetHighlight(bool highlighted)
    {
        spriteRenderer.color = highlighted ? Color.Lerp(baseColor, Color.white, 0.35f) : baseColor;
    }

    private static void EnsureSprite()
    {
        if (sharedTileSprite != null)
        {
            return;
        }

        Texture2D texture = Texture2D.whiteTexture;
        sharedTileSprite = Sprite.Create(texture, new Rect(0.0f, 0.0f, texture.width, texture.height), new Vector2(0.5f, 0.5f), texture.width);
    }
}
