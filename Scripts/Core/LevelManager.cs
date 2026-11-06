using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

public class LevelManager : MonoBehaviour
{
    public static LevelManager Instance { get; private set; }

    [Header("Level Info")]
    public int levelNumber = 1;
    public string levelName = "The Hallway";
    [TextArea] public string objective = "Reach the exit. Backstab the ghost to unlock it.";

    [Header("Spawning")]
    public Transform spawnPoint;
    [Tooltip("Falling below this world Y respawns the player.")]
    public float killY = -15f;

    [Header("Progression")]
    public bool requireAllEnemiesDefeated = true;
    [Tooltip("Objects to hide when the exit unlocks (e.g. a ghostly barrier on the door).")]
    public GameObject[] hideWhenUnlocked;
    [Tooltip("Objects to show when the exit unlocks (e.g. a glowing exit light).")]
    public GameObject[] showWhenUnlocked;
    [Tooltip("Scene to load after this level. Leave empty on the final level.")]
    public string nextSceneName = "Level2_Rooms";
    public string mainMenuSceneName = "MainMenu";
    public bool isFinalLevel = false;

    public bool ExitUnlocked { get; private set; }

    PlayerController player;
    Vector3 checkpointPos;
    Quaternion checkpointRot;
    bool finishing;

    void Awake()
    {
        Instance = this;
        Time.timeScale = 1f;
    }

    void Start()
    {
        player = FindFirstObjectByType<PlayerController>();
        if (spawnPoint != null)
        {
            checkpointPos = spawnPoint.position;
            checkpointRot = spawnPoint.rotation;
        }
        else if (player != null)
        {
            checkpointPos = player.transform.position;
            checkpointRot = player.transform.rotation;
        }
        RespawnPlayer();

        foreach (GameObject g in showWhenUnlocked) if (g != null) g.SetActive(false);
        ExitUnlocked = !requireAllEnemiesDefeated || CountAliveEnemies() == 0;
        if (ExitUnlocked) Unlock(false);

        GameHUD.SetLevel("<b>LEVEL " + levelNumber + ": " + levelName.ToUpper() + "</b>\n<size=70%>" + objective + "</size>");
        GameHUD.Show("LEVEL " + levelNumber + "\n<size=60%>" + levelName + "</size>", GameHUD.InfoColor, 2.5f);
    }

    void OnDestroy() { if (Instance == this) Instance = null; }

    void Update()
    {
        if (player == null || finishing) return;

        if (player.transform.position.y < killY)
        {
            GameHUD.Show("YOU FELL...", GameHUD.FailColor, 1.5f);
            RespawnPlayer();
        }

        if (GameInput.RestartPressed) SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
    }

    public void SetCheckpoint(Transform t)
    {
        checkpointPos = t.position;
        checkpointRot = t.rotation;
    }

    public void RespawnPlayer()
    {
        if (player != null) player.Teleport(checkpointPos, checkpointRot);
    }

    int CountAliveEnemies()
    {
        int n = 0;
        foreach (EnemyGhost e in EnemyGhost.All) if (e != null && e.IsAlive) n++;
        return n;
    }

    public void OnEnemyDefeated(EnemyGhost enemy)
    {
        if (ExitUnlocked) return;
        if (CountAliveEnemies() == 0) Unlock(true);
    }

    void Unlock(bool announce)
    {
        ExitUnlocked = true;
        foreach (GameObject g in hideWhenUnlocked) if (g != null) g.SetActive(false);
        foreach (GameObject g in showWhenUnlocked) if (g != null) g.SetActive(true);
        if (announce) StartCoroutine(AnnounceUnlock());
    }

    IEnumerator AnnounceUnlock()
    {
        yield return new WaitForSeconds(2.1f);
        GameHUD.Show("THE EXIT IS UNLOCKED", GameHUD.InfoColor, 2.5f);
    }

    public void TryFinishLevel()
    {
        if (finishing) return;
        if (!ExitUnlocked)
        {
            GameHUD.Show("THE DOOR WON'T OPEN\n<size=60%>Backstab the ghost first</size>", GameHUD.FailColor, 2f);
            return;
        }
        StartCoroutine(FinishRoutine());
    }

    IEnumerator FinishRoutine()
    {
        finishing = true;
        if (player != null) player.InputEnabled = false;

        if (isFinalLevel || string.IsNullOrEmpty(nextSceneName))
        {
            GameHUD.Show("YOU ESCAPED MAPUA!\n<size=50%>Thanks for playing</size>", GameHUD.SuccessColor, 5f);
            yield return new WaitForSeconds(5f);
            ThirdPersonCamera.SetCursorLocked(false);
            SceneManager.LoadScene(mainMenuSceneName);
        }
        else
        {
            GameHUD.Show("LEVEL " + levelNumber + " CLEAR", GameHUD.SuccessColor, 2f);
            yield return new WaitForSeconds(2f);
            SceneManager.LoadScene(nextSceneName);
        }
    }

    void OnDrawGizmos()
    {
        Gizmos.color = new Color(1f, 0f, 0f, 0.25f);
        Vector3 c = spawnPoint != null ? spawnPoint.position : transform.position;
        Gizmos.DrawCube(new Vector3(c.x, killY, c.z), new Vector3(60f, 0.05f, 60f));
    }
}
