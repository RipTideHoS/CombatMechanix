using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

/// <summary>
/// Automatically sets up the scene with all necessary GameObjects for testing
/// This script runs once and creates everything needed for the WebSocket client to work
/// </summary>
public class AutoSceneSetup : MonoBehaviour
{
    [Header("Auto Setup Settings")]
    public bool AutoSetupOnStart = true;
    public bool CreateGround = true;
    public bool CreatePlayer = true;
    public bool SetupCamera = true;
    
    private void Start()
    {
        if (AutoSetupOnStart)
        {
            SetupScene();
        }
        
        // Keep this GameObject alive during play mode for debugging
        DontDestroyOnLoad(gameObject);
        
        Debug.Log($"AutoSceneSetup: GameObject '{gameObject.name}' will persist during play mode");
    }

    [ContextMenu("Setup Scene")]
    public void SetupScene()
    {
        Debug.Log("=== Auto Scene Setup Starting ===");

        // 1. Create GameStartup GameObject (most important!)
        CreateGameStartup();

        // 2. Create Ground for visual reference
        if (CreateGround)
            CreateGroundPlane();

        // 3. Create Player GameObject
        if (CreatePlayer)
            CreateLocalPlayer();

        // 4. Setup Camera position
        if (SetupCamera)
            SetupMainCamera();

        Debug.Log("=== Auto Scene Setup Complete ===");
        Debug.Log("Objects created successfully! Check the Hierarchy for new GameObjects.");
        Debug.Log("Press Play to test the WebSocket connection!");
        
        // Log current hierarchy state
        LogHierarchyState();
    }
    
    private void LogHierarchyState()
    {
        Debug.Log("=== Current Scene Hierarchy ===");
        GameObject[] allObjects = FindObjectsOfType<GameObject>();
        foreach (GameObject obj in allObjects)
        {
            if (obj.transform.parent == null) // Only root objects
            {
                Debug.Log($"Root GameObject: {obj.name}");
            }
        }
    }

    private void CreateGameStartup()
    {
        // Check if GameStartup already exists
        if (FindObjectOfType<GameStartup>() != null)
        {
            Debug.Log("GameStartup already exists in scene");
            return;
        }

        Debug.Log("Creating GameStartup GameObject...");
        GameObject gameStartupObj = new GameObject("GameStartup");
        GameStartup startup = gameStartupObj.AddComponent<GameStartup>();
        
        // Configure the GameStartup script
        // These settings tell it to create all managers as components
        // since we don't have prefabs set up
        Debug.Log("GameStartup configured to auto-create all managers");
    }

    private void CreateGroundPlane()
    {
        // Check if ground already exists
        if (GameObject.Find("Ground") != null)
        {
            Debug.Log("Ground already exists in scene");
            return;
        }

        Debug.Log("Creating Ground plane...");
        GameObject ground = GameObject.CreatePrimitive(PrimitiveType.Plane);
        ground.name = "Ground";
        ground.transform.position = Vector3.zero;
        ground.transform.localScale = new Vector3(10, 1, 10); // 100x100 units

        // Add a simple material/color
        Renderer renderer = ground.GetComponent<Renderer>();
        if (renderer != null)
        {
            renderer.material.color = new Color(0.3f, 0.7f, 0.3f); // Green ground
        }
    }

    private void CreateLocalPlayer()
    {
        // Check if LocalPlayer already exists
        if (GameObject.Find("LocalPlayer") != null)
        {
            Debug.Log("LocalPlayer already exists in scene");
            return;
        }

        Debug.Log("Creating LocalPlayer GameObject...");
        GameObject player = GameObject.CreatePrimitive(PrimitiveType.Capsule);
        player.name = "LocalPlayer";
        player.transform.position = new Vector3(0, 1, 0); // Above ground
        
        // Add the PlayerController component
        PlayerController playerController = player.AddComponent<PlayerController>();
        
        // Make the player blue so it's easily visible
        Renderer renderer = player.GetComponent<Renderer>();
        if (renderer != null)
        {
            renderer.material.color = new Color(0.2f, 0.5f, 1.0f); // Blue player
        }

        // Add a Rigidbody for physics
        Rigidbody rb = player.AddComponent<Rigidbody>();
        rb.freezeRotation = true; // Prevent player from falling over

        Debug.Log("LocalPlayer created with PlayerController component");
    }

    private void SetupMainCamera()
    {
        Camera mainCamera = Camera.main;
        if (mainCamera == null)
        {
            Debug.LogWarning("No main camera found in scene");
            return;
        }

        Debug.Log("Setting up Main Camera for 3rd person view...");
        
        // Add camera controller if it doesn't exist
        CameraController cameraController = mainCamera.GetComponent<CameraController>();
        if (cameraController == null)
        {
            cameraController = mainCamera.gameObject.AddComponent<CameraController>();
            Debug.Log("Added CameraController to Main Camera");
        }
        
        // Connect camera to LocalPlayer
        GameObject localPlayer = GameObject.Find("LocalPlayer");
        if (localPlayer != null)
        {
            cameraController.SetTarget(localPlayer.transform);
            cameraController.SetCameraStyle(CameraStyle.ThirdPerson);
            Debug.Log("Connected camera to LocalPlayer for 3rd person following");
        }
        else
        {
            Debug.LogWarning("Could not find LocalPlayer to connect camera");
        }
        
        // Initial camera position (will be overridden by camera controller)
        mainCamera.transform.position = new Vector3(0, 3, -8);
        mainCamera.transform.rotation = Quaternion.Euler(15, 0, 0);
        
        Debug.Log("Camera configured for 3rd person gameplay with WASD controls");
    }

    // Helper method to create a basic UI Canvas if needed
    private void CreateBasicUI()
    {
        if (FindObjectOfType<Canvas>() != null)
        {
            Debug.Log("Canvas already exists in scene");
            return;
        }

        Debug.Log("Creating basic UI Canvas...");
        GameObject canvasObj = new GameObject("Canvas");
        Canvas canvas = canvasObj.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        
        canvasObj.AddComponent<UnityEngine.UI.CanvasScaler>();
        canvasObj.AddComponent<UnityEngine.UI.GraphicRaycaster>();

        // Create EventSystem if it doesn't exist
        if (FindObjectOfType<UnityEngine.EventSystems.EventSystem>() == null)
        {
            GameObject eventSystemObj = new GameObject("EventSystem");
            eventSystemObj.AddComponent<UnityEngine.EventSystems.EventSystem>();
            eventSystemObj.AddComponent<UnityEngine.EventSystems.StandaloneInputModule>();
        }
    }
}

// Editor script to make it easy to run setup from Unity Editor
#if UNITY_EDITOR

[CustomEditor(typeof(AutoSceneSetup))]
public class AutoSceneSetupEditor : Editor
{
    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();
        
        AutoSceneSetup setup = (AutoSceneSetup)target;
        
        GUILayout.Space(10);
        if (GUILayout.Button("Setup Scene Now"))
        {
            setup.SetupScene();
        }
        
        GUILayout.Space(5);
        EditorGUILayout.HelpBox(
            "This script will automatically create:\n" +
            "• GameStartup GameObject (creates all managers)\n" +
            "• Ground plane for visual reference\n" +
            "• LocalPlayer with PlayerController\n" +
            "• Proper camera positioning", 
            MessageType.Info);
    }
}
#endif