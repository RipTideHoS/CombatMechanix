using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Generates a regular octahedron (D8 die shape) mesh procedurally.
/// One face sits flat on the ground. Centered at centroid (geometric center).
/// </summary>
public static class OctahedronMesh
{
    // Distance from centroid to the lowest face plane
    public static readonly float CentroidToBase;

    static OctahedronMesh()
    {
        // For a regular octahedron with edge length 1, half-height = 1/sqrt(2).
        // We rotate so a face is flat on the ground. The face centroid is at
        // distance = 1/sqrt(6) from the geometric center along the face normal.
        CentroidToBase = 1f / Mathf.Sqrt(6f); // ≈ 0.4082
    }

    public static GameObject CreateOctahedron()
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

    public static Mesh CreateMesh()
    {
        // Regular octahedron edge length 1, centered at origin.
        // Standard vertices at (±a, 0, 0), (0, ±a, 0), (0, 0, ±a) where a = 1/sqrt(2).
        float a = 1f / Mathf.Sqrt(2f);

        Vector3[] baseVerts =
        {
            new Vector3( a,  0,  0), // 0: +X
            new Vector3(-a,  0,  0), // 1: -X
            new Vector3( 0,  a,  0), // 2: +Y
            new Vector3( 0, -a,  0), // 3: -Y
            new Vector3( 0,  0,  a), // 4: +Z
            new Vector3( 0,  0, -a), // 5: -Z
        };

        // 8 triangular faces (CCW winding when viewed from outside)
        int[][] faces =
        {
            new[] { 0, 2, 4 },
            new[] { 4, 2, 1 },
            new[] { 1, 2, 5 },
            new[] { 5, 2, 0 },
            new[] { 4, 3, 0 },
            new[] { 1, 3, 4 },
            new[] { 5, 3, 1 },
            new[] { 0, 3, 5 },
        };

        // We want one face flat on the ground. Pick face 0 (verts 0,2,4).
        // Its normal points outward. We'll rotate so that face's outward normal points DOWN (-Y).
        Vector3 faceCenter = (baseVerts[0] + baseVerts[2] + baseVerts[4]) / 3f;
        Vector3 faceNormal = Vector3.Cross(baseVerts[2] - baseVerts[0], baseVerts[4] - baseVerts[0]).normalized;
        if (Vector3.Dot(faceNormal, faceCenter) < 0) faceNormal = -faceNormal;

        // Rotate so faceNormal maps to -Y
        Quaternion rot = Quaternion.FromToRotation(faceNormal, Vector3.down);
        for (int i = 0; i < baseVerts.Length; i++)
            baseVerts[i] = rot * baseVerts[i];

        // Shift vertically so the bottom face sits at y = -CentroidToBase
        float minY = float.MaxValue;
        for (int i = 0; i < baseVerts.Length; i++)
            if (baseVerts[i].y < minY) minY = baseVerts[i].y;

        float shift = -CentroidToBase - minY;
        for (int i = 0; i < baseVerts.Length; i++)
            baseVerts[i] += Vector3.up * shift;

        // Geometric center after shift (was at origin before, now shifted up)
        Vector3 center = Vector3.up * shift;

        // Build flat-shaded mesh
        var vertices = new List<Vector3>();
        var triangles = new List<int>();
        var normals = new List<Vector3>();
        var uvs = new List<Vector2>();

        foreach (var face in faces)
        {
            Vector3 va = baseVerts[face[0]];
            Vector3 vb = baseVerts[face[1]];
            Vector3 vc = baseVerts[face[2]];

            Vector3 fc = (va + vb + vc) / 3f;
            Vector3 n = Vector3.Cross(vb - va, vc - va).normalized;
            if (Vector3.Dot(n, fc - center) < 0)
            {
                n = -n;
                (vb, vc) = (vc, vb);
            }

            int idx = vertices.Count;
            vertices.Add(va); vertices.Add(vb); vertices.Add(vc);
            normals.Add(n); normals.Add(n); normals.Add(n);
            uvs.Add(new Vector2(0.5f, 1f));
            uvs.Add(new Vector2(0f, 0f));
            uvs.Add(new Vector2(1f, 0f));
            triangles.Add(idx); triangles.Add(idx + 1); triangles.Add(idx + 2);
        }

        Mesh mesh = new Mesh
        {
            name = "Octahedron",
            vertices = vertices.ToArray(),
            triangles = triangles.ToArray(),
            normals = normals.ToArray(),
            uv = uvs.ToArray()
        };

        mesh.RecalculateBounds();
        return mesh;
    }
}
