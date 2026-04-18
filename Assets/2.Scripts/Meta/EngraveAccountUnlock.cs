using System;
using UnityEngine;

/// <summary>
/// 계정 단위: 첫 캐릭터 Lv2 도달 시 각인 메뉴(타이틀·인게임)를 연다.
/// </summary>
public static class EngraveAccountUnlock
{
    private const string KeyMenuUnlocked = "Account_EngraveMenuUnlocked";

    /// <summary>해금 직후 타이틀 등 UI가 다시 스스로 갱신하도록</summary>
    public static event Action OnMenuUnlocked;

    public static bool IsMenuUnlocked => PlayerPrefs.GetInt(KeyMenuUnlocked, 0) == 1;

    public static void UnlockMenuFromFirstLevelUp()
    {
        if (PlayerPrefs.GetInt(KeyMenuUnlocked, 0) == 1)
            return;

        PlayerPrefs.SetInt(KeyMenuUnlocked, 1);
        PlayerPrefs.Save();
        OnMenuUnlocked?.Invoke();
    }
}
