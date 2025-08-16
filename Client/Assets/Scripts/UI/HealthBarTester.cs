using UnityEngine;

/// <summary>
/// Simple test script to verify health bar visibility
/// </summary>
public class HealthBarTester : MonoBehaviour
{
    [Header("Test Settings")]
    public bool EnableTester = false; // Disabled by default for production
    public KeyCode TestKey = KeyCode.T;
    public KeyCode ToggleTestKey = KeyCode.F2;
    public float TestScale = 0.1f;
    public Vector3 TestOffset = new Vector3(0, 5f, 0);

    private void Update()
    {
        if (Input.GetKeyDown(ToggleTestKey))
        {
            EnableTester = !EnableTester;
        }

        if (EnableTester && Input.GetKeyDown(TestKey))
        {
            CreateVisibleTestHealthBar();
        }
    }

    private void CreateVisibleTestHealthBar()
    {
        Debug.Log("[HealthBarTester] Creating visible test health bar...");

        // Find the player position
        var player = FindObjectOfType<PlayerController>();
        Vector3 spawnPos = player != null ? player.transform.position + new Vector3(2, 0, 2) : Vector3.zero;

        // Create a simple, highly visible test health bar
        GameObject testHealthBar = new GameObject("TestHealthBar");
        testHealthBar.transform.position = spawnPos + TestOffset;

        // Create Canvas
        Canvas canvas = testHealthBar.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.WorldSpace;
        canvas.sortingOrder = 100; // Very high to ensure visibility

        // Set canvas size and scale
        RectTransform canvasRect = canvas.GetComponent<RectTransform>();
        canvasRect.sizeDelta = new Vector2(500, 100); // Large size
        canvas.transform.localScale = Vector3.one * TestScale; // Adjustable scale

        // Make it face the camera
        if (Camera.main != null)
        {
            Vector3 directionToCamera = Camera.main.transform.position - testHealthBar.transform.position;
            testHealthBar.transform.rotation = Quaternion.LookRotation(directionToCamera);
        }

        // Create a bright background for visibility
        GameObject background = new GameObject("Background");
        background.transform.SetParent(canvas.transform, false);
        
        var bgImage = background.AddComponent<UnityEngine.UI.Image>();
        bgImage.color = Color.magenta; // Bright magenta to make it very obvious
        
        var bgRect = background.GetComponent<RectTransform>();
        bgRect.anchorMin = Vector2.zero;
        bgRect.anchorMax = Vector2.one;
        bgRect.anchoredPosition = Vector2.zero;
        bgRect.sizeDelta = Vector2.zero;

        // Create text for identification
        GameObject text = new GameObject("Text");
        text.transform.SetParent(canvas.transform, false);
        
        var textComponent = text.AddComponent<UnityEngine.UI.Text>();
        textComponent.text = "TEST HEALTH BAR";
        textComponent.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        textComponent.fontSize = 50;
        textComponent.color = Color.white;
        textComponent.alignment = TextAnchor.MiddleCenter;
        textComponent.fontStyle = FontStyle.Bold;
        
        var textRect = text.GetComponent<RectTransform>();
        textRect.anchorMin = Vector2.zero;
        textRect.anchorMax = Vector2.one;
        textRect.anchoredPosition = Vector2.zero;
        textRect.sizeDelta = Vector2.zero;

        Debug.Log($"[HealthBarTester] Test health bar created at {testHealthBar.transform.position}");
        Debug.Log($"[HealthBarTester] Canvas scale: {canvas.transform.localScale}");
        Debug.Log($"[HealthBarTester] Canvas size: {canvasRect.sizeDelta}");

        // Make it persistent for testing
        DontDestroyOnLoad(testHealthBar);
        
        // Destroy after 30 seconds to avoid clutter
        Destroy(testHealthBar, 30f);
    }

    private void OnGUI()
    {
        if (!EnableTester) return;

        GUILayout.BeginArea(new Rect(10, 10, 300, 100));
        GUILayout.BeginVertical("box");
        
        GUILayout.Label("Health Bar Tester");
        GUILayout.Label($"Press '{TestKey}' to create test health bar");
        
        GUILayout.BeginHorizontal();
        GUILayout.Label("Scale:");
        TestScale = GUILayout.HorizontalSlider(TestScale, 0.01f, 0.5f, GUILayout.Width(100));
        GUILayout.Label(TestScale.ToString("F3"));
        GUILayout.EndHorizontal();
        
        GUILayout.EndVertical();
        GUILayout.EndArea();
    }
}