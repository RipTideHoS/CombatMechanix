using UnityEngine;

/// <summary>
/// Maps game levels to die-shaped enemy meshes (tabletop dice progression).
/// Sphere → D4 → D6 → D8 → D10 → D20 as levels advance.
/// </summary>
public static class EnemyShapeFactory
{
    public enum DieShape
    {
        Sphere, // Levels 1-3
        D4,     // Levels 4-5  (Tetrahedron)
        D6,     // Levels 6-7  (Cube)
        D8,     // Levels 8-9  (Octahedron)
        D10,    // Levels 10-19 (Trapezohedron)
        D20     // Levels 20+  (Icosahedron)
    }

    public static DieShape GetShapeForLevel(int level)
    {
        if (level <= 3) return DieShape.Sphere;
        if (level <= 5) return DieShape.D4;
        if (level <= 7) return DieShape.D6;
        if (level <= 9) return DieShape.D8;
        if (level <= 19) return DieShape.D10;
        return DieShape.D20;
    }

    /// <summary>
    /// Create a GameObject with the appropriate die-shape mesh for the given level.
    /// Includes MeshFilter, MeshRenderer, and Collider.
    /// </summary>
    public static GameObject CreateEnemyShape(int level)
    {
        DieShape shape = GetShapeForLevel(level);

        switch (shape)
        {
            case DieShape.Sphere:
                return CreateSphere();
            case DieShape.D4:
                return TetrahedronMesh.CreateTetrahedron();
            case DieShape.D6:
                return CreateCube();
            case DieShape.D8:
                return OctahedronMesh.CreateOctahedron();
            case DieShape.D10:
                return D10Mesh.CreateD10();
            case DieShape.D20:
                return IcosahedronMesh.CreateIcosahedron();
            default:
                return CreateSphere();
        }
    }

    /// <summary>
    /// Get the distance from the shape's centroid to its base face, for ground placement.
    /// Add this to ground height to position the enemy so it sits on the surface.
    /// </summary>
    public static float GetCentroidToBase(int level)
    {
        DieShape shape = GetShapeForLevel(level);

        switch (shape)
        {
            case DieShape.Sphere:
                return 0.5f; // Default sphere radius
            case DieShape.D4:
                return TetrahedronMesh.CentroidToBase;
            case DieShape.D6:
                return 0.5f; // Default cube half-extent
            case DieShape.D8:
                return OctahedronMesh.CentroidToBase;
            case DieShape.D10:
                return D10Mesh.CentroidToBase;
            case DieShape.D20:
                return IcosahedronMesh.CentroidToBase;
            default:
                return 0.5f;
        }
    }

    private static GameObject CreateSphere()
    {
        GameObject obj = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        // CreatePrimitive already adds MeshFilter, MeshRenderer, and SphereCollider
        return obj;
    }

    private static GameObject CreateCube()
    {
        GameObject obj = GameObject.CreatePrimitive(PrimitiveType.Cube);
        // CreatePrimitive already adds MeshFilter, MeshRenderer, and BoxCollider
        return obj;
    }
}
