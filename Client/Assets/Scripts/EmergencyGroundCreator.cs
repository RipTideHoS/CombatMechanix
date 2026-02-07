using UnityEngine;

/// <summary>
/// Emergency ground creator that ensures there's always ground in the scene
/// This runs independently of AutoSceneSetup to guarantee the player doesn't fall
/// </summary>
public class EmergencyGroundCreator : MonoBehaviour
{
    [Header("Emergency Ground Settings")]
    public bool ForceCreateGround = true;
    public float GroundSize = 500f; // 500x500 units
    public float CheckDelay = 1f; // Wait 1 second before checking

    private void Start()
    {
        Debug.Log("🚨 EMERGENCY GROUND CREATOR STARTED 🚨");

        if (ForceCreateGround)
        {
            // Wait a moment for other scripts to run first
            Invoke(nameof(EnsureGroundExists), CheckDelay);
        }
    }

    private void EnsureGroundExists()
    {
        Debug.Log("🚨 CHECKING IF GROUND EXISTS...");

        GameObject ground = GameObject.Find("Ground");

        if (ground == null)
        {
            Debug.Log("🚨 NO GROUND FOUND - CREATING EMERGENCY GROUND!");
            CreateEmergencyGround();
        }
        else
        {
            Debug.Log($"🚨 Ground found: {ground.name} at {ground.transform.position}");

            // Check if the ground has proper collision
            Collider groundCollider = ground.GetComponent<Collider>();
            if (groundCollider == null)
            {
                Debug.Log("🚨 Ground has no collider - adding one!");
                AddColliderToGround(ground);
            }
            else
            {
                Debug.Log("🚨 Ground has collider - player should not fall!");
            }
        }
    }

    private void CreateEmergencyGround()
    {
        Debug.Log("🚨 Creating emergency ground plane...");

        // Create a large ground plane
        GameObject ground = GameObject.CreatePrimitive(PrimitiveType.Plane);
        ground.name = "Ground";
        ground.transform.position = Vector3.zero;
        ground.transform.localScale = new Vector3(GroundSize / 10f, 1, GroundSize / 10f); // Plane is 10x10 by default

        // Make it green so it's visible
        Renderer renderer = ground.GetComponent<Renderer>();
        if (renderer != null)
        {
            renderer.material.color = new Color(0.3f, 0.7f, 0.3f); // Bright green for visibility
        }

        Debug.Log($"🚨 ✅ Emergency ground created! Size: {GroundSize}x{GroundSize} units");
        Debug.Log($"🚨 ✅ Ground position: {ground.transform.position}");
        Debug.Log($"🚨 ✅ Ground active: {ground.activeInHierarchy}");
    }

    private void AddColliderToGround(GameObject ground)
    {
        // If it's a terrain, ensure TerrainCollider exists
        Terrain terrain = ground.GetComponent<Terrain>();
        if (terrain != null)
        {
            TerrainCollider terrainCollider = ground.GetComponent<TerrainCollider>();
            if (terrainCollider == null)
            {
                terrainCollider = ground.AddComponent<TerrainCollider>();
                terrainCollider.terrainData = terrain.terrainData;
                Debug.Log("🚨 Added TerrainCollider to existing terrain");
            }
        }
        else
        {
            // Add a mesh collider for other types of ground
            MeshCollider meshCollider = ground.AddComponent<MeshCollider>();
            Debug.Log("🚨 Added MeshCollider to existing ground");
        }
    }

    // Manual trigger for testing
    [ContextMenu("Force Create Emergency Ground")]
    public void ForceCreateEmergencyGround()
    {
        Debug.Log("🚨 MANUALLY TRIGGERED - Force creating emergency ground");
        CreateEmergencyGround();
    }

    [ContextMenu("Check Ground Status")]
    public void CheckGroundStatus()
    {
        EnsureGroundExists();
    }
}