using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public sealed class GameManager : MonoBehaviour
{
    [SerializeField] private string gameOverText = "Game Over";
    [SerializeField] private string winText = "You Win";

    private GridManager gridManager;
    private PlayerController playerController;
    private GhostController ghostController;

    private Text stateText;
    private Text progressText;
    private Button restartButton;

    private bool isGameFinished;
    private int totalItemsToBurn;
    private static bool hasRegisteredSceneLoadedCallback;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void RegisterSceneBootstrap()
    {
        if (hasRegisteredSceneLoadedCallback)
        {
            return;
        }

        hasRegisteredSceneLoadedCallback = true;
        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    private static void OnSceneLoaded(Scene scene, LoadSceneMode loadSceneMode)
    {
        AutoCreateIfMissing();
    }

    private static void AutoCreateIfMissing()
    {
        GameManager existingGameManager = FindObjectOfType<GameManager>();
        if (existingGameManager != null)
        {
            return;
        }

        GameObject gameManagerObject = new GameObject(nameof(GameManager));
        gameManagerObject.AddComponent<GameManager>();
    }

    private void Awake()
    {
        CreateWorldObjects();
        CreateUserInterface();
        InitializeGame();
    }

    private void Update()
    {
        if (isGameFinished)
        {
            return;
        }

        if (TryReadPointerDownPosition(out Vector3 worldPosition) && gridManager.TryGetTilePositionFromWorld(worldPosition, out Vector2Int selectedGridPosition))
        {
            TryRunPlayerTurn(selectedGridPosition);
        }
    }

    public void RestartScene()
    {
        Scene activeScene = SceneManager.GetActiveScene();
        if (!string.IsNullOrEmpty(activeScene.path))
        {
            SceneManager.LoadScene(activeScene.path);
            return;
        }

        SceneManager.LoadScene(activeScene.name);
    }

    private void CreateWorldObjects()
    {
        GameObject gridObject = new GameObject(nameof(GridManager));
        gridManager = gridObject.AddComponent<GridManager>();

        GameObject playerObject = new GameObject(nameof(PlayerController));
        playerController = playerObject.AddComponent<PlayerController>();

        GameObject ghostObject = new GameObject(nameof(GhostController));
        ghostController = ghostObject.AddComponent<GhostController>();
    }

    private void CreateUserInterface()
    {
        EnsureEventSystemExists();

        Canvas existingCanvas = FindObjectOfType<Canvas>();
        Canvas canvas = existingCanvas;

        if (canvas == null)
        {
            GameObject canvasObject = new GameObject("Canvas");
            canvas = canvasObject.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvasObject.AddComponent<CanvasScaler>();
            canvasObject.AddComponent<GraphicRaycaster>();
        }

        stateText = CreateText("StateText", canvas.transform, new Vector2(0.5f, 0.9f), 48, string.Empty);
        progressText = CreateText("ProgressText", canvas.transform, new Vector2(0.5f, 0.82f), 26, string.Empty);
        restartButton = CreateRestartButton(canvas.transform);
        restartButton.gameObject.SetActive(false);
    }

    private static void EnsureEventSystemExists()
    {
        EventSystem existingEventSystem = FindObjectOfType<EventSystem>();
        if (existingEventSystem != null)
        {
            return;
        }

        GameObject eventSystemObject = new GameObject("EventSystem");
        eventSystemObject.AddComponent<EventSystem>();
        eventSystemObject.AddComponent<StandaloneInputModule>();
    }

    private void InitializeGame()
    {
        totalItemsToBurn = gridManager.ItemCount;

        Vector2Int playerStartPosition = gridManager.GetRandomWalkablePositionForPlayer();
        playerController.SetInitialPosition(playerStartPosition, gridManager.GetWorldPosition(playerStartPosition), gridManager.TileSize);

        Vector2Int ghostStartPosition = gridManager.GetRandomWalkablePositionForGhost();
        if (ghostStartPosition == playerStartPosition)
        {
            ghostStartPosition = gridManager.GetRandomWalkablePositionForGhost();
        }
        ghostController.SetInitialPosition(ghostStartPosition, gridManager.GetWorldPosition(ghostStartPosition), gridManager.TileSize);

        gridManager.FrameMainCamera();
        RefreshProgressText();
        RefreshMoveHighlights();
    }

    private void TryRunPlayerTurn(Vector2Int selectedGridPosition)
    {
        Vector2Int currentPosition = playerController.GridPosition;
        if (!IsAdjacentCardinal(currentPosition, selectedGridPosition))
        {
            return;
        }

        if (!gridManager.IsWalkableForPlayer(selectedGridPosition))
        {
            return;
        }

        playerController.MoveTo(selectedGridPosition, gridManager.GetWorldPosition(selectedGridPosition));

        if (gridManager.TryConsumeItem(selectedGridPosition))
        {
            playerController.PickUpItem();
        }

        ghostController.TakeTurn(playerController.GridPosition, gridManager);
        if (ghostController.GridPosition == playerController.GridPosition)
        {
            SetGameFinished(gameOverText);
            return;
        }

        if (gridManager.GetTileType(playerController.GridPosition) == TileType.Ritual)
        {
            playerController.TryBurnOneItem();
        }

        RefreshProgressText();

        if (playerController.BurnedItemCount >= totalItemsToBurn)
        {
            SetGameFinished(winText);
            return;
        }

        RefreshMoveHighlights();
    }

    private void SetGameFinished(string textValue)
    {
        isGameFinished = true;
        stateText.text = textValue;
        restartButton.gameObject.SetActive(true);
        gridManager.ClearHighlights();
    }

    private void RefreshProgressText()
    {
        progressText.text =
            $"Carried: {playerController.CarriedItemCount}  Burned: {playerController.BurnedItemCount}/{totalItemsToBurn}";
    }

    private void RefreshMoveHighlights()
    {
        gridManager.HighlightTiles(gridManager.GetValidAdjacentTilesForPlayer(playerController.GridPosition));
    }

    private static bool IsAdjacentCardinal(Vector2Int a, Vector2Int b)
    {
        int manhattanDistance = Mathf.Abs(a.x - b.x) + Mathf.Abs(a.y - b.y);
        return manhattanDistance == 1;
    }

    private static bool TryReadPointerDownPosition(out Vector3 worldPosition)
    {
        worldPosition = Vector3.zero;
        Camera mainCamera = Camera.main;
        if (mainCamera == null)
        {
            return false;
        }

        Vector3 screenPosition;
        if (Input.touchCount > 0 && Input.GetTouch(0).phase == TouchPhase.Began)
        {
            screenPosition = Input.GetTouch(0).position;
        }
        else if (Input.GetMouseButtonDown(0))
        {
            screenPosition = Input.mousePosition;
        }
        else
        {
            return false;
        }

        screenPosition.z = Mathf.Abs(mainCamera.transform.position.z);
        worldPosition = mainCamera.ScreenToWorldPoint(screenPosition);

        // Uses a 2D raycast so tile colliders can be clicked or tapped in both editor and WebGL.
        RaycastHit2D hit = Physics2D.Raycast(worldPosition, Vector2.zero);
        if (hit.collider == null)
        {
            return false;
        }

        worldPosition = hit.point;
        return true;
    }

    private static Text CreateText(string objectName, Transform parent, Vector2 anchor, int fontSize, string initialText)
    {
        GameObject textObject = new GameObject(objectName);
        textObject.transform.SetParent(parent, false);

        RectTransform rectTransform = textObject.AddComponent<RectTransform>();
        rectTransform.anchorMin = anchor;
        rectTransform.anchorMax = anchor;
        rectTransform.pivot = new Vector2(0.5f, 0.5f);
        rectTransform.sizeDelta = new Vector2(900.0f, 80.0f);
        rectTransform.anchoredPosition = Vector2.zero;

        Text textComponent = textObject.AddComponent<Text>();
        textComponent.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        textComponent.alignment = TextAnchor.MiddleCenter;
        textComponent.fontSize = fontSize;
        textComponent.color = Color.white;
        textComponent.text = initialText;
        textComponent.raycastTarget = false;
        return textComponent;
    }

    private Button CreateRestartButton(Transform parent)
    {
        GameObject buttonObject = new GameObject("RestartButton");
        buttonObject.transform.SetParent(parent, false);

        RectTransform rectTransform = buttonObject.AddComponent<RectTransform>();
        rectTransform.anchorMin = new Vector2(0.5f, 0.1f);
        rectTransform.anchorMax = new Vector2(0.5f, 0.1f);
        rectTransform.pivot = new Vector2(0.5f, 0.5f);
        rectTransform.sizeDelta = new Vector2(220.0f, 60.0f);
        rectTransform.anchoredPosition = Vector2.zero;

        Image backgroundImage = buttonObject.AddComponent<Image>();
        backgroundImage.color = new Color(0.15f, 0.15f, 0.15f, 0.92f);

        Button button = buttonObject.AddComponent<Button>();
        button.onClick.AddListener(RestartScene);

        Text buttonText = CreateText("Text", buttonObject.transform, new Vector2(0.5f, 0.5f), 26, "Restart");
        buttonText.rectTransform.sizeDelta = new Vector2(220.0f, 60.0f);
        buttonText.color = Color.white;

        return button;
    }
}
