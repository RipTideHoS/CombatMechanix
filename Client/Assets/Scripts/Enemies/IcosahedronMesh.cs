using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Generates a regular icosahedron (D20 die shape) mesh procedurally.
/// 12 vertices (golden ratio coordinates), 20 triangular faces, flat shading.
/// One face sits flat on the ground. Centered at centroid.
/// </summary>
public static class IcosahedronMesh
{
    public static readonly float CentroidToBase;

    static IcosahedronMesh()
    {
        // For a regular icosahedron with edge length 1, circumradius = sin(2π/5) ≈ 0.9511.
        // The distance from center to any face (inradius) = edge * φ² / (2√3) ≈ 0.7558
        // where φ = (1+√5)/2. We scale to edge=1 below.
        float phi = (1f + Mathf.Sqrt(5f)) / 2f;
        // With vertices at (0, ±1, ±φ) etc., edge length = 2.
        // Inradius for edge=2: φ² / √3
        // Scale factor to edge=1: 0.5
        // So inradius for edge=1: φ² / (2√3)
        CentroidToBase = phi * phi / (2f * Mathf.Sqrt(3f));
    }

    public static GameObject CreateIcosahedron()
    {
        var obj = new GameObject();
        var mesh = CreateMesh();

        obj.AddComponent<MeshFilter>().mesh = mesh;
        obj.AddComponent<MeshRenderer>();

        // Use SphereCollider instead of MeshCollider for reliable projectile hit detection.
        var collider = obj.AddComponent<SphereCollider>();
        collider.center = mesh.bounds.center;
        collider.radius = Mathf.Max(mesh.bounds.extents.x, mesh.bounds.extents.y, mesh.bounds.extents.z) * 1.15f;

        return obj;
    }

    public static Mesh CreateMesh()
    {
        float phi = (1f + Mathf.Sqrt(5f)) / 2f;

        // 12 vertices of a regular icosahedron (edge length = 2, we'll scale to 1)
        Vector3[] baseVerts =
        {
            new Vector3(-1,  phi, 0),
            new Vector3( 1,  phi, 0),
            new Vector3(-1, -phi, 0),
            new Vector3( 1, -phi, 0),

            new Vector3(0, -1,  phi),
            new Vector3(0,  1,  phi),
            new Vector3(0, -1, -phi),
            new Vector3(0,  1, -phi),

            new Vector3( phi, 0, -1),
            new Vector3( phi, 0,  1),
            new Vector3(-phi, 0, -1),
            new Vector3(-phi, 0,  1),
        };

        // Scale to edge length = 1 (current edge length is 2)
        float scale = 0.5f;
        for (int i = 0; i < baseVerts.Length; i++)
            baseVerts[i] *= scale;

        // 20 triangular faces
        int[][] faces =
        {
            // 5 faces around vertex 0
            new[] { 0, 11,  5 },
            new[] { 0,  5,  1 },
            new[] { 0,  1,  7 },
            new[] { 0,  7, 10 },
            new[] { 0, 10, 11 },

            // 5 adjacent faces
            new[] {  1,  5, 9 },
            new[] {  5, 11, 4 },
            new[] { 11, 10, 2 },
            new[] { 10,  7, 6 },
            new[] {  7,  1, 8 },

            // 5 faces around vertex 3
            new[] { 3, 9, 4 },
            new[] { 3, 4, 2 },
            new[] { 3, 2, 6 },
            new[] { 3, 6, 8 },
            new[] { 3, 8, 9 },

            // 5 adjacent faces
            new[] { 4, 9,  5 },
            new[] { 2, 4, 11 },
            new[] { 6, 2, 10 },
            new[] { 8, 6,  7 },
            new[] { 9, 8,  1 },
        };

        // Orient so one face is flat on the ground.
        // Pick face 0 (verts 0, 11, 5), rotate its outward normal to point DOWN.
        Vector3 fa = baseVerts[0], fb = baseVerts[11], fc = baseVerts[5];
        Vector3 faceCenter = (fa + fb + fc) / 3f;
        Vector3 faceNormal = Vector3.Cross(fb - fa, fc - fa).normalized;
        if (Vector3.Dot(faceNormal, faceCenter) < 0) faceNormal = -faceNormal;

        Quaternion rot = Quaternion.FromToRotation(faceNormal, Vector3.down);
        for (int i = 0; i < baseVerts.Length; i++)
            baseVerts[i] = rot * baseVerts[i];

        // Shift so the bottom face plane is at y = -CentroidToBase
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

            Vector3 fc2 = (va + vb + vc) / 3f;
            Vector3 n = Vector3.Cross(vb - va, vc - va).normalized;
            if (Vector3.Dot(n, fc2 - center) < 0)
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
            name = "Icosahedron",
            vertices = vertices.ToArray(),
            triangles = triangles.ToArray(),
            normals = normals.ToArray(),
            uv = uvs.ToArray()
        };

        mesh.RecalculateBounds();
        return mesh;
    }
}
