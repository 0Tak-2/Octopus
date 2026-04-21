using UnityEngine;

/// <summary>
/// 던전에서 필드로 돌아올 때: 필드 적 AI가 가진 GridBoard 참조가 던전 격자를 가리키지 않도록
/// 필드 격자로 재바인딩만 한다. 위치·점유 복원은 <see cref="FieldFreezeController"/> 가 담당한다.
/// </summary>
public static class FieldDungeonReturnSync
{
    public static void Apply(GridBoard fieldGrid, GameObject fieldRoot)
    {
        if (fieldGrid == null || fieldRoot == null) return;

        var multi = fieldRoot.GetComponentInChildren<FieldMultiEnemyAttack>(true);
        if (multi != null)
            multi.gridBoard = fieldGrid;

        foreach (var ai in fieldRoot.GetComponentsInChildren<FieldEnemyWanderChase>(true))
            ai.gridBoard = fieldGrid;

        foreach (var ai in fieldRoot.GetComponentsInChildren<FieldEnemyNeutral>(true))
            ai.gridBoard = fieldGrid;

        foreach (var ai in fieldRoot.GetComponentsInChildren<FieldEnemyPassive>(true))
            ai.gridBoard = fieldGrid;
    }
}
