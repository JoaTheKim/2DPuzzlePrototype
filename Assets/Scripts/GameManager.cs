using System;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using System.Collections;
using System.Collections.Generic;

public sealed class GameManager : MonoBehaviour
{
    private const string VISUAL_CONFIGURATION_RESOURCE_PATH = "GameVisualConfiguration";
    private const int DEFAULT_VISION_RADIUS = 3;
    private const int BASE_CHASE_RADIUS = 2;
    private const int ESCALATED_CHASE_RADIUS = 3;
    private const int GHOST_STUN_TURNS_AFTER_SACRIFICE = 2;
    private const int STICKY_AGGRO_BONUS_MOVE_INTERVAL = 3;
    private static readonly Color GHOST_COUNTER_DEFAULT_COLOR = Color.white;
    private static readonly Color GHOST_COUNTER_WARNING_COLOR = Color.red;
    private static int? nextGridSeed;

    [SerializeField] private GameVisualConfiguration visualConfiguration;
    [SerializeField] private string gameOverText = "Game Over";
    [SerializeField] private string winText = "You Win";
    [SerializeField] private bool reduceVisionWhenCarryingItem = true;
    [SerializeField] private bool debugFogEnabled = true;
    [SerializeField] private bool debugLogGhostState = true;
    [SerializeField] private Color jumpscareFlashColor = new Color(1.0f, 0.2f, 0.2f, 0.75f);
    [SerializeField] private float jumpscareFlashDuration = 0.18f;
    [SerializeField] private float jumpscareGhostScaleMultiplier = 1.4f;
    [SerializeField] private float jumpscareGhostScaleDuration = 0.22f;

    private GridManager gridManager;
    private PlayerController playerController;
    private GhostController ghostController;

    private Text stateText;
    private Text progressText;
    private Text ghostActionText;
    private Text ghostStunnedText;
    private Button generateNewButton;
    private Button restartButton;
    private Image jumpscareFlashImage;

    private bool isGameFinished;
    private int totalItemsToBurn;
    private int turnCount;
    private int currentGridSeed;
    private int currentChaseRadius;
    private int pendingGhostStunTurns;
    private bool hasTriggeredJumpscare;
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
        ResolveVisualConfiguration();
        ResolveGridSeed();
        CreateWorldObjects();
        CreateUserInterface();
        InitializeGame();
    }

    private void Update()
    {
        if (Input.GetKeyDown(KeyCode.F1))
        {
            debugFogEnabled = !debugFogEnabled;
            gridManager.SetFogEnabled(debugFogEnabled);
        }

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
        ReloadSceneWithSeed(currentGridSeed);
    }

    public void GenerateNewScene()
    {
        int newSeed = Guid.NewGuid().GetHashCode();
        ReloadSceneWithSeed(newSeed);
    }

    private void CreateWorldObjects()
    {
        GameObject gridObject = new GameObject(nameof(GridManager));
        gridManager = gridObject.AddComponent<GridManager>();
        gridManager.Initialize(visualConfiguration, currentGridSeed);

        GameObject playerObject = new GameObject(nameof(PlayerController));
        playerController = playerObject.AddComponent<PlayerController>();
        playerController.Initialize(visualConfiguration);

        GameObject ghostObject = new GameObject(nameof(GhostController));
        ghostController = ghostObject.AddComponent<GhostController>();
        ghostController.Initialize(visualConfiguration);
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
        ghostActionText = CreateText("GhostActionText", canvas.transform, new Vector2(0.5f, 0.75f), 24, string.Empty);
        ghostStunnedText = CreateText("GhostStunnedText", canvas.transform, new Vector2(0.5f, 0.69f), 24, string.Empty);
        generateNewButton = CreateActionButton("GenerateNewButton", "Generate New", canvas.transform, new Vector2(0.5f, 0.24f), GenerateNewScene);
        restartButton = CreateActionButton("RestartButton", "Restart", canvas.transform, new Vector2(0.5f, 0.14f), RestartScene);
        restartButton.gameObject.SetActive(false);
        generateNewButton.gameObject.SetActive(false);
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
        currentChaseRadius = BASE_CHASE_RADIUS;
        gridManager.SetFogEnabled(debugFogEnabled);

        Vector2Int playerStartPosition = gridManager.GetRandomWalkablePositionForPlayer();
        playerController.SetInitialPosition(playerStartPosition, gridManager.GetWorldPosition(playerStartPosition), gridManager.TileSize);

        Vector2Int ghostStartPosition = gridManager.GetRandomWalkablePositionForGhost();
        if (ghostStartPosition == playerStartPosition)
        {
            ghostStartPosition = gridManager.GetRandomWalkablePositionForGhost();
        }
        ghostController.SetInitialPosition(ghostStartPosition, gridManager.GetWorldPosition(ghostStartPosition), gridManager.TileSize);

        UpdateFogAndGhostVisibility();
        gridManager.FrameMainCamera();
        RefreshProgressText();
        UpdateGhostDoubleTurnCounterUi();
        UpdateGhostStunnedUi();
        RefreshMoveHighlights();
    }

    private void TryRunPlayerTurn(Vector2Int selectedGridPosition)
    {
        Vector2Int currentPosition = playerController.GridPosition;
        if (!IsAdjacentCardinal(currentPosition, selectedGridPosition))
        {
            return;
        }

        bool consumedTurn = false;
        if (gridManager.IsRitualTile(selectedGridPosition))
        {
            consumedTurn = TryPerformSacrificeAction();
        }
        else if (gridManager.IsWalkableForPlayer(selectedGridPosition))
        {
            consumedTurn = TryPerformMoveAction(selectedGridPosition);
        }

        if (!consumedTurn)
        {
            return;
        }

        ResolveTurn();
    }

    private bool TryPerformMoveAction(Vector2Int selectedGridPosition)
    {
        playerController.MoveTo(selectedGridPosition, gridManager.GetWorldPosition(selectedGridPosition));
        if (!playerController.HasItem && gridManager.TryConsumeItem(selectedGridPosition))
        {
            playerController.TryPickUpItem();
        }

        return true;
    }

    private bool TryPerformSacrificeAction()
    {
        if (!playerController.HasItem)
        {
            return false;
        }

        bool burned = playerController.TryBurnOneItem();
        if (!burned)
        {
            return false;
        }

        pendingGhostStunTurns = GHOST_STUN_TURNS_AFTER_SACRIFICE;
        ApplyDifficultyEscalation();
        return true;
    }

    private void ResolveTurn()
    {
        turnCount++;
        UpdateFogAndGhostVisibility();
        ghostController.UpdateState(playerController.GridPosition, playerController.HasItem, currentChaseRadius);

        bool bonusGhostMove = playerController.BurnedItemCount >= 2 && turnCount % STICKY_AGGRO_BONUS_MOVE_INTERVAL == 0;
        ghostController.TakeTurn(playerController.GridPosition, gridManager, bonusGhostMove);
        UpdateGhostDoubleTurnCounterUi();
        UpdateFogAndGhostVisibility();

        if (debugLogGhostState)
        {
            Debug.Log($"Turn: {turnCount} | GhostState: {ghostController.State} | AggroTimer: {ghostController.AggroTimer}");
        }

        if (ghostController.GridPosition == playerController.GridPosition)
        {
            SetGameFinished(gameOverText);
            return;
        }

        if (pendingGhostStunTurns > 0)
        {
            ghostController.ApplyStun(pendingGhostStunTurns);
            pendingGhostStunTurns = 0;
        }

        RefreshProgressText();
        UpdateGhostStunnedUi();
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
        generateNewButton.gameObject.SetActive(true);
        gridManager.ClearHighlights();
    }

    private void RefreshProgressText()
    {
        progressText.text =
            $"Turns: {turnCount}  Carried: {playerController.CarriedItemCount}  Burned: {playerController.BurnedItemCount}/{totalItemsToBurn}";
    }

    private void RefreshMoveHighlights()
    {
        gridManager.HighlightTiles(gridManager.GetValidAdjacentActionTiles(playerController.GridPosition));
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

    private Button CreateActionButton(string objectName, string buttonLabel, Transform parent, Vector2 anchor, UnityEngine.Events.UnityAction clickAction)
    {
        GameObject buttonObject = new GameObject(objectName);
        buttonObject.transform.SetParent(parent, false);

        RectTransform rectTransform = buttonObject.AddComponent<RectTransform>();
        rectTransform.anchorMin = anchor;
        rectTransform.anchorMax = anchor;
        rectTransform.pivot = new Vector2(0.5f, 0.5f);
        rectTransform.sizeDelta = new Vector2(220.0f, 60.0f);
        rectTransform.anchoredPosition = Vector2.zero;

        Image backgroundImage = buttonObject.AddComponent<Image>();
        backgroundImage.color = new Color(0.15f, 0.15f, 0.15f, 0.92f);

        Button button = buttonObject.AddComponent<Button>();
        button.onClick.AddListener(clickAction);

        Text buttonText = CreateText("Text", buttonObject.transform, new Vector2(0.5f, 0.5f), 26, buttonLabel);
        buttonText.rectTransform.sizeDelta = new Vector2(220.0f, 60.0f);
        buttonText.color = Color.white;

        return button;
    }

    private void UpdateFogAndGhostVisibility()
    {
        int visionRadius = DEFAULT_VISION_RADIUS;
        if (reduceVisionWhenCarryingItem && playerController.HasItem)
        {
            visionRadius = Mathf.Max(1, DEFAULT_VISION_RADIUS - 1);
        }

        gridManager.UpdateFog(playerController.GridPosition, visionRadius);
        bool ghostVisible = gridManager.IsTileVisible(ghostController.GridPosition) || !gridManager.IsFogEnabled;
        ghostController.SetVisible(ghostVisible);

        if (ghostVisible && !hasTriggeredJumpscare)
        {
            hasTriggeredJumpscare = true;
            StartCoroutine(PlayJumpscareEffect());
        }
    }

    private void ApplyDifficultyEscalation()
    {
        if (playerController.BurnedItemCount >= 1)
        {
            currentChaseRadius = ESCALATED_CHASE_RADIUS;
        }
    }

    private IEnumerator PlayJumpscareEffect()
    {
        if (jumpscareFlashImage == null)
        {
            jumpscareFlashImage = CreateJumpscareFlashImage();
        }

        Vector3 initialScale = ghostController.transform.localScale;
        jumpscareFlashImage.gameObject.SetActive(true);
        jumpscareFlashImage.color = jumpscareFlashColor;
        ghostController.transform.localScale = initialScale * jumpscareGhostScaleMultiplier;

        yield return new WaitForSeconds(jumpscareFlashDuration);

        jumpscareFlashImage.gameObject.SetActive(false);
        yield return new WaitForSeconds(jumpscareGhostScaleDuration);
        ghostController.transform.localScale = initialScale;
    }

    private Image CreateJumpscareFlashImage()
    {
        Canvas canvas = FindObjectOfType<Canvas>();
        GameObject flashObject = new GameObject("JumpscareFlash");
        flashObject.transform.SetParent(canvas.transform, false);

        RectTransform rectTransform = flashObject.AddComponent<RectTransform>();
        rectTransform.anchorMin = Vector2.zero;
        rectTransform.anchorMax = Vector2.one;
        rectTransform.offsetMin = Vector2.zero;
        rectTransform.offsetMax = Vector2.zero;

        Image image = flashObject.AddComponent<Image>();
        image.color = Color.clear;
        flashObject.SetActive(false);
        return image;
    }

    private void ResolveVisualConfiguration()
    {
        if (visualConfiguration != null)
        {
            return;
        }

        visualConfiguration = Resources.Load<GameVisualConfiguration>(VISUAL_CONFIGURATION_RESOURCE_PATH);
    }

    private void ResolveGridSeed()
    {
        currentGridSeed = nextGridSeed ?? Guid.NewGuid().GetHashCode();
        nextGridSeed = null;
    }

    private static void ReloadSceneWithSeed(int seed)
    {
        nextGridSeed = seed;
        Scene activeScene = SceneManager.GetActiveScene();
        if (!string.IsNullOrEmpty(activeScene.path))
        {
            SceneManager.LoadScene(activeScene.path);
            return;
        }

        SceneManager.LoadScene(activeScene.name);
    }

    private void UpdateGhostDoubleTurnCounterUi()
    {
        if (playerController.BurnedItemCount < 2)
        {
            ghostActionText.gameObject.SetActive(false);
            return;
        }

        ghostActionText.gameObject.SetActive(true);
        int remainder = turnCount % STICKY_AGGRO_BONUS_MOVE_INTERVAL;
        int turnsUntilDouble = remainder == 0 ? STICKY_AGGRO_BONUS_MOVE_INTERVAL : STICKY_AGGRO_BONUS_MOVE_INTERVAL - remainder;
        if (turnsUntilDouble == 1)
        {
            ghostActionText.color = GHOST_COUNTER_WARNING_COLOR;
            ghostActionText.text = "Ghost double turn NEXT TURN";
            return;
        }

        ghostActionText.color = GHOST_COUNTER_DEFAULT_COLOR;
        ghostActionText.text = $"Ghost double turn in {turnsUntilDouble} turns";
    }

    private void UpdateGhostStunnedUi()
    {
        int stunnedTurns = ghostController.StunnedTurns;
        if (stunnedTurns <= 0)
        {
            ghostStunnedText.gameObject.SetActive(false);
            return;
        }

        ghostStunnedText.gameObject.SetActive(true);
        ghostStunnedText.color = GHOST_COUNTER_WARNING_COLOR;
        ghostStunnedText.text = $"Ghost stunned for {stunnedTurns} turns";
    }
}
