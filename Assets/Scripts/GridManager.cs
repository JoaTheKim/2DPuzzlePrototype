using System.Collections.Generic;
using UnityEngine;

public sealed class GridManager : MonoBehaviour
{
    private static Sprite sharedDefaultTileSprite;

    private static readonly Vector2Int[] CardinalDirections =
    {
        new Vector2Int(1, 0),
        new Vector2Int(-1, 0),
        new Vector2Int(0, 1),
        new Vector2Int(0, -1)
    };

    [SerializeField] private int width = 8;
    [SerializeField] private int height = 8;
    [SerializeField] private float tileSize = 1.0f;
    [SerializeField] private int wallCount = 10;
    [SerializeField] private int itemCount = 3;
    [SerializeField] private int safeTileCount = 4;

    [SerializeField] private Color emptyColor = new Color(0.85f, 0.85f, 0.85f, 1.0f);
    [SerializeField] private Color wallColor = new Color(0.25f, 0.25f, 0.25f, 1.0f);
    [SerializeField] private Color itemColor = Color.white;
    [SerializeField] private Color ritualColor = new Color(1.0f, 0.55f, 0.0f, 1.0f);
    [SerializeField] private Color safeTileColor = new Color(0.15f, 0.7f, 0.3f, 1.0f);
    [SerializeField] private bool fogEnabled = true;

    private GameVisualConfiguration visualConfiguration;
    private System.Random generationRandom;
    private TileType[,] tileTypes;
    private TileView[,] tileViews;
    private FogState[,] fogStates;

    public int Width => width;
    public int Height => height;
    public int ItemCount => itemCount;
    public float TileSize => tileSize;
    public bool IsFogEnabled => fogEnabled;

    public bool IsInsideGrid(Vector2Int gridPosition)
    {
        return gridPosition.x >= 0 && gridPosition.x < width && gridPosition.y >= 0 && gridPosition.y < height;
    }

    public bool IsWalkableForPlayer(Vector2Int gridPosition)
    {
        if (!IsInsideGrid(gridPosition))
        {
            return false;
        }

        TileType tileType = tileTypes[gridPosition.x, gridPosition.y];
        return tileType != TileType.Wall && tileType != TileType.Ritual;
    }

    public bool IsWalkableForGhost(Vector2Int gridPosition)
    {
        if (!IsInsideGrid(gridPosition))
        {
            return false;
        }

        TileType tileType = tileTypes[gridPosition.x, gridPosition.y];
        return tileType != TileType.Wall && tileType != TileType.Safe;
    }

    public bool TryGetTilePositionFromWorld(Vector3 worldPosition, out Vector2Int gridPosition)
    {
        gridPosition = WorldToGrid(worldPosition);
        return IsInsideGrid(gridPosition);
    }

    public Vector3 GetWorldPosition(Vector2Int gridPosition)
    {
        float startX = -((width - 1) * tileSize) * 0.5f;
        float startY = -((height - 1) * tileSize) * 0.5f;
        return new Vector3(startX + (gridPosition.x * tileSize), startY + (gridPosition.y * tileSize), 0.0f);
    }

    public TileType GetTileType(Vector2Int gridPosition)
    {
        return tileTypes[gridPosition.x, gridPosition.y];
    }

    public bool TryConsumeItem(Vector2Int gridPosition)
    {
        if (tileTypes[gridPosition.x, gridPosition.y] != TileType.Item)
        {
            return false;
        }

        tileTypes[gridPosition.x, gridPosition.y] = TileType.Empty;
        tileViews[gridPosition.x, gridPosition.y].SetType(TileType.Empty, GetColorForType(TileType.Empty), GetSpriteForType(TileType.Empty));
        return true;
    }

    public Vector2Int GetRandomWalkablePositionForPlayer()
    {
        return GetRandomPosition(
            position =>
                tileTypes[position.x, position.y] == TileType.Empty ||
                tileTypes[position.x, position.y] == TileType.Ritual ||
                tileTypes[position.x, position.y] == TileType.Safe);
    }

    public Vector2Int GetRandomWalkablePositionForGhost()
    {
        return GetRandomPosition(
            position =>
                tileTypes[position.x, position.y] == TileType.Empty ||
                tileTypes[position.x, position.y] == TileType.Ritual ||
                tileTypes[position.x, position.y] == TileType.Item);
    }

    public Vector2Int GetRitualPosition()
    {
        return GetRandomPosition(position => tileTypes[position.x, position.y] == TileType.Ritual);
    }

    public List<Vector2Int> GetValidAdjacentTilesForPlayer(Vector2Int from)
    {
        List<Vector2Int> positions = new List<Vector2Int>();
        for (int index = 0; index < CardinalDirections.Length; index++)
        {
            Vector2Int next = from + CardinalDirections[index];
            if (IsWalkableForPlayer(next))
            {
                positions.Add(next);
            }
        }

        return positions;
    }

    public void HighlightTiles(IEnumerable<Vector2Int> positions)
    {
        ClearHighlights();

        foreach (Vector2Int position in positions)
        {
            if (IsInsideGrid(position))
            {
                tileViews[position.x, position.y].SetHighlight(true);
            }
        }
    }

    public void ClearHighlights()
    {
        for (int x = 0; x < width; x++)
        {
            for (int y = 0; y < height; y++)
            {
                tileViews[x, y].SetHighlight(false);
            }
        }
    }

    public void FrameMainCamera(float extraPadding = 0.75f)
    {
        Camera mainCamera = Camera.main;
        if (mainCamera == null || !mainCamera.orthographic)
        {
            return;
        }

        float worldWidth = width * tileSize;
        float worldHeight = height * tileSize;
        float aspect = mainCamera.aspect <= 0.0f ? (16.0f / 9.0f) : mainCamera.aspect;

        float sizeByHeight = (worldHeight * 0.5f) + extraPadding;
        float sizeByWidth = ((worldWidth / aspect) * 0.5f) + extraPadding;
        mainCamera.orthographicSize = Mathf.Max(sizeByHeight, sizeByWidth);
        mainCamera.transform.position = new Vector3(0.0f, 0.0f, -10.0f);
    }

    public void Initialize(GameVisualConfiguration configuration, int generationSeed)
    {
        visualConfiguration = configuration;
        generationRandom = new System.Random(generationSeed);
        GenerateGrid();
    }

    public List<Vector2Int> GetTilesInRadius(Vector2Int center, int radius)
    {
        List<Vector2Int> tiles = new List<Vector2Int>();
        for (int x = 0; x < width; x++)
        {
            for (int y = 0; y < height; y++)
            {
                Vector2Int position = new Vector2Int(x, y);
                int distance = Mathf.Abs(position.x - center.x) + Mathf.Abs(position.y - center.y);
                if (distance <= radius)
                {
                    tiles.Add(position);
                }
            }
        }

        return tiles;
    }

    public void UpdateFog(Vector2Int playerPosition, int visionRadius)
    {
        for (int x = 0; x < width; x++)
        {
            for (int y = 0; y < height; y++)
            {
                if (fogStates[x, y] == FogState.Visible)
                {
                    fogStates[x, y] = FogState.Revealed;
                }
            }
        }

        List<Vector2Int> visibleTiles = GetTilesInRadius(playerPosition, visionRadius);
        for (int index = 0; index < visibleTiles.Count; index++)
        {
            Vector2Int visibleTile = visibleTiles[index];
            fogStates[visibleTile.x, visibleTile.y] = FogState.Visible;
        }

        RefreshFogVisuals();
    }

    public bool IsTileVisible(Vector2Int position)
    {
        return IsInsideGrid(position) && fogStates[position.x, position.y] == FogState.Visible;
    }

    public bool IsRitualTile(Vector2Int position)
    {
        return IsInsideGrid(position) && tileTypes[position.x, position.y] == TileType.Ritual;
    }

    public void SetFogEnabled(bool enabled)
    {
        fogEnabled = enabled;
        RefreshFogVisuals();
    }

    public List<Vector2Int> GetValidAdjacentActionTiles(Vector2Int from)
    {
        List<Vector2Int> positions = new List<Vector2Int>();
        for (int index = 0; index < CardinalDirections.Length; index++)
        {
            Vector2Int next = from + CardinalDirections[index];
            if (!IsInsideGrid(next))
            {
                continue;
            }

            if (IsWalkableForPlayer(next) || IsRitualTile(next))
            {
                positions.Add(next);
            }
        }

        return positions;
    }

    private void GenerateGrid()
    {
        tileTypes = new TileType[width, height];
        tileViews = new TileView[width, height];
        fogStates = new FogState[width, height];

        for (int x = 0; x < width; x++)
        {
            for (int y = 0; y < height; y++)
            {
                tileTypes[x, y] = TileType.Empty;
                fogStates[x, y] = FogState.Hidden;
                CreateTileView(new Vector2Int(x, y), TileType.Empty);
            }
        }

        PlaceRandomTiles(TileType.Wall, wallCount, avoidEdges: false);
        PlaceRandomTiles(TileType.Safe, safeTileCount, avoidEdges: false);
        PlaceRandomTiles(TileType.Item, itemCount, avoidEdges: true);
        PlaceRandomTiles(TileType.Ritual, 1, avoidEdges: true);
        RefreshFogVisuals();
    }

    private void PlaceRandomTiles(TileType tileType, int count, bool avoidEdges)
    {
        int placed = 0;
        int safetyLimit = width * height * 8;
        int attempts = 0;

        while (placed < count && attempts < safetyLimit)
        {
            attempts++;
            int x = generationRandom.Next(0, width);
            int y = generationRandom.Next(0, height);
            Vector2Int position = new Vector2Int(x, y);

            if (avoidEdges && (x == 0 || y == 0 || x == width - 1 || y == height - 1))
            {
                continue;
            }

            if (tileTypes[x, y] != TileType.Empty)
            {
                continue;
            }

            tileTypes[x, y] = tileType;
            tileViews[x, y].SetType(tileType, GetColorForType(tileType), GetSpriteForType(tileType));
            placed++;
        }
    }

    private Vector2Int GetRandomPosition(System.Predicate<Vector2Int> predicate)
    {
        List<Vector2Int> candidates = new List<Vector2Int>();
        for (int x = 0; x < width; x++)
        {
            for (int y = 0; y < height; y++)
            {
                Vector2Int position = new Vector2Int(x, y);
                if (predicate(position))
                {
                    candidates.Add(position);
                }
            }
        }

        if (candidates.Count == 0)
        {
            return Vector2Int.zero;
        }

        return candidates[generationRandom.Next(0, candidates.Count)];
    }

    private void CreateTileView(Vector2Int gridPosition, TileType tileType)
    {
        GameObject tileObject = new GameObject();
        tileObject.transform.SetParent(transform, false);
        TileView tileView = tileObject.AddComponent<TileView>();
        tileView.Initialize(gridPosition, tileType, GetWorldPosition(gridPosition), tileSize, GetColorForType(tileType), GetSpriteForType(tileType));
        tileViews[gridPosition.x, gridPosition.y] = tileView;
    }

    private Vector2Int WorldToGrid(Vector3 worldPosition)
    {
        float startX = -((width - 1) * tileSize) * 0.5f;
        float startY = -((height - 1) * tileSize) * 0.5f;
        int x = Mathf.RoundToInt((worldPosition.x - startX) / tileSize);
        int y = Mathf.RoundToInt((worldPosition.y - startY) / tileSize);
        return new Vector2Int(x, y);
    }

    private Color GetColorForType(TileType tileType)
    {
        switch (tileType)
        {
            case TileType.Empty:
                return emptyColor;
            case TileType.Wall:
                return wallColor;
            case TileType.Item:
                return itemColor;
            case TileType.Ritual:
                return ritualColor;
            case TileType.Safe:
                return safeTileColor;
            default:
                return Color.magenta;
        }
    }

    private Sprite GetSpriteForType(TileType tileType)
    {
        if (tileType == TileType.Item && visualConfiguration != null && visualConfiguration.ItemSprite != null)
        {
            return visualConfiguration.ItemSprite;
        }

        return GetDefaultTileSprite();
    }

    private static Sprite GetDefaultTileSprite()
    {
        if (sharedDefaultTileSprite != null)
        {
            return sharedDefaultTileSprite;
        }

        Texture2D texture = Texture2D.whiteTexture;
        sharedDefaultTileSprite = Sprite.Create(texture, new Rect(0.0f, 0.0f, texture.width, texture.height), new Vector2(0.5f, 0.5f), texture.width);
        return sharedDefaultTileSprite;
    }

    private void RefreshFogVisuals()
    {
        if (tileViews == null)
        {
            return;
        }

        for (int x = 0; x < width; x++)
        {
            for (int y = 0; y < height; y++)
            {
                tileViews[x, y].SetFogState(fogStates[x, y], fogEnabled);
            }
        }
    }
}
