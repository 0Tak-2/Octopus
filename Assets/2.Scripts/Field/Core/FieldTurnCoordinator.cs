using System.Collections;
using UnityEngine;

/// <summary>
/// 필드 턴 진행 단일 진입점.
/// 플레이어 액션 종료 시 Time 진행 + 적 턴 처리 순서를 직렬화한다.
/// </summary>
public class FieldTurnCoordinator : MonoBehaviour
{
    public static FieldTurnCoordinator Instance { get; private set; }

    [Header("Refs")]
    public FieldTimeManager fieldTimeManager;
    public FieldMultiEnemyAttack multiEnemyAttack;

    [Header("Debug")]
    public bool showDebugLogs = false;

    private bool _isCommitting;

    public bool IsCommittingPlayerAction => _isCommitting;
    public bool IsResolvingEnemyTurn => multiEnemyAttack != null && multiEnemyAttack.IsResolvingEnemyTurn;
    public bool IsBusy => _isCommitting || IsResolvingEnemyTurn;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        ResolveRefs();
    }

    private void OnDestroy()
    {
        if (Instance == this)
            Instance = null;
    }

    private void ResolveRefs()
    {
        if (fieldTimeManager == null)
            fieldTimeManager = FieldTimeManager.Instance ?? FindObjectOfType<FieldTimeManager>();
        if (multiEnemyAttack == null)
            multiEnemyAttack = FindObjectOfType<FieldMultiEnemyAttack>();
    }

    /// <summary>
    /// 비동기 커밋. 이미 턴 처리 중이면 false.
    /// </summary>
    public bool TryCommitPlayerAction(int timeCost, bool triggerEnemyTurn = true)
    {
        if (IsBusy)
            return false;

        StartCoroutine(CommitRoutine(timeCost, triggerEnemyTurn));
        return true;
    }

    /// <summary>
    /// 코루틴 문맥에서 사용. 완료까지 대기.
    /// </summary>
    public IEnumerator CommitPlayerActionAndWait(int timeCost, bool triggerEnemyTurn = true)
    {
        if (IsBusy)
            yield break;

        yield return CommitRoutine(timeCost, triggerEnemyTurn);
    }

    private IEnumerator CommitRoutine(int timeCost, bool triggerEnemyTurn)
    {
        _isCommitting = true;
        ResolveRefs();

        int delta = Mathf.Max(0, timeCost);
        if (delta > 0 && fieldTimeManager != null)
        {
            fieldTimeManager.Advance(delta);
            if (showDebugLogs)
                Debug.Log($"[FieldTurnCoordinator] Time advanced: +{delta}");
        }

        if (triggerEnemyTurn && multiEnemyAttack != null)
        {
            multiEnemyAttack.OnPlayerTurnEnd();
            while (multiEnemyAttack != null && multiEnemyAttack.IsResolvingEnemyTurn)
                yield return null;
        }

        _isCommitting = false;
    }
}
