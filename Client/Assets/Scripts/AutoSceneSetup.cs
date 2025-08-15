using UnityEngine;
using System.Reflection;
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
    public bool CreateReferenceBox = true;
    
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

        // 1. Create GameManager and all managers first (most important!)
        CreateGameManagerSystem();

        // 2. Create GameStartup GameObject for additional setup
        CreateGameStartup();

        // 3. Create Ground for visual reference
        if (CreateGround)
            CreateGroundPlane();

        // 4. Create Player GameObject
        if (CreatePlayer)
            CreateLocalPlayer();

        // 5. Setup Camera position
        if (SetupCamera)
            SetupMainCamera();

        // 6. Create Reference Box
        if (CreateReferenceBox)
            CreateReferenceBoxObject();

        // 7. Create basic UI system (after GameManager exists)
        CreateBasicUI();

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

    private void CreateGameManagerSystem()
    {
        // Check if GameManager already exists
        if (FindObjectOfType<GameManager>() != null)
        {
            Debug.Log("GameManager already exists in scene");
            return;
        }

        Debug.Log("Creating GameManager system...");
        GameObject gameManagerObj = new GameObject("GameManager");
        
        // Add all manager components to the GameManager GameObject
        gameManagerObj.AddComponent<GameManager>();
        gameManagerObj.AddComponent<NetworkManager>();
        gameManagerObj.AddComponent<WorldManager>();
        var uiManager = gameManagerObj.AddComponent<UIManager>();
        gameManagerObj.AddComponent<CombatSystem>();
        gameManagerObj.AddComponent<ChatSystem>();
        gameManagerObj.AddComponent<InventoryManager>();
        
        // Add AudioSource for sound effects
        gameManagerObj.AddComponent<AudioSource>();
        
        // Make it persistent
        DontDestroyOnLoad(gameManagerObj);
        
        Debug.Log("GameManager system created with all manager components");
        Debug.Log("UIManager component added - inventory system will be available");
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
        player.transform.position = new Vector3(0, 1f, 0); // Transform at y=1 so CC bottom sits on ground
        
        // Add the PlayerController component
        PlayerController playerController = player.AddComponent<PlayerController>();
        
        // Add the ClientPlayerStats component for server-authoritative stats
        ClientPlayerStats playerStats = player.AddComponent<ClientPlayerStats>();
        
        // Make the player blue so it's easily visible
        Renderer renderer = player.GetComponent<Renderer>();
        if (renderer != null)
        {
            renderer.material.color = new Color(0.2f, 0.5f, 1.0f); // Blue player
        }

        // Remove the capsule collider (CharacterController will handle collision)
        Collider capsuleCollider = player.GetComponent<Collider>();
        if (capsuleCollider != null)
        {
            DestroyImmediate(capsuleCollider);
        }
        
        // Don't add Rigidbody - CharacterController handles physics

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
        mainCamera.transform.position = new Vector3(0, 35, -25);
        mainCamera.transform.LookAt(new Vector3(0, 1, 0)); // Look at player spawn point
        
        Debug.Log("Camera configured for 3rd person gameplay with WASD controls");
    }

    private void CreateReferenceBoxObject()
    {
        // Check if ReferenceBox already exists
        if (GameObject.Find("ReferenceBox") != null)
        {
            Debug.Log("ReferenceBox already exists in scene");
            return;
        }

        Debug.Log("Creating ReferenceBox GameObject...");
        GameObject referenceBoxObj = new GameObject("ReferenceBox");
        ReferenceBox referenceBox = referenceBoxObj.AddComponent<ReferenceBox>();
        
        Debug.Log("ReferenceBox created with collision and positioned relative to player/camera");
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
        canvas.sortingOrder = 100; // Ensure it's on top
        
        var canvasScaler = canvasObj.AddComponent<UnityEngine.UI.CanvasScaler>();
        canvasScaler.uiScaleMode = UnityEngine.UI.CanvasScaler.ScaleMode.ScaleWithScreenSize;
        canvasScaler.referenceResolution = new Vector2(1920, 1080);
        canvasScaler.matchWidthOrHeight = 0.5f;
        
        var graphicRaycaster = canvasObj.AddComponent<UnityEngine.UI.GraphicRaycaster>();

        // Create EventSystem if it doesn't exist
        if (FindObjectOfType<UnityEngine.EventSystems.EventSystem>() == null)
        {
            GameObject eventSystemObj = new GameObject("EventSystem");
            eventSystemObj.AddComponent<UnityEngine.EventSystems.EventSystem>();
            eventSystemObj.AddComponent<UnityEngine.EventSystems.StandaloneInputModule>();
            Debug.Log("EventSystem created for UI interaction");
        }

        // Create the inventory panel
        GameObject inventoryPanel = CreateInventoryPanel(canvasObj);
        
        // Create the login panel
        GameObject loginPanel = CreateLoginPanel(canvasObj);
        
        // Connect the panels to UIManager
        ConnectUIManagerReferences(inventoryPanel, loginPanel);
        
        // Force Canvas to update
        Canvas.ForceUpdateCanvases();
        
        Debug.Log("UI Canvas created with inventory panel and connected to UIManager");
        Debug.Log($"Canvas settings: RenderMode={canvas.renderMode}, SortingOrder={canvas.sortingOrder}");
    }


    private GameObject CreateInventoryPanel(GameObject canvasObj)
    {
        // Create inventory panel
        GameObject inventoryPanel = new GameObject("InventoryPanel");
        inventoryPanel.transform.SetParent(canvasObj.transform, false);
        
        // Add Image component for background
        var image = inventoryPanel.AddComponent<UnityEngine.UI.Image>();
        image.color = new Color(0.2f, 0.2f, 0.2f, 0.9f); // Darker, more opaque background
        image.raycastTarget = true;
        
        // Set up RectTransform for positioning (right side of screen)
        var rectTransform = inventoryPanel.GetComponent<RectTransform>();
        
        // Use absolute positioning to ensure it's visible
        rectTransform.anchorMin = new Vector2(0.75f, 0.25f);
        rectTransform.anchorMax = new Vector2(0.98f, 0.85f);
        rectTransform.anchoredPosition = Vector2.zero;
        rectTransform.sizeDelta = Vector2.zero;
        rectTransform.offsetMin = Vector2.zero;
        rectTransform.offsetMax = Vector2.zero;
        
        // Add title text
        GameObject titleObj = new GameObject("Title");
        titleObj.transform.SetParent(inventoryPanel.transform, false);
        var titleText = titleObj.AddComponent<UnityEngine.UI.Text>();
        titleText.text = "INVENTORY";
        titleText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        titleText.fontSize = 20;
        titleText.color = Color.white;
        titleText.alignment = TextAnchor.MiddleCenter;
        titleText.fontStyle = FontStyle.Bold;
        
        var titleRect = titleObj.GetComponent<RectTransform>();
        titleRect.anchorMin = new Vector2(0, 0.85f);
        titleRect.anchorMax = new Vector2(1, 1f);
        titleRect.anchoredPosition = Vector2.zero;
        titleRect.sizeDelta = Vector2.zero;
        
        // Add some sample content
        GameObject contentObj = new GameObject("Content");
        contentObj.transform.SetParent(inventoryPanel.transform, false);
        var contentText = contentObj.AddComponent<UnityEngine.UI.Text>();
        contentText.text = "• Inventory items will appear here\n\n• Press WASD to move player\n• Press I to toggle this panel\n\nPanel Status: Working!";
        contentText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        contentText.fontSize = 16;
        contentText.color = Color.white;
        contentText.alignment = TextAnchor.UpperLeft;
        
        var contentRect = contentObj.GetComponent<RectTransform>();
        contentRect.anchorMin = new Vector2(0.05f, 0.05f);
        contentRect.anchorMax = new Vector2(0.95f, 0.8f);
        contentRect.anchoredPosition = Vector2.zero;
        contentRect.sizeDelta = Vector2.zero;
        
        // Start with panel hidden
        inventoryPanel.SetActive(false);
        
        Debug.Log($"Inventory panel created: Pos={rectTransform.anchoredPosition}, Size={rectTransform.rect.size}, Active={inventoryPanel.activeSelf}");
        Debug.Log($"Panel hierarchy: Canvas->InventoryPanel->Title+Content");
        
        return inventoryPanel;
    }

    private GameObject CreateLoginPanel(GameObject canvasObj)
    {
        // Create login panel
        GameObject loginPanel = new GameObject("LoginPanel");
        loginPanel.transform.SetParent(canvasObj.transform, false);
        
        // Add Image component for background
        var image = loginPanel.AddComponent<UnityEngine.UI.Image>();
        image.color = new Color(0.1f, 0.1f, 0.1f, 0.95f); // Dark semi-transparent background
        image.raycastTarget = true;
        
        // Set up RectTransform for full screen coverage
        var rectTransform = loginPanel.GetComponent<RectTransform>();
        rectTransform.anchorMin = Vector2.zero;
        rectTransform.anchorMax = Vector2.one;
        rectTransform.anchoredPosition = Vector2.zero;
        rectTransform.sizeDelta = Vector2.zero;
        rectTransform.offsetMin = Vector2.zero;
        rectTransform.offsetMax = Vector2.zero;
        
        // Create main login container (centered)
        GameObject loginContainer = new GameObject("LoginContainer");
        loginContainer.transform.SetParent(loginPanel.transform, false);
        
        var containerImage = loginContainer.AddComponent<UnityEngine.UI.Image>();
        containerImage.color = new Color(0.2f, 0.2f, 0.2f, 0.9f);
        
        var containerRect = loginContainer.GetComponent<RectTransform>();
        containerRect.anchorMin = new Vector2(0.35f, 0.3f);
        containerRect.anchorMax = new Vector2(0.65f, 0.7f);
        containerRect.anchoredPosition = Vector2.zero;
        containerRect.sizeDelta = Vector2.zero;
        
        // Create title
        GameObject titleObj = new GameObject("Title");
        titleObj.transform.SetParent(loginContainer.transform, false);
        var titleText = titleObj.AddComponent<UnityEngine.UI.Text>();
        titleText.text = "COMBAT MECHANIX LOGIN";
        titleText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        titleText.fontSize = 24;
        titleText.color = Color.white;
        titleText.alignment = TextAnchor.MiddleCenter;
        titleText.fontStyle = FontStyle.Bold;
        
        var titleRect = titleObj.GetComponent<RectTransform>();
        titleRect.anchorMin = new Vector2(0, 0.8f);
        titleRect.anchorMax = new Vector2(1, 1f);
        titleRect.anchoredPosition = Vector2.zero;
        titleRect.sizeDelta = Vector2.zero;
        
        // Create username input field
        GameObject usernameField = CreateInputField(loginContainer, "UsernameField", "Username", 0.55f, 0.7f);
        var usernameInput = usernameField.GetComponent<UnityEngine.UI.InputField>();
        usernameInput.placeholder.GetComponent<UnityEngine.UI.Text>().text = "Enter username...";
        
        // Create password input field  
        GameObject passwordField = CreateInputField(loginContainer, "PasswordField", "Password", 0.4f, 0.55f);
        var passwordInput = passwordField.GetComponent<UnityEngine.UI.InputField>();
        passwordInput.contentType = UnityEngine.UI.InputField.ContentType.Password;
        passwordInput.placeholder.GetComponent<UnityEngine.UI.Text>().text = "Enter password...";
        
        // Create login button
        GameObject loginButton = CreateButton(loginContainer, "LoginButton", "LOGIN", 0.25f, 0.4f);
        
        // Create status text
        GameObject statusObj = new GameObject("StatusText");
        statusObj.transform.SetParent(loginContainer.transform, false);
        var statusText = statusObj.AddComponent<UnityEngine.UI.Text>();
        statusText.text = "Enter your credentials to login";
        statusText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        statusText.fontSize = 16;
        statusText.color = Color.yellow;
        statusText.alignment = TextAnchor.MiddleCenter;
        
        var statusRect = statusObj.GetComponent<RectTransform>();
        statusRect.anchorMin = new Vector2(0.05f, 0.05f);
        statusRect.anchorMax = new Vector2(0.95f, 0.2f);
        statusRect.anchoredPosition = Vector2.zero;
        statusRect.sizeDelta = Vector2.zero;
        
        // Add LoginUI component to the loginPanel
        var loginUIComponent = loginPanel.AddComponent<LoginUI>();
        
        // Set references in LoginUI component using reflection (since fields might be private)
        SetFieldValue(loginUIComponent, "LoginPanel", loginPanel);
        SetFieldValue(loginUIComponent, "UsernameInput", usernameInput);
        SetFieldValue(loginUIComponent, "PasswordInput", passwordInput);
        SetFieldValue(loginUIComponent, "LoginButton", loginButton.GetComponent<UnityEngine.UI.Button>());
        SetFieldValue(loginUIComponent, "StatusText", statusText);
        
        // Start with panel visible (login should show first)
        loginPanel.SetActive(true);
        
        Debug.Log("Login panel created with username/password fields and LoginUI component");
        return loginPanel;
    }
    
    private GameObject CreateInputField(GameObject parent, string name, string labelText, float minY, float maxY)
    {
        GameObject fieldObj = new GameObject(name);
        fieldObj.transform.SetParent(parent.transform, false);
        
        // Add background image
        var image = fieldObj.AddComponent<UnityEngine.UI.Image>();
        image.color = new Color(0.8f, 0.8f, 0.8f, 1f);
        
        var fieldRect = fieldObj.GetComponent<RectTransform>();
        fieldRect.anchorMin = new Vector2(0.1f, minY);
        fieldRect.anchorMax = new Vector2(0.9f, maxY);
        fieldRect.anchoredPosition = Vector2.zero;
        fieldRect.sizeDelta = Vector2.zero;
        
        // Add InputField component
        var inputField = fieldObj.AddComponent<UnityEngine.UI.InputField>();
        
        // Create text component
        GameObject textObj = new GameObject("Text");
        textObj.transform.SetParent(fieldObj.transform, false);
        var text = textObj.AddComponent<UnityEngine.UI.Text>();
        text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        text.fontSize = 16;
        text.color = Color.black;
        text.alignment = TextAnchor.MiddleLeft;
        
        var textRect = textObj.GetComponent<RectTransform>();
        textRect.anchorMin = Vector2.zero;
        textRect.anchorMax = Vector2.one;
        textRect.anchoredPosition = Vector2.zero;
        textRect.sizeDelta = Vector2.zero;
        textRect.offsetMin = new Vector2(10, 0);
        textRect.offsetMax = new Vector2(-10, 0);
        
        // Create placeholder
        GameObject placeholderObj = new GameObject("Placeholder");
        placeholderObj.transform.SetParent(fieldObj.transform, false);
        var placeholder = placeholderObj.AddComponent<UnityEngine.UI.Text>();
        placeholder.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        placeholder.fontSize = 16;
        placeholder.color = new Color(0.5f, 0.5f, 0.5f, 1f);
        placeholder.alignment = TextAnchor.MiddleLeft;
        placeholder.text = $"Enter {labelText.ToLower()}...";
        
        var placeholderRect = placeholderObj.GetComponent<RectTransform>();
        placeholderRect.anchorMin = Vector2.zero;
        placeholderRect.anchorMax = Vector2.one;
        placeholderRect.anchoredPosition = Vector2.zero;
        placeholderRect.sizeDelta = Vector2.zero;
        placeholderRect.offsetMin = new Vector2(10, 0);
        placeholderRect.offsetMax = new Vector2(-10, 0);
        
        // Connect components
        inputField.textComponent = text;
        inputField.placeholder = placeholder;
        
        return fieldObj;
    }
    
    private GameObject CreateButton(GameObject parent, string name, string buttonText, float minY, float maxY)
    {
        GameObject buttonObj = new GameObject(name);
        buttonObj.transform.SetParent(parent.transform, false);
        
        // Add background image
        var image = buttonObj.AddComponent<UnityEngine.UI.Image>();
        image.color = new Color(0.2f, 0.6f, 0.2f, 1f); // Green button
        
        var buttonRect = buttonObj.GetComponent<RectTransform>();
        buttonRect.anchorMin = new Vector2(0.25f, minY);
        buttonRect.anchorMax = new Vector2(0.75f, maxY);
        buttonRect.anchoredPosition = Vector2.zero;
        buttonRect.sizeDelta = Vector2.zero;
        
        // Add Button component
        var button = buttonObj.AddComponent<UnityEngine.UI.Button>();
        button.targetGraphic = image;
        
        // Create text
        GameObject textObj = new GameObject("Text");
        textObj.transform.SetParent(buttonObj.transform, false);
        var text = textObj.AddComponent<UnityEngine.UI.Text>();
        text.text = buttonText;
        text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        text.fontSize = 18;
        text.color = Color.white;
        text.alignment = TextAnchor.MiddleCenter;
        text.fontStyle = FontStyle.Bold;
        
        var textRect = textObj.GetComponent<RectTransform>();
        textRect.anchorMin = Vector2.zero;
        textRect.anchorMax = Vector2.one;
        textRect.anchoredPosition = Vector2.zero;
        textRect.sizeDelta = Vector2.zero;
        
        return buttonObj;
    }
    
    private void SetFieldValue(object target, string fieldName, object value)
    {
        var field = target.GetType().GetField(fieldName, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
        if (field != null)
        {
            field.SetValue(target, value);
            Debug.Log($"Set {fieldName} field in {target.GetType().Name}");
        }
        else
        {
            Debug.LogWarning($"Could not find field {fieldName} in {target.GetType().Name}");
        }
    }

    private void ConnectUIManagerReferences(GameObject inventoryPanel, GameObject loginPanel)
    {
        // Find the UIManager component
        UIManager uiManager = FindObjectOfType<UIManager>();
        
        if (uiManager != null)
        {
            // Set the InventoryPanel field
            SetFieldValue(uiManager, "InventoryPanel", inventoryPanel);
            
            // Set the LoginPanel field  
            SetFieldValue(uiManager, "LoginPanel", loginPanel);
            
            // Set the LoginUIComponent field
            var loginUIComponent = loginPanel.GetComponent<LoginUI>();
            if (loginUIComponent != null)
            {
                SetFieldValue(uiManager, "LoginUIComponent", loginUIComponent);
                Debug.Log("LoginUI component connected to UIManager");
            }
            else
            {
                Debug.LogWarning("LoginUI component not found on login panel");
            }
        }
        else
        {
            Debug.LogWarning("UIManager not found - panels will not be connected");
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
            "• Proper camera positioning\n" +
            "• Brown reference box with collision", 
            MessageType.Info);
    }
}
#endif