using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class CombatHUDController : MonoBehaviour
{
    [Header("Sprites (optional)")]
    public Sprite apFilledSprite;
    public Sprite apEmptySprite;

    [Header("If sprites are empty, use color instead")]
    [Range(0f, 1f)] public float emptyAlpha = 0.25f;

    [Header("Player UI")]
    public TMP_Text playerHPText;
    public Image[] playerAPDots;          // 0~maxAP-1 (보통 5개)
    public BuffIconGroup playerBuffs;

    [Header("Enemy UI")]
    public TMP_Text enemyHPText;
    public BuffIconGroup enemyBuffs;

    private FocusedCombatManager _combat;

    public void Bind(FocusedCombatManager combat)
    {
        _combat = combat;
        RefreshAll();
    }

    public void RefreshAll()
    {
        if (_combat == null) return;
        RefreshPlayer();
        RefreshEnemy();
    }

    private void RefreshPlayer()
    {
        var s = _combat.State;

        if (playerHPText != null)
            playerHPText.text = $"HP  {s.playerHP}";

        UpdateAPDots(playerAPDots, s.currentAP, s.maxAP);

        if (playerBuffs != null)
            playerBuffs.RefreshFromToken(_combat.PlayerToken);
    }

    private void RefreshEnemy()
    {
        var s = _combat.State;

        if (enemyHPText != null)
            enemyHPText.text = $"HP  {s.enemyHP}";

        if (enemyBuffs != null)
            enemyBuffs.RefreshFromToken(_combat.EnemyToken);
    }

    private void UpdateAPDots(Image[] dots, int currentAP, int maxAP)
    {
        if (dots == null || dots.Length == 0) return;

        int usable = Mathf.Min(dots.Length, Mathf.Max(0, maxAP));
        int cur = Mathf.Clamp(currentAP, 0, usable);

        for (int i = 0; i < dots.Length; i++)
        {
            if (dots[i] == null) continue;

            bool inUse = i < usable;
            dots[i].gameObject.SetActive(inUse);
            if (!inUse) continue;

            bool filled = i < cur;

            // ✅ 스프라이트가 있으면 스프라이트로
            if (apFilledSprite != null && apEmptySprite != null)
            {
                dots[i].sprite = filled ? apFilledSprite : apEmptySprite;

                // 색은 원래대로(알파 1)
                Color c = dots[i].color;
                c.a = 1f;
                dots[i].color = c;
            }
            else
            {
                // ✅ 스프라이트가 없으면 알파로 표시
                Color c = dots[i].color;
                c.a = filled ? 1f : emptyAlpha;
                dots[i].color = c;
            }
        }
    }
}
