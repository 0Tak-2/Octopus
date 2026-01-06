using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 필드에서 스킬(AP2/3)을 "쿨다운(턴 기반)"으로 처리하기 위한 최소 구현.
/// - UseSkill(def, cooldownTurns): 사용 시 해당 스킬에 쿨다운 부여
/// - TickDown(): "내 턴이 지나갈 때" 1씩 감소
/// </summary>
public class FieldSkillCooldownTracker : MonoBehaviour
{
    // key: ScriptableObject instance id (CombatAttackDefinition 등)
    private readonly Dictionary<int, int> cooldowns = new Dictionary<int, int>();

    public bool IsUsable(Object skillDef)
    {
        if (skillDef == null) return false;
        int key = skillDef.GetInstanceID();
        return !cooldowns.TryGetValue(key, out int left) || left <= 0;
    }

    public int GetRemaining(Object skillDef)
    {
        if (skillDef == null) return 0;
        int key = skillDef.GetInstanceID();
        return cooldowns.TryGetValue(key, out int left) ? Mathf.Max(0, left) : 0;
    }

    public void UseSkill(Object skillDef, int cooldownTurns)
    {
        if (skillDef == null) return;
        int key = skillDef.GetInstanceID();
        cooldowns[key] = Mathf.Max(0, cooldownTurns);
    }

    /// <summary>
    /// 보통 "플레이어 턴이 끝날 때" 호출해서 내 턴 경과를 반영.
    /// (원하면 적도 별도 트래커를 두면 됨)
    /// </summary>
    public void TickDown()
    {
        if (cooldowns.Count == 0) return;

        // 키 목록을 복사해서 안전하게 감소
        var keys = new List<int>(cooldowns.Keys);
        foreach (var k in keys)
        {
            cooldowns[k] = Mathf.Max(0, cooldowns[k] - 1);
        }
    }
}
