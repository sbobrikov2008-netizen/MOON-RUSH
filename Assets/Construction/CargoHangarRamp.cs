using UnityEngine;

public sealed class CargoHangarRamp : MonoBehaviour
{
    internal static Rect Footprint => Rect.MinMaxRect(-5.6f, 10.9f, 5.6f, 22.9f);
    const int Rows = 24, Columns = 8;
    const float Deck = .62f;
    Mesh surface;
    MeshCollider collision;
    Vector3 previousOrigin;
    Quaternion previousFacing;
    Terrain previousTerrain;
    bool previousFit;

    internal static CargoHangarRamp Add(Transform parent, Material[] materials)
    {
        var root = new GameObject("Terrain-fitted cargo ramp", typeof(MeshFilter), typeof(MeshRenderer), typeof(MeshCollider));
        root.transform.SetParent(parent, false);
        var ramp = root.AddComponent<CargoHangarRamp>();
        ramp.surface = new Mesh { name = "Longitudinal cargo ramp panels" };
        ramp.surface.MarkDynamic();
        root.GetComponent<MeshFilter>().sharedMesh = ramp.surface;
        root.GetComponent<MeshRenderer>().sharedMaterials = new[] { materials[1], materials[3] };
        ramp.collision = root.GetComponent<MeshCollider>();
        ramp.Fit(null, parent.position, parent.rotation, false);
        return ramp;
    }

    internal bool Fit(Terrain terrain, Vector3 origin, Quaternion facing, bool solid)
    {
        if (terrain != null && terrain == previousTerrain && (previousOrigin-origin).sqrMagnitude < .0004f &&
            facing == previousFacing && !solid) return previousFit;
        previousOrigin=origin; previousFacing=facing; previousTerrain=terrain;
        Rect rect=Footprint;
        var vertices=new Vector3[(Rows+1)*(Columns+1)];
        var deck=new System.Collections.Generic.List<int>();
        var lanes=new System.Collections.Generic.List<int>();
        bool fits=true;
        for(int x=0;x<=Columns;x++)
        {
            float across=Mathf.Lerp(rect.xMin,rect.xMax,x/(float)Columns);
            float end=Ground(across,rect.yMax);
            for(int row=0;row<=Rows;row++)
            {
                float t=row/(float)Rows, z=Mathf.Lerp(rect.yMin,rect.yMax,t);
                float desired=Mathf.Lerp(Deck,end,t*t*(3f-2f*t));
                float ground=Ground(across,z);
                int index=row*(Columns+1)+x;
                float y=row==0?Deck:Mathf.Max(desired,ground);
                vertices[index]=new Vector3(across,y,z);
                if (row==0 && ground>Deck+.03f || row>0 &&
                    Mathf.Abs(y-vertices[index-Columns-1].y)>.33f*rect.height/Rows) fits=false;
                if (x>0 && Mathf.Abs(y-vertices[index-1].y)>.24f*rect.width/Columns) fits=false;
                if(row==0||x==0) continue;
                int lower=index-Columns-1;
                var triangles=x==1||x==Columns?lanes:deck;
                triangles.Add(lower-1); triangles.Add(index-1); triangles.Add(index);
                triangles.Add(lower-1); triangles.Add(index); triangles.Add(lower);
            }
        }
        surface.Clear();
        surface.SetVertices(vertices);
        surface.subMeshCount=2;
        surface.SetTriangles(deck,0); surface.SetTriangles(lanes,1);
        surface.RecalculateNormals(); surface.RecalculateBounds();
        if(solid) collision.sharedMesh=surface;
        previousFit=fits;
        return fits;

        float Ground(float x,float z)
        {
            if(terrain==null) return .05f;
            Vector3 point=origin+facing*new Vector3(x,0,z);
            return terrain.transform.position.y+terrain.SampleHeight(point)-origin.y+.041f;
        }
    }

    void OnDestroy()=>Destroy(surface);
}
