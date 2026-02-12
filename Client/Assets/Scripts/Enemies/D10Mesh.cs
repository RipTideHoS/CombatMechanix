using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Generates a pentagonal trapezohedron (D10 die shape) mesh procedurally.
/// 12 vertices (2 apex + two staggered rings of 5), 10 kite faces (20 triangles).
/// Proportions match a real tabletop D10: tall, pointed apexes with a narrow equatorial band.
/// One kite face sits flat on the ground. Centered at centroid.
/// </summary>
public static class D10Mesh
{
    // Shape proportions based on real pentagonal trapezohedron geometry.
    // Ring height offset ~= tan(6°) * radius ≈ 0.105 * radius, per reference:
    // https://aqandrew.com/blog/10-sided-die-react/
    private const float RingRadius = 0.55f;
    private const float ApexHeight = 0.65f;
    private const float RingHeight = 0.058f;

    public static readonly float CentroidToBase;

    static D10Mesh()
    {
        float angleStep = 2f * Mathf.PI / 5f;
        float halfStagger = angleStep / 2f;

        // Bottom apex
        Vector3 bottomApex = new Vector3(0, -ApexHeight, 0);
        // First two vertices of bottom ring
        float angle0 = 0f;
        Vector3 br0 = new Vector3(RingRadius * Mathf.Cos(angle0), -RingHeight, RingRadius * Mathf.Sin(angle0));
        float angle1 = angleStep;
        Vector3 br1 = new Vector3(RingRadius * Mathf.Cos(angle1), -RingHeight, RingRadius * Mathf.Sin(angle1));
        // Adjacent top ring vertex
        float topAngle = halfStagger;
        Vector3 tr0 = new Vector3(RingRadius * Mathf.Cos(topAngle), RingHeight, RingRadius * Mathf.Sin(topAngle));

        // One bottom kite face: bottomApex, br0, tr0, br1
        Vector3 faceCenter = (bottomApex + br0 + tr0 + br1) / 4f;
        Vector3 normal = Vector3.Cross(br0 - bottomApex, tr0 - bottomApex).normalized;
        if (Vector3.Dot(normal, faceCenter) < 0) normal = -normal;

        // Distance from origin to face plane along normal
        float dist = Mathf.Abs(Vector3.Dot(normal, bottomApex));
        CentroidToBase = dist;
    }

    public static GameObject CreateD10()
    {
        var obj = new GameObject();
        var mesh = CreateMesh();

        obj.AddComponent<MeshFilter>().mesh = mesh;
        obj.AddComponent<MeshRenderer>();

        // Use SphereCollider instead of MeshCollider for reliable projectile hit detection.
        // The faceted trapezohedron mesh has thin kite faces that fast projectiles can slip through.
        var collider = obj.AddComponent<SphereCollider>();
        collider.center = mesh.bounds.center;
        collider.radius = Mathf.Max(mesh.bounds.extents.x, mesh.bounds.extents.y, mesh.bounds.extents.z) * 1.15f;

        return obj;
    }

    public static Mesh CreateMesh()
    {
        float angleStep = 2f * Mathf.PI / 5f;
        float halfStagger = angleStep / 2f;

        // Build the 12 base vertices
        Vector3 topApex = new Vector3(0, ApexHeight, 0);
        Vector3 bottomApex = new Vector3(0, -ApexHeight, 0);

        Vector3[] topRing = new Vector3[5];
        Vector3[] bottomRing = new Vector3[5];

        for (int i = 0; i < 5; i++)
        {
            float topAngle = i * angleStep + halfStagger;
            topRing[i] = new Vector3(RingRadius * Mathf.Cos(topAngle), RingHeight, RingRadius * Mathf.Sin(topAngle));

            float bottomAngle = i * angleStep;
            bottomRing[i] = new Vector3(RingRadius * Mathf.Cos(bottomAngle), -RingHeight, RingRadius * Mathf.Sin(bottomAngle));
        }

        // 10 kite faces: 5 top kites + 5 bottom kites
        // Each kite is 2 triangles
        // Top kite i: topApex, topRing[i], bottomRing[(i+1)%5], topRing[(i+1)%5]
        // Bottom kite i: bottomApex, bottomRing[(i+1)%5], topRing[i], bottomRing[i]

        // We want one face flat on the ground. Pick bottom kite 0:
        // bottomApex, bottomRing[1], topRing[0], bottomRing[0]
        Vector3 faceA = bottomApex;
        Vector3 faceB = bottomRing[0];
        Vector3 faceC = topRing[0];
        Vector3 faceD = bottomRing[1];

        Vector3 faceCenter = (faceA + faceB + faceC + faceD) / 4f;
        Vector3 faceNormal = Vector3.Cross(faceB - faceA, faceC - faceA).normalized;
        if (Vector3.Dot(faceNormal, faceCenter) < 0) faceNormal = -faceNormal;

        // Rotate so this face's outward normal points DOWN
        Quaternion rot = Quaternion.FromToRotation(faceNormal, Vector3.down);

        topApex = rot * topApex;
        bottomApex = rot * bottomApex;
        for (int i = 0; i < 5; i++)
        {
            topRing[i] = rot * topRing[i];
            bottomRing[i] = rot * bottomRing[i];
        }

        // Shift so bottom face is at y = -CentroidToBase
        float minY = float.MaxValue;
        CheckMin(ref minY, topApex.y);
        CheckMin(ref minY, bottomApex.y);
        for (int i = 0; i < 5; i++)
        {
            CheckMin(ref minY, topRing[i].y);
            CheckMin(ref minY, bottomRing[i].y);
        }

        float shift = -CentroidToBase - minY;
        topApex += Vector3.up * shift;
        bottomApex += Vector3.up * shift;
        for (int i = 0; i < 5; i++)
        {
            topRing[i] += Vector3.up * shift;
            bottomRing[i] += Vector3.up * shift;
        }

        // Geometric center after shift (was at origin before, now shifted up)
        Vector3 center = Vector3.up * shift;

        // Build mesh with flat shading
        var vertices = new List<Vector3>();
        var triangles = new List<int>();
        var normals = new List<Vector3>();
        var uvs = new List<Vector2>();

        // 5 top kites
        for (int i = 0; i < 5; i++)
        {
            int next = (i + 1) % 5;
            // Top kite: topApex, topRing[i], bottomRing[next], topRing[next]
            AddKiteFace(vertices, triangles, normals, uvs,
                topApex, topRing[i], bottomRing[next], topRing[next], center);
        }

        // 5 bottom kites
        for (int i = 0; i < 5; i++)
        {
            int next = (i + 1) % 5;
            // Bottom kite: bottomApex, bottomRing[next], topRing[i], bottomRing[i]
            AddKiteFace(vertices, triangles, normals, uvs,
                bottomApex, bottomRing[next], topRing[i], bottomRing[i], center);
        }

        Mesh mesh = new Mesh
        {
            name = "D10_Trapezohedron",
            vertices = vertices.ToArray(),
            triangles = triangles.ToArray(),
            normals = normals.ToArray(),
            uv = uvs.ToArray()
        };

        mesh.RecalculateBounds();
        return mesh;
    }

    private static void AddKiteFace(List<Vector3> vertices, List<int> triangles,
        List<Vector3> normals, List<Vector2> uvs,
        Vector3 apex, Vector3 left, Vector3 bottom, Vector3 right, Vector3 meshCenter)
    {
        // Kite = 2 triangles sharing the apex-bottom diagonal
        // Triangle 1: apex, left, bottom
        // Triangle 2: apex, bottom, right
        Vector3 faceCenter = (apex + left + bottom + right) / 4f;
        Vector3 normal = Vector3.Cross(left - apex, bottom - apex).normalized;
        if (Vector3.Dot(normal, faceCenter - meshCenter) < 0) normal = -normal;

        // Triangle 1: apex, left, bottom
        AddTriangle(vertices, triangles, normals, uvs, apex, left, bottom, normal, faceCenter, meshCenter);
        // Triangle 2: apex, bottom, right
        AddTriangle(vertices, triangles, normals, uvs, apex, bottom, right, normal, faceCenter, meshCenter);
    }

    private static void AddTriangle(List<Vector3> vertices, List<int> triangles,
        List<Vector3> normals, List<Vector2> uvs,
        Vector3 a, Vector3 b, Vector3 c, Vector3 faceNormal, Vector3 faceCenter, Vector3 meshCenter)
    {
        Vector3 triNormal = Vector3.Cross(b - a, c - a).normalized;
        // Ensure winding matches outward normal
        if (Vector3.Dot(triNormal, faceCenter - meshCenter) < 0)
        {
            (b, c) = (c, b);
        }

        int idx = vertices.Count;
        vertices.Add(a); vertices.Add(b); vertices.Add(c);
        normals.Add(faceNormal); normals.Add(faceNormal); normals.Add(faceNormal);
        uvs.Add(new Vector2(0.5f, 1f));
        uvs.Add(new Vector2(0f, 0f));
        uvs.Add(new Vector2(1f, 0f));
        triangles.Add(idx); triangles.Add(idx + 1); triangles.Add(idx + 2);
    }

    private static void CheckMin(ref float min, float val)
    {
        if (val < min) min = val;
    }
}
