using UnityEngine;

internal sealed class IndustrialPipeVisual : MonoBehaviour
{
    Material body, collar, stripe;
    Transform geometry;
    Vector3[] previous;

    internal void Initialize(Material bodyMaterial, Material collarMaterial, Material stripeMaterial)
    {
        body = bodyMaterial;
        collar = collarMaterial;
        stripe = stripeMaterial;
    }

    internal void SetRoute(Vector3[] route)
    {
        if (previous != null && previous.Length == route.Length)
        {
            int sample=0;
            while(sample<route.Length && previous[sample]==route[sample]) sample++;
            if(sample==route.Length) return;
        }
        previous=route;
        if (geometry != null)
        {
            geometry.gameObject.SetActive(false);
            Destroy(geometry.gameObject);
        }
        geometry = new GameObject("Pipe segments").transform;
        geometry.SetParent(transform, false);
        float nextFitting = .25f;
        for (int i = 1; i < route.Length; i++)
        {
            Vector3 a = route[i-1], b = route[i];
            float length = Vector3.Distance(a,b);
            if (length < .001f) continue;
            Vector3 axis = (b-a)/length;
            Quaternion rotation = Quaternion.FromToRotation(Vector3.up,axis);
            Part(PrimitiveType.Cylinder,(a+b)*.5f,rotation,new Vector3(.62f,(length+.065f)*.5f,.62f),body,true);
            while (nextFitting < length)
            {
                Vector3 position = a + axis*nextFitting;
                Part(PrimitiveType.Cylinder,position,rotation,new Vector3(.77f,.085f,.77f),collar,false);
                Part(PrimitiveType.Cylinder,position+axis*.09f,rotation,new Vector3(.66f,.033f,.66f),stripe,false);
                Part(PrimitiveType.Cube,position-Vector3.up*.34f,Quaternion.identity,new Vector3(.46f,.13f,.43f),collar,false);
                nextFitting += 3.15f;
            }
            nextFitting -= length;
        }
    }

    void Part(PrimitiveType type, Vector3 point, Quaternion rotation, Vector3 scale, Material material, bool pickable)
    {
        GameObject part = GameObject.CreatePrimitive(type);
        part.transform.SetParent(geometry,false);
        part.transform.SetPositionAndRotation(point,rotation);
        part.transform.localScale = scale;
        part.GetComponent<Renderer>().sharedMaterial = material;
        Collider original = part.GetComponent<Collider>();
        original.enabled = false;
        Destroy(original);
        if (!pickable) return;
        part.layer = 2;
        part.AddComponent<BoxCollider>().isTrigger = true;
    }
}
