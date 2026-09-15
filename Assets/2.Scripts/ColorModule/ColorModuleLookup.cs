using UnityEngine;

/// <summary>
/// 이름으로 ColorModuleDefinition 을 되찾는다.
///
/// 모듈 에셋은 Resources 폴더 밖에 있어서 런타임 직접 로드가 안 된다.
/// 세이브에는 에셋 이름만 남기고, 복원할 때 이미 씬에 있는 참조 목록에서 찾는다.
/// </summary>
public static class ColorModuleLookup
{
    public static ColorModuleDefinition FindByName(string assetName)
    {
        if (string.IsNullOrEmpty(assetName)) return null;

        // 1) ItemDatabase — 모듈은 아이템으로도 등록돼 있다.
        var db = ItemDatabase.Instance;
        if (db != null && db.itemDataList != null)
        {
            foreach (var data in db.itemDataList)
            {
                if (data != null && data.colorModuleDefinition != null &&
                    data.colorModuleDefinition.name == assetName)
                    return data.colorModuleDefinition;
            }
        }

        // 2) 드랍 풀 — 아이템으로 등록되지 않은 모듈도 여기엔 있다.
        var dropper = ColorModuleDropper.Instance;
        if (dropper != null)
        {
            var found = FindInPool(dropper.redModulePool, assetName)
                        ?? FindInPool(dropper.blueModulePool, assetName)
                        ?? FindInPool(dropper.blackModulePool, assetName);
            if (found != null) return found;
        }

        Debug.LogWarning($"[ColorModuleLookup] 모듈을 찾을 수 없습니다: {assetName} " +
                         "(ItemDatabase 또는 ColorModuleDropper 풀에 등록되어 있어야 복원됩니다)");
        return null;
    }

    private static ColorModuleDefinition FindInPool(
        System.Collections.Generic.List<ColorModuleDefinition> pool, string assetName)
    {
        if (pool == null) return null;

        foreach (var def in pool)
            if (def != null && def.name == assetName)
                return def;

        return null;
    }
}
