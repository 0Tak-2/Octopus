using System.Collections;
using UnityEngine;
using TMPro;

public class CombatVfxController : MonoBehaviour
{
    [Header("Popup")]
    public TMP_Text popupPrefab;
    public Vector2 popupOffset = new Vector2(0f, 34f);
    public float popupDuration = 0.6f;

    [Header("Step Move")]
    public float stepMoveDuration = 0.10f;

    [Header("Enemy Dash Motion")]
    public float enemyDashGoTime = 0.07f;
    public float enemyDashPauseTime = 0.03f;
    public float enemyDashBackTime = 0.08f;

    // ????
    private CombatBoardUI _boardUI;

    public void Bind(CombatBoardUI boardUI)
    {
        _boardUI = boardUI;
    }

    public void ShowPopup(CombatUnitToken token, string text)
    {
        if (token == null) return;
        if (popupPrefab == null) return;

        TMP_Text inst = Instantiate(popupPrefab, token.transform);
        inst.name = "CombatPopupTMP";
        inst.text = text;
        inst.gameObject.SetActive(true);

        RectTransform rt = inst.rectTransform;
        rt.anchorMin = new Vector2(0.5f, 1f);
        rt.anchorMax = new Vector2(0.5f, 1f);
        rt.pivot = new Vector2(0.5f, 0f);
        rt.anchoredPosition = popupOffset;
        rt.localScale = Vector3.one;

        StartCoroutine(DestroyAfter(inst, popupDuration));
    }

    private IEnumerator DestroyAfter(TMP_Text inst, float t)
    {
        if (inst == null) yield break;
        yield return new WaitForSeconds(Mathf.Max(0.05f, t));
        if (inst != null) Destroy(inst.gameObject);
    }

    /// <summary>
    /// ????? ???? ? ?????? "?????" ???(Lerp) + ??? ?? ???? ????(??? ?? ?? ????)
    /// ????? ?????? ?????? MoveExistingToken???? ????????? ??.
    /// </summary>
    public IEnumerator StepMoveToCell(CombatUnitToken token, Vector2Int cell, float durationOverride = -1f)
    {
        if (_boardUI == null || token == null) yield break;

        RectTransform rt = token.GetComponent<RectTransform>();
        if (rt == null) yield break;

        float dur = (durationOverride >= 0f) ? durationOverride : stepMoveDuration;

        // ??? ??? ???? ????(overlay)
        if (_boardUI.boardRoot != null)
        {
            rt.SetParent(_boardUI.boardRoot, worldPositionStays: true);
            rt.SetAsLastSibling();
        }

        Vector3 start = rt.position;
        Vector3 end = _boardUI.GetTileWorldCenter(cell.x, cell.y);

        if (dur <= 0f)
        {
            rt.position = end;
            yield break;
        }

        float t = 0f;
        while (t < dur)
        {
            if (rt == null) yield break;
            t += Time.deltaTime;
            float u = Mathf.Clamp01(t / dur);
            rt.position = Vector3.Lerp(start, end, u);
            yield return null;
        }
        if (rt == null) yield break;
        rt.position = end;
    }

    public IEnumerator DashHitReturn(
        CombatUnitToken attacker,
        Vector2Int targetCell,
        System.Action onImpact,
        float goTime,
        float pauseTime,
        float backTime)
    {
        if (_boardUI == null) yield break;
        if (attacker == null) yield break;

        RectTransform rt = attacker.GetComponent<RectTransform>();
        if (rt == null) yield break;

        Transform originalParent = rt.parent;

        Vector3 start = rt.position;
        Vector3 end = _boardUI.GetTileWorldCenter(targetCell.x, targetCell.y);

        // ??? ??? ????
        if (_boardUI.boardRoot != null)
        {
            rt.SetParent(_boardUI.boardRoot, worldPositionStays: true);
            rt.SetAsLastSibling();
        }

        yield return LerpWorldPos(rt, start, end, goTime);

        onImpact?.Invoke();
        yield return new WaitForSeconds(pauseTime);

        yield return LerpWorldPos(rt, end, start, backTime);

        // ???? ?? ????? ?????? MoveExistingToken???? "????"????? ?? ??????????,
        // ??? ???? ????? ???? ???? ????? ????.
        if (rt != null && originalParent != null)
            rt.SetParent(originalParent, worldPositionStays: true);
    }

    public IEnumerator EnemyDashHitReturn(CombatUnitToken attacker, Vector2Int targetCell, System.Action onImpact)
        => DashHitReturn(attacker, targetCell, onImpact, enemyDashGoTime, enemyDashPauseTime, enemyDashBackTime);

    public IEnumerator HitPulse(CombatUnitToken token)
    {
        if (token == null) yield break;
        RectTransform rt = token.GetComponent<RectTransform>();
        if (rt == null) yield break;

        Vector3 baseScale = rt.localScale;
        rt.localScale = baseScale * 1.15f;
        yield return new WaitForSeconds(0.06f);
        // ????? ???? ?? ????(RemoveToken)??? RectTransform?? ?©¥??? ?? ??????? ?¢¥?.
        if (rt == null) yield break;
        rt.localScale = baseScale;
    }

    private IEnumerator LerpWorldPos(RectTransform rt, Vector3 a, Vector3 b, float dur)
    {
        if (rt == null) yield break;

        if (dur <= 0f)
        {
            rt.position = b;
            yield break;
        }

        float t = 0f;
        while (t < dur)
        {
            if (rt == null) yield break;
            t += Time.deltaTime;
            float u = Mathf.Clamp01(t / dur);
            rt.position = Vector3.LerpUnclamped(a, b, u);
            yield return null;
        }
        if (rt == null) yield break;
        rt.position = b;
    }
}
