using UnityEngine;
using UnityEngine.EventSystems;

/// <summary>
/// UI Canvas와 EventSystem을 씬 전환 시에도 유지
/// </summary>
public class UIPersistence : MonoBehaviour
{
    public static UIPersistence Instance { get; private set; }

    [Header("UI References")]
    public Canvas mainCanvas;
    public EventSystem eventSystem;

    private void Awake()
    {
        Debug.Log($"[UIPersistence] Awake - GameObject: {gameObject.name}");

        if (Instance == null)
        {
            Instance = this;

            // 부모에서 분리
            if (transform.parent != null)
            {
                Debug.Log($"[UIPersistence] Detaching from parent: {transform.parent.name}");
                transform.SetParent(null);
            }

            // DontDestroyOnLoad
            DontDestroyOnLoad(gameObject);

            // Canvas 찾기
            if (mainCanvas == null)
            {
                mainCanvas = GetComponentInChildren<Canvas>();
            }

            // EventSystem 찾기
            if (eventSystem == null)
            {
                eventSystem = FindObjectOfType<EventSystem>();
            }

            Debug.Log($"[UIPersistence] ✅ UI set to persist - Canvas: {mainCanvas != null}, EventSystem: {eventSystem != null}");
        }
        else
        {
            Debug.Log($"[UIPersistence] ❌ Duplicate UI, destroying: {gameObject.name}");
            Destroy(gameObject);
        }
    }

    private void OnDestroy()
    {
        if (Instance == this)
        {
            Instance = null;
        }
    }
}