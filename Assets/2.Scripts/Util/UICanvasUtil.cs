using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// 런타임 UI를 붙일 캔버스를 찾을 때 쓰는 헬퍼.
///
/// FindObjectOfType&lt;Canvas&gt;() 를 그냥 쓰면 DontDestroyOnLoad 로 살아남은 캔버스가 잡힐 수 있다.
/// 그 아래에 UI를 붙이면:
///   - 씬을 옮겨도 그 UI가 고아로 살아남아 다음 씬의 클릭을 전부 삼키고
///   - 그 캔버스의 CanvasGroup(alpha 0 등) 설정을 그대로 물려받아 안 보이거나 안 눌린다.
/// 그래서 '현재 씬에 속한' 캔버스만 고른다.
/// </summary>
public static class UICanvasUtil
{
    /// <summary>현재 활성 씬에 속한 캔버스를 찾는다. 없으면 null.</summary>
    public static Canvas FindCanvasInActiveScene()
    {
        Scene active = SceneManager.GetActiveScene();

        var canvases = Object.FindObjectsOfType<Canvas>();
        foreach (var canvas in canvases)
        {
            if (canvas == null) continue;
            // DontDestroyOnLoad 오브젝트는 전용 씬에 있으므로 이 비교에서 걸러진다.
            if (canvas.gameObject.scene == active)
                return canvas;
        }

        return null;
    }
}
