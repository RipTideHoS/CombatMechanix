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
    public bool CreateTestEnemy = false; // Disable local enemy creation - use network enemies
    public bool SetupEnemyNetworkManager = true;
    public bool SetupHealthBarSystem = true;
    
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

        // 7. Create Test Enemy (disabled by default - use network enemies)
        if (CreateTestEnemy)
            CreateTestEnemyObject();

        // 8. Setup Enemy Network Manager
        if (SetupEnemyNetworkManager)
            SetupEnemyNetworkManagerComponent();

        // 9. Create basic UI system (after GameManager exists)
        CreateBasicUI();

        // 10. Setup Health Bar System
        if (SetupHealthBarSystem)
            SetupHealthBarSystemComponents();

        // 11. Setup Level Up Banner and Audio System
        SetupLevelUpSystem();

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
        GameObject ground = GameObject.Find("Ground");
        
        // Check if ground already exists
        if (ground != null)
        {
            Debug.Log("Ground already exists in scene - updating color to grey");
            
            // Update existing ground color
            Renderer existingRenderer = ground.GetComponent<Renderer>();
            if (existingRenderer != null)
            {
                existingRenderer.material.color = new Color(0.5f, 0.5f, 0.5f); // Grey ground for better contrast
                Debug.Log("Updated existing ground color to grey");
            }
            return;
        }

        Debug.Log("Creating Ground plane...");
        ground = GameObject.CreatePrimitive(PrimitiveType.Plane);
        ground.name = "Ground";
        ground.transform.position = Vector3.zero;
        ground.transform.localScale = new Vector3(10, 1, 10); // 100x100 units

        // Add a simple material/color
        Renderer newRenderer = ground.GetComponent<Renderer>();
        if (newRenderer != null)
        {
            newRenderer.material.color = new Color(0.5f, 0.5f, 0.5f); // Grey ground for better contrast
        }
        
        Debug.Log("Created new grey ground plane");
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

    private void CreateTestEnemyObject()
    {
        // Check if TestEnemy already exists
        if (GameObject.Find("TestEnemy") != null)
        {
            Debug.Log("TestEnemy already exists in scene");
            return;
        }

        Debug.Log("Creating TestEnemy GameObject...");
        
        // Create the enemy GameObject
        GameObject enemyObj = GameObject.CreatePrimitive(PrimitiveType.Cube);
        enemyObj.name = "TestEnemy";
        
        // Add the EnemyBase component
        EnemyBase enemyBase = enemyObj.AddComponent<EnemyBase>();
        
        // Configure enemy stats
        enemyBase.EnemyName = "Test Enemy";
        enemyBase.EnemyType = "Basic";
        enemyBase.Level = 1;
        enemyBase.BaseHealth = 100f;
        enemyBase.BaseDamage = 15f;
        
        // Make it red
        Renderer enemyRenderer = enemyObj.GetComponent<Renderer>();
        if (enemyRenderer != null)
        {
            Material redMaterial = new Material(Shader.Find("Standard"));
            redMaterial.color = Color.red;
            enemyRenderer.material = redMaterial;
        }
        
        // Position it next to the reference box
        GameObject referenceBox = GameObject.Find("ReferenceBox");
        if (referenceBox != null)
        {
            // Position enemy 3 units to the right of reference box
            Vector3 enemyPosition = referenceBox.transform.position + Vector3.right * 3f;
            enemyObj.transform.position = enemyPosition;
        }
        else
        {
            // Fallback position if reference box doesn't exist
            GameObject localPlayer = GameObject.Find("LocalPlayer");
            if (localPlayer != null)
            {
                Vector3 enemyPosition = localPlayer.transform.position + new Vector3(5f, 0f, 5f);
                enemyObj.transform.position = enemyPosition;
            }
            else
            {
                enemyObj.transform.position = new Vector3(5f, 0.5f, 5f);
            }
        }
        
        // Make sure it's on the ground
        Vector3 pos = enemyObj.transform.position;
        pos.y = 0.5f; // Half cube height above ground
        enemyObj.transform.position = pos;
        
        // Ensure collision is enabled (cube primitive already has a BoxCollider)
        BoxCollider enemyCollider = enemyObj.GetComponent<BoxCollider>();
        if (enemyCollider != null)
        {
            enemyCollider.isTrigger = false; // Solid collision
        }
        
        Debug.Log($"TestEnemy created at position: {enemyObj.transform.position} with red color and EnemyBase component");
        Debug.Log($"Enemy stats - Level: {enemyBase.Level}, Health: {enemyBase.BaseHealth}, Damage: {enemyBase.BaseDamage}");
    }

    private void SetupEnemyNetworkManagerComponent()
    {
        // Check if EnemyNetworkManager already exists
        if (FindObjectOfType<EnemyNetworkManager>() != null)
        {
            Debug.Log("EnemyNetworkManager already exists in scene");
            return;
        }

        Debug.Log("Setting up EnemyNetworkManager...");
        
        // Add EnemyNetworkManager to the GameManager GameObject if it exists
        GameObject gameManagerObj = GameObject.Find("GameManager");
        if (gameManagerObj != null)
        {
            var enemyNetworkManager = gameManagerObj.AddComponent<EnemyNetworkManager>();
            enemyNetworkManager.EnableDebugLogging = true;
            Debug.Log("EnemyNetworkManager added to GameManager GameObject");
        }
        else
        {
            // Create a dedicated GameObject for EnemyNetworkManager
            GameObject enemyManagerObj = new GameObject("EnemyNetworkManager");
            var enemyNetworkManager = enemyManagerObj.AddComponent<EnemyNetworkManager>();
            enemyNetworkManager.EnableDebugLogging = true;
            
            // Make it persistent
            DontDestroyOnLoad(enemyManagerObj);
            Debug.Log("EnemyNetworkManager created as standalone GameObject");
        }
        
        Debug.Log("EnemyNetworkManager setup complete - ready to receive enemy data from server");
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
        
        // Add Image component for background - dark box style like HealthBarDebugger
        var image = inventoryPanel.AddComponent<UnityEngine.UI.Image>();
        image.color = new Color(0.2f, 0.2f, 0.2f, 0.85f); // Dark transparent background like Unity's box GUI
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
        titleText.color = Color.white; // White text on dark background
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
        contentText.color = Color.white; // White text on dark background
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
        
        // Add Image component for background - dark box style like HealthBarDebugger
        var image = loginPanel.AddComponent<UnityEngine.UI.Image>();
        image.color = new Color(0.15f, 0.15f, 0.15f, 0.85f); // Dark transparent background like Unity's box GUI
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
        containerImage.color = new Color(0.25f, 0.25f, 0.25f, 0.95f); // Dark box style background like Unity GUI
        
        var containerRect = loginContainer.GetComponent<RectTransform>();
        containerRect.anchorMin = new Vector2(0.3f, 0.2f); // Wider and taller container
        containerRect.anchorMax = new Vector2(0.7f, 0.8f);
        containerRect.anchoredPosition = Vector2.zero;
        containerRect.sizeDelta = Vector2.zero;
        
        // Create title
        GameObject titleObj = new GameObject("Title");
        titleObj.transform.SetParent(loginContainer.transform, false);
        var titleText = titleObj.AddComponent<UnityEngine.UI.Text>();
        titleText.text = "COMBAT MECHANIX LOGIN";
        titleText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        titleText.fontSize = 24;
        titleText.color = Color.white; // White text on dark background
        titleText.alignment = TextAnchor.MiddleCenter;
        titleText.fontStyle = FontStyle.Bold;
        
        var titleRect = titleObj.GetComponent<RectTransform>();
        titleRect.anchorMin = new Vector2(0, 0.8f);
        titleRect.anchorMax = new Vector2(1, 1f);
        titleRect.anchoredPosition = Vector2.zero;
        titleRect.sizeDelta = Vector2.zero;
        
        // Create username input field - even larger height for better readability
        GameObject usernameField = CreateInputField(loginContainer, "UsernameField", "Username", 0.55f, 0.7f);
        var usernameInput = usernameField.GetComponent<UnityEngine.UI.InputField>();
        usernameInput.placeholder.GetComponent<UnityEngine.UI.Text>().text = "Enter username...";
        
        // Create password input field - even larger height, positioned lower
        GameObject passwordField = CreateInputField(loginContainer, "PasswordField", "Password", 0.35f, 0.5f);
        var passwordInput = passwordField.GetComponent<UnityEngine.UI.InputField>();
        passwordInput.contentType = UnityEngine.UI.InputField.ContentType.Password;
        passwordInput.placeholder.GetComponent<UnityEngine.UI.Text>().text = "Enter password...";
        
        // Create login button - positioned lower to avoid overlap
        GameObject loginButton = CreateButton(loginContainer, "LoginButton", "LOGIN", 0.15f, 0.28f);
        
        // Create status text
        GameObject statusObj = new GameObject("StatusText");
        statusObj.transform.SetParent(loginContainer.transform, false);
        var statusText = statusObj.AddComponent<UnityEngine.UI.Text>();
        statusText.text = "Enter your credentials to login";
        statusText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        statusText.fontSize = 16;
        statusText.color = Color.cyan; // Bright cyan text for visibility on dark background
        statusText.alignment = TextAnchor.MiddleCenter;
        
        var statusRect = statusObj.GetComponent<RectTransform>();
        statusRect.anchorMin = new Vector2(0.05f, 0.02f);
        statusRect.anchorMax = new Vector2(0.95f, 0.12f);
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

    private void SetupHealthBarSystemComponents()
    {
        Debug.Log("=== Setting up Health Bar System ===");

        try
        {
            // 1. Create Enemy Health Bar Prefab
            GameObject enemyHealthBarPrefab = CreateEnemyHealthBarPrefab();
            if (enemyHealthBarPrefab == null)
            {
                Debug.LogError("Failed to create Enemy Health Bar Prefab!");
                return;
            }

            // 2. Create Player Health Bar Prefab
            GameObject playerHealthBarPrefab = CreatePlayerHealthBarPrefab();
            if (playerHealthBarPrefab == null)
            {
                Debug.LogError("Failed to create Player Health Bar Prefab!");
                return;
            }

            // 3. Setup Health Bar Manager
            Debug.Log("*** ABOUT TO CALL SetupHealthBarManager ***");
            SetupHealthBarManager(enemyHealthBarPrefab, playerHealthBarPrefab);
            Debug.Log("*** FINISHED CALLING SetupHealthBarManager ***");

            // 4. Setup Player Health UI
            Debug.Log("*** ABOUT TO CALL SetupPlayerHealthUI ***");
            SetupPlayerHealthUI();
            Debug.Log("*** FINISHED CALLING SetupPlayerHealthUI ***");

            // 5. Connect to existing UI Manager
            ConnectHealthUIToUIManager();

            // 6. Add health bar debugger for testing
            SetupHealthBarDebugger();

            // 7. Add health bar tester for visibility debugging
            SetupHealthBarTester();

            // 8. Setup Enemy Damage Text Manager
            Debug.Log("*** ABOUT TO CALL SetupEnemyDamageTextManager ***");
            SetupEnemyDamageTextManager();
            Debug.Log("*** FINISHED CALLING SetupEnemyDamageTextManager ***");

            Debug.Log("Health Bar System setup complete!");
            Debug.Log("- Enemy health bars will appear automatically above enemies");
            Debug.Log("- Player health bar is integrated into main UI");
            Debug.Log("- Health bars are performance optimized with pooling");
            Debug.Log("- HealthBarDebugger added - press E to create test enemies, D to damage them");
            Debug.Log("- EnemyDamageTextManager added - floating damage text with color coding");
            Debug.Log("- FloatingDamageTextTester added - press 1/2/3/4 keys to test damage text");
        }
        catch (System.Exception ex)
        {
            Debug.LogError($"Failed to setup Health Bar System: {ex.Message}");
            Debug.LogError($"Stack trace: {ex.StackTrace}");
        }
    }

    private GameObject CreateEnemyHealthBarPrefab()
    {
        Debug.Log("Creating Enemy Health Bar Prefab...");

        // Create main prefab GameObject
        GameObject prefab = new GameObject("EnemyHealthBarPrefab");

        // Create Canvas for world space rendering
        GameObject canvasObj = new GameObject("HealthBarCanvas");
        canvasObj.transform.SetParent(prefab.transform, false);
        
        Canvas canvas = canvasObj.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.WorldSpace;
        canvas.sortingOrder = 10;
        
        var canvasScaler = canvasObj.AddComponent<UnityEngine.UI.CanvasScaler>();
        canvasScaler.dynamicPixelsPerUnit = 100;
        
        var canvasRect = canvasObj.GetComponent<RectTransform>();
        canvasRect.sizeDelta = new Vector2(200, 20); // Reduced height by half
        canvasObj.transform.localScale = Vector3.one * 0.02f; // Smaller, more appropriate scale

        // Create Background
        GameObject backgroundObj = new GameObject("Background");
        backgroundObj.transform.SetParent(canvasObj.transform, false);
        
        var backgroundImage = backgroundObj.AddComponent<UnityEngine.UI.Image>();
        backgroundImage.color = new Color(0.2f, 0.2f, 0.2f, 0.9f); // More visible dark background
        
        var backgroundRect = backgroundObj.GetComponent<RectTransform>();
        backgroundRect.anchorMin = Vector2.zero;
        backgroundRect.anchorMax = Vector2.one;
        backgroundRect.anchoredPosition = Vector2.zero;
        backgroundRect.sizeDelta = Vector2.zero;

        // Create Health Slider
        GameObject sliderObj = new GameObject("HealthSlider");
        sliderObj.transform.SetParent(canvasObj.transform, false);
        
        var healthSlider = sliderObj.AddComponent<UnityEngine.UI.Slider>();
        healthSlider.minValue = 0f;
        healthSlider.maxValue = 1f;
        healthSlider.value = 1f;
        
        var sliderRect = sliderObj.GetComponent<RectTransform>();
        sliderRect.anchorMin = new Vector2(0.02f, 0.1f); // Very small transparent border
        sliderRect.anchorMax = new Vector2(0.98f, 0.9f);
        sliderRect.anchoredPosition = Vector2.zero;
        sliderRect.sizeDelta = Vector2.zero;

        // Create Slider Background
        GameObject sliderBg = new GameObject("Background");
        sliderBg.transform.SetParent(sliderObj.transform, false);
        
        var sliderBgImage = sliderBg.AddComponent<UnityEngine.UI.Image>();
        sliderBgImage.color = new Color(0.3f, 0.3f, 0.3f, 1f); // Dark gray
        sliderBgImage.type = UnityEngine.UI.Image.Type.Sliced;
        
        var sliderBgRect = sliderBg.GetComponent<RectTransform>();
        sliderBgRect.anchorMin = Vector2.zero;
        sliderBgRect.anchorMax = Vector2.one;
        sliderBgRect.anchoredPosition = Vector2.zero;
        sliderBgRect.sizeDelta = Vector2.zero;

        // Create Fill Area
        GameObject fillArea = new GameObject("Fill Area");
        fillArea.transform.SetParent(sliderObj.transform, false);
        
        // Add RectTransform component (empty GameObjects need this for UI)
        if (fillArea.GetComponent<RectTransform>() == null)
        {
            Debug.Log("Adding RectTransform to Fill Area");
            fillArea.AddComponent<RectTransform>();
        }
        var fillAreaRect = fillArea.GetComponent<RectTransform>();
        Debug.Log($"Fill Area RectTransform: {fillAreaRect != null}");
        fillAreaRect.anchorMin = Vector2.zero;
        fillAreaRect.anchorMax = Vector2.one;
        fillAreaRect.anchoredPosition = Vector2.zero;
        fillAreaRect.sizeDelta = Vector2.zero;

        // Create Fill
        GameObject fill = new GameObject("Fill");
        fill.transform.SetParent(fillArea.transform, false);
        
        var fillImage = fill.AddComponent<UnityEngine.UI.Image>();
        fillImage.color = Color.green; // Start with full health color
        
        // Use Simple type instead of Filled for slider compatibility
        fillImage.type = UnityEngine.UI.Image.Type.Simple;
        
        // Get RectTransform (automatically added with Image component)
        var fillRect = fill.GetComponent<RectTransform>();
        fillRect.anchorMin = Vector2.zero;
        fillRect.anchorMax = Vector2.one;
        fillRect.anchoredPosition = Vector2.zero;
        fillRect.sizeDelta = Vector2.zero;

        // No health text - removed for cleaner appearance
        GameObject textObj = null;
        UnityEngine.UI.Text healthText = null;

        // Connect slider components
        healthSlider.fillRect = fillRect;
        healthSlider.targetGraphic = fillImage;

        // Add EnemyHealthBar component
        var enemyHealthBar = prefab.AddComponent<EnemyHealthBar>();
        SetFieldValue(enemyHealthBar, "HealthBarCanvas", canvas);
        SetFieldValue(enemyHealthBar, "HealthSlider", healthSlider);
        SetFieldValue(enemyHealthBar, "HealthText", healthText);
        SetFieldValue(enemyHealthBar, "HealthFillImage", fillImage);
        SetFieldValue(enemyHealthBar, "BackgroundImage", backgroundImage);

        // Add CanvasGroup for fading
        prefab.AddComponent<CanvasGroup>();

        // Make prefab inactive so it doesn't appear in scene
        prefab.SetActive(false);
        
        // Make the prefab persistent across scene loads
        DontDestroyOnLoad(prefab);

        Debug.Log("Enemy Health Bar Prefab created with Canvas, Slider, and EnemyHealthBar component");
        return prefab;
    }

    private GameObject CreatePlayerHealthBarPrefab()
    {
        Debug.Log("Creating Player Health Bar Prefab...");

        // This is similar to enemy health bar but designed for optional world-space display above player
        GameObject prefab = new GameObject("PlayerHealthBarPrefab");

        // Create Canvas for world space rendering
        GameObject canvasObj = new GameObject("PlayerHealthCanvas");
        canvasObj.transform.SetParent(prefab.transform, false);
        
        Canvas canvas = canvasObj.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.WorldSpace;
        canvas.sortingOrder = 15; // Higher than enemy health bars
        
        var canvasRect = canvasObj.GetComponent<RectTransform>();
        canvasRect.sizeDelta = new Vector2(250, 40); // Slightly larger for player
        canvasObj.transform.localScale = Vector3.one * 0.008f; // Slightly smaller scale

        // Create similar structure to enemy health bar but with player-specific styling
        GameObject backgroundObj = new GameObject("Background");
        backgroundObj.transform.SetParent(canvasObj.transform, false);
        
        var backgroundImage = backgroundObj.AddComponent<UnityEngine.UI.Image>();
        backgroundImage.color = new Color(0.1f, 0.1f, 0.3f, 0.8f); // Slightly blue tint for player
        
        var backgroundRect = backgroundObj.GetComponent<RectTransform>();
        backgroundRect.anchorMin = Vector2.zero;
        backgroundRect.anchorMax = Vector2.one;
        backgroundRect.anchoredPosition = Vector2.zero;
        backgroundRect.sizeDelta = Vector2.zero;

        // Create slider components (similar to enemy but with blue theme)
        GameObject sliderObj = new GameObject("HealthSlider");
        sliderObj.transform.SetParent(canvasObj.transform, false);
        
        var healthSlider = sliderObj.AddComponent<UnityEngine.UI.Slider>();
        healthSlider.minValue = 0f;
        healthSlider.maxValue = 1f;
        healthSlider.value = 1f;
        
        var sliderRect = sliderObj.GetComponent<RectTransform>();
        sliderRect.anchorMin = new Vector2(0.05f, 0.3f);
        sliderRect.anchorMax = new Vector2(0.95f, 0.7f);
        sliderRect.anchoredPosition = Vector2.zero;
        sliderRect.sizeDelta = Vector2.zero;

        // Add fill components
        GameObject fillArea = new GameObject("Fill Area");
        fillArea.transform.SetParent(sliderObj.transform, false);
        
        // Add RectTransform component (empty GameObjects need this for UI)
        if (fillArea.GetComponent<RectTransform>() == null)
        {
            Debug.Log("Adding RectTransform to Fill Area");
            fillArea.AddComponent<RectTransform>();
        }
        var fillAreaRect = fillArea.GetComponent<RectTransform>();
        Debug.Log($"Fill Area RectTransform: {fillAreaRect != null}");
        fillAreaRect.anchorMin = Vector2.zero;
        fillAreaRect.anchorMax = Vector2.one;
        fillAreaRect.anchoredPosition = Vector2.zero;
        fillAreaRect.sizeDelta = Vector2.zero;

        GameObject fill = new GameObject("Fill");
        fill.transform.SetParent(fillArea.transform, false);
        
        var fillImage = fill.AddComponent<UnityEngine.UI.Image>();
        fillImage.color = new Color(0.2f, 0.7f, 1f, 1f); // Blue for player health
        fillImage.type = UnityEngine.UI.Image.Type.Filled;
        fillImage.fillMethod = UnityEngine.UI.Image.FillMethod.Horizontal;
        
        // Get RectTransform (automatically added with Image component)
        var fillRect = fill.GetComponent<RectTransform>();
        fillRect.anchorMin = Vector2.zero;
        fillRect.anchorMax = Vector2.one;
        fillRect.anchoredPosition = Vector2.zero;
        fillRect.sizeDelta = Vector2.zero;

        healthSlider.fillRect = fillRect;
        healthSlider.targetGraphic = fillImage;

        // Make prefab inactive so it doesn't appear in scene
        prefab.SetActive(false);
        
        // Make the prefab persistent across scene loads
        DontDestroyOnLoad(prefab);

        Debug.Log("Player Health Bar Prefab created");
        return prefab;
    }

    private void SetupHealthBarManager(GameObject enemyHealthBarPrefab, GameObject playerHealthBarPrefab)
    {
        Debug.Log("Setting up Health Bar Manager...");

        // Check if HealthBarManager already exists
        if (FindObjectOfType<HealthBarManager>() != null)
        {
            Debug.Log("HealthBarManager already exists in scene");
            return;
        }

        // Create HealthBarManager GameObject
        GameObject healthBarManagerObj = new GameObject("HealthBarManager");
        var healthBarManager = healthBarManagerObj.AddComponent<HealthBarManager>();

        // Store prefabs as children of HealthBarManager to ensure they persist
        enemyHealthBarPrefab.transform.SetParent(healthBarManagerObj.transform);
        playerHealthBarPrefab.transform.SetParent(healthBarManagerObj.transform);

        // Configure the health bar manager with direct field assignment
        healthBarManager.EnemyHealthBarPrefab = enemyHealthBarPrefab;
        healthBarManager.PlayerHealthBarPrefab = playerHealthBarPrefab;
        healthBarManager.AutoManageEnemyHealthBars = true;
        healthBarManager.EnableHealthBarPooling = true;
        healthBarManager.InitialPoolSize = 10;
        healthBarManager.MaxPoolSize = 30;
        healthBarManager.UpdateRate = 0.1f;
        healthBarManager.MaxViewDistance = 50f;
        healthBarManager.HideWhenBehindObjects = true;

        // Verify the assignment worked
        Debug.Log($"HealthBarManager EnemyHealthBarPrefab assigned: {healthBarManager.EnemyHealthBarPrefab != null}");
        Debug.Log($"HealthBarManager PlayerHealthBarPrefab assigned: {healthBarManager.PlayerHealthBarPrefab != null}");

        // Make it persistent
        DontDestroyOnLoad(healthBarManagerObj);

        // Force initialize the health bar manager to start working immediately
        healthBarManager.SetAutoManagement(true);

        Debug.Log("HealthBarManager created and configured");
        Debug.Log("- Auto-management enabled for enemy health bars");
        Debug.Log("- Object pooling enabled with 10 initial, 30 max pool size");
        Debug.Log("- 50 unit view distance with occlusion checking");
        Debug.Log($"- Enemy prefab name: {enemyHealthBarPrefab.name}");
        Debug.Log($"- Player prefab name: {playerHealthBarPrefab.name}");
    }

    private void SetupPlayerHealthUI()
    {
        Debug.Log("*** SETTING UP PLAYER HEALTH UI - START ***");

        // Find the main Canvas (Screen Space Overlay only)
        Canvas[] allCanvases = FindObjectsOfType<Canvas>();
        Debug.Log($"[SetupPlayerHealthUI] Found {allCanvases.Length} canvases in scene:");
        foreach (Canvas c in allCanvases)
        {
            Debug.Log($"  - Canvas: {c.name}, renderMode: {c.renderMode}, active: {c.gameObject.activeInHierarchy}, sortingOrder: {c.sortingOrder}");
        }
        
        // Find the FIRST ScreenSpaceOverlay canvas, prioritizing the main UI canvas
        Canvas mainCanvas = null;
        foreach (Canvas c in allCanvases)
        {
            if (c.renderMode == RenderMode.ScreenSpaceOverlay && c.name == "Canvas")
            {
                mainCanvas = c;
                Debug.Log($"[SetupPlayerHealthUI] Found main UI Canvas: {c.name}");
                break;
            }
        }
        
        // If we didn't find the main "Canvas", take any ScreenSpaceOverlay canvas
        if (mainCanvas == null)
        {
            foreach (Canvas c in allCanvases)
            {
                if (c.renderMode == RenderMode.ScreenSpaceOverlay)
                {
                    mainCanvas = c;
                    Debug.Log($"[SetupPlayerHealthUI] Using fallback ScreenSpaceOverlay Canvas: {c.name}");
                    break;
                }
            }
        }
        
        if (mainCanvas == null)
        {
            Debug.LogError("*** EARLY RETURN: No ScreenSpaceOverlay Canvas found for Player Health UI! Player health bar will not be created.");
            Debug.LogError("Available canvases and their render modes:");
            foreach (Canvas c in allCanvases)
            {
                Debug.LogError($"  - {c.name}: {c.renderMode}");
            }
            return;
        }
        
        Debug.Log($"[SetupPlayerHealthUI] Selected canvas: {mainCanvas.name} with renderMode: {mainCanvas.renderMode}, sortingOrder: {mainCanvas.sortingOrder}");

        // Check if PlayerHealthUI already exists
        if (FindObjectOfType<PlayerHealthUI>() != null)
        {
            Debug.LogError("*** EARLY RETURN: PlayerHealthUI already exists in scene");
            return;
        }

        // Verify canvas is truly in ScreenSpaceOverlay mode before proceeding
        if (mainCanvas.renderMode != RenderMode.ScreenSpaceOverlay)
        {
            Debug.LogError($"*** EARLY RETURN: Selected canvas {mainCanvas.name} is not in ScreenSpaceOverlay mode! RenderMode: {mainCanvas.renderMode}");
            return;
        }

        Debug.Log($"Canvas validated - proceeding with player health UI creation on {mainCanvas.name}");

        // Create Player Health UI container
        Debug.Log("*** CREATING PlayerHealthUI CONTAINER ***");
        GameObject playerHealthContainer = new GameObject("PlayerHealthUI");
        playerHealthContainer.transform.SetParent(mainCanvas.transform, false);

        // Set up the container's RectTransform to be properly sized and positioned
        var containerRect = playerHealthContainer.GetComponent<RectTransform>();
        if (containerRect == null)
        {
            containerRect = playerHealthContainer.AddComponent<RectTransform>();
        }
        
        // Position the PlayerHealthUI container at bottom center for the health bar
        containerRect.anchorMin = new Vector2(0.3f, 0.02f);
        containerRect.anchorMax = new Vector2(0.7f, 0.08f);
        containerRect.anchoredPosition = Vector2.zero;
        containerRect.sizeDelta = Vector2.zero;

        // Verify the container is parented correctly
        Debug.Log($"PlayerHealthUI container created, parent: {playerHealthContainer.transform.parent?.name}");
        Debug.Log($"Container anchors: {containerRect.anchorMin} to {containerRect.anchorMax}");
        Debug.Log($"Container active: {playerHealthContainer.activeInHierarchy}");

        // Add PlayerHealthUI component
        var playerHealthUI = playerHealthContainer.AddComponent<PlayerHealthUI>();

        // Create health slider for main UI
        Debug.Log("*** ABOUT TO CALL CreateMainUIHealthSlider ***");
        GameObject healthSliderObj = CreateMainUIHealthSlider(playerHealthContainer);
        Debug.Log("*** FINISHED CALLING CreateMainUIHealthSlider ***");
        
        // Verify the health slider object was created
        if (healthSliderObj == null)
        {
            Debug.LogError("Failed to create healthSliderObj in CreateMainUIHealthSlider!");
            return;
        }
        
        var healthSlider = healthSliderObj.GetComponent<UnityEngine.UI.Slider>();
        var healthText = healthSliderObj.GetComponentInChildren<UnityEngine.UI.Text>();
        
        // Find the fill image and background image from the simplified structure
        var healthFillImage = healthSliderObj.transform.Find("HealthFill")?.GetComponent<UnityEngine.UI.Image>();
        var backgroundImage = healthSliderObj.transform.Find("InnerBackground")?.GetComponent<UnityEngine.UI.Image>();

        // Verify components were created correctly
        Debug.Log($"HealthSliderObj created: {healthSliderObj != null}");
        Debug.Log($"Health slider component: {healthSlider != null}");
        Debug.Log($"Health text component: {healthText != null}");
        Debug.Log($"Health fill image component: {healthFillImage != null}");
        
        if (healthSlider == null)
        {
            Debug.LogError("HealthSlider component is null! Cannot configure PlayerHealthUI.");
            return;
        }

        // Configure PlayerHealthUI component with correct field names
        SetFieldValue(playerHealthUI, "HealthSlider", healthSlider);
        SetFieldValue(playerHealthUI, "HealthText", healthText);
        SetFieldValue(playerHealthUI, "HealthFillImage", healthFillImage);
        SetFieldValue(playerHealthUI, "HealthBackgroundImage", backgroundImage);
        SetFieldValue(playerHealthUI, "HealthBarContainer", playerHealthContainer);
        
        // Debug the field assignments
        Debug.Log($"Setting HealthSlider: {healthSlider != null}");
        Debug.Log($"Setting HealthText: {healthText != null}");
        Debug.Log($"Setting HealthFillImage: {healthFillImage != null}");
        Debug.Log($"Setting HealthBackgroundImage: {backgroundImage != null}");
        SetFieldValue(playerHealthUI, "ShowAbovePlayer", false); // Disabled by default - use main UI only
        SetFieldValue(playerHealthUI, "PlayerHealthBarPrefab", null); // Don't assign prefab to prevent world health bar
        SetFieldValue(playerHealthUI, "AnimateHealthChanges", true);
        SetFieldValue(playerHealthUI, "ShowDamageFlash", true);
        SetFieldValue(playerHealthUI, "EnableLowHealthWarning", true);

        Debug.Log("PlayerHealthUI created with main UI health bar - checking final hierarchy...");
        Debug.Log($"Final hierarchy: Canvas->{playerHealthContainer.name}->{healthSliderObj.name}");
        
        // Safe access to healthSlider properties
        if (healthSlider != null)
        {
            Debug.Log($"Health slider value: {healthSlider.value}, min: {healthSlider.minValue}, max: {healthSlider.maxValue}");
        }
        else
        {
            Debug.LogError("healthSlider is null, cannot log slider values");
        }
        
        // Force the PlayerHealthUI to initialize immediately using reflection (since Start() hasn't been called yet)
        var setupMethod = playerHealthUI.GetType().GetMethod("SetupUI", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        if (setupMethod != null)
        {
            setupMethod.Invoke(playerHealthUI, null);
            Debug.Log("Forced PlayerHealthUI.SetupUI() call");
        }
        
        var updateMethod = playerHealthUI.GetType().GetMethod("UpdateHealthDisplay", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        if (updateMethod != null)
        {
            updateMethod.Invoke(playerHealthUI, null);
            Debug.Log("Forced PlayerHealthUI.UpdateHealthDisplay() call");
        }
        
        // Verify that the health UI components are all active and visible
        Debug.Log($"PlayerHealthContainer active: {playerHealthContainer.activeInHierarchy}");
        Debug.Log($"HealthSlider active: {healthSliderObj.activeInHierarchy}");
        Debug.Log($"Canvas render mode: {mainCanvas.renderMode}");
        Debug.Log($"Canvas sorting order: {mainCanvas.sortingOrder}");
        
        // Position test confirmed - health bar components should be visible
        
        // Final verification
        Debug.Log("=== PLAYER HEALTH UI SETUP COMPLETE ===");
    }

    private void SetupLevelUpSystem()
    {
        Debug.Log("Setting up Level Up System...");

        // 1. Create AudioManager
        SetupAudioManager();

        // 2. Create Level Up Banner
        SetupLevelUpBanner();

        Debug.Log("Level Up System setup complete");
    }

    private void SetupAudioManager()
    {
        Debug.Log("Setting up Audio Manager...");

        // Check if AudioManager already exists
        if (AudioManager.Instance != null)
        {
            Debug.Log("AudioManager already exists in scene");
            return;
        }

        // Create AudioManager GameObject
        GameObject audioManagerObj = new GameObject("AudioManager");
        
        // Add AudioManager component
        var audioManager = audioManagerObj.AddComponent<AudioManager>();
        
        // Initialize with placeholder sounds for testing
        audioManager.InitializePlaceholderSounds();
        
        // Make it persistent across scenes
        DontDestroyOnLoad(audioManagerObj);

        Debug.Log("AudioManager created with placeholder sounds for testing");
    }

    private void SetupLevelUpBanner()
    {
        Debug.Log("Setting up Level Up Banner...");

        // Check if LevelUpBanner already exists
        if (FindObjectOfType<LevelUpBanner>() != null)
        {
            Debug.Log("LevelUpBanner already exists in scene");
            return;
        }

        // Find the main UI Canvas
        Canvas[] allCanvases = FindObjectsOfType<Canvas>();
        Canvas mainCanvas = null;
        
        foreach (Canvas c in allCanvases)
        {
            if (c.renderMode == RenderMode.ScreenSpaceOverlay)
            {
                mainCanvas = c;
                Debug.Log($"Found UI Canvas for Level Up Banner: {c.name}");
                break;
            }
        }

        if (mainCanvas == null)
        {
            Debug.LogError("No ScreenSpaceOverlay Canvas found for Level Up Banner!");
            return;
        }

        // Create Level Up Banner GameObject
        GameObject bannerObj = new GameObject("LevelUpBanner");
        bannerObj.transform.SetParent(mainCanvas.transform, false);
        
        // Add LevelUpBanner component
        var levelUpBanner = bannerObj.AddComponent<LevelUpBanner>();
        
        // Configure banner settings
        levelUpBanner.DisplayDuration = 4f;
        levelUpBanner.FadeInDuration = 0.5f;
        levelUpBanner.FadeOutDuration = 0.5f;
        levelUpBanner.BannerTextFormat = "LEVEL {0} ACHIEVED!";
        levelUpBanner.BannerColor = Color.yellow;
        levelUpBanner.FontSize = 48;
        levelUpBanner.FontStyle = FontStyle.Bold;
        levelUpBanner.PlaySoundEffect = true;
        levelUpBanner.SoundVolume = 0.8f;

        // Set high sorting order to appear above other UI
        bannerObj.transform.SetSiblingIndex(mainCanvas.transform.childCount - 1);

        Debug.Log("LevelUpBanner created and configured");
        Debug.Log("- 4 second display duration with fade in/out");
        Debug.Log("- Yellow text with bold styling");
        Debug.Log("- Sound effects enabled");
        Debug.Log("- Positioned in main UI Canvas");
    }

    private GameObject CreateMainUIHealthSlider(GameObject parent)
    {
        Debug.Log($"[CreateMainUIHealthSlider] *** CREATING PLAYER HEALTH BAR *** with parent: {parent.name}");
        
        // Create health bar container positioned in top-left of screen
        GameObject healthBarObj = new GameObject("MainHealthSlider");
        healthBarObj.transform.SetParent(parent.transform, false);

        // Add RectTransform component for UI GameObject
        if (healthBarObj.GetComponent<RectTransform>() == null)
        {
            healthBarObj.AddComponent<RectTransform>();
        }
        var containerRect = healthBarObj.GetComponent<RectTransform>();
        
        // Fill the parent container completely (PlayerHealthUI container is already positioned)
        containerRect.anchorMin = Vector2.zero;
        containerRect.anchorMax = Vector2.one;
        containerRect.anchoredPosition = Vector2.zero;
        containerRect.sizeDelta = Vector2.zero;
        
        Debug.Log($"[CreateMainUIHealthSlider] Container rect - anchorMin: {containerRect.anchorMin}, anchorMax: {containerRect.anchorMax}");

        // Add dark border background
        var borderImage = healthBarObj.AddComponent<UnityEngine.UI.Image>();
        borderImage.color = new Color(0.1f, 0.1f, 0.1f, 0.8f);  // Dark border
        
        // Create inner background container with padding for border effect
        GameObject innerBg = new GameObject("InnerBackground");
        innerBg.transform.SetParent(healthBarObj.transform, false);
        
        var innerBgImage = innerBg.AddComponent<UnityEngine.UI.Image>();
        innerBgImage.color = new Color(0.2f, 0.2f, 0.2f, 0.9f);  // Dark background
        
        var innerBgRect = innerBg.GetComponent<RectTransform>();
        // Create border by inset positioning - larger margins for visible border
        innerBgRect.anchorMin = new Vector2(0.05f, 0.15f);
        innerBgRect.anchorMax = new Vector2(0.95f, 0.85f);
        innerBgRect.anchoredPosition = Vector2.zero;
        innerBgRect.sizeDelta = Vector2.zero;

        // Create fill inside the inner background (not the main container)
        GameObject fillObj = new GameObject("HealthFill");
        fillObj.transform.SetParent(innerBg.transform, false);

        var fillImage = fillObj.AddComponent<UnityEngine.UI.Image>();
        fillImage.color = Color.green;  // Green health fill
        fillImage.type = UnityEngine.UI.Image.Type.Filled;
        fillImage.fillMethod = UnityEngine.UI.Image.FillMethod.Horizontal;

        var fillRect = fillObj.GetComponent<RectTransform>();
        // Fill most of the inner background, leaving some padding
        fillRect.anchorMin = new Vector2(0.05f, 0.1f);
        fillRect.anchorMax = new Vector2(0.75f, 0.9f);
        fillRect.anchoredPosition = Vector2.zero;
        fillRect.sizeDelta = Vector2.zero;

        // Create health text
        GameObject healthTextObj = new GameObject("HealthText");
        healthTextObj.transform.SetParent(healthBarObj.transform, false);

        var healthText = healthTextObj.AddComponent<UnityEngine.UI.Text>();
        healthText.text = "100/100";
        healthText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        healthText.fontSize = 16;
        healthText.color = Color.white;
        healthText.alignment = TextAnchor.MiddleCenter;
        healthText.fontStyle = FontStyle.Bold;

        var textRect = healthTextObj.GetComponent<RectTransform>();
        textRect.anchorMin = new Vector2(0.72f, 0);
        textRect.anchorMax = new Vector2(1f, 1f);
        textRect.anchoredPosition = Vector2.zero;
        textRect.sizeDelta = Vector2.zero;

        // Create a simple slider component that works with the simple structure
        var healthSlider = healthBarObj.AddComponent<UnityEngine.UI.Slider>();
        healthSlider.minValue = 0f;
        healthSlider.maxValue = 1f;
        healthSlider.value = 1f;
        healthSlider.fillRect = fillRect;
        healthSlider.targetGraphic = fillImage;
        
        // Force everything to be active and visible
        healthBarObj.SetActive(true);
        innerBg.SetActive(true);
        fillObj.SetActive(true);
        healthTextObj.SetActive(true);
        
        // Verify everything is properly configured
        Debug.Log($"[CreateMainUIHealthSlider] Health bar created - checking all components:");
        Debug.Log($"[CreateMainUIHealthSlider] Container active: {healthBarObj.activeInHierarchy}");
        Debug.Log($"[CreateMainUIHealthSlider] Container anchors: {containerRect.anchorMin} to {containerRect.anchorMax}");
        Debug.Log($"[CreateMainUIHealthSlider] Fill active: {fillObj.activeInHierarchy}");
        Debug.Log($"[CreateMainUIHealthSlider] Border color (dark): {borderImage.color}");
        Debug.Log($"[CreateMainUIHealthSlider] Inner background color: {innerBgImage.color}");
        Debug.Log($"[CreateMainUIHealthSlider] Fill color: {fillImage.color}");
        Debug.Log($"[CreateMainUIHealthSlider] Slider value: {healthSlider.value}");
        Debug.Log($"[CreateMainUIHealthSlider] Container parent: {healthBarObj.transform.parent?.name}");

        return healthBarObj;
    }

    private void ConnectHealthUIToUIManager()
    {
        Debug.Log("Connecting Health UI to UIManager...");

        // Find UIManager
        UIManager uiManager = FindObjectOfType<UIManager>();
        if (uiManager == null)
        {
            Debug.LogWarning("UIManager not found - health UI integration skipped");
            return;
        }

        // Find PlayerHealthUI
        PlayerHealthUI playerHealthUI = FindObjectOfType<PlayerHealthUI>();
        if (playerHealthUI != null)
        {
            // Connect PlayerHealthUI to UIManager
            SetFieldValue(uiManager, "PlayerHealthUI", playerHealthUI);

            // Also connect the slider and text to legacy fields for backward compatibility
            if (playerHealthUI.HealthSlider != null)
            {
                SetFieldValue(uiManager, "HealthBar", playerHealthUI.HealthSlider);
            }
            if (playerHealthUI.HealthText != null)
            {
                SetFieldValue(uiManager, "HealthText", playerHealthUI.HealthText);
            }

            Debug.Log("PlayerHealthUI connected to UIManager with backward compatibility");
        }
        else
        {
            Debug.LogWarning("PlayerHealthUI not found - UIManager integration incomplete");
        }
    }

    private void SetupHealthBarDebugger()
    {
        Debug.Log("Setting up Health Bar Debugger...");

        // Check if debugger already exists
        if (FindObjectOfType<HealthBarDebugger>() != null)
        {
            Debug.Log("HealthBarDebugger already exists in scene");
            return;
        }

        // Add debugger to the GameManager or create dedicated GameObject
        GameObject gameManagerObj = GameObject.Find("GameManager");
        if (gameManagerObj != null)
        {
            var debugger = gameManagerObj.AddComponent<HealthBarDebugger>();
            debugger.EnableDebugLogs = false; // Disabled by default for production
            debugger.ShowGUI = false; // Disabled by default for production
            Debug.Log("HealthBarDebugger added to GameManager GameObject (disabled by default - press F1 to enable)");
        }
        else
        {
            GameObject debuggerObj = new GameObject("HealthBarDebugger");
            var debugger = debuggerObj.AddComponent<HealthBarDebugger>();
            debugger.EnableDebugLogs = false; // Disabled by default for production
            debugger.ShowGUI = false; // Disabled by default for production
            Debug.Log("HealthBarDebugger created as standalone GameObject (disabled by default - press F1 to enable)");
        }

        Debug.Log("HealthBarDebugger setup complete - use E to create test enemies, D to damage them");
    }

    private void SetupHealthBarTester()
    {
        Debug.Log("Setting up Health Bar Tester...");

        // Check if tester already exists
        if (FindObjectOfType<HealthBarTester>() != null)
        {
            Debug.Log("HealthBarTester already exists in scene");
            return;
        }

        // Add tester to the GameManager or create dedicated GameObject
        GameObject gameManagerObj = GameObject.Find("GameManager");
        if (gameManagerObj != null)
        {
            var tester = gameManagerObj.AddComponent<HealthBarTester>();
            tester.EnableTester = false; // Disabled by default for production
            Debug.Log("HealthBarTester added to GameManager GameObject (disabled by default - press F2 to enable)");
        }
        else
        {
            GameObject testerObj = new GameObject("HealthBarTester");
            var tester = testerObj.AddComponent<HealthBarTester>();
            tester.EnableTester = false; // Disabled by default for production
            Debug.Log("HealthBarTester created as standalone GameObject (disabled by default - press F2 to enable)");
        }

        Debug.Log("HealthBarTester setup complete - press F2 to enable, then T to create test health bar");
    }
    
    private GameObject CreateInputField(GameObject parent, string name, string labelText, float minY, float maxY)
    {
        GameObject fieldObj = new GameObject(name);
        fieldObj.transform.SetParent(parent.transform, false);
        
        // Add background image - dark box style like HealthBarDebugger
        var image = fieldObj.AddComponent<UnityEngine.UI.Image>();
        image.color = new Color(0.3f, 0.3f, 0.3f, 1f); // Dark gray background for better contrast
        
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
        text.fontSize = 20; // Larger font for better readability
        text.color = Color.white; // White text on dark background
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
        placeholder.fontSize = 20; // Larger placeholder font to match input text
        placeholder.color = new Color(0.7f, 0.7f, 0.7f, 1f); // Lighter gray placeholder text for better visibility
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
        
        // Add background image - dark button style like HealthBarDebugger
        var image = buttonObj.AddComponent<UnityEngine.UI.Image>();
        image.color = new Color(0.4f, 0.4f, 0.4f, 1f); // Dark gray button like Unity's GUI buttons
        
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
    
    private void CreateVisibilityTestElement(Canvas canvas)
    {
        Debug.Log("Creating visibility test element to verify canvas is working...");
        
        // Create a bright red test rectangle that should be impossible to miss
        GameObject testObj = new GameObject("VisibilityTest");
        testObj.transform.SetParent(canvas.transform, false);
        
        var testImage = testObj.AddComponent<UnityEngine.UI.Image>();
        testImage.color = Color.red; // Bright red
        
        var testRect = testObj.GetComponent<RectTransform>();
        testRect.anchorMin = new Vector2(0.4f, 0.4f);
        testRect.anchorMax = new Vector2(0.6f, 0.6f);
        testRect.anchoredPosition = Vector2.zero;
        testRect.sizeDelta = Vector2.zero;
        
        // Add text to the test element
        GameObject testTextObj = new GameObject("TestText");
        testTextObj.transform.SetParent(testObj.transform, false);
        
        var testText = testTextObj.AddComponent<UnityEngine.UI.Text>();
        testText.text = "HEALTH BAR TEST - DELETE ME";
        testText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        testText.fontSize = 20;
        testText.color = Color.white;
        testText.alignment = TextAnchor.MiddleCenter;
        testText.fontStyle = FontStyle.Bold;
        
        var testTextRect = testTextObj.GetComponent<RectTransform>();
        testTextRect.anchorMin = Vector2.zero;
        testTextRect.anchorMax = Vector2.one;
        testTextRect.anchoredPosition = Vector2.zero;
        testTextRect.sizeDelta = Vector2.zero;
        
        Debug.Log($"Visibility test element created on canvas: {canvas.name}");
        Debug.Log($"Test element active: {testObj.activeInHierarchy}");
        Debug.Log($"Test element parent: {testObj.transform.parent?.name}");
    }
    
    private void CreateHealthBarPositionTest(Canvas canvas)
    {
        Debug.Log("Creating health bar position test element...");
        
        // Create a green test rectangle in the exact position where health bar should be
        GameObject testObj = new GameObject("HealthBarPositionTest");
        testObj.transform.SetParent(canvas.transform, false);
        
        var testImage = testObj.AddComponent<UnityEngine.UI.Image>();
        testImage.color = Color.green; // Bright green
        
        var testRect = testObj.GetComponent<RectTransform>();
        // Use same positioning as health bar
        testRect.anchorMin = new Vector2(0.02f, 0.8f);
        testRect.anchorMax = new Vector2(0.4f, 0.98f);
        testRect.anchoredPosition = Vector2.zero;
        testRect.sizeDelta = Vector2.zero;
        
        // Add text to show this is the position test
        GameObject testTextObj = new GameObject("TestText");
        testTextObj.transform.SetParent(testObj.transform, false);
        
        var testText = testTextObj.AddComponent<UnityEngine.UI.Text>();
        testText.text = "HEALTH BAR SHOULD BE HERE";
        testText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        testText.fontSize = 16;
        testText.color = Color.white;
        testText.alignment = TextAnchor.MiddleCenter;
        testText.fontStyle = FontStyle.Bold;
        
        var testTextRect = testTextObj.GetComponent<RectTransform>();
        testTextRect.anchorMin = Vector2.zero;
        testTextRect.anchorMax = Vector2.one;
        testTextRect.anchoredPosition = Vector2.zero;
        testTextRect.sizeDelta = Vector2.zero;
        
        Debug.Log($"Health bar position test element created on canvas: {canvas.name}");
    }
    
    private void CreateSimpleHealthBarTest(Canvas canvas)
    {
        Debug.Log("Creating simple health bar test...");
        
        // Create a simple health bar with just background and fill - no complex slider
        GameObject simpleHealthBar = new GameObject("SimpleHealthBarTest");
        simpleHealthBar.transform.SetParent(canvas.transform, false);
        
        // Add blue background
        var bgImage = simpleHealthBar.AddComponent<UnityEngine.UI.Image>();
        bgImage.color = Color.blue;
        
        var bgRect = simpleHealthBar.GetComponent<RectTransform>();
        // Position slightly below the green test box
        bgRect.anchorMin = new Vector2(0.02f, 0.65f);
        bgRect.anchorMax = new Vector2(0.4f, 0.75f);
        bgRect.anchoredPosition = Vector2.zero;
        bgRect.sizeDelta = Vector2.zero;
        
        // Add yellow fill inside
        GameObject simpleFill = new GameObject("SimpleFill");
        simpleFill.transform.SetParent(simpleHealthBar.transform, false);
        
        var fillImage = simpleFill.AddComponent<UnityEngine.UI.Image>();
        fillImage.color = Color.yellow;
        
        var fillRect = simpleFill.GetComponent<RectTransform>();
        fillRect.anchorMin = new Vector2(0.05f, 0.2f);
        fillRect.anchorMax = new Vector2(0.95f, 0.8f);
        fillRect.anchoredPosition = Vector2.zero;
        fillRect.sizeDelta = Vector2.zero;
        
        // Add text
        GameObject simpleText = new GameObject("SimpleText");
        simpleText.transform.SetParent(simpleHealthBar.transform, false);
        
        var textComponent = simpleText.AddComponent<UnityEngine.UI.Text>();
        textComponent.text = "SIMPLE HEALTH BAR";
        textComponent.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        textComponent.fontSize = 14;
        textComponent.color = Color.white;
        textComponent.alignment = TextAnchor.MiddleCenter;
        
        var textRect = simpleText.GetComponent<RectTransform>();
        textRect.anchorMin = Vector2.zero;
        textRect.anchorMax = Vector2.one;
        textRect.anchoredPosition = Vector2.zero;
        textRect.sizeDelta = Vector2.zero;
        
        // Force active
        simpleHealthBar.SetActive(true);
        simpleFill.SetActive(true);
        simpleText.SetActive(true);
        
        Debug.Log($"Simple health bar test created - should be blue with yellow fill");
    }
    
    private void CreatePlayerHealthUITest(GameObject playerHealthContainer)
    {
        Debug.Log($"Creating PlayerHealthUI container test with parent: {playerHealthContainer?.name}");
        
        if (playerHealthContainer == null)
        {
            Debug.LogError("PlayerHealthContainer is null! This is the problem.");
            return;
        }
        
        // Create a test element directly in the PlayerHealthUI container
        GameObject containerTest = new GameObject("PlayerHealthUIContainerTest");
        containerTest.transform.SetParent(playerHealthContainer.transform, false);
        
        // Add purple background to distinguish from other tests
        var testImage = containerTest.AddComponent<UnityEngine.UI.Image>();
        testImage.color = Color.magenta; // Bright magenta/purple
        
        var testRect = containerTest.GetComponent<RectTransform>();
        // Position in middle-left like the health bar should be
        testRect.anchorMin = new Vector2(0.02f, 0.5f);
        testRect.anchorMax = new Vector2(0.4f, 0.6f);
        testRect.anchoredPosition = Vector2.zero;
        testRect.sizeDelta = Vector2.zero;
        
        // Add text
        GameObject testTextObj = new GameObject("TestText");
        testTextObj.transform.SetParent(containerTest.transform, false);
        
        var testText = testTextObj.AddComponent<UnityEngine.UI.Text>();
        testText.text = "INSIDE PLAYERHEALTHUI CONTAINER";
        testText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        testText.fontSize = 12;
        testText.color = Color.white;
        testText.alignment = TextAnchor.MiddleCenter;
        
        var testTextRect = testTextObj.GetComponent<RectTransform>();
        testTextRect.anchorMin = Vector2.zero;
        testTextRect.anchorMax = Vector2.one;
        testTextRect.anchoredPosition = Vector2.zero;
        testTextRect.sizeDelta = Vector2.zero;
        
        // Force active
        containerTest.SetActive(true);
        testTextObj.SetActive(true);
        
        Debug.Log($"PlayerHealthUI container test created - should be purple/magenta");
        Debug.Log($"Container test parent: {containerTest.transform.parent?.name}");
        Debug.Log($"Container test active: {containerTest.activeInHierarchy}");
        Debug.Log($"PlayerHealthContainer active: {playerHealthContainer.activeInHierarchy}");
    }
    
    private void SetupEnemyDamageTextManager()
    {
        Debug.Log("Setting up Enemy Damage Text Manager...");

        // Check if EnemyDamageTextManager already exists
        if (FindObjectOfType<EnemyDamageTextManager>() != null)
        {
            Debug.Log("EnemyDamageTextManager already exists in scene");
            return;
        }

        // Create GameObject for the damage text manager
        GameObject damageTextManagerObj = new GameObject("EnemyDamageTextManager");
        
        // Add the EnemyDamageTextManager component
        var damageTextManager = damageTextManagerObj.AddComponent<EnemyDamageTextManager>();
        
        // Configure the damage text manager
        damageTextManager.InitialPoolSize = 20;
        damageTextManager.MaxPoolSize = 50;
        damageTextManager.EnablePooling = true;
        damageTextManager.MaxActiveTexts = 30;
        damageTextManager.EnableDebugLogging = false; // Disable debug for better performance
        
        // Set damage text positioning (above health bars)
        damageTextManager.DamageTextOffset = new Vector3(0, 4f, 0); // Above health bar (which is at 3f)
        damageTextManager.RandomSpreadX = 1f;
        damageTextManager.RandomSpreadY = 0.5f;
        
        // Set critical damage threshold (25% of max health)
        damageTextManager.CriticalDamageThreshold = 0.25f;
        
        // Make persistent
        DontDestroyOnLoad(damageTextManagerObj);
        
        Debug.Log("EnemyDamageTextManager created and configured");
        Debug.Log("- Object pooling enabled with 20 initial texts");
        Debug.Log("- Damage text will appear 4 units above enemies");
        Debug.Log("- Critical damage threshold set to 25% of enemy max health");
        Debug.Log("- Color coding: Red (critical), Orange (regular), Green (healing)");
        
        // Add testing component for easy testing
        // Testing component removed to clean up UI
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
            "• Brown reference box with collision\n" +
            "• EnemyNetworkManager for server-synchronized enemies\n" +
            "• Health Bar System (enemy health bars + player health UI)\n" +
            "• Level Up System (animated banner + audio manager)\n" +
            "• (Optional) Local test enemy for debugging", 
            MessageType.Info);
    }
}
#endif