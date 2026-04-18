using UnityEngine;

/// <summary>
/// 플레이 모드 진입 시 메타 시스템 오브젝트를 한 번 생성합니다.
/// 씬에 수동 배치한 경우 중복 생성하지 않습니다.
/// </summary>
public static class MetaSystemsBootstrapper
{
    private const string RootName = "[MetaSystems]";

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void EnsureMetaSystems()
    {
        if (!Application.isPlaying)
            return;

        if (Object.FindObjectOfType<MetaProgression>() != null)
            return;

        var go = new GameObject(RootName);
        Object.DontDestroyOnLoad(go);
        go.AddComponent<MetaProgression>();
        go.AddComponent<CharacterLevelProgression>();
        go.AddComponent<MetaXpDebugHUD>();
    }
}
