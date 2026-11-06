using UnityEngine;

public class PlayerBackstab : MonoBehaviour
{
    public float attackRange = 2f;
    [Tooltip("behindDot must be <= -this. 0.5 = 120° cone behind the enemy.")]
    [Range(0f, 1f)] public float behindThreshold = 0.5f;
    [Tooltip("facingDot must be >= this. 0.5 = enemy within 60° of the player's forward.")]
    [Range(0f, 1f)] public float facingThreshold = 0.5f;
    public float attackCooldown = 0.6f;

    [Header("Optional")]
    public AudioSource audioSource;
    public AudioClip successClip;
    public AudioClip failClip;

    public EnemyGhost NearestEnemy { get; private set; }
    public float NearestDistance { get; private set; }
    public float BehindDot { get; private set; }
    public float FacingDot { get; private set; }

    PlayerController controller;
    float lastAttackTime = -999f;

    void Awake()
    {
        controller = GetComponent<PlayerController>();
    }

    void Update()
    {
        UpdateNearestEnemy();
        GameHUD.SetBackstabInfo(NearestEnemy != null, NearestDistance, BehindDot, FacingDot,
                                IsBehind(BehindDot), IsFacing(FacingDot), attackRange);

        bool canAct = controller == null || controller.InputEnabled;
        if (canAct && GameInput.AttackPressed && Time.time - lastAttackTime >= attackCooldown)
        {
            lastAttackTime = Time.time;
            TryBackstab();
        }
    }

    void UpdateNearestEnemy()
    {
        NearestEnemy = null;
        NearestDistance = float.MaxValue;
        foreach (EnemyGhost e in EnemyGhost.All)
        {
            if (e == null || !e.IsAlive) continue;
            Vector3 d = e.transform.position - transform.position; d.y = 0f;
            float dist = d.magnitude;
            if (dist < NearestDistance) { NearestDistance = dist; NearestEnemy = e; }
        }

        if (NearestEnemy != null)
        {
            ComputeDots(NearestEnemy, out float behind, out float facing);
            BehindDot = behind;
            FacingDot = facing;
        }
    }

    public void ComputeDots(EnemyGhost enemy, out float behindDot, out float facingDot)
    {
        Vector3 enemyForward = enemy.FlatForward;

        Vector3 enemyToPlayer = transform.position - enemy.transform.position;
        enemyToPlayer.y = 0f;
        enemyToPlayer = enemyToPlayer.sqrMagnitude > 1e-6f ? enemyToPlayer.normalized : -enemyForward;

        Vector3 playerForward = transform.forward; playerForward.y = 0f; playerForward.Normalize();
        Vector3 playerToEnemy = -enemyToPlayer;

        behindDot = Vector3.Dot(enemyForward, enemyToPlayer);
        facingDot = Vector3.Dot(playerForward, playerToEnemy);
    }

    bool IsBehind(float dot) => dot <= -behindThreshold;
    bool IsFacing(float dot) => dot >= facingThreshold;

    void TryBackstab()
    {
        if (NearestEnemy == null || NearestDistance > attackRange)
        {
            GameHUD.Show("ATTACK FAILED\n<size=60%>No enemy in range</size>", GameHUD.FailColor, 1.5f);
            Play(failClip);
            return;
        }

        float angle = Mathf.Acos(Mathf.Clamp(BehindDot, -1f, 1f)) * Mathf.Rad2Deg;

        if (IsBehind(BehindDot) && IsFacing(FacingDot))
        {
            NearestEnemy.Die();
            GameHUD.Show("BACKSTAB SUCCESSFUL!\n<size=60%>dot = " + BehindDot.ToString("F2") +
                         "   angle = " + angle.ToString("F0") + "°</size>", GameHUD.SuccessColor, 2f);
            Play(successClip);
        }
        else
        {
            string reason = !IsBehind(BehindDot) ? "You are not behind the enemy" : "You are not facing the enemy";
            GameHUD.Show("ATTACK FAILED\n<size=60%>" + reason + "  (dot = " + BehindDot.ToString("F2") + ")</size>",
                         GameHUD.FailColor, 2f);
            Play(failClip);
            NearestEnemy.Alert();
        }
    }

    void Play(AudioClip clip)
    {
        if (audioSource != null && clip != null) audioSource.PlayOneShot(clip);
    }

    void OnDrawGizmosSelected()
    {
        Gizmos.color = new Color(1f, 0.5f, 0f);
        Gizmos.DrawWireSphere(transform.position, attackRange);
    }
}
