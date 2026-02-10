using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Generates a regular tetrahedron (D4 die shape) mesh procedurally.
/// One face sits flat on the ground, apex points up. Centered at centroid.
/// </summary>
public static class TetrahedronMesh
{
    // Distance from centroid to base — use this to position the tetrahedron on the ground
    public static readonly float CentroidToBase;

    static TetrahedronMesh()
    {
        float h = Mathf.Sqrt(2f / 3f); // height for edge=1
        CentroidToBase = h / 4f;
    }

    /// <summary>
    /// Create a GameObject with tetrahedron mesh, renderer, and convex collider.
    /// Drop-in replacement for GameObject.CreatePrimitive(PrimitiveType.Cube).
    /// </summary>
    public static GameObject CreateTetrahedron()
    {
        var obj = new GameObject();
        var mesh = CreateMesh();

        obj.AddComponent<MeshFilter>().mesh = mesh;
        obj.AddComponent<MeshRenderer>();

        var collider = obj.AddComponent<MeshCollider>();
        collider.sharedMesh = mesh;
        collider.convex = true;

        return obj;
    }

    /// <summary>
    /// Create a regular tetrahedron mesh (edge length 1, centered at centroid)
    /// Uses flat shading with separate vertices per face for sharp edges.
    /// </summary>
    public static Mesh CreateMesh()
    {
        float edge = 1.0f;
        float h = edge * Mathf.Sqrt(2f / 3f);           // height ≈ 0.8165
        float R = edge / Mathf.Sqrt(3f);                 // base circumradius ≈ 0.5774
        float r = edge / (2f * Mathf.Sqrt(3f));          // base inradius ≈ 0.2887
        float cy = h / 4f;                                // centroid height ≈ 0.2041

        // 4 vertices of regular tetrahedron, centered at centroid
        Vector3 v0 = new Vector3(0f, -cy, R);             // front base
        Vector3 v1 = new Vector3(-edge / 2f, -cy, -r);    // back-left base
        Vector3 v2 = new Vector3(edge / 2f, -cy, -r);     // back-right base
        Vector3 v3 = new Vector3(0f, h - cy, 0f);         // apex

        Vector3[] baseVerts = { v0, v1, v2, v3 };

        // Define 4 triangular faces (vertex indices into baseVerts)
        int[][] faces =
        {
            new[] { 0, 1, 2 }, // base
            new[] { 0, 1, 3 }, // front-left
            new[] { 1, 2, 3 }, // back
            new[] { 2, 0, 3 }, // front-right
        };

        var vertices = new List<Vector3>();
        var triangles = new List<int>();
        var normals = new List<Vector3>();
        var uvs = new List<Vector2>();

        for (int f = 0; f < 4; f++)
        {
            Vector3 a = baseVerts[faces[f][0]];
            Vector3 b = baseVerts[faces[f][1]];
            Vector3 c = baseVerts[faces[f][2]];

            Vector3 faceCenter = (a + b + c) / 3f;
            Vector3 normal = Vector3.Cross(b - a, c - a).normalized;

            // Ensure normal points outward (away from centroid at origin)
            if (Vector3.Dot(normal, faceCenter) < 0)
            {
                normal = -normal;
                // Swap b and c to fix winding
                (b, c) = (c, b);
            }

            int idx = vertices.Count;
            vertices.Add(a);
            vertices.Add(b);
            vertices.Add(c);
            normals.Add(normal);
            normals.Add(normal);
            normals.Add(normal);
            uvs.Add(new Vector2(0.5f, 1f));
            uvs.Add(new Vector2(0f, 0f));
            uvs.Add(new Vector2(1f, 0f));
            triangles.Add(idx);
            triangles.Add(idx + 1);
            triangles.Add(idx + 2);
        }

        Mesh mesh = new Mesh
        {
            name = "Tetrahedron",
            vertices = vertices.ToArray(),
            triangles = triangles.ToArray(),
            normals = normals.ToArray(),
            uv = uvs.ToArray()
        };

        mesh.RecalculateBounds();
        return mesh;
    }
}
