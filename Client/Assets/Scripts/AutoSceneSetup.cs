using UnityEngine;
using System.Reflection;
using CombatMechanix.Unity;
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
        Debug.Log("*** AUTO SCENE SETUP *** About to call CreateGameManagerSystem()");
        CreateGameManagerSystem();
        Debug.Log("*** AUTO SCENE SETUP *** CreateGameManagerSystem() completed");

        // 2. Create GameStartup GameObject for additional setup
        CreateGameStartup();

        // 3. Create Ground for visual reference
        if (CreateGround)
            CreateGroundPlane();

        // 4. Create Player GameObject
        Debug.Log($"*** AUTO SCENE SETUP *** CreatePlayer flag is: {CreatePlayer}");
        if (CreatePlayer)
            CreateLocalPlayer();
        else
            Debug.Log("*** AUTO SCENE SETUP *** Skipping player creation (CreatePlayer = false)");

        // 5. Setup Camera position
        if (SetupCamera)
            SetupMainCamera();

        // 6. Create Reference Box (disabled - replaced by vendor)
        // if (CreateReferenceBox)
        //     CreateReferenceBoxObject();

        // 7. Create Test Enemy (disabled by default - use network enemies)
        if (CreateTestEnemy)
            CreateTestEnemyObject();

        // 7.5. Create Vendor
        CreateVendorObject();

        // 8. Setup Enemy Network Manager
        if (SetupEnemyNetworkManager)
            SetupEnemyNetworkManagerComponent();

        // 9. Create basic UI system (after GameManager exists)
        CreateBasicUI();

        // 10. Setup Health Bar System
        if (SetupHealthBarSystem)
            SetupHealthBarSystemComponents();

        // 10.5. Setup Simple Player Health Bar (NEW IMPLEMENTATION)
        SetupSimplePlayerHealthBar();

        // 11. Setup Level Up Banner and Audio System
        SetupLevelUpSystem();

        // 11.5. Setup Death Banner System
        SetupDeathBanner();

        // 12. Setup Inventory System
        SetupInventorySystem();

        // 13. Setup Player Stats Panel (under inventory)
        SetupPlayerStatsPanel();

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
        Debug.Log("*** AUTO SCENE SETUP *** CreateGameManagerSystem() called");
        
        // Check if GameManager already exists
        var existingGameManager = FindObjectOfType<GameManager>();
        if (existingGameManager != null)
        {
            Debug.Log($"*** AUTO SCENE SETUP *** GameManager already exists in scene: {existingGameManager.name}");
            
            // Check if it has all required components and add missing ones
            EnsureRequiredComponents(existingGameManager.gameObject);
            return;
        }
        
        Debug.Log("*** AUTO SCENE SETUP *** No existing GameManager found, creating new one");

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

        Debug.Log("*** AUTO SCENE SETUP *** InventoryManager added successfully, now trying GrenadeInputHandler");
        Debug.Log("*** AUTO SCENE SETUP *** About to add GrenadeInputHandler component");
        try
        {
            var grenadeInputHandler = gameManagerObj.AddComponent<GrenadeInputHandler>();
            Debug.Log($"*** AUTO SCENE SETUP *** GrenadeInputHandler component added: {grenadeInputHandler != null}");
            if (grenadeInputHandler != null)
            {
                Debug.Log($"*** AUTO SCENE SETUP *** GrenadeInputHandler type: {grenadeInputHandler.GetType().FullName}");
            }
        }
        catch (System.Exception ex)
        {
            Debug.LogError($"*** AUTO SCENE SETUP *** Failed to add GrenadeInputHandler: {ex.Message}");
            Debug.LogError($"*** AUTO SCENE SETUP *** Exception details: {ex}");
        }

        // NOTE: GrenadeManager no longer needed - grenades now handled by WorldManager following projectile pattern
        Debug.Log("*** AUTO SCENE SETUP *** Grenades now handled by WorldManager + CombatSystem pattern");

        Debug.Log("*** AUTO SCENE SETUP *** About to add LootDropManager component");
        var lootDropManager = gameManagerObj.AddComponent<LootDropManager>();
        Debug.Log($"*** AUTO SCENE SETUP *** LootDropManager component added: {lootDropManager != null}");
        
        Debug.Log("*** AUTO SCENE SETUP *** About to add LootTextManager component");
        var lootTextManager = gameManagerObj.AddComponent<LootTextManager>();
        Debug.Log($"*** AUTO SCENE SETUP *** LootTextManager component added: {lootTextManager != null}");
        
        Debug.Log("*** AUTO SCENE SETUP *** About to add LootDropManagerEnsurer component for debugging");
        var lootDropManagerEnsurer = gameManagerObj.AddComponent<LootDropManagerEnsurer>();
        Debug.Log($"*** AUTO SCENE SETUP *** LootDropManagerEnsurer component added: {lootDropManagerEnsurer != null}");
        
        // Add AudioSource for sound effects
        gameManagerObj.AddComponent<AudioSource>();
        
        // Make it persistent
        DontDestroyOnLoad(gameManagerObj);
        
        Debug.Log("GameManager system created with all manager components");
        Debug.Log("UIManager component added - inventory system will be available");
    }

    private void EnsureRequiredComponents(GameObject gameManagerObj)
    {
        Debug.Log($"*** AUTO SCENE SETUP *** Ensuring required components on existing GameManager: {gameManagerObj.name}");
        
        // Check and add missing components
        if (gameManagerObj.GetComponent<NetworkManager>() == null)
        {
            Debug.Log("*** AUTO SCENE SETUP *** Adding missing NetworkManager component");
            gameManagerObj.AddComponent<NetworkManager>();
        }
        
        if (gameManagerObj.GetComponent<WorldManager>() == null)
        {
            Debug.Log("*** AUTO SCENE SETUP *** Adding missing WorldManager component");
            gameManagerObj.AddComponent<WorldManager>();
        }
        
        if (gameManagerObj.GetComponent<UIManager>() == null)
        {
            Debug.Log("*** AUTO SCENE SETUP *** Adding missing UIManager component");
            gameManagerObj.AddComponent<UIManager>();
        }
        
        if (gameManagerObj.GetComponent<CombatSystem>() == null)
        {
            Debug.Log("*** AUTO SCENE SETUP *** Adding missing CombatSystem component");
            var combatSystem = gameManagerObj.AddComponent<CombatSystem>();
            ConfigureCombatSystem(combatSystem);
        }
        else
        {
            var combatSystem = gameManagerObj.GetComponent<CombatSystem>();
            ConfigureCombatSystem(combatSystem);
        }
        
        if (gameManagerObj.GetComponent<ChatSystem>() == null)
        {
            Debug.Log("*** AUTO SCENE SETUP *** Adding missing ChatSystem component");
            gameManagerObj.AddComponent<ChatSystem>();
        }
        
        if (gameManagerObj.GetComponent<InventoryManager>() == null)
        {
            Debug.Log("*** AUTO SCENE SETUP *** Adding missing InventoryManager component");
            gameManagerObj.AddComponent<InventoryManager>();
        }
        
        // THIS IS THE IMPORTANT ONE FOR LOOT SYSTEM
        if (gameManagerObj.GetComponent<LootDropManager>() == null)
        {
            Debug.Log("*** AUTO SCENE SETUP *** Adding missing LootDropManager component");
            var lootDropManager = gameManagerObj.AddComponent<LootDropManager>();
            Debug.Log($"*** AUTO SCENE SETUP *** LootDropManager component added: {lootDropManager != null}");
        }
        else
        {
            Debug.Log("*** AUTO SCENE SETUP *** LootDropManager component already exists");
        }
        
        if (gameManagerObj.GetComponent<LootTextManager>() == null)
        {
            Debug.Log("*** AUTO SCENE SETUP *** Adding missing LootTextManager component");
            var lootTextManager = gameManagerObj.AddComponent<LootTextManager>();
            Debug.Log($"*** AUTO SCENE SETUP *** LootTextManager component added: {lootTextManager != null}");
        }
        else
        {
            Debug.Log("*** AUTO SCENE SETUP *** LootTextManager component already exists");
        }
        
        if (gameManagerObj.GetComponent<LootDropManagerEnsurer>() == null)
        {
            Debug.Log("*** AUTO SCENE SETUP *** Adding missing LootDropManagerEnsurer component for debugging");
            var lootDropManagerEnsurer = gameManagerObj.AddComponent<LootDropManagerEnsurer>();
            Debug.Log($"*** AUTO SCENE SETUP *** LootDropManagerEnsurer component added: {lootDropManagerEnsurer != null}");
        }
        else
        {
            Debug.Log("*** AUTO SCENE SETUP *** LootDropManagerEnsurer component already exists");
        }
        
        if (gameManagerObj.GetComponent<AudioSource>() == null)
        {
            Debug.Log("*** AUTO SCENE SETUP *** Adding missing AudioSource component");
            gameManagerObj.AddComponent<AudioSource>();
        }
        
        Debug.Log("*** AUTO SCENE SETUP *** Required components check completed");
    }

    private void ConfigureCombatSystem(CombatSystem combatSystem)
    {
        if (combatSystem == null) return;
        
        Debug.Log("*** AUTO SCENE SETUP *** Configuring CombatSystem for projectile support");
        
        // Set projectile spawn point to player position (will be overridden during gameplay)
        var playerObj = GameObject.Find("LocalPlayer");
        if (playerObj != null)
        {
            combatSystem.ProjectileSpawnPoint = playerObj.transform;
            Debug.Log("*** AUTO SCENE SETUP *** ProjectileSpawnPoint set to LocalPlayer");
        }
        else
        {
            Debug.LogWarning("*** AUTO SCENE SETUP *** LocalPlayer not found for ProjectileSpawnPoint");
        }
        
        // Note: ProjectilePrefab is left null - CombatSystem will create projectiles dynamically
        Debug.Log("*** AUTO SCENE SETUP *** CombatSystem configured successfully (using dynamic projectile creation)");
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
        Debug.Log("*** AUTO SCENE SETUP *** CreateLocalPlayer() called");
        
        // Check if LocalPlayer already exists
        var existingPlayer = GameObject.Find("LocalPlayer");
        if (existingPlayer != null)
        {
            Debug.Log($"*** AUTO SCENE SETUP *** LocalPlayer already exists in scene: {existingPlayer.name}");
            
            // Check if it has ClientPlayerStats
            var existingStats = existingPlayer.GetComponent<ClientPlayerStats>();
            if (existingStats == null)
            {
                Debug.Log("*** AUTO SCENE SETUP *** Adding missing ClientPlayerStats to existing LocalPlayer");
                existingPlayer.AddComponent<ClientPlayerStats>();
            }
            else
            {
                Debug.Log("*** AUTO SCENE SETUP *** Existing LocalPlayer already has ClientPlayerStats");
            }
            return;
        }

        Debug.Log("Creating LocalPlayer GameObject...");
        GameObject player = GameObject.CreatePrimitive(PrimitiveType.Capsule);
        player.name = "LocalPlayer";
        player.transform.position = new Vector3(0, 1f, 0); // Transform at y=1 so CC bottom sits on ground
        
        // Add the PlayerController component
        PlayerController playerController = player.AddComponent<PlayerController>();
        
        // Add the ClientPlayerStats component for server-authoritative stats
        Debug.Log("*** AUTO SCENE SETUP *** About to add ClientPlayerStats component");
        ClientPlayerStats playerStats = player.AddComponent<ClientPlayerStats>();
        Debug.Log($"*** AUTO SCENE SETUP *** ClientPlayerStats component added: {playerStats != null}");
        
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
        // DEPRECATED: This method is disabled - replaced by CreateVendorObject()
        // Keeping for backward compatibility but not used
        Debug.Log("CreateReferenceBoxObject called but disabled - using CreateVendorObject instead");
    }

    private void CreateVendorObject()
    {
        // Check if Vendor already exists
        if (GameObject.Find("Vendor") != null)
        {
            Debug.Log("Vendor already exists in scene");
            return;
        }

        Debug.Log("Creating Vendor GameObject...");
        GameObject vendorObj = new GameObject("Vendor");
        
        // Position vendor at a fixed location in the world (can be adjusted later)
        vendorObj.transform.position = new Vector3(10f, 0f, 10f);
        
        // Add the Vendor component
        Vendor vendor = vendorObj.AddComponent<Vendor>();
        
        // Configure vendor settings
        vendor.VendorName = "General Merchant";
        vendor.InteractionRange = 5f;
        
        Debug.Log($"Vendor '{vendor.VendorName}' created at position {vendorObj.transform.position}");
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
        
        // Create the character panel
        GameObject characterPanel = CreateCharacterPanel(canvasObj);
        
        // Create the chat panel
        GameObject chatPanel = CreateChatPanel(canvasObj);
        
        // Create the vendor panel
        GameObject vendorPanel = CreateVendorPanel(canvasObj);
        
        // Create the login panel
        GameObject loginPanel = CreateLoginPanel(canvasObj);
        
        // Connect the panels to UIManager
        ConnectUIManagerReferences(inventoryPanel, characterPanel, chatPanel, vendorPanel, loginPanel);
        
        // Force Canvas to update
        Canvas.ForceUpdateCanvases();
        
        Debug.Log("UI Canvas created with inventory panel and connected to UIManager");
        Debug.Log($"Canvas settings: RenderMode={canvas.renderMode}, SortingOrder={canvas.sortingOrder}");
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
        
        // Double-check Canvas parent and panel setup
        Debug.Log($"Login panel created: parent={loginPanel.transform.parent?.name}, active={loginPanel.activeInHierarchy}, canvas={canvasObj.name}");
        Debug.Log($"Login panel rect: {loginPanel.GetComponent<RectTransform>().rect}");
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

            // 4. Setup Player Health UI - REMOVED (using SimplePlayerHealthBar instead)

            // 5. Connect to existing UI Manager
            ConnectHealthUIToUIManager();

            // 6. Add health bar debugger for testing
            // HealthBarDebugger removed

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
            // HealthBarDebugger removed
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

    // OLD SetupPlayerHealthUI method removed - using SimplePlayerHealthBar instead

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

    private void SetupDeathBanner()
    {
        Debug.Log("Setting up Death Banner...");

        // Check if DeathBanner already exists
        if (FindObjectOfType<DeathBanner>() != null)
        {
            Debug.Log("DeathBanner already exists in scene");
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
                Debug.Log($"Found UI Canvas for Death Banner: {c.name}");
                break;
            }
        }

        if (mainCanvas == null)
        {
            Debug.LogError("No ScreenSpaceOverlay Canvas found for Death Banner!");
            return;
        }

        // Create Death Banner GameObject
        GameObject bannerObj = new GameObject("DeathBanner");
        bannerObj.transform.SetParent(mainCanvas.transform, false);
        
        // Add DeathBanner component
        var deathBanner = bannerObj.AddComponent<DeathBanner>();
        
        // Configure banner settings
        deathBanner.FadeInDuration = 0.5f;
        deathBanner.FadeOutDuration = 0.5f;
        deathBanner.BannerText = "YOU ARE DEAD!";
        deathBanner.BannerColor = Color.red;
        deathBanner.FontSize = 48;
        deathBanner.FontStyle = FontStyle.Bold;
        deathBanner.RespawnButtonText = "RESPAWN";
        deathBanner.PlaySoundEffect = true;
        deathBanner.SoundVolume = 0.8f;

        // Set high sorting order to appear above other UI (higher than level up banner)
        bannerObj.transform.SetSiblingIndex(mainCanvas.transform.childCount - 1);

        Debug.Log("DeathBanner created and configured");
        Debug.Log("- Fade in/out animation on player death");
        Debug.Log("- Red death text with respawn button");
        Debug.Log("- Network respawn integration");
        Debug.Log("- Positioned in main UI Canvas above all other elements");
    }

    private void SetupSimplePlayerHealthBar()
    {
        Debug.Log("=== Setting up Simple Player Health Bar ===");

        // Check if SimplePlayerHealthBar already exists
        if (FindObjectOfType<SimplePlayerHealthBar>() != null)
        {
            Debug.Log("SimplePlayerHealthBar already exists in scene");
            return;
        }

        // Find the main Canvas (Screen Space Overlay only)
        Canvas[] allCanvases = FindObjectsOfType<Canvas>();
        Debug.Log($"[SetupSimplePlayerHealthBar] Found {allCanvases.Length} canvases in scene:");
        
        Canvas mainCanvas = null;
        foreach (Canvas c in allCanvases)
        {
            Debug.Log($"  - Canvas: {c.name}, renderMode: {c.renderMode}, active: {c.gameObject.activeInHierarchy}");
            if (c.renderMode == RenderMode.ScreenSpaceOverlay && c.name == "Canvas")
            {
                mainCanvas = c;
                Debug.Log($"[SetupSimplePlayerHealthBar] Found main UI Canvas: {c.name}");
                break;
            }
        }
        
        if (mainCanvas == null)
        {
            // Fallback to any ScreenSpaceOverlay canvas
            foreach (Canvas c in allCanvases)
            {
                if (c.renderMode == RenderMode.ScreenSpaceOverlay)
                {
                    mainCanvas = c;
                    Debug.Log($"[SetupSimplePlayerHealthBar] Using fallback canvas: {c.name}");
                    break;
                }
            }
        }
        
        if (mainCanvas == null)
        {
            Debug.LogError("No ScreenSpaceOverlay Canvas found for Simple Player Health Bar!");
            return;
        }

        // Create Simple Player Health Bar container
        GameObject simpleHealthBarContainer = new GameObject("SimplePlayerHealthBar");
        simpleHealthBarContainer.transform.SetParent(mainCanvas.transform, false);

        // Position at center top of screen
        var containerRect = simpleHealthBarContainer.GetComponent<RectTransform>();
        if (containerRect == null)
        {
            containerRect = simpleHealthBarContainer.AddComponent<RectTransform>();
        }
        
        containerRect.anchorMin = new Vector2(0.3f, 0.92f);
        containerRect.anchorMax = new Vector2(0.7f, 0.98f);
        containerRect.anchoredPosition = Vector2.zero;
        containerRect.sizeDelta = Vector2.zero;

        // Create the health slider using the simple structure that works
        GameObject healthSliderObj = CreateSimpleHealthSlider(simpleHealthBarContainer);
        
        // Add SimplePlayerHealthBar component
        var simpleHealthBar = simpleHealthBarContainer.AddComponent<SimplePlayerHealthBar>();
        
        // Get references to components
        var healthSlider = healthSliderObj.GetComponent<UnityEngine.UI.Slider>();
        var healthText = healthSliderObj.GetComponentInChildren<UnityEngine.UI.Text>();
        var healthFillImage = healthSliderObj.transform.Find("Fill Area/Fill")?.GetComponent<UnityEngine.UI.Image>();
        var backgroundImage = healthSliderObj.transform.Find("Background")?.GetComponent<UnityEngine.UI.Image>();

        // Set the component references using reflection
        SetFieldValue(simpleHealthBar, "HealthSlider", healthSlider);
        SetFieldValue(simpleHealthBar, "HealthText", healthText);
        SetFieldValue(simpleHealthBar, "HealthFillImage", healthFillImage);
        SetFieldValue(simpleHealthBar, "BackgroundImage", backgroundImage);

        Debug.Log($"SimplePlayerHealthBar created successfully:");
        Debug.Log($"- Container active: {simpleHealthBarContainer.activeInHierarchy}");
        Debug.Log($"- Slider: {healthSlider != null}");
        Debug.Log($"- Text: {healthText != null}");
        Debug.Log($"- Fill Image: {healthFillImage != null}");
        Debug.Log($"- Background Image: {backgroundImage != null}");
    }

    private GameObject CreateSimpleHealthSlider(GameObject parent)
    {
        Debug.Log("Creating simple health slider using working enemy health bar pattern...");
        
        // Create main slider GameObject
        GameObject sliderObj = new GameObject("SimpleHealthSlider");
        sliderObj.transform.SetParent(parent.transform, false);

        // Set to fill parent container
        var sliderRect = sliderObj.GetComponent<RectTransform>();
        if (sliderRect == null)
        {
            sliderRect = sliderObj.AddComponent<RectTransform>();
        }
        sliderRect.anchorMin = Vector2.zero;
        sliderRect.anchorMax = Vector2.one;
        sliderRect.anchoredPosition = Vector2.zero;
        sliderRect.sizeDelta = Vector2.zero;

        // Add Slider component - COPY EXACT PATTERN FROM ENEMY HEALTH BARS
        var slider = sliderObj.AddComponent<UnityEngine.UI.Slider>();
        slider.minValue = 0f;
        slider.maxValue = 1f;
        slider.value = 1f;

        // Create Background
        GameObject backgroundObj = new GameObject("Background");
        backgroundObj.transform.SetParent(sliderObj.transform, false);
        
        var backgroundImage = backgroundObj.AddComponent<UnityEngine.UI.Image>();
        backgroundImage.color = new Color(0.2f, 0.2f, 0.2f, 0.9f); // Dark background
        
        var backgroundRect = backgroundObj.GetComponent<RectTransform>();
        backgroundRect.anchorMin = Vector2.zero;
        backgroundRect.anchorMax = Vector2.one;
        backgroundRect.anchoredPosition = Vector2.zero;
        backgroundRect.sizeDelta = Vector2.zero;

        // Create Fill Area
        GameObject fillAreaObj = new GameObject("Fill Area");
        fillAreaObj.transform.SetParent(sliderObj.transform, false);
        
        if (fillAreaObj.GetComponent<RectTransform>() == null)
        {
            fillAreaObj.AddComponent<RectTransform>();
        }
        var fillAreaRect = fillAreaObj.GetComponent<RectTransform>();
        fillAreaRect.anchorMin = Vector2.zero;
        fillAreaRect.anchorMax = Vector2.one;
        fillAreaRect.anchoredPosition = Vector2.zero;
        fillAreaRect.sizeDelta = Vector2.zero;

        // Create Fill - EXACT PATTERN FROM WORKING ENEMY HEALTH BARS
        GameObject fillObj = new GameObject("Fill");
        fillObj.transform.SetParent(fillAreaObj.transform, false);
        
        var fillImage = fillObj.AddComponent<UnityEngine.UI.Image>();
        fillImage.color = Color.green; // Start with green
        fillImage.type = UnityEngine.UI.Image.Type.Simple; // Use Simple type like enemy health bars
        
        var fillRect = fillObj.GetComponent<RectTransform>();
        fillRect.anchorMin = Vector2.zero;
        fillRect.anchorMax = Vector2.one;
        fillRect.anchoredPosition = Vector2.zero;
        fillRect.sizeDelta = Vector2.zero;

        // Create Health Text
        GameObject textObj = new GameObject("HealthText");
        textObj.transform.SetParent(sliderObj.transform, false);
        
        var healthText = textObj.AddComponent<UnityEngine.UI.Text>();
        healthText.text = "100/100";
        healthText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        healthText.fontSize = 14;
        healthText.color = Color.white;
        healthText.alignment = TextAnchor.MiddleCenter;
        healthText.fontStyle = FontStyle.Bold;
        
        var textRect = textObj.GetComponent<RectTransform>();
        textRect.anchorMin = new Vector2(0.7f, 0);
        textRect.anchorMax = new Vector2(1f, 1f);
        textRect.anchoredPosition = Vector2.zero;
        textRect.sizeDelta = Vector2.zero;

        // Connect slider components - EXACT PATTERN FROM ENEMY HEALTH BARS
        slider.fillRect = fillRect;
        slider.targetGraphic = fillImage;

        // Force everything active
        sliderObj.SetActive(true);
        backgroundObj.SetActive(true);
        fillAreaObj.SetActive(true);
        fillObj.SetActive(true);
        textObj.SetActive(true);

        Debug.Log("Simple health slider created with exact enemy health bar pattern");
        Debug.Log($"Slider min/max/value: {slider.minValue}/{slider.maxValue}/{slider.value}");
        Debug.Log($"Fill image color: {fillImage.color}");
        
        return sliderObj;
    }

    private void SetupInventorySystem()
    {
        Debug.Log("Setting up Inventory System...");

        // Check if InventoryUI already exists
        if (FindObjectOfType<InventoryUI>() != null)
        {
            Debug.Log("InventoryUI already exists in scene");
            return;
        }

        // Find the Canvas first since InventoryPanel should be a child of Canvas
        Canvas mainCanvas = null;
        Canvas[] allCanvases = FindObjectsOfType<Canvas>();
        
        foreach (Canvas canvas in allCanvases)
        {
            if (canvas.renderMode == RenderMode.ScreenSpaceOverlay)
            {
                mainCanvas = canvas;
                Debug.Log($"Found main Canvas: {canvas.name}");
                break;
            }
        }
        
        if (mainCanvas == null)
        {
            Debug.LogError("No Canvas found! Cannot create inventory system.");
            return;
        }

        // Look for InventoryPanel as child of Canvas
        GameObject inventoryPanel = null;
        for (int i = 0; i < mainCanvas.transform.childCount; i++)
        {
            var child = mainCanvas.transform.GetChild(i);
            if (child.name == "InventoryPanel")
            {
                inventoryPanel = child.gameObject;
                Debug.Log($"Found InventoryPanel as child of Canvas: {child.name}");
                break;
            }
        }
        
        if (inventoryPanel == null)
        {
            Debug.LogWarning("InventoryPanel not found as Canvas child. Creating new InventoryPanel...");
            inventoryPanel = CreateInventoryPanel(mainCanvas.gameObject);
        }

        // Add InventoryUI component to the inventory panel
        Debug.Log($"Adding InventoryUI component to panel: {inventoryPanel.name}");
        var inventoryUI = inventoryPanel.AddComponent<InventoryUI>();
        
        if (inventoryUI == null)
        {
            Debug.LogError("Failed to add InventoryUI component!");
            return;
        }
        
        Debug.Log("InventoryUI component added successfully, configuring settings...");
        
        // Configure inventory settings
        inventoryUI.MaxSlots = 20;
        inventoryUI.SlotSize = new Vector2(64, 64);
        inventoryUI.SlotSpacing = new Vector2(8, 8);
        inventoryUI.SlotsPerRow = 5;
        inventoryUI.InventoryContainer = inventoryPanel.transform;
        inventoryUI.ShowPlaceholderIcons = true;
        inventoryUI.PlaceholderColor = Color.gray;
        
        Debug.Log("InventoryUI settings configured successfully");

        Debug.Log("InventoryUI component added to InventoryPanel");
        Debug.Log("- 20 inventory slots in 4x5 grid layout");
        Debug.Log("- Placeholder icons for different item types");
        Debug.Log("- Network integration for server inventory data");
        Debug.Log("- Opens/closes with 'I' key (handled by UIManager)");
        
        Debug.Log("Inventory System setup complete");
    }

    private void SetupPlayerStatsPanel()
    {
        Debug.Log("Setting up Player Stats Panel...");

        // Find the main Canvas
        Canvas[] allCanvases = FindObjectsOfType<Canvas>();
        Canvas mainCanvas = null;
        foreach (Canvas c in allCanvases)
        {
            if (c.renderMode == RenderMode.ScreenSpaceOverlay && c.name == "Canvas")
            {
                mainCanvas = c;
                break;
            }
        }
        
        if (mainCanvas == null)
        {
            foreach (Canvas c in allCanvases)
            {
                if (c.renderMode == RenderMode.ScreenSpaceOverlay)
                {
                    mainCanvas = c;
                    break;
                }
            }
        }
        
        if (mainCanvas == null)
        {
            Debug.LogError("No ScreenSpaceOverlay Canvas found for Player Stats Panel!");
            return;
        }

        // Create Player Stats Panel
        GameObject playerStatsPanel = new GameObject("PlayerStatsPanel");
        playerStatsPanel.transform.SetParent(mainCanvas.transform, false);

        // Add Image component for background
        var image = playerStatsPanel.AddComponent<UnityEngine.UI.Image>();
        image.color = new Color(0.15f, 0.15f, 0.15f, 0.9f); // Darker than inventory
        image.raycastTarget = true;

        // Position below inventory panel (right side, bottom)
        var rectTransform = playerStatsPanel.GetComponent<RectTransform>();
        rectTransform.anchorMin = new Vector2(0.75f, 0.02f);  // Below inventory
        rectTransform.anchorMax = new Vector2(0.98f, 0.23f);  // Just below inventory bottom
        rectTransform.anchoredPosition = Vector2.zero;
        rectTransform.sizeDelta = Vector2.zero;

        // Create Player Info Text
        GameObject playerInfoTextObj = new GameObject("PlayerInfoText");
        playerInfoTextObj.transform.SetParent(playerStatsPanel.transform, false);
        
        var playerInfoText = playerInfoTextObj.AddComponent<UnityEngine.UI.Text>();
        playerInfoText.text = "Player Stats\nLoading...";
        playerInfoText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        playerInfoText.fontSize = 12;
        playerInfoText.color = Color.white;
        playerInfoText.alignment = TextAnchor.UpperLeft;
        playerInfoText.fontStyle = FontStyle.Normal;

        // Position text with padding
        var textRect = playerInfoTextObj.GetComponent<RectTransform>();
        textRect.anchorMin = Vector2.zero;
        textRect.anchorMax = Vector2.one;
        textRect.anchoredPosition = Vector2.zero;
        textRect.sizeDelta = Vector2.zero;
        textRect.offsetMin = new Vector2(10, 10); // Padding
        textRect.offsetMax = new Vector2(-10, -10); // Padding

        // Start hidden (will be shown with inventory)
        playerStatsPanel.SetActive(false);

        // Connect to UIManager
        var uiManager = FindObjectOfType<UIManager>();
        if (uiManager != null)
        {
            SetFieldValue(uiManager, "PlayerInfoText", playerInfoText);
            SetFieldValue(uiManager, "PlayerStatsPanel", playerStatsPanel);
            Debug.Log("Player Stats Panel connected to UIManager");
        }

        Debug.Log("Player Stats Panel created successfully:");
        Debug.Log($"- Positioned below inventory panel at bottom right");
        Debug.Log($"- Hidden by default, will show with inventory panel");
        Debug.Log($"- Connected to UIManager.PlayerInfoText");
    }
    
    private GameObject CreateInventoryPanel(GameObject canvasObj)
    {
        Debug.Log("Creating new InventoryPanel...");
        
        // Create inventory panel
        GameObject inventoryPanel = new GameObject("InventoryPanel");
        inventoryPanel.transform.SetParent(canvasObj.transform, false);
        
        // Add Image component for background - dark box style
        var image = inventoryPanel.AddComponent<UnityEngine.UI.Image>();
        image.color = new Color(0.2f, 0.2f, 0.2f, 0.85f);
        image.raycastTarget = true;
        
        // Set up RectTransform for positioning (right side of screen)
        var rectTransform = inventoryPanel.GetComponent<RectTransform>();
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
        
        // Start with panel hidden
        inventoryPanel.SetActive(false);
        
        Debug.Log("InventoryPanel created successfully");
        return inventoryPanel;
    }

    private GameObject CreateCharacterPanel(GameObject canvasObj)
    {
        Debug.Log("Creating new CharacterPanel...");
        
        // Create character panel
        GameObject characterPanel = new GameObject("CharacterPanel");
        characterPanel.transform.SetParent(canvasObj.transform, false);
        
        // Add Image component for background - dark box style
        var image = characterPanel.AddComponent<UnityEngine.UI.Image>();
        image.color = new Color(0.2f, 0.2f, 0.2f, 0.85f); // Same dark gray as inventory panel for consistency
        image.raycastTarget = true;
        
        // Set up RectTransform for positioning (same location as inventory panel - right side)
        var rectTransform = characterPanel.GetComponent<RectTransform>();
        rectTransform.anchorMin = new Vector2(0.75f, 0.25f);  // Right side of screen (same as inventory)
        rectTransform.anchorMax = new Vector2(0.98f, 0.85f);  // Same size and position as inventory
        rectTransform.anchoredPosition = Vector2.zero;
        rectTransform.sizeDelta = Vector2.zero;
        rectTransform.offsetMin = Vector2.zero;
        rectTransform.offsetMax = Vector2.zero;
        
        // Add title text
        GameObject titleObj = new GameObject("Title");
        titleObj.transform.SetParent(characterPanel.transform, false);
        var titleText = titleObj.AddComponent<UnityEngine.UI.Text>();
        titleText.text = "CHARACTER";
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
        
        // Add CharacterUI component to handle the equipment display
        var characterUI = characterPanel.AddComponent<CharacterUI>();
        
        // Set the CharacterContainer reference
        SetFieldValue(characterUI, "CharacterContainer", characterPanel.transform);
        
        // Start with panel hidden
        characterPanel.SetActive(false);
        
        Debug.Log("CharacterPanel created successfully with CharacterUI component");
        return characterPanel;
    }

    private GameObject CreateChatPanel(GameObject canvasObj)
    {
        Debug.Log("Creating new ChatPanel...");
        
        // Create chat panel
        GameObject chatPanel = new GameObject("ChatPanel");
        chatPanel.transform.SetParent(canvasObj.transform, false);
        
        // Add Image component for background - dark box style (matching inventory panel)
        var image = chatPanel.AddComponent<UnityEngine.UI.Image>();
        image.color = new Color(0.2f, 0.2f, 0.2f, 0.85f); // Same dark gray as inventory panel
        image.raycastTarget = true;
        
        // Set up RectTransform for positioning (bottom left, thinner width)
        var rectTransform = chatPanel.GetComponent<RectTransform>();
        rectTransform.anchorMin = new Vector2(0.02f, 0.02f);
        rectTransform.anchorMax = new Vector2(0.35f, 0.4f);
        rectTransform.anchoredPosition = Vector2.zero;
        rectTransform.sizeDelta = Vector2.zero;
        rectTransform.offsetMin = Vector2.zero;
        rectTransform.offsetMax = Vector2.zero;
        
        // Add title text
        GameObject titleObj = new GameObject("Title");
        titleObj.transform.SetParent(chatPanel.transform, false);
        var titleText = titleObj.AddComponent<UnityEngine.UI.Text>();
        titleText.text = "CHAT";
        titleText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        titleText.fontSize = 20;
        titleText.color = Color.white;
        titleText.alignment = TextAnchor.MiddleCenter;
        titleText.fontStyle = FontStyle.Bold;
        
        var titleRect = titleObj.GetComponent<RectTransform>();
        titleRect.anchorMin = new Vector2(0, 0.9f);
        titleRect.anchorMax = new Vector2(1, 1f);
        titleRect.anchoredPosition = Vector2.zero;
        titleRect.sizeDelta = Vector2.zero;
        
        // Create chat display area (scrollable text)
        GameObject chatDisplayObj = new GameObject("ChatDisplay");
        chatDisplayObj.transform.SetParent(chatPanel.transform, false);
        var chatDisplay = chatDisplayObj.AddComponent<UnityEngine.UI.Text>();
        chatDisplay.text = "Welcome to Combat Mechanix!\nPress Enter to type...";
        chatDisplay.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        chatDisplay.fontSize = 12;
        chatDisplay.color = Color.white;
        chatDisplay.alignment = TextAnchor.UpperLeft;
        chatDisplay.fontStyle = FontStyle.Normal;
        
        var chatDisplayRect = chatDisplayObj.GetComponent<RectTransform>();
        chatDisplayRect.anchorMin = new Vector2(0.02f, 0.15f);
        chatDisplayRect.anchorMax = new Vector2(0.98f, 0.88f);
        chatDisplayRect.anchoredPosition = Vector2.zero;
        chatDisplayRect.sizeDelta = Vector2.zero;
        
        // Create chat input field at bottom
        GameObject chatInputObj = CreateInputField(chatPanel, "ChatInput", "Message", 0.02f, 0.12f);
        var chatInput = chatInputObj.GetComponent<UnityEngine.UI.InputField>();
        chatInput.placeholder.GetComponent<UnityEngine.UI.Text>().text = "Type message...";
        
        // Create channel dropdown
        GameObject channelDropdownObj = new GameObject("ChannelDropdown");
        channelDropdownObj.transform.SetParent(chatPanel.transform, false);
        
        // Add background for dropdown
        var dropdownImage = channelDropdownObj.AddComponent<UnityEngine.UI.Image>();
        dropdownImage.color = new Color(0.3f, 0.3f, 0.3f, 1f);
        
        var dropdown = channelDropdownObj.AddComponent<UnityEngine.UI.Dropdown>();
        dropdown.options.Clear();
        dropdown.options.Add(new UnityEngine.UI.Dropdown.OptionData("Global"));
        dropdown.options.Add(new UnityEngine.UI.Dropdown.OptionData("Local"));
        dropdown.options.Add(new UnityEngine.UI.Dropdown.OptionData("Private"));
        
        var dropdownRect = channelDropdownObj.GetComponent<RectTransform>();
        dropdownRect.anchorMin = new Vector2(0.7f, 0.02f);
        dropdownRect.anchorMax = new Vector2(0.98f, 0.12f);
        dropdownRect.anchoredPosition = Vector2.zero;
        dropdownRect.sizeDelta = Vector2.zero;
        
        // Create dropdown label
        GameObject labelObj = new GameObject("Label");
        labelObj.transform.SetParent(channelDropdownObj.transform, false);
        var labelText = labelObj.AddComponent<UnityEngine.UI.Text>();
        labelText.text = "Global";
        labelText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        labelText.fontSize = 12;
        labelText.color = Color.white;
        labelText.alignment = TextAnchor.MiddleCenter;
        
        var labelRect = labelObj.GetComponent<RectTransform>();
        labelRect.anchorMin = Vector2.zero;
        labelRect.anchorMax = Vector2.one;
        labelRect.anchoredPosition = Vector2.zero;
        labelRect.sizeDelta = Vector2.zero;
        
        dropdown.captionText = labelText;
        
        // Start with panel hidden
        chatPanel.SetActive(false);
        
        Debug.Log("ChatPanel created successfully with chat display, input field, and channel dropdown");
        return chatPanel;
    }

    private GameObject CreateVendorPanel(GameObject canvasObj)
    {
        Debug.Log("Creating new VendorPanel...");
        
        // Create vendor panel (start disabled to prevent showing before login)
        GameObject vendorPanel = new GameObject("VendorPanel");
        vendorPanel.transform.SetParent(canvasObj.transform, false);
        vendorPanel.SetActive(false); // Ensure it starts disabled
        
        // Add Image component for background - dark box style (matching other panels)
        var image = vendorPanel.AddComponent<UnityEngine.UI.Image>();
        image.color = new Color(0.2f, 0.2f, 0.2f, 0.9f); // Slightly more opaque for main panel
        image.raycastTarget = true;
        
        // Set up RectTransform for center positioning (larger than other panels)
        var rectTransform = vendorPanel.GetComponent<RectTransform>();
        rectTransform.anchorMin = new Vector2(0.2f, 0.1f);
        rectTransform.anchorMax = new Vector2(0.8f, 0.9f);
        rectTransform.anchoredPosition = Vector2.zero;
        rectTransform.sizeDelta = Vector2.zero;
        rectTransform.offsetMin = Vector2.zero;
        rectTransform.offsetMax = Vector2.zero;
        
        // Add title text
        GameObject titleObj = new GameObject("VendorTitle");
        titleObj.transform.SetParent(vendorPanel.transform, false);
        var titleText = titleObj.AddComponent<UnityEngine.UI.Text>();
        titleText.text = "GENERAL MERCHANT - SELL ITEMS";
        titleText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        titleText.fontSize = 18;
        titleText.color = Color.white;
        titleText.alignment = TextAnchor.MiddleCenter;
        titleText.fontStyle = FontStyle.Bold;
        
        var titleRect = titleObj.GetComponent<RectTransform>();
        titleRect.anchorMin = new Vector2(0, 0.92f);
        titleRect.anchorMax = new Vector2(1, 1f);
        titleRect.anchoredPosition = Vector2.zero;
        titleRect.sizeDelta = Vector2.zero;
        
        // Add gold display
        GameObject goldObj = new GameObject("PlayerGold");
        goldObj.transform.SetParent(vendorPanel.transform, false);
        var goldText = goldObj.AddComponent<UnityEngine.UI.Text>();
        goldText.text = "Gold: 0";
        goldText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        goldText.fontSize = 14;
        goldText.color = Color.yellow;
        goldText.alignment = TextAnchor.MiddleLeft;
        goldText.fontStyle = FontStyle.Bold;
        
        var goldRect = goldObj.GetComponent<RectTransform>();
        goldRect.anchorMin = new Vector2(0.02f, 0.85f);
        goldRect.anchorMax = new Vector2(0.3f, 0.92f);
        goldRect.anchoredPosition = Vector2.zero;
        goldRect.sizeDelta = Vector2.zero;
        
        // Create vendor container (for item slots)
        GameObject vendorContainer = new GameObject("VendorContainer");
        vendorContainer.transform.SetParent(vendorPanel.transform, false);
        
        // Add RectTransform for UI positioning (regular GameObjects don't have this automatically)
        var containerRect = vendorContainer.AddComponent<RectTransform>();
        containerRect.anchorMin = new Vector2(0.05f, 0.1f);
        containerRect.anchorMax = new Vector2(0.95f, 0.8f);
        containerRect.anchoredPosition = Vector2.zero;
        containerRect.sizeDelta = Vector2.zero;
        
        // Add close button
        GameObject closeButtonObj = new GameObject("CloseButton");
        closeButtonObj.transform.SetParent(vendorPanel.transform, false);
        
        var closeButton = closeButtonObj.AddComponent<UnityEngine.UI.Button>();
        var closeImage = closeButtonObj.AddComponent<UnityEngine.UI.Image>();
        closeImage.color = new Color(0.8f, 0.2f, 0.2f, 1f); // Red close button
        
        // Get or add RectTransform (Button component should have added it)
        var closeRect = closeButtonObj.GetComponent<RectTransform>();
        if (closeRect == null)
        {
            Debug.LogWarning("Close button missing RectTransform - adding manually");
            closeRect = closeButtonObj.AddComponent<RectTransform>();
        }
        closeRect.anchorMin = new Vector2(0.92f, 0.92f);
        closeRect.anchorMax = new Vector2(0.98f, 0.98f);
        closeRect.anchoredPosition = Vector2.zero;
        closeRect.sizeDelta = Vector2.zero;
        
        // Add close button text
        GameObject closeTextObj = new GameObject("CloseText");
        closeTextObj.transform.SetParent(closeButtonObj.transform, false);
        var closeText = closeTextObj.AddComponent<UnityEngine.UI.Text>();
        closeText.text = "X";
        closeText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        closeText.fontSize = 16;
        closeText.color = Color.white;
        closeText.alignment = TextAnchor.MiddleCenter;
        closeText.fontStyle = FontStyle.Bold;
        
        var closeTextRect = closeTextObj.GetComponent<RectTransform>();
        closeTextRect.anchorMin = Vector2.zero;
        closeTextRect.anchorMax = Vector2.one;
        closeTextRect.anchoredPosition = Vector2.zero;
        closeTextRect.sizeDelta = Vector2.zero;
        
        // Create item details panel directly (since VendorUI creation seems to be failing)
        GameObject itemDetailsPanel = new GameObject("ItemDetailsPanel");
        itemDetailsPanel.transform.SetParent(vendorContainer.transform, false);
        
        // Add Image component for background
        var detailsBgImage = itemDetailsPanel.AddComponent<UnityEngine.UI.Image>();
        detailsBgImage.color = new Color(0.1f, 0.1f, 0.1f, 0.9f); // Default dark background
        detailsBgImage.raycastTarget = false; // Don't block hover events
        
        // Position below the grid layout (bottom 25% of VendorContainer)
        var detailsRect = itemDetailsPanel.GetComponent<RectTransform>();
        detailsRect.anchorMin = new Vector2(0.0f, 0.0f);
        detailsRect.anchorMax = new Vector2(1.0f, 0.25f);
        detailsRect.anchoredPosition = Vector2.zero;
        detailsRect.sizeDelta = Vector2.zero;
        
        // Create text component for item details
        GameObject detailsTextObj = new GameObject("VendorDetailsText");
        detailsTextObj.transform.SetParent(itemDetailsPanel.transform, false);
        
        var detailsText = detailsTextObj.AddComponent<UnityEngine.UI.Text>();
        detailsText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        detailsText.fontSize = 14;
        detailsText.color = Color.white;
        detailsText.alignment = TextAnchor.UpperLeft;
        detailsText.verticalOverflow = VerticalWrapMode.Overflow;
        detailsText.horizontalOverflow = HorizontalWrapMode.Wrap;
        detailsText.text = "Debug: ItemDetailsPanel created by AutoSceneSetup";
        
        // Position text to fill the panel with padding
        var detailsTextRect = detailsTextObj.GetComponent<RectTransform>();
        detailsTextRect.anchorMin = new Vector2(0.02f, 0.02f);
        detailsTextRect.anchorMax = new Vector2(0.98f, 0.98f);
        detailsTextRect.anchoredPosition = Vector2.zero;
        detailsTextRect.sizeDelta = Vector2.zero;
        
        // Make the panel visible for debugging
        itemDetailsPanel.SetActive(true);
        
        Debug.Log("[AutoSceneSetup] Created ItemDetailsPanel directly in AutoSceneSetup");
        
        // Immediate verification
        if (itemDetailsPanel == null)
        {
            Debug.LogError("[AutoSceneSetup] CRITICAL: ItemDetailsPanel is null immediately after creation!");
        }
        else
        {
            Debug.Log($"[AutoSceneSetup] ItemDetailsPanel verification - Name: {itemDetailsPanel.name}, Parent: {itemDetailsPanel.transform.parent?.name ?? "NULL"}, Active: {itemDetailsPanel.activeSelf}");
        }
        
        // Add VendorUI component
        Debug.Log("[AutoSceneSetup] Adding VendorUI component to vendorPanel");
        try 
        {
            var vendorUI = vendorPanel.AddComponent<VendorUI>();
            if (vendorUI == null)
            {
                Debug.LogError("[AutoSceneSetup] Failed to add VendorUI component - AddComponent returned null!");
            }
            else
            {
                Debug.Log("[AutoSceneSetup] VendorUI component successfully added");
                vendorUI.VendorName = "General Merchant";
                vendorUI.VendorContainer = vendorContainer.transform;
                vendorUI.VendorTitleText = titleText;
                vendorUI.PlayerGoldText = goldText;
                vendorUI.ItemDetailsPanel = itemDetailsPanel;
                Debug.Log($"[AutoSceneSetup] VendorUI configured - Container: {vendorContainer.name}, VendorUI: {vendorUI != null}, ItemDetailsPanel: {itemDetailsPanel != null}");
                
                // Check if ItemDetailsPanel still exists after assignment
                if (itemDetailsPanel == null)
                {
                    Debug.LogError("[AutoSceneSetup] CRITICAL: ItemDetailsPanel became null after VendorUI assignment!");
                }
                else
                {
                    try 
                    {
                        string panelName = itemDetailsPanel.name; // This will throw if destroyed
                        Debug.Log($"[AutoSceneSetup] ItemDetailsPanel still exists after VendorUI assignment - Name: {panelName}");
                    }
                    catch (System.Exception e)
                    {
                        Debug.LogError($"[AutoSceneSetup] ItemDetailsPanel was destroyed after VendorUI assignment! Exception: {e.Message}");
                    }
                }
                
                // Verify the component was actually added
                var verifyComponent = vendorPanel.GetComponent<VendorUI>();
                if (verifyComponent == null)
                {
                    Debug.LogError("[AutoSceneSetup] VERIFICATION FAILED: VendorUI component not found on vendorPanel after adding!");
                }
                else
                {
                    Debug.Log("[AutoSceneSetup] VERIFICATION SUCCESS: VendorUI component confirmed on vendorPanel");
                }
            }
        }
        catch (System.Exception e)
        {
            Debug.LogError($"[AutoSceneSetup] Exception while adding VendorUI component: {e.Message}");
            Debug.Log("[AutoSceneSetup] Creating ItemDetailsPanel manually as fallback");
            CreateVendorItemDetailsPanelFallback(vendorContainer);
        }
        
        // Fallback check: if VendorUI component still doesn't exist, create ItemDetailsPanel manually
        if (vendorPanel.GetComponent<VendorUI>() == null)
        {
            Debug.LogWarning("[AutoSceneSetup] VendorUI component missing, creating ItemDetailsPanel as fallback");
            CreateVendorItemDetailsPanelFallback(vendorContainer);
        }
        
        // Configure close button to hide vendor panel
        closeButton.onClick.AddListener(() => {
            vendorPanel.SetActive(false);
            Debug.Log("Vendor panel closed via close button");
        });
        
        Debug.Log("VendorPanel created successfully with container, gold display, and close button (starts disabled)");
        return vendorPanel;
    }
    
    private void CreateVendorItemDetailsPanelFallback(GameObject vendorContainer)
    {
        Debug.Log("[AutoSceneSetup] Creating ItemDetailsPanel fallback");
        
        // Create item details panel (positioned on the right side)
        GameObject itemDetailsPanel = new GameObject("ItemDetailsPanel");
        itemDetailsPanel.transform.SetParent(vendorContainer.transform, false);
        
        // Add Image component for background
        var detailsBgImage = itemDetailsPanel.AddComponent<UnityEngine.UI.Image>();
        detailsBgImage.color = new Color(0.1f, 0.1f, 0.1f, 0.9f); // Dark background
        detailsBgImage.raycastTarget = false; // Don't block hover events
        
        // Position panel on the right side of vendor container
        var detailsRect = itemDetailsPanel.GetComponent<RectTransform>();
        detailsRect.anchorMin = new Vector2(0.65f, 0.05f);
        detailsRect.anchorMax = new Vector2(0.95f, 0.55f);
        detailsRect.anchoredPosition = Vector2.zero;
        detailsRect.sizeDelta = Vector2.zero;
        
        // Create text component for item details
        GameObject detailsTextObj = new GameObject("VendorDetailsText");
        detailsTextObj.transform.SetParent(itemDetailsPanel.transform, false);
        
        var detailsText = detailsTextObj.AddComponent<UnityEngine.UI.Text>();
        detailsText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        detailsText.fontSize = 14;
        detailsText.color = Color.white;
        detailsText.alignment = TextAnchor.UpperLeft;
        detailsText.verticalOverflow = VerticalWrapMode.Overflow;
        detailsText.horizontalOverflow = HorizontalWrapMode.Wrap;
        detailsText.text = "Hover over items to see details (VendorUI component missing)";
        
        // Position text to fill the panel with padding
        var detailsTextRect = detailsTextObj.GetComponent<RectTransform>();
        detailsTextRect.anchorMin = new Vector2(0.05f, 0.05f);
        detailsTextRect.anchorMax = new Vector2(0.95f, 0.95f);
        detailsTextRect.anchoredPosition = Vector2.zero;
        detailsTextRect.sizeDelta = Vector2.zero;
        
        // Make the panel visible so you can see it in the hierarchy
        itemDetailsPanel.SetActive(true);
        
        Debug.Log("[AutoSceneSetup] ItemDetailsPanel fallback created and made visible");
    }

    // OLD CreateMainUIHealthSlider method removed - using CreateSimpleHealthSlider instead

    private void ConnectHealthUIToUIManager()
    {
        Debug.Log("Connecting Simple Health UI to UIManager...");

        // Find UIManager
        UIManager uiManager = FindObjectOfType<UIManager>();
        if (uiManager == null)
        {
            Debug.LogWarning("UIManager not found - health UI integration skipped");
            return;
        }

        // Find SimplePlayerHealthBar
        SimplePlayerHealthBar simpleHealthBar = FindObjectOfType<SimplePlayerHealthBar>();
        if (simpleHealthBar != null)
        {
            // Connect SimplePlayerHealthBar to UIManager legacy fields for backward compatibility
            if (simpleHealthBar.HealthSlider != null)
            {
                SetFieldValue(uiManager, "HealthBar", simpleHealthBar.HealthSlider);
            }
            if (simpleHealthBar.HealthText != null)
            {
                SetFieldValue(uiManager, "HealthText", simpleHealthBar.HealthText);
            }

            Debug.Log("SimplePlayerHealthBar connected to UIManager with backward compatibility");
        }
        else
        {
            Debug.LogWarning("SimplePlayerHealthBar not found - UIManager integration incomplete");
        }
    }

    // SetupHealthBarDebugger method removed

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

    private void ConnectUIManagerReferences(GameObject inventoryPanel, GameObject characterPanel, GameObject chatPanel, GameObject vendorPanel, GameObject loginPanel)
    {
        Debug.Log("Connecting UI Manager references - ensuring UIManager is active...");
        
        // Find the UIManager component - wait for it to be properly initialized
        UIManager uiManager = FindObjectOfType<UIManager>();
        
        // If UIManager not found, wait a frame and try again
        if (uiManager == null)
        {
            Debug.LogWarning("UIManager not found on first attempt - GameManager may still be initializing...");
            
            // Try to find GameManager and get UIManager from it
            var gameManager = FindObjectOfType<GameManager>();
            if (gameManager != null)
            {
                uiManager = gameManager.GetComponent<UIManager>();
                Debug.Log($"Found UIManager via GameManager: {uiManager != null}");
            }
        }
        
        if (uiManager != null && uiManager.gameObject.activeInHierarchy)
        {
            Debug.Log("UIManager found and active - connecting panel references...");
            
            // Set the InventoryPanel field
            SetFieldValue(uiManager, "InventoryPanel", inventoryPanel);
            
            // Set the CharacterPanel field
            SetFieldValue(uiManager, "CharacterPanel", characterPanel);
            
            // Set the ChatPanel field
            SetFieldValue(uiManager, "ChatPanel", chatPanel);
            
            // Set the VendorPanel field
            SetFieldValue(uiManager, "VendorPanel", vendorPanel);
            
            // Set the LoginPanel field  
            SetFieldValue(uiManager, "LoginPanel", loginPanel);
            Debug.Log($"Connected LoginPanel to UIManager: panel={loginPanel?.name}, active={loginPanel?.activeInHierarchy}");
            
            // Verify the field was set correctly
            var verifyLoginPanel = uiManager.LoginPanel;
            Debug.Log($"Verification - UIManager.LoginPanel is now: {(verifyLoginPanel == null ? "NULL" : verifyLoginPanel.name)}");
            
            // Connect ChatDisplay and ChatInput to UIManager
            var chatDisplay = chatPanel.transform.Find("ChatDisplay")?.GetComponent<UnityEngine.UI.Text>();
            var chatInput = chatPanel.transform.Find("ChatInput")?.GetComponent<UnityEngine.UI.InputField>();
            var channelDropdown = chatPanel.transform.Find("ChannelDropdown")?.GetComponent<UnityEngine.UI.Dropdown>();
            
            if (chatDisplay != null)
            {
                SetFieldValue(uiManager, "ChatDisplay", chatDisplay);
                Debug.Log("ChatDisplay connected to UIManager");
            }
            
            if (chatInput != null)
            {
                SetFieldValue(uiManager, "ChatInput", chatInput);
                Debug.Log("ChatInput connected to UIManager");
            }
            
            if (channelDropdown != null)
            {
                SetFieldValue(uiManager, "ChannelDropdown", channelDropdown);
                Debug.Log("ChannelDropdown connected to UIManager");
            }
            
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
            
            Debug.Log("All UI panels successfully connected to UIManager");
        }
        else
        {
            Debug.LogWarning("UIManager not found or not active - panels will not be connected");
            Debug.LogWarning("This means the GameManager system may not have been created properly");
        }
    }

    /// <summary>
    /// Create basic prefabs for the grenade system
    /// </summary>
    private void CreateGrenadePrefabs(GrenadeManager grenadeManager)
    {
        Debug.Log("*** AUTO SCENE SETUP *** Creating grenade prefabs...");

        // Create basic grenade prefabs (simple spheres)
        grenadeManager.fragGrenadePrefab = CreateBasicGrenadePrefab("FragGrenade", Color.red);
        grenadeManager.smokeGrenadePrefab = CreateBasicGrenadePrefab("SmokeGrenade", Color.gray);
        grenadeManager.flashGrenadePrefab = CreateBasicGrenadePrefab("FlashGrenade", Color.yellow);

        // Create basic effect prefabs
        grenadeManager.explosionEffectPrefab = CreateBasicEffectPrefab("ExplosionEffect", new Color(1f, 0.5f, 0f), 2f); // Orange color
        grenadeManager.smokeEffectPrefab = CreateBasicEffectPrefab("SmokeEffect", Color.gray, 3f);
        grenadeManager.flashEffectPrefab = CreateBasicEffectPrefab("FlashEffect", Color.white, 4f);

        // Create warning indicator (red transparent sphere)
        grenadeManager.warningIndicatorPrefab = CreateWarningIndicatorPrefab();

        Debug.Log("*** AUTO SCENE SETUP *** Grenade prefabs created successfully");
    }

    private GameObject CreateBasicGrenadePrefab(string name, Color color)
    {
        GameObject prefab = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        prefab.name = name;
        prefab.transform.localScale = Vector3.one * 0.2f; // Small sphere

        // Color the grenade
        var renderer = prefab.GetComponent<Renderer>();
        if (renderer != null)
        {
            renderer.material.color = color;
        }

        // Make it a prefab by deactivating and not adding to scene
        prefab.SetActive(false);

        return prefab;
    }

    private GameObject CreateBasicEffectPrefab(string name, Color color, float size)
    {
        GameObject prefab = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        prefab.name = name;
        prefab.transform.localScale = Vector3.one * size;

        // Make it semi-transparent
        var renderer = prefab.GetComponent<Renderer>();
        if (renderer != null)
        {
            Material material = new Material(Shader.Find("Standard"));
            material.color = new Color(color.r, color.g, color.b, 0.6f);
            material.SetFloat("_Mode", 3); // Set to transparent mode
            material.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
            material.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
            material.SetInt("_ZWrite", 0);
            material.DisableKeyword("_ALPHATEST_ON");
            material.EnableKeyword("_ALPHABLEND_ON");
            material.DisableKeyword("_ALPHAPREMULTIPLY_ON");
            material.renderQueue = 3000;
            renderer.material = material;
        }

        // Remove collider since it's just an effect
        Collider collider = prefab.GetComponent<Collider>();
        if (collider != null)
        {
            DestroyImmediate(collider);
        }

        prefab.SetActive(false);
        return prefab;
    }

    private GameObject CreateWarningIndicatorPrefab()
    {
        GameObject prefab = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        prefab.name = "WarningIndicator";
        prefab.transform.localScale = Vector3.one;

        // Make it a red transparent warning circle
        var renderer = prefab.GetComponent<Renderer>();
        if (renderer != null)
        {
            Material material = new Material(Shader.Find("Standard"));
            material.color = new Color(1f, 0f, 0f, 0.3f); // Semi-transparent red
            material.SetFloat("_Mode", 3); // Transparent mode
            material.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
            material.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
            material.SetInt("_ZWrite", 0);
            material.DisableKeyword("_ALPHATEST_ON");
            material.EnableKeyword("_ALPHABLEND_ON");
            material.DisableKeyword("_ALPHAPREMULTIPLY_ON");
            material.renderQueue = 3000;
            renderer.material = material;
        }

        // Remove collider since it's just a warning indicator
        Collider collider = prefab.GetComponent<Collider>();
        if (collider != null)
        {
            DestroyImmediate(collider);
        }

        prefab.SetActive(false);
        return prefab;
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
            "• Inventory System (20 slots with placeholder icons)\n" +
            "• (Optional) Local test enemy for debugging", 
            MessageType.Info);
    }
}
#endif