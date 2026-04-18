using UnityEngine;

/// <summary>
/// 플레이 중 메타 XP·캐릭터 레벨·각인 포인트를 화면에 표시 (F9 토글).
/// </summary>
public class MetaXpDebugHUD : MonoBehaviour
{
    public KeyCode toggleKey = KeyCode.F9;

    private bool _visible = true;
    private GUIStyle _boxStyle;
    private GUIStyle _labelStyle;
    private float _unlockToastUntil;

    private void OnEnable()
    {
        EngraveAccountUnlock.OnMenuUnlocked += OnEngraveMenuUnlocked;
    }

    private void OnDisable()
    {
        EngraveAccountUnlock.OnMenuUnlocked -= OnEngraveMenuUnlocked;
    }

    private void OnEngraveMenuUnlocked()
    {
        _unlockToastUntil = Time.unscaledTime + 6f;
    }

    private void Update()
    {
        if (Input.GetKeyDown(toggleKey))
            _visible = !_visible;
    }

    private void OnGUI()
    {
        if (!_visible)
            return;

        EnsureStyles();

        var meta = MetaProgression.Instance;
        var ch = CharacterLevelProgression.Instance;
        var engrave = EngraveManager.Instance;

        float bank = meta != null ? meta.UnspentMetaXp : 0f;
        float k = meta != null ? meta.metaXpPerEngravePoint : 0f;
        int level = ch != null ? ch.Level : 0;
        int cxp = ch != null ? ch.CurrentXp : 0;
        int need = ch != null ? ch.XpToNext : 0;
        int metaPts = engrave != null ? engrave.AvailableMetaPoints : 0;
        float p = meta != null ? meta.GetProgressIndex() : 0f;
        float f = meta != null ? meta.EvaluateMultiplier(p) : 1f;
        bool engraveUnlocked = EngraveAccountUnlock.IsMenuUnlocked;

        string text =
            $"[Meta XP] bank: {bank:F1} / {k:F0} (next point)\n" +
            $"P={p:F1}  f(P)={f:F2}\n" +
            $"[Char] Lv{level}  XP {cxp}/{need}\n" +
            $"[Engrave] spendable pts: {metaPts}\n" +
            $"[Account] engrave menu: {(engraveUnlocked ? "UNLOCKED" : "locked until first Lv2")}\n" +
            $"{toggleKey} = toggle HUD";

        const float pad = 8f;
        var content = new GUIContent(text);
        Vector2 size = _labelStyle.CalcSize(content);
        size.x = Mathf.Min(Screen.width - pad * 2, size.x + pad * 2);
        size.y += pad * 2;

        float x = 10f;
        float y = 10f;
        GUI.Box(new Rect(x, y, size.x, size.y), GUIContent.none, _boxStyle);
        GUI.Label(new Rect(x + pad, y + pad, size.x - pad * 2, size.y), text, _labelStyle);

        if (Time.unscaledTime < _unlockToastUntil)
        {
            var toast = new GUIStyle(_labelStyle) { normal = { textColor = new Color(0.5f, 1f, 0.6f) }, fontSize = 16 };
            GUI.Label(new Rect(x, y + size.y + 6f, Screen.width - 20f, 40f),
                "각인 메뉴 해금됨 — 타이틀로 돌아가면 [각인] 버튼이 켜집니다.", toast);
        }
    }

    private void EnsureStyles()
    {
        if (_boxStyle != null)
            return;

        _boxStyle = new GUIStyle(GUI.skin.box)
        {
            normal = { background = MakeTex(2, 2, new Color(0f, 0f, 0f, 0.65f)) }
        };
        _labelStyle = new GUIStyle(GUI.skin.label)
        {
            fontSize = 14,
            alignment = TextAnchor.UpperLeft,
            normal = { textColor = Color.white }
        };
    }

    private static Texture2D MakeTex(int w, int h, Color col)
    {
        var t = new Texture2D(w, h);
        var pix = new Color[w * h];
        for (int i = 0; i < pix.Length; i++)
            pix[i] = col;
        t.SetPixels(pix);
        t.Apply();
        return t;
    }
}
