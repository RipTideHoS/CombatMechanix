using System.Collections;
using UnityEngine;

public class MeleeSwipeEffect : MonoBehaviour
{
    [Header("Swipe Visual Settings")]
    public float SwipeWidth = 60f; // Arc width in degrees
    public float SwipeDuration = 0.3f;
    public int ArcSegments = 20;
    public float SwipeThickness = 0.2f;
    
    [Header("Materials")]
    public Material SwipeMaterial;
    
    private LineRenderer _lineRenderer;
    private float _weaponRange;
    private Vector3 _swipeDirection;
    private Vector3 _attackerPosition;
    private bool _isAnimating = false;
    
    private void Awake()
    {
        SetupLineRenderer();
        CreateDefaultMaterial();
    }
    
    private void SetupLineRenderer()
    {
        _lineRenderer = gameObject.AddComponent<LineRenderer>();
        _lineRenderer.useWorldSpace = true;
        _lineRenderer.startWidth = SwipeThickness;
        _lineRenderer.endWidth = SwipeThickness * 0.1f;
        _lineRenderer.positionCount = ArcSegments + 1;
        _lineRenderer.sortingOrder = 5; // Render on top
        
        // Disable shadows and lighting for better visibility
        _lineRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        _lineRenderer.receiveShadows = false;
    }
    
    private void CreateDefaultMaterial()
    {
        if (SwipeMaterial == null)
        {
            SwipeMaterial = new Material(Shader.Find("Sprites/Default"));
            SwipeMaterial.color = new Color(1f, 0.8f, 0.2f, 0.8f); // Orange-yellow like projectile trail
            SwipeMaterial.SetFloat("_Mode", 3); // Transparent rendering mode
            SwipeMaterial.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
            SwipeMaterial.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
            SwipeMaterial.SetInt("_ZWrite", 0);
            SwipeMaterial.DisableKeyword("_ALPHATEST_ON");
            SwipeMaterial.EnableKeyword("_ALPHABLEND_ON");
            SwipeMaterial.DisableKeyword("_ALPHAPREMULTIPLY_ON");
            SwipeMaterial.renderQueue = 3000;
        }
        
        _lineRenderer.material = SwipeMaterial;
    }
    
    /// <summary>
    /// Initialize and play the melee swipe effect
    /// </summary>
    /// <param name="attackerPos">Position of the attacker</param>
    /// <param name="targetPos">Position being attacked (or direction)</param>
    /// <param name="weaponRange">Range of the weapon for the swipe length</param>
    public void PlaySwipeEffect(Vector3 attackerPos, Vector3 targetPos, float weaponRange)
    {
        if (_isAnimating) return;
        
        _attackerPosition = attackerPos;
        _weaponRange = Mathf.Max(weaponRange, 1.5f); // Minimum swipe range
        _swipeDirection = (targetPos - attackerPos).normalized;
        
        // Position the effect slightly above ground to avoid z-fighting
        _attackerPosition.y += 0.1f;
        
        Debug.Log($"[MeleeSwipeEffect] Playing swipe: Range={weaponRange}, Direction={_swipeDirection}");
        
        StartCoroutine(AnimateSwipe());
    }
    
    private IEnumerator AnimateSwipe()
    {
        _isAnimating = true;
        float elapsed = 0f;
        
        // Show full swipe immediately, then fade out
        while (elapsed < SwipeDuration)
        {
            float progress = elapsed / SwipeDuration;
            
            // Create the arc shape
            CreateSwipeArc(progress);
            
            // Fade out the effect
            float alpha = 1f - progress;
            Color color = SwipeMaterial.color;
            color.a = alpha;
            SwipeMaterial.color = color;
            
            elapsed += Time.deltaTime;
            yield return null;
        }
        
        // Hide the line renderer
        _lineRenderer.enabled = false;
        _isAnimating = false;
        
        // Destroy this effect after a short delay
        Destroy(gameObject, 0.1f);
    }
    
    private void CreateSwipeArc(float animationProgress)
    {
        Vector3[] positions = new Vector3[ArcSegments + 1];
        
        // Calculate the perpendicular vector for the arc
        Vector3 right = Vector3.Cross(_swipeDirection, Vector3.up).normalized;
        
        // Create arc from -SwipeWidth/2 to +SwipeWidth/2 degrees
        float halfWidth = SwipeWidth * 0.5f;
        
        for (int i = 0; i <= ArcSegments; i++)
        {
            float t = (float)i / ArcSegments;
            float angle = Mathf.Lerp(-halfWidth, halfWidth, t);
            
            // Rotate the forward direction by the angle
            Vector3 direction = Quaternion.AngleAxis(angle, Vector3.up) * _swipeDirection;
            
            // Calculate position along the arc at weapon range
            // Add slight curve to make it more visually appealing
            float distanceMultiplier = Mathf.Lerp(0.7f, 1f, 1f - Mathf.Abs(t - 0.5f) * 2f);
            Vector3 arcPosition = _attackerPosition + direction * (_weaponRange * distanceMultiplier);
            
            positions[i] = arcPosition;
        }
        
        _lineRenderer.SetPositions(positions);
        _lineRenderer.enabled = true;
    }
    
    /// <summary>
    /// Set custom swipe parameters
    /// </summary>
    public void SetSwipeParameters(float width, float duration, float thickness)
    {
        SwipeWidth = width;
        SwipeDuration = duration;
        SwipeThickness = thickness;
        
        if (_lineRenderer != null)
        {
            _lineRenderer.startWidth = thickness;
            _lineRenderer.endWidth = thickness * 0.1f;
        }
    }
    
    /// <summary>
    /// Set custom material for the swipe
    /// </summary>
    public void SetSwipeMaterial(Material material)
    {
        SwipeMaterial = material;
        if (_lineRenderer != null)
        {
            _lineRenderer.material = material;
        }
    }
}