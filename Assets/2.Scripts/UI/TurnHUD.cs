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

        int baseRange = vision != null ? vision.baseVisionRange : 0;
        int bonus = (vision != null && crouchOn) ? vision.crouchBonusRange : 0;
        int range = baseRange + bonus;

        int moveCost = 1;
        if (player != null)
            moveCost = player.baseMoveTimeCost + (crouchOn ? player.crouchExtraTimeCost : 0);

        string hpStr = stats != null ? $"{stats.hp}/{stats.maxHP}" : "-";
        string fatStr = stats != null ? $"{stats.fatigue}/{stats.maxFatigue}" : "-";
        string hunStr = stats != null ? $"{stats.hunger}/{stats.maxHunger}" : "-";

        bool resting = rest != null && rest.IsResting;
        bool confirm = rest != null && rest.IsConfirmPrompt;

        string prompt = "";
        if (confirm) prompt = "\n휴식을 취하시겠습니까? (R)";
        if (resting) prompt = "\n휴식 중... (R로 중단)";

        text.text =
            $"Time: {t}\n" +
            $"Cell: {cellStr}\n" +
            $"Crouch: {(crouchOn ? "ON" : "OFF")}  |  Vision: {range} (MoveCost {moveCost})\n" +
            $"HP: {hpStr}   Fatigue: {fatStr}   Hunger: {hunStr}\n" +
            $"[WASD/Arrow] Move  |  [C] Crouch  |  [V] Vision  |  [R] Rest\n" +
            $"(5 Time마다 Fatigue-1 / Hunger-2)" +
            prompt;
    }
}
