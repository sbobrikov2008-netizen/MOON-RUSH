using UnityEngine;

[RequireComponent(typeof(MeshFilter))]
internal sealed class ExpeditionMeshLifetime : MonoBehaviour
{
    void OnDestroy() => Destroy(GetComponent<MeshFilter>().sharedMesh);
}
