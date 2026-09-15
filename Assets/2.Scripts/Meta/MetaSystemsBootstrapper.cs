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

        var meta = Object.FindObjectOfType<MetaProgression>();
        if (meta == null)
        {
            var go = new GameObject(RootName);
            Object.DontDestroyOnLoad(go);
            meta = go.AddComponent<MetaProgression>();
        }

        var host = meta.gameObject;
        if (host.GetComponent<CharacterLevelProgression>() == null)
            host.AddComponent<CharacterLevelProgression>();

        // 메타 XP·각인 시스템을 걷어내면서 디버그 HUD 도 더 이상 붙이지 않는다.
        // (화면 좌상단에 항상 떠 있던 [Meta XP]/[Engrave] 표시)
        // EngraveEffectRuntime 은 PlayerStats 가 참조하므로 남겨두되,
        // BuildStatsFromInvestments 가 전부 0을 돌려주어 효과는 없다.
        if (host.GetComponent<EngraveEffectRuntime>() == null)
            host.AddComponent<EngraveEffectRuntime>();
    }
}
