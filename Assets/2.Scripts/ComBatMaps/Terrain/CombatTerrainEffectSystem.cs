using UnityEngine;

public class CombatTerrainEffectSystem : MonoBehaviour
{
    public CombatUnitStats EnsureStats(CombatUnitToken token)
    {
        if (token == null) return null;

        CombatUnitStats stats = token.GetComponent<CombatUnitStats>();
        if (stats == null) stats = token.gameObject.AddComponent<CombatUnitStats>();
        return stats;
    }

    public TerrainEffectApplier EnsureApplier(CombatUnitToken token)
    {
        if (token == null) return null;

        TerrainEffectApplier applier = token.GetComponent<TerrainEffectApplier>();
        if (applier == null) applier = token.gameObject.AddComponent<TerrainEffectApplier>();
        return applier;
    }

    public void Apply(CombatGridData gridData, Vector2Int cell, CombatUnitToken token)
    {
        if (gridData == null || token == null) return;

        EnsureStats(token);
        TerrainEffectApplier applier = EnsureApplier(token);
        if (applier == null) return;

        string terrainName = gridData.Get(cell.x, cell.y).ToString();
        applier.ApplyFromCombatTileTypeName(terrainName);
    }
}
