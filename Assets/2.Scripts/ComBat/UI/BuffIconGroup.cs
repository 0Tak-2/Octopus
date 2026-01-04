using UnityEngine;
using TMPro;

public class BuffIconGroup : MonoBehaviour
{
    [Header("Icons")]
    public GameObject freeMoveIcon;   // 무료이동 +1
    public GameObject rangeIcon;      // 사거리 +1
    public GameObject evasionIcon;    // 회피율 +

    [Header("Optional Labels (없어도 됨)")]
    public TMP_Text freeMoveText;
    public TMP_Text rangeText;
    public TMP_Text evasionText;

    public void RefreshFromToken(CombatUnitToken token)
    {
        if (token == null)
        {
            SetAll(false);
            return;
        }

        CombatUnitStats s = token.GetComponent<CombatUnitStats>();
        if (s == null)
        {
            // 토큰에 CombatUnitStats가 없다면 표시 못 함
            SetAll(false);
            return;
        }

        // ✅ 너 프로젝트 기준:
        // - 해류: FreeMovesPerTurn이 1보다 커짐
        // - 언덕: rangeBonus가 0보다 커짐
        // - 해초: Evasion이 0보다 커짐
        bool hasFree = s.FreeMovesPerTurn > 1;
        bool hasRange = s.rangeBonus > 0;
        bool hasEva = s.Evasion > 0f;

        if (freeMoveIcon != null) freeMoveIcon.SetActive(hasFree);
        if (rangeIcon != null) rangeIcon.SetActive(hasRange);
        if (evasionIcon != null) evasionIcon.SetActive(hasEva);

        // 라벨은 있으면 간단 표기(원하면 제거 가능)
        if (freeMoveText != null) freeMoveText.text = hasFree ? "+1" : "";
        if (rangeText != null) rangeText.text = hasRange ? $"+{s.rangeBonus}" : "";
        if (evasionText != null) evasionText.text = hasEva ? "+" : "";
    }

    private void SetAll(bool on)
    {
        if (freeMoveIcon != null) freeMoveIcon.SetActive(on);
        if (rangeIcon != null) rangeIcon.SetActive(on);
        if (evasionIcon != null) evasionIcon.SetActive(on);

        if (freeMoveText != null) freeMoveText.text = "";
        if (rangeText != null) rangeText.text = "";
        if (evasionText != null) evasionText.text = "";
    }
}
