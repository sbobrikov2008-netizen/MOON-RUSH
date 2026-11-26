using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

// Flat faces retain the stamped panels' edges and are grouped by material in one renderer.
internal sealed class ExpeditionMesh
{
    readonly List<Vector3> vertices = new List<Vector3>();
    readonly List<int>[] indices;
    internal ExpeditionMesh(int count)
    {
        indices = new List<int>[count];
        for (int i=0;i<count;i++) indices[i]=new List<int>();
    }

    internal void Face(int material, Vector3 a, Vector3 b, Vector3 c)
    {
        int start=vertices.Count;
        vertices.Add(a); vertices.Add(b); vertices.Add(c);
        indices[material].Add(start); indices[material].Add(start+1); indices[material].Add(start+2);
    }

    internal void Quad(int material, Vector3 a, Vector3 b, Vector3 c, Vector3 d)
    {
        Face(material,a,b,c); Face(material,a,c,d);
    }

    internal void Box(int material, Vector3 center, Vector3 size, Quaternion facing)
    {
        Vector3 P(float x,float y,float z) => center+facing*Vector3.Scale(size*.5f,new Vector3(x,y,z));
        Vector3 a=P(-1,-1,-1),b=P(1,-1,-1),c=P(1,1,-1),d=P(-1,1,-1);
        Vector3 e=P(-1,-1,1),f=P(1,-1,1),g=P(1,1,1),h=P(-1,1,1);
        Quad(material,d,c,b,a); Quad(material,e,f,g,h); Quad(material,a,e,h,d);
        Quad(material,f,b,c,g); Quad(material,d,h,g,c); Quad(material,b,f,e,a);
    }

    internal void Tube(int material,Vector3 a,Vector3 b,float r1,float r2,int sides=10)
    {
        Vector3 axis=(b-a).normalized;
        Vector3 right=Vector3.Cross(Mathf.Abs(axis.y) > .9f?Vector3.forward:Vector3.up,axis).normalized;
        Vector3 up=Vector3.Cross(axis,right);
        for (int i=0;i<sides;i++)
        {
            Vector3 u=Radial(i), v=Radial(i+1);
            Quad(material,a+u*r1,a+v*r1,b+v*r2,b+u*r2);
        }

        Vector3 Radial(int i)
        {
            float angle=i*Mathf.PI*2f/sides;
            return right*Mathf.Cos(angle)+up*Mathf.Sin(angle);
        }
    }

    internal Mesh Build(string name)
    {
        var result=new Mesh{name=name,indexFormat=IndexFormat.UInt32};
        result.SetVertices(vertices);
        result.subMeshCount=indices.Length;
        for (int i=0;i<indices.Length;i++) result.SetTriangles(indices[i],i);
        result.RecalculateNormals();
        result.RecalculateBounds();
        return result;
    }
}
