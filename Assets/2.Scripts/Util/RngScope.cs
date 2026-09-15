using System;
using UnityEngine;

/// <summary>
/// 맵 생성용 난수를 '격리된 구간'으로 묶는다.
///
/// 문제였던 것:
///   Random.InitState(seed) 로 전역 난수를 seed 한 뒤, 지형 생성이 끝나도 같은 전역 스트림을
///   적 배치·자원 배치·입구 개수가 이어서 소비했다. 그래서 맵 재현이 시드가 아니라
///   "Random 이 몇 번, 어떤 순서로 호출됐는가"에 의존했다.
///   → 기능을 하나 추가해 난수 소비가 한 번만 늘어도 같은 시드가 다른 맵을 만들고,
///     세이브는 시드만 들고 있으므로 기존 세이브가 조용히 깨진다.
///
/// 해결:
///   1) 구간 진입 시 전역 난수 상태를 저장하고, 나갈 때 되돌린다.
///      → 생성이 바깥 난수를 오염시키지 않고, 바깥이 생성을 흔들지도 못한다.
///   2) 구간마다 시드에서 파생된 '다른' 시드를 쓴다.
///      → 지형 쪽 난수 소비가 늘어도 적/자원 배치 결과가 바뀌지 않는다.
///
/// 맵 생성은 전부 동기 코드라(코루틴 없음) 구간 중간에 다른 시스템이 끼어들 수 없다.
///
/// 사용:
///   using (new RngScope(seed, "terrain")) { ...지형 생성... }
///   using (new RngScope(seed, "spawn"))   { ...적·자원 배치... }
/// </summary>
public readonly struct RngScope : IDisposable
{
    private readonly UnityEngine.Random.State _saved;

    public RngScope(int seed, string phase)
    {
        _saved = UnityEngine.Random.state;
        UnityEngine.Random.InitState(Derive(seed, phase));
    }

    public RngScope(int derivedSeed)
    {
        _saved = UnityEngine.Random.state;
        UnityEngine.Random.InitState(derivedSeed);
    }

    public void Dispose()
    {
        UnityEngine.Random.state = _saved;
    }

    /// <summary>
    /// 시드 + 구간 이름으로 안정적인 파생 시드를 만든다.
    ///
    /// string.GetHashCode() 는 런타임/버전에 따라 값이 달라질 수 있어 쓰지 않는다.
    /// 세이브 재현성이 걸린 값이므로 직접 구현한 FNV-1a 로 고정한다.
    /// </summary>
    public static int Derive(int seed, string phase)
    {
        unchecked
        {
            const uint FnvOffset = 2166136261;
            const uint FnvPrime = 16777619;

            uint hash = FnvOffset;

            // 시드를 바이트 단위로 먼저 섞는다.
            uint s = (uint)seed;
            for (int i = 0; i < 4; i++)
            {
                hash ^= (s >> (i * 8)) & 0xFF;
                hash *= FnvPrime;
            }

            if (!string.IsNullOrEmpty(phase))
            {
                for (int i = 0; i < phase.Length; i++)
                {
                    hash ^= phase[i];
                    hash *= FnvPrime;
                }
            }

            // Random.InitState 는 int 를 받는다. 0 은 '시드 없음'으로 취급되는 자리가 있어 피한다.
            int result = (int)(hash & 0x7FFFFFFF);
            return result == 0 ? 1 : result;
        }
    }
}
