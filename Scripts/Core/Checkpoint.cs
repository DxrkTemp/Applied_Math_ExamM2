using UnityEngine;

public class Checkpoint : MonoBehaviour
{
    public string message = "CHECKPOINT";
    bool used;

    void OnTriggerEnter(Collider other)
    {
        if (used || !other.CompareTag("Player")) return;
        used = true;
        if (LevelManager.Instance != null) LevelManager.Instance.SetCheckpoint(transform);
        GameHUD.Show(message, GameHUD.InfoColor, 1.5f);
    }
}
