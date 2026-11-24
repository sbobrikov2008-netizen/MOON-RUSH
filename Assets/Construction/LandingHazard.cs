using System.Collections.Generic;
using UnityEngine;

internal sealed class LandingHazard : MonoBehaviour
{
    Mesh mesh;

    public static bool Overlaps(Vector3 pad, float zone, Vector3 structure, float footprint)
    {
        Vector3 offset = structure - pad;
        float limit = zone + footprint;
        return offset.x * offset.x + offset.z * offset.z <= limit * limit;
    }

    public static LandingHazard Create(Transform parent, Terrain ground, Vector3 center, float radius, Material material)
    {
        var root = new GameObject("Dashed landing exclusion marker", typeof(MeshFilter), typeof(MeshRenderer));
        root.transform.SetParent(parent, false);
        var hazard = root.AddComponent<LandingHazard>();
        hazard.mesh = new Mesh { name = "Dashed terrain-conforming orange ring" };
        var renderer = root.GetComponent<MeshRenderer>();
        renderer.sharedMaterial = material;
        renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        root.GetComponent<MeshFilter>().sharedMesh = hazard.mesh;
        hazard.Fit(ground, center, radius);
        return hazard;
    }

    public void Fit(Terrain terrain, Vector3 center, float radius)
    {
        int dashes = Mathf.Clamp(Mathf.CeilToInt(Mathf.PI * 2f * radius / 5.2f), 15, 140);
        const int subdivisions = 4;
        var points = new List<Vector3>(dashes * (subdivisions + 1) * 2);
        var triangles = new List<int>(dashes * subdivisions * 6);
        for (int dash = 0; dash < dashes; dash++)
        for (int step = 0; step <= subdivisions; step++)
        {
            float angle = Mathf.PI * 2f * (dash + .61f * step / subdivisions) / dashes;
            Vector3 direction = new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle));
            int index = points.Count;
            points.Add(Sample(center + direction * (radius - .47f)));
            points.Add(Sample(center + direction * (radius + .47f)));
            if (step == 0) continue;
            triangles.Add(index-2); triangles.Add(index+1); triangles.Add(index-1);
            triangles.Add(index-2); triangles.Add(index); triangles.Add(index+1);
        }
        mesh.Clear();
        mesh.SetVertices(points);
        mesh.SetTriangles(triangles, 0);
        mesh.RecalculateBounds();

        Vector3 Sample(Vector3 point)
        {
            Vector3 min = terrain.transform.position, size = terrain.terrainData.size;
            point.x = Mathf.Clamp(point.x, min.x, min.x + size.x);
            point.z = Mathf.Clamp(point.z, min.z, min.z + size.z);
            point.y = min.y + terrain.SampleHeight(point) + .23f;
            return transform.InverseTransformPoint(point);
        }
    }

    void OnDestroy() => Destroy(mesh);
}
