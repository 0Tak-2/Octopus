using System.Collections;
using UnityEngine;

/// <summary>
/// 필드 ↔ 던전 모드 전환의 단일 창구. 씬 로드 대신 FieldRoot / DungeonRoot 를 전환한다.
/// </summary>
public class ModeManager : MonoBehaviour
{
    public static ModeManager Instance { get; private set; }

    public enum GameplayMode
    {
        Field,
        Dungeon
    }

    public GameplayMode CurrentMode { get; private set; } = GameplayMode.Field;

    [Header("Roots")]
    [Tooltip("필드 타일·적·입구 등")]
    public GameObject fieldRoot;
    [Tooltip("던전 전용 (DungeonController, 던전 GridBoard 등)")]
    public GameObject dungeonRoot;

    [Header("Camera (optional)")]
    public Camera mainCamera;
    [Tooltip("필드·던전 공통 ortho 크기를 쓸 때 동일 값으로 맞춤")]
    public float fieldOrthoSize = 7f;
    public float dungeonOrthoSize = 7f;

    [Header("Fade")]
    public ScreenFader fader;
    public float fadeOutDuration = 0.25f;
    public float fadeInDuration = 0.25f;

    [Header("Refs")]
    public DungeonController dungeonController;

    private void Awake()
    {
        Instance = this;
        if (fieldRoot != null && fieldRoot.activeSelf)
            CurrentMode = GameplayMode.Field;
        else if (dungeonRoot != null && dungeonRoot.activeSelf)
            CurrentMode = GameplayMode.Dungeon;

        EnsureDungeonControllerReference();
    }

    private void Start()
    {
        EnsureDungeonControllerReference();
    }

    private void EnsureDungeonControllerReference()
    {
        if (dungeonController != null) return;
        if (dungeonRoot != null)
            dungeonController = dungeonRoot.GetComponentInChildren<DungeonController>(true);
    }

    private void OnDestroy()
    {
        if (Instance == this)
            Instance = null;
    }

    public void EnterDungeon(string dungeonId)
    {
        if (string.IsNullOrEmpty(dungeonId))
        {
            Debug.LogError("[ModeManager] EnterDungeon: dungeonId 가 비어 있습니다.");
            return;
        }

        StartCoroutine(EnterDungeonRoutine(dungeonId));
    }

    public void ExitToField()
    {
        StartCoroutine(ExitToFieldRoutine());
    }

    private IEnumerator EnterDungeonRoutine(string dungeonId)
    {
        if (fader != null)
            yield return fader.FadeOutIn(() => EnterDungeonCore(dungeonId), fadeOutDuration, fadeInDuration);
        else
        {
            EnterDungeonCore(dungeonId);
            yield return null;
        }
    }

    private void EnterDungeonCore(string dungeonId)
    {
        if (GameManager.Instance != null)
        {
            GameManager.Instance.SaveAllCurrentState();
            GameManager.Instance.playerData.currentDungeonId = dungeonId;
            GameManager.Instance.playerData.returnToMapIndex = GameManager.Instance.currentMapIndex;
            GameManager.Instance.currentDungeonData = GameManager.Instance.GetOrCreateDungeonData(dungeonId);

            if (GameManager.Instance.logSceneTransitions)
                Debug.Log($"[ModeManager] 던전 입장 준비: {dungeonId}, 저장 층={GameManager.Instance.currentDungeonData.currentFloor}");
        }

        // 얼음땡 스냅샷: 현재 필드 적 위치를 그대로 잠가 두고 GameObject 도 즉시 비활성화한다.
        // (fieldRoot 하위가 아닌 위치에 적이 스폰되어 있어도 여기서 강제로 멈춘다.)
        FieldFreezeController.Capture(fieldRoot, dungeonRoot);

        if (fieldRoot != null)
            fieldRoot.SetActive(false);

        if (dungeonRoot != null)
            dungeonRoot.SetActive(true);

        CurrentMode = GameplayMode.Dungeon;

        ApplyCameraForDungeon();

        EnsureDungeonControllerReference();
        if (dungeonController != null)
            dungeonController.Initialize(dungeonId);
        else
            Debug.LogError("[ModeManager] DungeonController 를 찾을 수 없습니다. dungeonRoot 아래에 배치하거나 인스펙터에 할당하세요.");

        var dungeonGrid = dungeonController != null ? dungeonController.gridBoard : null;
        int dFloor = 1;
        if (GameManager.Instance != null && GameManager.Instance.currentDungeonData != null)
            dFloor = Mathf.Max(1, GameManager.Instance.currentDungeonData.currentFloor);
        string dungeonFogKey = $"{dungeonId}_F{dFloor}";

        if (dungeonGrid != null)
        {
            if (FieldVisionSystem.Instance != null)
            {
                FieldVisionSystem.Instance.BindGridBoard(dungeonGrid);
                FieldVisionSystem.Instance.SetDungeonVisionBoost(true);
            }
            if (FogOfWarRenderer.Instance != null)
                FogOfWarRenderer.Instance.BindRuntimeGrid(dungeonGrid, "_Dungeon", dungeonFogKey);
        }

        var mover = FindObjectOfType<PlayerGridMover>();
        if (mover != null)
            mover.NotifyReferencesNeedRefresh();

        BindScreenBlackBars(dungeonGrid);
    }

    private IEnumerator ExitToFieldRoutine()
    {
        if (fader != null)
            yield return fader.FadeOutIn(ExitToFieldCore, fadeOutDuration, fadeInDuration);
        else
        {
            ExitToFieldCore();
            yield return null;
        }

        // GridBoard 가 필드 쪽으로 바뀐 뒤 플레이어 이동/점유 동기화
        var mover = FindObjectOfType<PlayerGridMover>();
        if (mover != null)
            mover.NotifyReferencesNeedRefresh();
    }

    private void ExitToFieldCore()
    {
        if (dungeonController != null)
            dungeonController.SaveCurrentFloorToGameManager();

        if (GameManager.Instance != null)
        {
            GameManager.Instance.SavePlayerStats();
            GameManager.Instance.SaveInventory();

            if (GameManager.Instance.currentDungeonData != null)
                GameManager.Instance.SaveDungeonData(GameManager.Instance.currentDungeonData);
        }

        if (dungeonController != null)
            dungeonController.Cleanup();

        if (dungeonRoot != null)
            dungeonRoot.SetActive(false);

        if (fieldRoot != null)
            fieldRoot.SetActive(true);

        CurrentMode = GameplayMode.Field;

        ApplyCameraForField();

        GridBoard fieldGrid = FieldMapGenerator.ResolveGameplayGrid(fieldRoot);
        if (FieldVisionSystem.Instance != null)
            FieldVisionSystem.Instance.SetDungeonVisionBoost(false);

        if (fieldGrid != null)
        {
            if (FieldVisionSystem.Instance != null)
                FieldVisionSystem.Instance.BindGridBoard(fieldGrid);
            if (FogOfWarRenderer.Instance != null)
                FogOfWarRenderer.Instance.BindRuntimeGrid(fieldGrid, "_Field");
        }

        if (GameManager.Instance != null)
        {
            GameManager.Instance.RestorePlayerStats();
            GameManager.Instance.RestoreInventory();
        }

        // 얼음땡 해제: 저장된 "셀" 좌표 기반 복원(RestoreFieldState)은 GridBoard 불일치로 위치가 튀는
        // 문제가 있어 사용하지 않는다. 던전 입장 직전에 잠가 둔 월드 좌표를 그대로 복원한다.
        FieldFreezeController.Apply(fieldRoot, fieldGrid);

        // AI들의 gridBoard 참조를 필드 격자로 재바인딩(점유는 Apply에서 이미 재등록됨).
        FieldDungeonReturnSync.Apply(fieldGrid, fieldRoot);

        TryRestorePlayerToDungeonEntrance(fieldGrid);

        WorldProgressManager.Instance?.ExitDungeon();

        BindScreenBlackBars(fieldGrid);
    }

    /// <summary>
    /// 같은 씬 모드 전환(던전→필드)에서는 FieldMapGenerator.SpawnPlayer가 다시 호출되지 않으므로
    /// 여기서 던전 입구 복귀 좌표를 즉시 적용한다.
    /// </summary>
    private void TryRestorePlayerToDungeonEntrance(GridBoard fieldGrid)
    {
        if (fieldGrid == null || GameManager.Instance == null)
            return;

        var pd = GameManager.Instance.playerData;
        if (!pd.hasDungeonReturnPosition)
            return;

        Vector2Int targetCell = pd.dungeonEntrancePosition;
        if (!fieldGrid.InBounds(targetCell))
        {
            if (GameManager.Instance.logSceneTransitions)
                Debug.LogWarning($"[ModeManager] 저장된 던전 복귀 좌표가 필드 범위를 벗어남: {targetCell}");
            pd.hasDungeonReturnPosition = false;
            pd.dungeonEntrancePosition = Vector2Int.zero;
            return;
        }

        var mover = FindObjectOfType<PlayerGridMover>();
        if (mover != null)
        {
            if (mover.grid != fieldGrid)
                mover.grid = fieldGrid;
            mover.WarpToFieldCell(targetCell);
        }
        else
        {
            var player = GameObject.FindGameObjectWithTag("Player");
            if (player != null)
                player.transform.position = fieldGrid.CellToWorld(targetCell);
        }

        pd.hasDungeonReturnPosition = false;
        pd.dungeonEntrancePosition = Vector2Int.zero;
        pd.hasPendingEntranceHint = false;

        if (GameManager.Instance.logSceneTransitions)
            Debug.Log($"[ModeManager] 던전 복귀 위치 적용: {targetCell}");
    }

    private static void BindScreenBlackBars(GridBoard board)
    {
        if (board == null) return;

        FieldMapScreenBlackBars bars = null;
        if (FogOfWarRenderer.Instance != null)
            bars = FogOfWarRenderer.Instance.GetComponent<FieldMapScreenBlackBars>();
        if (bars == null)
            bars = Object.FindObjectOfType<FieldMapScreenBlackBars>();
        if (bars != null)
            bars.BindGridBoard(board);
    }

    private void ApplyCameraForDungeon()
    {
        if (mainCamera == null)
            mainCamera = Camera.main;
        if (mainCamera != null)
            mainCamera.orthographicSize = dungeonOrthoSize;
    }

    private void ApplyCameraForField()
    {
        if (mainCamera == null)
            mainCamera = Camera.main;
        if (mainCamera != null)
            mainCamera.orthographicSize = fieldOrthoSize;
    }
}
