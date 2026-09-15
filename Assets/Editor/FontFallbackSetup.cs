using System.Collections.Generic;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.TextCore.LowLevel;

/// <summary>
/// Pretendard SDF 는 정적(Static) 아틀라스라 구워둔 11,646자 밖의 글자는 영영 못 그린다.
/// 그래서 '·'(U+00B7) 같은 기호나, 한글 IME 조합 중 나오는 호환 자모(ㄱ ㄴ ㅇ, U+3131~U+318E)가
/// □ 로 표시되고 매 프레임 경고가 쏟아진다.
///
/// 해결: 같은 Pretendard 원본 폰트로 '동적(Dynamic)' 폰트 에셋을 만들어 TMP 폴백에 등록한다.
/// 기존 정적 아틀라스는 그대로 두므로 이미 구워진 한글은 빠르게 나오고,
/// 빠진 글자만 런타임에 원본 OTF 에서 채워 넣는다.
/// </summary>
public static class FontFallbackSetup
{
    private const string SourceFontPath = "Assets/Font/Pretendard-1.3.9/public/static/Pretendard-Regular.otf";
    private const string OutputPath = "Assets/Font/Pretendard-Dynamic-Fallback SDF.asset";

    [MenuItem("Octopus/Setup/Create Dynamic Font Fallback")]
    public static void Run()
    {
        var fallback = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(OutputPath);

        if (fallback == null)
        {
            var source = AssetDatabase.LoadAssetAtPath<Font>(SourceFontPath);
            if (source == null)
            {
                Debug.LogError($"[FontFallbackSetup] 원본 폰트를 찾을 수 없다: {SourceFontPath}");
                return;
            }

            // 동적 모드: 아틀라스는 작게 시작하고 필요한 글자가 나올 때마다 채워진다.
            fallback = TMP_FontAsset.CreateFontAsset(
                source,
                90,                                  // sampling point size
                9,                                   // atlas padding
                GlyphRenderMode.SDFAA,
                1024, 1024,                          // 시작 아틀라스 크기
                AtlasPopulationMode.Dynamic,
                enableMultiAtlasSupport: true);

            if (fallback == null)
            {
                Debug.LogError("[FontFallbackSetup] 동적 폰트 에셋 생성 실패.");
                return;
            }

            fallback.name = "Pretendard-Dynamic-Fallback SDF";
            AssetDatabase.CreateAsset(fallback, OutputPath);

            // CreateFontAsset 이 만든 아틀라스 텍스처와 머티리얼을 에셋 하위에 같이 저장해야
            // 재생 후에도 참조가 유지된다.
            if (fallback.atlasTextures != null)
            {
                foreach (var tex in fallback.atlasTextures)
                {
                    if (tex != null && !AssetDatabase.Contains(tex))
                        AssetDatabase.AddObjectToAsset(tex, fallback);
                }
            }

            if (fallback.material != null && !AssetDatabase.Contains(fallback.material))
                AssetDatabase.AddObjectToAsset(fallback.material, fallback);

            AssetDatabase.SaveAssets();
            Debug.Log($"[FontFallbackSetup] 동적 폴백 폰트 생성: {OutputPath}");
        }
        else
        {
            Debug.Log("[FontFallbackSetup] 폴백 폰트가 이미 있다. 등록만 확인한다.");
        }

        RegisterInTmpSettings(fallback);
    }

    private static void RegisterInTmpSettings(TMP_FontAsset fallback)
    {
        var settings = Resources.Load<TMP_Settings>("TMP Settings");
        if (settings == null)
        {
            Debug.LogError("[FontFallbackSetup] TMP Settings 를 찾을 수 없다.");
            return;
        }

        var so = new SerializedObject(settings);
        var prop = so.FindProperty("m_fallbackFontAssets");
        if (prop == null)
        {
            Debug.LogError("[FontFallbackSetup] m_fallbackFontAssets 프로퍼티를 찾을 수 없다.");
            return;
        }

        for (int i = 0; i < prop.arraySize; i++)
        {
            if (prop.GetArrayElementAtIndex(i).objectReferenceValue == fallback)
            {
                Debug.Log("[FontFallbackSetup] 이미 폴백에 등록돼 있다.");
                return;
            }
        }

        int index = prop.arraySize;
        prop.InsertArrayElementAtIndex(index);
        prop.GetArrayElementAtIndex(index).objectReferenceValue = fallback;
        so.ApplyModifiedPropertiesWithoutUndo();

        EditorUtility.SetDirty(settings);
        AssetDatabase.SaveAssets();

        Debug.Log($"[FontFallbackSetup] TMP Settings 폴백에 등록 완료 (총 {prop.arraySize}개).");
    }
}
