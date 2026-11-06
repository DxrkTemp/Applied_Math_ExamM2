using UnityEngine;

public class KillZone : MonoBehaviour
{
    public string message = "YOU FELL...";

    void OnTriggerEnter(Collider other)
    {
        if (!other.CompareTag("Player")) return;
        GameHUD.Show(message, GameHUD.FailColor, 1.5f);
        if (LevelManager.Instance != null) LevelManager.Instance.RespawnPlayer();
    }
}
