using System.Collections.Generic;
using UnityEngine;

public sealed class GridManager : MonoBehaviour
{
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
    [SerializeField] private Color itemColor = Color.yellow;
    [SerializeField] private Color ritualColor = new Color(1.0f, 0.55f, 0.0f, 1.0f);
    [SerializeField] private Color safeTileColor = new Color(0.15f, 0.7f, 0.3f, 1.0f);

    private TileType[,] tileTypes;
    private TileView[,] tileViews;

    public int Width => width;
    public int Height => height;
    public int ItemCount => itemCount;
    public float TileSize => tileSize;

    private void Awake()
    {
        GenerateGrid();
    }

    public bool IsInsideGrid(Vector2Int gridPosition)
    {
        return gridPosition.x >= 0 && gridPosition.x < width && gridPosition.y >= 0 && gridPosition.y < height;
    }

    public bool IsWalkableForPlayer(Vector2Int gridPosition)
    {
        return IsInsideGrid(gridPosition) && tileTypes[gridPosition.x, gridPosition.y] != TileType.Wall;
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
        tileViews[gridPosition.x, gridPosition.y].SetType(TileType.Empty, GetColorForType(TileType.Empty));
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

    private void GenerateGrid()
    {
        tileTypes = new TileType[width, height];
        tileViews = new TileView[width, height];

        for (int x = 0; x < width; x++)
        {
            for (int y = 0; y < height; y++)
            {
                tileTypes[x, y] = TileType.Empty;
                CreateTileView(new Vector2Int(x, y), TileType.Empty);
            }
        }

        PlaceRandomTiles(TileType.Wall, wallCount, avoidEdges: false);
        PlaceRandomTiles(TileType.Safe, safeTileCount, avoidEdges: false);
        PlaceRandomTiles(TileType.Item, itemCount, avoidEdges: true);
        PlaceRandomTiles(TileType.Ritual, 1, avoidEdges: true);
    }

    private void PlaceRandomTiles(TileType tileType, int count, bool avoidEdges)
    {
        int placed = 0;
        int safetyLimit = width * height * 8;
        int attempts = 0;

        while (placed < count && attempts < safetyLimit)
        {
            attempts++;
            int x = Random.Range(0, width);
            int y = Random.Range(0, height);
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
            tileViews[x, y].SetType(tileType, GetColorForType(tileType));
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

        return candidates[Random.Range(0, candidates.Count)];
    }

    private void CreateTileView(Vector2Int gridPosition, TileType tileType)
    {
        GameObject tileObject = new GameObject();
        tileObject.transform.SetParent(transform, false);
        TileView tileView = tileObject.AddComponent<TileView>();
        tileView.Initialize(gridPosition, tileType, GetWorldPosition(gridPosition), tileSize, GetColorForType(tileType));
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
}
