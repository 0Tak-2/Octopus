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
        OnTimeAdvanced?.Invoke(delta, time);

        Debug.Log($"[FieldTime] +{delta} => {time}");
    }
}
