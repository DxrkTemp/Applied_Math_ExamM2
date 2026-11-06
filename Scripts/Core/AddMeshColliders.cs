using UnityEngine;

public class AddMeshColliders : MonoBehaviour
{
    public bool addOnAwake = true;
    [Tooltip("Usually OFF. Non-convex works for static levels AND for MatrixPlatforms (they use a kinematic Rigidbody).")]
    public bool convex = false;

    void Awake()
    {
        if (addOnAwake) AddNow();
    }

    [ContextMenu("Add Colliders Now")]
    public void AddNow()
    {
        int added = 0;
        foreach (MeshFilter mf in GetComponentsInChildren<MeshFilter>(true))
        {
            if (mf.sharedMesh == null) continue;
            if (mf.GetComponent<Collider>() != null) continue;
            MeshCollider mc = mf.gameObject.AddComponent<MeshCollider>();
            mc.sharedMesh = mf.sharedMesh;
            mc.convex = convex;
            added++;
        }
        if (added > 0) Debug.Log("[AddMeshColliders] Added " + added + " MeshColliders under " + name);
    }

    [ContextMenu("Remove All MeshColliders")]
    public void RemoveAll()
    {
        foreach (MeshCollider mc in GetComponentsInChildren<MeshCollider>(true))
        {
            if (Application.isPlaying) Destroy(mc); else DestroyImmediate(mc);
        }
    }
}
