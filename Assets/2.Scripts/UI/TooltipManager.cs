using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// Global tooltip manager
/// Shows tooltips that follow the mouse
/// </summary>
public class TooltipManager : MonoBehaviour
{
    public static TooltipManager Instance { get; private set; }
    
    [Header("Tooltip Panel")]
    public GameObject tooltipPanel;
    public TMP_Text tooltipText;
    public Image tooltipBackground;
    
    [Header("Settings")]
    public Vector2 offset = new Vector2(15f, -15f);
    public float showDelay = 0.3f;
    public float maxWidth = 300f;
    
    [Header("Animation")]
    public bool fadeIn = true;
    public float fadeSpeed = 10f;
    
    private RectTransform _tooltipRect;
    private CanvasGroup _canvasGroup;
    private Canvas _canvas;
    
    private string _pendingText;
    private float _showTimer;
    private bool _isShowing;
    
    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
        
        if (tooltipPanel != null)
        {
            _tooltipRect = tooltipPanel.GetComponent<RectTransform>();
            _canvasGroup = tooltipPanel.GetComponent<CanvasGroup>();
            
            if (_canvasGroup == null)
                _canvasGroup = tooltipPanel.AddComponent<CanvasGroup>();
        }
        
        _canvas = GetComponentInParent<Canvas>();
        
        Hide();
    }
    
    private void Update()
    {
        // Show delay
        if (!string.IsNullOrEmpty(_pendingText) && !_isShowing)
        {
            _showTimer += Time.unscaledDeltaTime;
            if (_showTimer >= showDelay)
            {
                ShowImmediate(_pendingText);
            }
        }
        
        // Follow mouse
        if (_isShowing && tooltipPanel != null)
        {
            UpdatePosition();
            
            // Fade in
            if (fadeIn && _canvasGroup != null && _canvasGroup.alpha < 1f)
            {
                _canvasGroup.alpha += Time.unscaledDeltaTime * fadeSpeed;
            }
        }
    }
    
    /// <summary>
    /// Request to show tooltip (with delay)
    /// </summary>
    public void Show(string text)
    {
        if (string.IsNullOrEmpty(text))
        {
            Hide();
            return;
        }
        
        _pendingText = text;
        _showTimer = 0f;
    }
    
    /// <summary>
    /// Show tooltip immediately
    /// </summary>
    public void ShowImmediate(string text)
    {
        if (string.IsNullOrEmpty(text) || tooltipPanel == null)
            return;
        
        _pendingText = null;
        _isShowing = true;
        
        // Set text
        if (tooltipText != null)
            tooltipText.text = text;
        
        // Resize
        if (_tooltipRect != null)
        {
            LayoutRebuilder.ForceRebuildLayoutImmediate(_tooltipRect);
        }
        
        // Show
        tooltipPanel.SetActive(true);
        
        // Reset alpha for fade
        if (fadeIn && _canvasGroup != null)
            _canvasGroup.alpha = 0f;
        
        // Position
        UpdatePosition();
    }
    
    /// <summary>
    /// Hide tooltip
    /// </summary>
    public void Hide()
    {
        _pendingText = null;
        _showTimer = 0f;
        _isShowing = false;
        
        if (tooltipPanel != null)
            tooltipPanel.SetActive(false);
    }
    
    private void UpdatePosition()
    {
        if (_tooltipRect == null || _canvas == null)
            return;
        
        Vector2 mousePos = Input.mousePosition;
        
        // Convert to canvas space
        RectTransformUtility.ScreenPointToLocalPointInRectangle(
            _canvas.transform as RectTransform,
            mousePos,
            _canvas.worldCamera,
            out Vector2 localPoint
        );
        
        // Apply offset
        localPoint += offset;
        
        // Keep on screen
        Vector2 tooltipSize = _tooltipRect.sizeDelta;
        Vector2 canvasSize = (_canvas.transform as RectTransform).sizeDelta;
        
        // Right edge
        if (localPoint.x + tooltipSize.x > canvasSize.x / 2f)
            localPoint.x = canvasSize.x / 2f - tooltipSize.x;
        
        // Left edge
        if (localPoint.x < -canvasSize.x / 2f)
            localPoint.x = -canvasSize.x / 2f;
        
        // Bottom edge
        if (localPoint.y - tooltipSize.y < -canvasSize.y / 2f)
            localPoint.y = -canvasSize.y / 2f + tooltipSize.y;
        
        // Top edge
        if (localPoint.y > canvasSize.y / 2f)
            localPoint.y = canvasSize.y / 2f;
        
        _tooltipRect.anchoredPosition = localPoint;
    }
    
    // ============================================
    // Static convenience methods
    // ============================================
    
    public static void ShowTooltip(string text)
    {
        Instance?.Show(text);
    }
    
    public static void ShowTooltipImmediate(string text)
    {
        Instance?.ShowImmediate(text);
    }
    
    public static void HideTooltip()
    {
        Instance?.Hide();
    }
}
