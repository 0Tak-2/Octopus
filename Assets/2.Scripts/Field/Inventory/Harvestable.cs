using UnityEngine;

/// <summary>
/// ??? ?????? ??????? (????, ???, ???, ?? ??)
/// </summary>
public class Harvestable : MonoBehaviour
{
    private static readonly System.Collections.Generic.List<Harvestable> ActiveHarvestables = new System.Collections.Generic.List<Harvestable>();

    [Header("?????? ????")]
    public string itemName = "????"; // ?????? ???
    public int itemID = 1; // ?????? ID (1=????, 2=???, 3=???, 4=??...)
    public GameObject itemDropPrefab; // ?????? ?????? ??????

    [Header("???? ??????")]
    public bool requiresPickaxe = false; // ???? ??? (???)
    public bool requiresShovel = false; // ?? ??? (??)

    [Header("??? ????")]
    public int minDropCount = 1; // ??? ??? ????
    public int maxDropCount = 1; // ??? ??? ????
    public float dropSpread = 0.2f; // ??? ????? ????

    [Header("??? ??? (????)")]
    public int stealthBonus = 0; // ????? ????? (????? ???)

    [Header("????")]
    public bool isHarvested = false;

    private SpriteRenderer spriteRenderer;
    private Color _baseColor = Color.white;
    private bool _hasBaseColor;
    private bool _isPreviewTransparent;

    private void Awake()
    {
        spriteRenderer = GetComponent<SpriteRenderer>();
        if (spriteRenderer != null)
        {
            _baseColor = spriteRenderer.color;
            _hasBaseColor = true;
        }
    }

    private void OnEnable()
    {
        if (!ActiveHarvestables.Contains(this))
            ActiveHarvestables.Add(this);
    }

    private void OnDisable()
    {
        ResetPreviewTransparency();
        ActiveHarvestables.Remove(this);
    }

    /// <summary>
    /// ??? ???????? ???
    /// </summary>
    public bool CanHarvest(out string reason)
    {
        if (isHarvested)
        {
            reason = "??? ?????????.";
            return false;
        }

        // ???? ???
        if (requiresPickaxe)
        {
            if (ToolManager.Instance == null || !ToolManager.Instance.HasPickaxe())
            {
                reason = $"{itemName}??(??) ?????? ????? ???????!";
                return false;
            }
        }

        // ?? ???
        if (requiresShovel)
        {
            if (ToolManager.Instance == null || !ToolManager.Instance.HasShovel())
            {
                reason = $"{itemName}??(??) ?????? ???? ???????!";
                return false;
            }
        }

        reason = "";
        return true;
    }

    /// <summary>
    /// ???
    /// </summary>
    public void Harvest()
    {
        if (isHarvested) return;

        Vector2Int harvestedCell = Vector2Int.zero;
        GridBoard grid = FindObjectOfType<GridBoard>();
        if (grid != null)
            harvestedCell = grid.WorldToCell(transform.position);

        isHarvested = true;

        // ?????? ???
        if (itemDropPrefab != null)
        {
            // ???? ????
            int actualDropCount = Random.Range(minDropCount, maxDropCount + 1);

            for (int i = 0; i < actualDropCount; i++)
            {
                // ???? ??? ????? ???
                Vector3 dropPos = transform.position + new Vector3(
                    Random.Range(-dropSpread, dropSpread),
                    Random.Range(-dropSpread, dropSpread),
                    0
                );

                Instantiate(itemDropPrefab, dropPos, Quaternion.identity);
            }
        }

        // ����/�þ� ���� ����
        if (FieldVisionSystem.Instance != null)
        {
            if (itemID == 1 || itemID == 2)
                FieldVisionSystem.Instance.RemoveCoverCell(harvestedCell);
            if (itemID == 2)
                FieldVisionSystem.Instance.ClearTouchedCoralGlowAt(harvestedCell);
        }
        if (FogOfWarRenderer.Instance != null)
            FogOfWarRenderer.Instance.ForceRefresh();

        // ??????? ????
        Destroy(gameObject);
    }

    /// <summary>
    /// ??? ?? ??????? ????? ???? ??? ?????? ???
    /// </summary>
    public void SetPreviewTransparency(float alpha)
    {
        if (spriteRenderer == null) return;
        if (isHarvested) return;

        if (!_hasBaseColor)
        {
            _baseColor = spriteRenderer.color;
            _hasBaseColor = true;
        }

        Color c = _baseColor;
        c.a = Mathf.Clamp01(alpha);
        spriteRenderer.color = c;
        _isPreviewTransparent = true;
    }

    /// <summary>
    /// ??? ?????? ??? ????
    /// </summary>
    public void ResetPreviewTransparency()
    {
        if (spriteRenderer == null) return;
        if (!_hasBaseColor) return;
        if (!_isPreviewTransparent) return;

        spriteRenderer.color = _baseColor;
        _isPreviewTransparent = false;
    }

    public static void GetHarvestablesAtCell(GridBoard gridBoard, Vector2Int cell, System.Collections.Generic.List<Harvestable> results)
    {
        if (results == null) return;
        results.Clear();
        if (gridBoard == null) return;

        for (int i = 0; i < ActiveHarvestables.Count; i++)
        {
            Harvestable h = ActiveHarvestables[i];
            if (h == null || h.isHarvested || !h.gameObject.activeInHierarchy)
                continue;

            if (gridBoard.WorldToCell(h.transform.position) == cell)
                results.Add(h);
        }
    }

    /// <summary>
    /// ??????? ?? ??????? ??? ????? ?? (?????)
    /// </summary>
    public bool IsPlayerInside(Vector3 playerPosition, GridBoard gridBoard)
    {
        if (gridBoard == null || stealthBonus <= 0) return false;

        Vector2Int objectCell = gridBoard.WorldToCell(transform.position);
        Vector2Int playerCell = gridBoard.WorldToCell(playerPosition);

        return objectCell == playerCell;
    }
}