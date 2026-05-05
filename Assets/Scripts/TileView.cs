using UnityEngine;

[RequireComponent(typeof(SpriteRenderer))]
[RequireComponent(typeof(BoxCollider2D))]
public sealed class TileView : MonoBehaviour
{
    private const int TILE_SORTING_ORDER = 0;
    private const int FOG_SORTING_ORDER = 5;

    private SpriteRenderer spriteRenderer;
    private SpriteRenderer fogOverlayRenderer;
    private BoxCollider2D boxCollider2D;
    private Color baseColor;

    public Vector2Int GridPosition { get; private set; }
    public TileType TileType { get; private set; }

    private void Awake()
    {
        spriteRenderer = GetComponent<SpriteRenderer>();
        spriteRenderer.sortingOrder = TILE_SORTING_ORDER;
        boxCollider2D = GetComponent<BoxCollider2D>();
        CreateFogOverlayRenderer();
    }

    public void Initialize(Vector2Int gridPosition, TileType tileType, Vector3 worldPosition, float tileSize, Color color, Sprite sprite)
    {
        GridPosition = gridPosition;
        TileType = tileType;
        baseColor = color;

        transform.position = worldPosition;
        transform.localScale = Vector3.one * tileSize;
        gameObject.name = $"Tile_{gridPosition.x}_{gridPosition.y}_{tileType}";

        spriteRenderer.sprite = sprite;
        spriteRenderer.color = color;
        fogOverlayRenderer.sprite = sprite;

        boxCollider2D.size = Vector2.one;
        boxCollider2D.isTrigger = true;
    }

    public void SetType(TileType tileType, Color color, Sprite sprite)
    {
        TileType = tileType;
        baseColor = color;
        spriteRenderer.sprite = sprite;
        spriteRenderer.color = color;
        gameObject.name = $"Tile_{GridPosition.x}_{GridPosition.y}_{tileType}";
    }

    public void SetHighlight(bool highlighted)
    {
        spriteRenderer.color = highlighted ? Color.Lerp(baseColor, Color.white, 0.35f) : baseColor;
    }

    public void SetFogState(FogState fogState, bool fogEnabled)
    {
        if (!fogEnabled)
        {
            fogOverlayRenderer.color = Color.clear;
            return;
        }

        switch (fogState)
        {
            case FogState.Hidden:
                fogOverlayRenderer.color = new Color(0.0f, 0.0f, 0.0f, 1.0f);
                break;
            case FogState.Revealed:
                fogOverlayRenderer.color = new Color(0.0f, 0.0f, 0.0f, 0.5f);
                break;
            case FogState.Visible:
                fogOverlayRenderer.color = Color.clear;
                break;
        }
    }

    private void CreateFogOverlayRenderer()
    {
        GameObject fogOverlayObject = new GameObject("FogOverlay");
        fogOverlayObject.transform.SetParent(transform, false);
        fogOverlayObject.transform.localPosition = Vector3.zero;
        fogOverlayObject.transform.localScale = Vector3.one;

        fogOverlayRenderer = fogOverlayObject.AddComponent<SpriteRenderer>();
        fogOverlayRenderer.sortingOrder = FOG_SORTING_ORDER;
        fogOverlayRenderer.color = new Color(0.0f, 0.0f, 0.0f, 0.95f);
    }
}
