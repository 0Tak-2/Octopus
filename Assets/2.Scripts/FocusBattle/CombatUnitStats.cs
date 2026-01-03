using UnityEngine;

public class CombatUnitStats : MonoBehaviour
{
    [Header("Base")]
    public int baseFreeMovesPerTurn = 1;   // 기존: 턴당 무료 이동 1회
    [Range(0f, 1f)]
    public float baseEvasion = 0f;         // 기본 회피율(0~1)

    [Header("Terrain Bonuses (runtime)")]
    public int freeMovesBonus = 0;         // 해류: +1
    public int rangeBonus = 0;             // 언덕: +1
    public float evasionBonus = 0f;        // 해초: +0.40

    public int FreeMovesPerTurn => Mathf.Max(0, baseFreeMovesPerTurn + freeMovesBonus);
    public float Evasion => Mathf.Clamp01(baseEvasion + evasionBonus);

    public void ClearTerrainBonuses()
    {
        freeMovesBonus = 0;
        rangeBonus = 0;
        evasionBonus = 0f;
    }
}
