using System;
using UnityEngine;

public class ResourceNodeClient : MonoBehaviour
{
    [Header("Resource Settings")]
    [SerializeField] private string _resourceId;
    [SerializeField] private string _resourceType;
    [SerializeField] private int _currentAmount;
    [SerializeField] private int _maxAmount;

    [Header("Visual Components")]
    public TextMesh ResourceText;
    public ParticleSystem GatherEffect;
    public GameObject DepletedVisual;

    [Header("Interaction")]
    public float InteractionRange = 3f;

    // Public properties (no [Header] on these)
    public string ResourceId => _resourceId;
    public string ResourceType => _resourceType;
    public int CurrentAmount => _currentAmount;
    public int MaxAmount => _maxAmount;

    private Renderer _renderer;
    private Collider _collider;
    private bool _isInitialized = false;

    private void Awake()
    {
        _renderer = GetComponent<Renderer>();
        _collider = GetComponent<Collider>();
        
        SetupVisualComponents();
    }

    private void SetupVisualComponents()
    {
        // Create resource text if it doesn't exist
        if (ResourceText == null)
        {
            var textObj = new GameObject("ResourceText");
            textObj.transform.SetParent(transform);
            textObj.transform.localPosition = new Vector3(0, 1.5f, 0);
            
            ResourceText = textObj.AddComponent<TextMesh>();
            ResourceText.text = "Resource";
            ResourceText.fontSize = 15;
            ResourceText.color = Color.white;
            ResourceText.anchor = TextAnchor.MiddleCenter;
            ResourceText.alignment = TextAlignment.Center;
        }

        // Create gather effect if it doesn't exist
        if (GatherEffect == null)
        {
            var effectObj = new GameObject("GatherEffect");
            effectObj.transform.SetParent(transform);
            effectObj.transform.localPosition = Vector3.zero;
            
            GatherEffect = effectObj.AddComponent<ParticleSystem>();
            var main = GatherEffect.main;
            main.startColor = Color.yellow;
            main.startSize = 0.1f;
            main.startLifetime = 1f;
            main.maxParticles = 30;
            
            var emission = GatherEffect.emission;
            emission.enabled = false;
        }

        // Create depleted visual if it doesn't exist
        if (DepletedVisual == null)
        {
            var depletedObj = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            depletedObj.name = "DepletedVisual";
            depletedObj.transform.SetParent(transform);
            depletedObj.transform.localPosition = Vector3.zero;
            depletedObj.transform.localScale = new Vector3(0.5f, 0.5f, 0.5f);
            
            var depletedRenderer = depletedObj.GetComponent<Renderer>();
            if (depletedRenderer != null)
            {
                depletedRenderer.material.color = Color.gray;
            }
            
            // Remove collider from depleted visual
            var depletedCollider = depletedObj.GetComponent<Collider>();
            if (depletedCollider != null)
            {
                DestroyImmediate(depletedCollider);
            }
            
            DepletedVisual = depletedObj;
            DepletedVisual.SetActive(false);
        }

        // Ensure main object has a collider for interaction
        if (_collider == null)
        {
            _collider = gameObject.AddComponent<BoxCollider>();
        }
    }

    public void Initialize(ResourceNode resource)
    {
        _resourceId = resource.ResourceId;
        _resourceType = resource.ResourceType;
        _currentAmount = resource.CurrentAmount;
        _maxAmount = resource.MaxAmount;

        transform.position = resource.Position;
        _isInitialized = true;
        
        UpdateVisuals();
        
        Debug.Log($"ResourceNode initialized: {_resourceType} ({_currentAmount}/{_maxAmount}) at {transform.position}");
    }

    public void UpdateResource(ResourceNode resource)
    {
        _currentAmount = resource.CurrentAmount;
        _maxAmount = resource.MaxAmount;
        UpdateVisuals();
    }

    private void UpdateVisuals()
    {
        if (!_isInitialized) return;

        // Update resource text
        if (ResourceText != null)
        {
            ResourceText.text = $"{_resourceType}\n{_currentAmount}/{_maxAmount}";
            
            // Face camera
            if (Camera.main != null)
            {
                ResourceText.transform.LookAt(Camera.main.transform);
                ResourceText.transform.Rotate(0, 180, 0);
            }
        }

        // Update visual state based on amount
        bool isDepleted = _currentAmount <= 0;
        
        if (DepletedVisual != null)
        {
            DepletedVisual.SetActive(isDepleted);
        }

        // Change material color based on amount
        if (_renderer != null)
        {
            float amountPercent = _maxAmount > 0 ? (float)_currentAmount / _maxAmount : 0;
            Color baseColor = GetResourceColor(_resourceType);
            _renderer.material.color = Color.Lerp(Color.gray, baseColor, amountPercent);
        }

        // Enable/disable collider based on availability
        if (_collider != null)
        {
            _collider.enabled = !isDepleted;
        }
    }

    public void PlayGatherEffect()
    {
        if (GatherEffect != null)
        {
            var emission = GatherEffect.emission;
            emission.enabled = true;
            GatherEffect.Play();
            
            // Disable emission after a short time
            StartCoroutine(DisableGatherEffectAfterDelay(2f));
        }
        
        Debug.Log($"Playing gather effect for {_resourceType}");
    }

    private System.Collections.IEnumerator DisableGatherEffectAfterDelay(float delay)
    {
        yield return new WaitForSeconds(delay);
        if (GatherEffect != null)
        {
            var emission = GatherEffect.emission;
            emission.enabled = false;
        }
    }

    private Color GetResourceColor(string resourceType)
    {
        switch (resourceType.ToLower())
        {
            case "wood": return new Color(0.6f, 0.3f, 0.1f); // Brown
            case "stone": return Color.gray;
            case "iron": return new Color(0.7f, 0.7f, 0.8f); // Silver
            case "gold": return Color.yellow;
            default: return Color.white;
        }
    }

    private void OnMouseDown()
    {
        // Handle click to gather resource
        if (PlayerController.Instance != null)
        {
            float distance = Vector3.Distance(transform.position, PlayerController.Instance.transform.position);
            if (distance <= InteractionRange && _currentAmount > 0)
            {
                var networkManager = GameManager.Instance?.NetworkManager;
                if (networkManager != null)
                {
                    // This would normally send to server
                    Debug.Log($"Gathering {_resourceType} from {_resourceId}");
                    PlayGatherEffect();
                }
            }
            else if (distance > InteractionRange)
            {
                var uiManager = GameManager.Instance?.UIManager;
                if (uiManager != null)
                {
                    uiManager.ShowMessage("Too far away to gather this resource!");
                }
            }
            else if (_currentAmount <= 0)
            {
                var uiManager = GameManager.Instance?.UIManager;
                if (uiManager != null)
                {
                    uiManager.ShowMessage("This resource is depleted!");
                }
            }
        }
    }

    public bool CanGather()
    {
        return _currentAmount > 0 && _isInitialized;
    }

    public float GetDistanceToLocalPlayer()
    {
        if (PlayerController.Instance != null)
        {
            return Vector3.Distance(transform.position, PlayerController.Instance.transform.position);
        }
        return float.MaxValue;
    }

    private void OnDestroy()
    {
        Debug.Log($"ResourceNode {_resourceType} destroyed");
    }
}