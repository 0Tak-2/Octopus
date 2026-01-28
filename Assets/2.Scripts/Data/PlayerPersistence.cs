using UnityEngine;

/// <summary>
/// 플레이어를 씬 전환 시에도 유지
/// </summary>
public class PlayerPersistence : MonoBehaviour
{
    public static PlayerPersistence Instance { get; private set; }

    private void Awake()
    {
        Debug.Log($"[PlayerPersistence] Awake - GameObject: {gameObject.name}, Active: {gameObject.activeSelf}, Parent: {(transform.parent != null ? transform.parent.name : "None")}");

        if (Instance == null)
        {
            Instance = this;

            // 부모가 있으면 분리 (Root로)
            if (transform.parent != null)
            {
                Debug.Log($"[PlayerPersistence] Detaching from parent: {transform.parent.name}");
                transform.SetParent(null);
            }

            // DontDestroyOnLoad 적용
            DontDestroyOnLoad(gameObject);

            Debug.Log($"[PlayerPersistence] ✅ Player set to persist: {gameObject.name}");
        }
        else
        {
            Debug.Log($"[PlayerPersistence] ❌ Duplicate found, destroying: {gameObject.name}");
            Destroy(gameObject);
        }
    }

    private void OnDestroy()
    {
        if (Instance == this)
        {
            Debug.Log("[PlayerPersistence] Instance destroyed, clearing reference");
            Instance = null;
        }
    }

    // 디버그용
    private void Start()
    {
        Debug.Log($"[PlayerPersistence] Start - In scene: {gameObject.scene.name}, Position: {transform.position}");
    }
}