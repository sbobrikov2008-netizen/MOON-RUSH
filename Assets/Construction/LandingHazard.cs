using UnityEngine;

internal sealed class LandingHazard : MonoBehaviour
{
    Mesh mesh;

    public static bool Overlaps(Vector3 pad, float zone, Vector3 structure, float footprint)
    {
        Vector3 offset = structure - pad;
        float limit = zone + footprint;
        return offset.x*offset.x + offset.z*offset.z <= limit*limit;
    }

    public static LandingHazard Create(Transform parent, Terrain ground, Vector3 center, float radius, Material material)
    {
        var root = new GameObject("Landing hazard preview", typeof(MeshFilter), typeof(MeshRenderer));
        root.transform.SetParent(parent, false);
        var hazard = root.AddComponent<LandingHazard>();
        hazard.mesh = new Mesh { name = "Terrain-following landing zone" };
        const int sectors = 96;
        int rings = Mathf.Clamp(Mathf.CeilToInt(radius/2.1f), 4, 90);
        var points = new Vector3[1 + rings*sectors];
        var triangles = new System.Collections.Generic.List<int>(sectors*(rings*6-3));
        Put(0,center);
        for(int ring=0;ring<rings;ring++)
        for(int sector=0;sector<sectors;sector++)
        {
            float angle=sector*Mathf.PI*2f/sectors;
            int index=1+ring*sectors+sector;
            int next=1+ring*sectors+(sector+1)%sectors;
            Put(index,center+new Vector3(Mathf.Cos(angle),0f,Mathf.Sin(angle))*radius*(ring+1f)/rings);
            if(ring==0)
            {
                triangles.Add(0);triangles.Add(next);triangles.Add(index);
            }
            else
            {
                triangles.Add(index-sectors);triangles.Add(next);triangles.Add(index);
                triangles.Add(index-sectors);triangles.Add(next-sectors);triangles.Add(next);
            }
        }
        hazard.mesh.SetVertices(points);
        hazard.mesh.SetTriangles(triangles,0);
        hazard.mesh.RecalculateBounds();
        root.GetComponent<MeshFilter>().sharedMesh=hazard.mesh;
        var renderer=root.GetComponent<MeshRenderer>();
        renderer.sharedMaterial=material;
        renderer.shadowCastingMode=UnityEngine.Rendering.ShadowCastingMode.Off;
        return hazard;

        void Put(int index,Vector3 point)
        {
            Vector3 min=ground.transform.position;
            Vector3 size=ground.terrainData.size;
            point.x=Mathf.Clamp(point.x,min.x,min.x+size.x);
            point.z=Mathf.Clamp(point.z,min.z,min.z+size.z);
            point.y=min.y+ground.SampleHeight(point)+.20f;
            points[index]=root.transform.InverseTransformPoint(point);
        }
    }

    void OnDestroy()=>Destroy(mesh);
}
