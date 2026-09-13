using System;

/// <summary>타이틀 캐릭터(슬롯) 목록 표시용 요약.</summary>
[Serializable]
public class RunSlotSummary
{
    public int slot;
    public bool isEmpty;
    public string characterName;
    public long savedUnixTime;
    public int currentChapter;
    public int currentMapIndex;

    public string DisplayName =>
        isEmpty ? "빈 슬롯" : (string.IsNullOrWhiteSpace(characterName) ? $"캐릭터 {slot}" : characterName);

    public string ProgressText =>
        isEmpty ? "" : $"Ch.{currentChapter} · Map {currentMapIndex + 1}";

    public string SavedAtText
    {
        get
        {
            if (isEmpty || savedUnixTime <= 0) return "";
            var dt = DateTimeOffset.FromUnixTimeSeconds(savedUnixTime).ToLocalTime();
            return dt.ToString("yyyy-MM-dd HH:mm");
        }
    }
}
