using UnityEngine;

public class TerrainEffectApplier : MonoBehaviour
{
    private CombatUnitStats _stats;

    public string CurrentTerrainName { get; private set; } = "Empty";

    private void Awake()
    {
        _stats = GetComponent<CombatUnitStats>();
        if (_stats == null)
            Debug.LogError($"{name}: CombatUnitStats가 필요합니다.");
    }

    public void ApplyFromCombatTileTypeName(string combatTileTypeName)
    {
        if (_stats == null) return;

        if (string.IsNullOrEmpty(combatTileTypeName))
            combatTileTypeName = "Empty";

        if (CurrentTerrainName == combatTileTypeName) return;

        CurrentTerrainName = combatTileTypeName;

        _stats.ClearTerrainBonuses();

        // CombatTileType enum 멤버 이름을 문자열로 매칭(컴파일 안전)
        switch (combatTileTypeName)
        {
            case "Seaweed":
                _stats.evasionBonus = 0.40f;
                break;

            case "Hill":
                _stats.rangeBonus = 1;
                break;

            case "Current":
                _stats.freeMovesBonus = 1;
                break;

            // Wall/Empty/기타는 보너스 없음
            default:
                break;
        }
    }
}
