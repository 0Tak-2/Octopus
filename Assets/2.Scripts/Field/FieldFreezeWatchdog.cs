using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// FieldFreezeController.Apply 직후 N프레임 동안 적의 위치가 또 움직이면 로그로 알려준다.
/// 어떤 스크립트가 덮어쓰는지 빠르게 식별하기 위한 일회성 감시기.
/// </summary>
public class FieldFreezeWatchdog : MonoBehaviour
{
    private static FieldFreezeWatchdog _instance;

    public static FieldFreezeWatchdog Ensure()
    {
        if (_instance != null) return _instance;
        var go = new GameObject("[FieldFreezeWatchdog]");
        DontDestroyOnLoad(go);
        _instance = go.AddComponent<FieldFreezeWatchdog>();
        return _instance;
    }

    public void StartWatch(List<(EnemyInstance e, Vector3 target)> applied, int frames)
    {
        if (applied == null || applied.Count == 0) return;
        StopAllCoroutines();
        StartCoroutine(Watch(applied, frames));
    }

    private IEnumerator Watch(List<(EnemyInstance e, Vector3 target)> applied, int frames)
    {
        for (int i = 0; i < frames; i++)
        {
            // Update 끝 시점에 검사
            yield return null;
            for (int k = 0; k < applied.Count; k++)
            {
                var pair = applied[k];
                if (pair.e == null) continue;
                Vector3 cur = pair.e.transform.position;
                if (Vector3.SqrMagnitude(cur - pair.target) > 0.01f)
                {
                    Debug.LogWarning($"[FieldFreeze][Watch] frame+{i + 1} '{pair.e.gameObject.name}' 위치가 어긋남: target={pair.target} cur={cur}");
                }
            }

            // LateUpdate 이후 한 번 더
            yield return new WaitForEndOfFrame();
            for (int k = 0; k < applied.Count; k++)
            {
                var pair = applied[k];
                if (pair.e == null) continue;
                Vector3 cur = pair.e.transform.position;
                if (Vector3.SqrMagnitude(cur - pair.target) > 0.01f)
                {
                    Debug.LogWarning($"[FieldFreeze][Watch] EoF+{i + 1} '{pair.e.gameObject.name}' 위치가 어긋남: target={pair.target} cur={cur}");
                }
            }
        }
    }
}
