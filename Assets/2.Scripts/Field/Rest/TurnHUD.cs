using TMPro;
using UnityEngine;

public class TurnHUD : MonoBehaviour
{
    public FieldTimeManager fieldTime;
    public PlayerGridMover player;
    public PlayerCrouch crouch;
    public VisionVisualizer vision;
    public PlayerStats stats;
    public RestController rest;
    public TextMeshProUGUI text;

    private void Awake()
    {
        if (fieldTime == null) fieldTime = FieldTimeManager.Instance ?? FindObjectOfType<FieldTimeManager>();
        if (player == null) player = FindObjectOfType<PlayerGridMover>();
        if (crouch == null) crouch = FindObjectOfType<PlayerCrouch>();
        if (vision == null) vision = FindObjectOfType<VisionVisualizer>();
        if (stats == null) stats = FindObjectOfType<PlayerStats>();
        if (rest == null) rest = FindObjectOfType<RestController>();
    }

    private void Update()
    {
        if (text == null) return;

        string cellStr = player != null ? player.CurrentCell.ToString() : "-";
        int t = fieldTime != null ? fieldTime.time : 0;

        bool crouchOn = crouch != null && crouch.IsCrouching;

        int range = 0;
        var fvs = FieldVisionSystem.Instance;
        if (fvs != null)
            range = fvs.GetEffectivePlayerVisionRange();
        else if (vision != null)
            range = vision.baseVisionRange;

        int moveCost = 1;
        if (player != null)
            moveCost = player.GetMoveTimeCostForDisplay();

        string hpStr = stats != null ? $"{stats.hp}/{stats.maxHP}" : "-";
        string fatStr = stats != null ? $"{stats.fatigue}/{stats.maxFatigue}" : "-";
        string hunStr = stats != null ? $"{stats.hunger}/{stats.maxHunger}" : "-";

        bool resting = rest != null && rest.IsResting;
        bool confirm = rest != null && rest.IsConfirmPrompt;

        string prompt = "";
        if (confirm) prompt = "\n????? ????��?????? (R)";
        if (resting) prompt = "\n??? ??... (R?? ???)";

        text.text =
            $"Time: {t}\n" +
            $"Cell: {cellStr}\n" +
            $"Crouch: {(crouchOn ? "ON" : "OFF")}  |  Vision: {range} (MoveCost {moveCost})\n" +
            $"HP: {hpStr}   Fatigue: {fatStr}   Hunger: {hunStr}\n" +
            $"[WASD/Arrow] Move  |  [C] Crouch  |  [V] Vision  |  [R] Rest\n" +
            $"(5 Time???? Fatigue-1 / Hunger-2)" +
            prompt;
    }
}
