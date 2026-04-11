using System.Collections;
using UnityEngine;

[DefaultExecutionOrder(-100)]
public class PlayerCrouch : MonoBehaviour
{
    [Header("Toggle")]
    public KeyCode toggleKey = KeyCode.C;
    [Tooltip("C가 다른 스크립트/IME와 겹치면 사용. None이면 비활성.")]
    public KeyCode alternateToggleKey = KeyCode.LeftShift;

    public bool IsCrouching { get; private set; }

    public event System.Action<bool> OnCrouchChanged;

    private RestController _rest;
    private FieldTimeManager _fieldTime;
    private FieldMultiEnemyAttack _multiEnemy;

    private void Awake()
    {
        _rest = GetComponent<RestController>()
            ?? GetComponentInParent<RestController>()
            ?? FindObjectOfType<RestController>();
        _fieldTime = FieldTimeManager.Instance ?? FindObjectOfType<FieldTimeManager>();
        _multiEnemy = FindObjectOfType<FieldMultiEnemyAttack>();
    }

    private void Update()
    {
        if (_rest != null && _rest.IsResting)
            return;

        bool pressed = Input.GetKeyDown(toggleKey)
            || (alternateToggleKey != KeyCode.None && Input.GetKeyDown(alternateToggleKey));

        if (!pressed) return;

        bool enteringCrouch = !IsCrouching;
        IsCrouching = enteringCrouch;

        Debug.Log($"[PlayerCrouch] IsCrouching = {IsCrouching}");

        OnCrouchChanged?.Invoke(IsCrouching);

        if (enteringCrouch)
        {
            if (_fieldTime == null)
                _fieldTime = FieldTimeManager.Instance ?? FindObjectOfType<FieldTimeManager>();
            _fieldTime?.Advance(1);

            if (_multiEnemy == null)
                _multiEnemy = FindObjectOfType<FieldMultiEnemyAttack>();
            if (_multiEnemy != null)
                StartCoroutine(CoEnemyTurnEndNextFrame());
        }
    }

    private IEnumerator CoEnemyTurnEndNextFrame()
    {
        yield return null;
        if (_multiEnemy != null)
            _multiEnemy.OnPlayerTurnEnd();
    }
}
