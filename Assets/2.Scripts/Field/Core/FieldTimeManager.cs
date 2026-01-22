using UnityEngine;

public class FieldTimeManager : MonoBehaviour
{
    public static FieldTimeManager Instance { get; private set; }

    [Header("Field Time")]
    public int time = 0;

    public event System.Action<int, int> OnTimeAdvanced;
    // (delta, newTotalTime)

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
    }

    public void Advance(int amount)
    {
        int delta = Mathf.Max(0, amount);
        if (delta <= 0) return;

        time += delta;

        // ✅ 모든 적의 턴 초기화
        ResetAllEnemyTurns();

        OnTimeAdvanced?.Invoke(delta, time);

        Debug.Log($"[FieldTime] +{delta} => {time}");
    }

    /// <summary>
    /// 모든 적의 행동 플래그 초기화
    /// </summary>
    private void ResetAllEnemyTurns()
    {
        EnemyInstance[] enemies = FindObjectsOfType<EnemyInstance>();
        foreach (var enemy in enemies)
        {
            if (enemy != null && enemy.gameObject.activeInHierarchy)
            {
                enemy.ResetTurn();
            }
        }
    }
}