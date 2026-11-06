using UnityEngine;

public class LevelExit : MonoBehaviour
{
    void OnTriggerEnter(Collider other)
    {
        if (!other.CompareTag("Player")) return;
        if (LevelManager.Instance != null) LevelManager.Instance.TryFinishLevel();
    }
}
