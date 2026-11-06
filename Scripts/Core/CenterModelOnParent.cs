using UnityEngine;

public class CenterModelOnParent : MonoBehaviour
{
    public bool centerOnAwake = true;
    public bool fitHeight = true;
    public float targetHeight = 1.8f;
    [Tooltip("Extra rotation if the model faces the wrong way (Y degrees).")]
    public float yawCorrection = 0f;

    [SerializeField, HideInInspector] bool hasOriginal;
    [SerializeField, HideInInspector] Quaternion originalRotation = Quaternion.identity;

    void Awake()
    {
        if (centerOnAwake) CenterNow();
    }

    [ContextMenu("Center Now")]
    public void CenterNow()
    {
        Transform parent = transform.parent;
        if (parent == null) return;

        if (!hasOriginal) { originalRotation = transform.localRotation; hasOriginal = true; }
        transform.localRotation = Quaternion.Euler(0f, yawCorrection, 0f) * originalRotation;

        if (fitHeight)
        {
            Bounds b0;
            if (!GetBounds(out b0) || b0.size.y < 1e-5f) return;
            float factor = targetHeight / b0.size.y;
            transform.localScale *= factor;
        }

        Bounds b;
        if (!GetBounds(out b)) return;
        Vector3 bottomCentre = new Vector3(b.center.x, b.min.y, b.center.z);
        Vector3 shift = parent.position - bottomCentre;
        transform.position += shift;
    }

    bool GetBounds(out Bounds bounds)
    {
        bounds = new Bounds();
        bool has = false;
        foreach (Renderer r in GetComponentsInChildren<Renderer>())
        {
            if (!has) { bounds = r.bounds; has = true; }
            else bounds.Encapsulate(r.bounds);
        }
        return has;
    }
}
