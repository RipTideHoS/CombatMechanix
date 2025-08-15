using UnityEngine;

public class GameStartup : MonoBehaviour
{
    [Header("Required Manager Prefabs")]
    public GameObject GameManagerPrefab;
    public GameObject NetworkManagerPrefab;
    public GameObject UIManagerPrefab;
    public GameObject WorldManagerPrefab;
    public GameObject CombatSystemPrefab;
    public GameObject ChatSystemPrefab;
    public GameObject InventoryManagerPrefab;

    [Header("Player Prefabs")]
    public GameObject LocalPlayerPrefab;

    [Header("Startup Settings")]
    public bool AutoStartGame = true;
    public bool CreateMissingManagers = true;

    private void Awake()
    {
        Debug.Log("GameStartup: Initializing game systems...");
        
        if (CreateMissingManagers)
        {
            EnsureManagersExist();
        }
        
        if (AutoStartGame)
        {
            CreateLocalPlayer();
        }
        
        Debug.Log("GameStartup: Initialization complete!");
    }

    private void EnsureManagersExist()
    {
        // Check for GameManager (should be the main hub)
        if (FindObjectOfType<GameManager>() == null)
        {
            CreateGameManager();
        }

        // Individual managers are usually components on GameManager,
        // but this method can create standalone managers if needed
        EnsureComponentExists<NetworkManager>(NetworkManagerPrefab, "NetworkManager");
        EnsureComponentExists<WorldManager>(WorldManagerPrefab, "WorldManager");
        EnsureComponentExists<UIManager>(UIManagerPrefab, "UIManager");
        EnsureComponentExists<CombatSystem>(CombatSystemPrefab, "CombatSystem");
        EnsureComponentExists<ChatSystem>(ChatSystemPrefab, "ChatSystem");
        EnsureComponentExists<InventoryManager>(InventoryManagerPrefab, "InventoryManager");
    }

    private void CreateGameManager()
    {
        GameObject gameManagerObj;
        
        if (GameManagerPrefab != null)
        {
            gameManagerObj = Instantiate(GameManagerPrefab);
            gameManagerObj.name = "GameManager";
        }
        else
        {
            // Create GameManager with all required components
            gameManagerObj = new GameObject("GameManager");
            
            // Add all manager components
            gameManagerObj.AddComponent<GameManager>();
            gameManagerObj.AddComponent<NetworkManager>();
            gameManagerObj.AddComponent<WorldManager>();
            gameManagerObj.AddComponent<UIManager>();
            gameManagerObj.AddComponent<CombatSystem>();
            gameManagerObj.AddComponent<ChatSystem>();
            gameManagerObj.AddComponent<InventoryManager>();
            
            // Add AudioSource for sound effects
            gameManagerObj.AddComponent<AudioSource>();
        }
        
        Debug.Log("GameStartup: Created GameManager with all components");
    }

    private void EnsureComponentExists<T>(GameObject prefab, string objectName) where T : MonoBehaviour
    {
        if (FindObjectOfType<T>() == null)
        {
            GameObject obj;
            
            if (prefab != null)
            {
                obj = Instantiate(prefab);
                obj.name = objectName;
            }
            else
            {
                // Create standalone manager
                obj = new GameObject(objectName);
                obj.AddComponent<T>();
           }
           
           Debug.Log($"GameStartup: Created {objectName}");
       }
   }

   private void CreateLocalPlayer()
   {
       // Don't create if one already exists
       if (FindObjectOfType<PlayerController>() != null)
       {
           Debug.Log("GameStartup: LocalPlayer already exists");
           return;
       }

       GameObject localPlayerObj;
       
       if (LocalPlayerPrefab != null)
       {
           localPlayerObj = Instantiate(LocalPlayerPrefab);
           localPlayerObj.name = "LocalPlayer";
       }
       else
       {
           // Create basic local player
           localPlayerObj = new GameObject("LocalPlayer");
           
           // Add required components
           localPlayerObj.AddComponent<CharacterController>();
           localPlayerObj.AddComponent<PlayerController>();
           
           // Create visual representation
           CreatePlayerVisual(localPlayerObj);
       }
       
       // Set initial position so CharacterController bottom sits on ground
       localPlayerObj.transform.position = new Vector3(0, 1f, 0);
       
       Debug.Log("GameStartup: Created LocalPlayer");
   }

   private void CreatePlayerVisual(GameObject playerObj)
   {
       // Create a simple capsule for player representation
       var visualObj = GameObject.CreatePrimitive(PrimitiveType.Capsule);
       visualObj.name = "PlayerModel";
       visualObj.transform.SetParent(playerObj.transform);
       // Visual mesh should align with CharacterController center (which is now at transform origin)
       visualObj.transform.localPosition = new Vector3(0, 0f, 0);
       
       // Remove the capsule collider (CharacterController handles collision)
       var capsuleCollider = visualObj.GetComponent<Collider>();
       if (capsuleCollider != null)
       {
           DestroyImmediate(capsuleCollider);
       }
       
       // Set a blue color for local player
       var renderer = visualObj.GetComponent<Renderer>();
       if (renderer != null)
       {
           renderer.material.color = Color.blue;
       }
   }

   private void Start()
   {
       // Verify all systems are working
       VerifySystemsAreWorking();
   }

   private void VerifySystemsAreWorking()
   {
       bool allSystemsOK = true;
       
       // Check GameManager
       var gameManager = FindObjectOfType<GameManager>();
       if (gameManager == null)
       {
           Debug.LogError("GameStartup: GameManager not found!");
           allSystemsOK = false;
       }
       
       // Check NetworkManager
       var networkManager = FindObjectOfType<NetworkManager>();
       if (networkManager == null)
       {
           Debug.LogError("GameStartup: NetworkManager not found!");
           allSystemsOK = false;
       }
       
       // Check LocalPlayer
       var localPlayer = FindObjectOfType<PlayerController>();
       if (localPlayer == null)
       {
           Debug.LogError("GameStartup: LocalPlayer not found!");
           allSystemsOK = false;
       }
       
       // Check UIManager
       var uiManager = FindObjectOfType<UIManager>();
       if (uiManager == null)
       {
           Debug.LogError("GameStartup: UIManager not found!");
           allSystemsOK = false;
       }
       
       // Check Camera
       var mainCamera = Camera.main;
       if (mainCamera == null)
       {
           Debug.LogWarning("GameStartup: No Main Camera found!");
           // Create a basic camera
           CreateBasicCamera();
       }
       
       if (allSystemsOK)
       {
           Debug.Log("GameStartup: All systems verified and working!");
       }
       else
       {
           Debug.LogError("GameStartup: Some systems are missing! Check the errors above.");
       }
   }

   private void CreateBasicCamera()
   {
       var cameraObj = new GameObject("Main Camera");
       var camera = cameraObj.AddComponent<Camera>();
       cameraObj.tag = "MainCamera";
       
       // Set up camera for maximum wide angled view of player
       cameraObj.transform.position = new Vector3(0, 35, -25);
       cameraObj.transform.LookAt(new Vector3(0, 1, 0)); // Look at player spawn point
       
       // Add AudioListener
       cameraObj.AddComponent<AudioListener>();
       
       Debug.Log("GameStartup: Created basic camera");
   }

   // Public methods for manual initialization
   public void ManualInitialize()
   {
       Debug.Log("GameStartup: Manual initialization started");
       EnsureManagersExist();
       CreateLocalPlayer();
       VerifySystemsAreWorking();
   }

   public void RestartGame()
   {
       Debug.Log("GameStartup: Restarting game...");
       
       // Find and destroy existing managers
       var existingGameManager = FindObjectOfType<GameManager>();
       if (existingGameManager != null)
       {
           DestroyImmediate(existingGameManager.gameObject);
       }
       
       var existingPlayer = FindObjectOfType<PlayerController>();
       if (existingPlayer != null)
       {
           DestroyImmediate(existingPlayer.gameObject);
       }
       
       // Recreate everything
       ManualInitialize();
   }

   // Utility method to check if game is ready
   public bool IsGameReady()
   {
       return FindObjectOfType<GameManager>() != null &&
              FindObjectOfType<NetworkManager>() != null &&
              FindObjectOfType<PlayerController>() != null &&
              FindObjectOfType<UIManager>() != null;
   }

   // Method to create test environment
   public void CreateTestEnvironment()
   {
       Debug.Log("GameStartup: Creating test environment...");
       
       // Create some test resource nodes
       CreateTestResources();
       
       // Create test lighting
       CreateTestLighting();
       
       Debug.Log("GameStartup: Test environment created");
   }

   private void CreateTestResources()
   {
       var resourceTypes = new[] { "Wood", "Stone", "Iron", "Gold" };
      var colors = new[] { new Color(0.6f, 0.3f, 0.1f), Color.gray, Color.white, Color.yellow };
       
       for (int i = 0; i < 10; i++)
       {
           var resourceObj = GameObject.CreatePrimitive(PrimitiveType.Cube);
           resourceObj.name = $"TestResource_{i}";
           
           // Random position around the origin
           Vector3 randomPos = new Vector3(
               Random.Range(-20f, 20f),
               0,
               Random.Range(-20f, 20f)
           );
           resourceObj.transform.position = randomPos;
           
           // Add ResourceNodeClient component
           var resourceNode = resourceObj.AddComponent<ResourceNodeClient>();
           
           // Create fake resource data
           int typeIndex = Random.Range(0, resourceTypes.Length);
           var fakeResource = new ResourceNode
           {
               ResourceId = System.Guid.NewGuid().ToString(),
               ResourceType = resourceTypes[typeIndex],
               Position = randomPos,
               CurrentAmount = Random.Range(50, 100),
               MaxAmount = 100,
               LastHarvested = System.DateTime.UtcNow
           };
           
           resourceNode.Initialize(fakeResource);
           
           // Set color
           var renderer = resourceObj.GetComponent<Renderer>();
           if (renderer != null)
           {
               renderer.material.color = colors[typeIndex];
           }
       }
   }

   private void CreateTestLighting()
   {
       // Create directional light if none exists
       var existingLight = FindObjectOfType<Light>();
       if (existingLight == null)
       {
           var lightObj = new GameObject("Directional Light");
           var light = lightObj.AddComponent<Light>();
           light.type = LightType.Directional;
           light.intensity = 1f;
           light.color = Color.white;
           lightObj.transform.rotation = Quaternion.Euler(50, -30, 0);
       }
       
       // Set ambient lighting
       RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Trilight;
       RenderSettings.ambientSkyColor = Color.blue;
       RenderSettings.ambientEquatorColor = Color.gray;
       RenderSettings.ambientGroundColor = Color.black;
   }

   // Debug method to show system status
   [ContextMenu("Show System Status")]
   public void ShowSystemStatus()
   {
       Debug.Log("=== GAME SYSTEM STATUS ===");
       Debug.Log($"GameManager: {(FindObjectOfType<GameManager>() != null ? "✓" : "✗")}");
       Debug.Log($"NetworkManager: {(FindObjectOfType<NetworkManager>() != null ? "✓" : "✗")}");
       Debug.Log($"PlayerController: {(FindObjectOfType<PlayerController>() != null ? "✓" : "✗")}");
       Debug.Log($"WorldManager: {(FindObjectOfType<WorldManager>() != null ? "✓" : "✗")}");
       Debug.Log($"UIManager: {(FindObjectOfType<UIManager>() != null ? "✓" : "✗")}");
       Debug.Log($"CombatSystem: {(FindObjectOfType<CombatSystem>() != null ? "✓" : "✗")}");
       Debug.Log($"ChatSystem: {(FindObjectOfType<ChatSystem>() != null ? "✓" : "✗")}");
       Debug.Log($"InventoryManager: {(FindObjectOfType<InventoryManager>() != null ? "✓" : "✗")}");
       Debug.Log($"Main Camera: {(Camera.main != null ? "✓" : "✗")}");
       Debug.Log($"Game Ready: {(IsGameReady() ? "✓" : "✗")}");
       Debug.Log("========================");
   }

   private void OnValidate()
   {
       // This runs in the editor when values change
       if (Application.isPlaying) return;
       
       // You can add editor-time validation here
   }
}