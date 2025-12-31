using System.Collections;
using UnityEngine;
using UnityEngine.UI;

public class ScreenFader : MonoBehaviour
{
    [Header("Refs")]
    public CanvasGroup canvasGroup;
    public Image fadeImage;

    [Header("Config")]
    public float defaultFadeDuration = 0.25f;

    private Coroutine fadeCo;

    private void Awake()
    {
        if (canvasGroup == null)
            canvasGroup = GetComponentInChildren<CanvasGroup>(true);

        if (fadeImage == null)
            fadeImage = GetComponentInChildren<Image>(true);

        // 🔥 핵심 1: Image 색 알파를 무조건 1로 강제
        if (fadeImage != null)
        {
            Color c = fadeImage.color;
            c.a = 1f;                // ← 여기 중요
            fadeImage.color = c;
        }

        // 🔥 핵심 2: 시작은 항상 투명 (검정 화면 방지)
        ForceClear();
    }

    /// <summary>
    /// 씬 시작 / 리셋용 (항상 투명)
    /// </summary>
    public void ForceClear()
    {
        if (fadeCo != null)
        {
            StopCoroutine(fadeCo);
            fadeCo = null;
        }

        canvasGroup.alpha = 0f;
        canvasGroup.blocksRaycasts = false;
        canvasGroup.interactable = false;
    }

    public IEnumerator FadeOutIn(System.Action midAction, float fadeOut, float fadeIn)
    {
        yield return FadeTo(1f, fadeOut);
        midAction?.Invoke();
        yield return FadeTo(0f, fadeIn);
    }

    public IEnumerator FadeTo(float targetAlpha, float duration)
    {
        if (fadeCo != null)
            StopCoroutine(fadeCo);

        bool done = false;
        fadeCo = StartCoroutine(FadeRoutine(targetAlpha, duration, () => done = true));

        while (!done)
            yield return null;

        fadeCo = null;
    }

    private IEnumerator FadeRoutine(float targetAlpha, float duration, System.Action onDone)
    {
        duration = Mathf.Max(0.0001f, duration);

        float start = canvasGroup.alpha;
        float t = 0f;

        canvasGroup.blocksRaycasts = true;

        while (t < 1f)
        {
            t += Time.unscaledDeltaTime / duration;
            canvasGroup.alpha = Mathf.Lerp(start, targetAlpha, t);
            yield return null;
        }

        canvasGroup.alpha = targetAlpha;
        canvasGroup.blocksRaycasts = targetAlpha > 0.01f;

        onDone?.Invoke();
    }
}
