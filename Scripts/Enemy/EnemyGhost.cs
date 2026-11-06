using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class EnemyGhost : MonoBehaviour
{
    public static readonly List<EnemyGhost> All = new List<EnemyGhost>();

    public enum State { Patrol, Chase, Dead }

    [Header("Patrol (direction vectors)")]
    public Transform[] waypoints;
    public float patrolSpeed = 1.6f;
    public float waitAtWaypoint = 1.2f;
    public float turnSpeed = 5f;

    [Header("Vision (dot product)")]
    public float viewDistance = 7f;
    [Tooltip("Full cone angle the ghost can see, in degrees.")]
    public float viewAngle = 100f;
    public LayerMask sightBlockers = ~0;
    public float eyeHeight = 1.5f;

    [Header("Chase / Catch")]
    public bool chasePlayer = true;
    public float chaseSpeed = 2.8f;
    public float catchDistance = 1.2f;
    [Tooltip("Seconds the ghost keeps chasing after losing sight.")]
    public float loseInterestTime = 2f;

    [Header("Look")]
    [Tooltip("Tints every renderer of the model (Character prefab) so the enemy looks different from the player.")]
    public bool tintModel = true;
    public Color tint = new Color(0.55f, 0.75f, 1f, 1f);
    public bool floatBob = true;
    public float bobHeight = 0.12f;
    public float bobSpeed = 1.5f;
    public Transform model;

    public State CurrentState { get; private set; } = State.Patrol;
    public bool IsAlive => CurrentState != State.Dead;

    public Vector3 FlatForward
    {
        get { Vector3 f = transform.forward; f.y = 0f; return f.sqrMagnitude > 0f ? f.normalized : Vector3.forward; }
    }

    Transform player;
    PlayerController playerController;
    int waypointIndex;
    float waitTimer;
    float lastSeenTime = -999f;
    float catchCooldown;
    Vector3 modelBaseLocalPos;

    void OnEnable() { All.Add(this); }
    void OnDisable() { All.Remove(this); }

    void Start()
    {
        GameObject p = GameObject.FindGameObjectWithTag("Player");
        if (p != null)
        {
            player = p.transform;
            playerController = p.GetComponent<PlayerController>();
        }
        if (model == null && transform.childCount > 0) model = transform.GetChild(0);
        if (model != null) modelBaseLocalPos = model.localPosition;
        if (tintModel) ApplyTint(tint);
    }

    void Update()
    {
        if (CurrentState == State.Dead) return;
        if (catchCooldown > 0f) catchCooldown -= Time.deltaTime;

        if (floatBob && model != null)
            model.localPosition = modelBaseLocalPos + Vector3.up * (Mathf.Sin(Time.time * bobSpeed * 2f * Mathf.PI) * bobHeight);

        bool sees = CanSeePlayer(out float dist);
        if (sees) lastSeenTime = Time.time;

        if (chasePlayer && sees) CurrentState = State.Chase;
        else if (CurrentState == State.Chase && Time.time - lastSeenTime > loseInterestTime) CurrentState = State.Patrol;

        if (CurrentState == State.Chase) Chase(dist);
        else Patrol();
    }

    public bool CanSeePlayer(out float distance)
    {
        distance = float.MaxValue;
        if (player == null) return false;

        Vector3 toPlayer = player.position - transform.position;
        toPlayer.y = 0f;
        distance = toPlayer.magnitude;
        if (distance > viewDistance || distance < 0.001f) return false;

        float dot = Vector3.Dot(FlatForward, toPlayer / distance);
        float cosHalfAngle = Mathf.Cos(viewAngle * 0.5f * Mathf.Deg2Rad);
        if (dot < cosHalfAngle) return false;

        Vector3 eye = transform.position + Vector3.up * eyeHeight;
        Vector3 target = player.position + Vector3.up * 1f;
        if (Physics.Linecast(eye, target, out RaycastHit hit, sightBlockers, QueryTriggerInteraction.Ignore))
        {
            if (!hit.transform.IsChildOf(player) && !hit.transform.IsChildOf(transform)) return false;
        }
        return true;
    }

    void Patrol()
    {
        if (waypoints == null || waypoints.Length == 0) return;
        Transform wp = waypoints[waypointIndex];
        if (wp == null) return;

        Vector3 toWp = wp.position - transform.position;
        toWp.y = 0f;

        if (toWp.magnitude < 0.2f)
        {
            waitTimer += Time.deltaTime;
            if (waitTimer >= waitAtWaypoint)
            {
                waitTimer = 0f;
                waypointIndex = (waypointIndex + 1) % waypoints.Length;
            }
            return;
        }
        MoveAlong(toWp.normalized, patrolSpeed);
    }

    void Chase(float dist)
    {
        if (player == null) return;
        Vector3 dir = player.position - transform.position;
        dir.y = 0f;
        if (dist <= catchDistance)
        {
            Face(dir.normalized);
            if (catchCooldown <= 0f) CatchPlayer();
            return;
        }
        MoveAlong(dir.normalized, chaseSpeed);
    }

    void MoveAlong(Vector3 direction, float speed)
    {
        transform.position += direction * speed * Time.deltaTime;
        Face(direction);
    }

    void Face(Vector3 direction)
    {
        if (direction.sqrMagnitude < 1e-6f) return;
        Quaternion target = Quaternion.LookRotation(direction, Vector3.up);
        transform.rotation = Quaternion.Slerp(transform.rotation, target, turnSpeed * Time.deltaTime);
    }

    void CatchPlayer()
    {
        catchCooldown = 2f;
        CurrentState = State.Patrol;
        GameHUD.Show("THE GHOST CAUGHT YOU!", GameHUD.FailColor, 2f);
        if (LevelManager.Instance != null) LevelManager.Instance.RespawnPlayer();
    }

    public void Alert()
    {
        if (!IsAlive || player == null) return;
        lastSeenTime = Time.time;
        if (chasePlayer) CurrentState = State.Chase;
        Vector3 dir = player.position - transform.position; dir.y = 0f;
        if (dir.sqrMagnitude > 0.001f) transform.rotation = Quaternion.LookRotation(dir.normalized, Vector3.up);
    }

    public void Die()
    {
        if (!IsAlive) return;
        CurrentState = State.Dead;
        foreach (Collider c in GetComponentsInChildren<Collider>()) c.enabled = false;
        StartCoroutine(DeathRoutine());
        if (LevelManager.Instance != null) LevelManager.Instance.OnEnemyDefeated(this);
    }

    IEnumerator DeathRoutine()
    {
        Quaternion start = transform.rotation;
        Quaternion end = start * Quaternion.Euler(85f, 0f, 0f);
        float t = 0f;
        while (t < 1f)
        {
            t += Time.deltaTime / 0.6f;
            transform.rotation = Quaternion.Slerp(start, end, t);
            if (tintModel) ApplyTint(Color.Lerp(tint, Color.black, t));
            yield return null;
        }
        yield return new WaitForSeconds(1.5f);
        gameObject.SetActive(false);
    }

    void ApplyTint(Color c)
    {
        MaterialPropertyBlock block = new MaterialPropertyBlock();
        foreach (Renderer r in GetComponentsInChildren<Renderer>())
        {
            r.GetPropertyBlock(block);
            block.SetColor("_BaseColor", c);
            block.SetColor("_Color", c);
            r.SetPropertyBlock(block);
        }
    }

    void OnDrawGizmosSelected()
    {
        Vector3 pos = transform.position + Vector3.up * 0.1f;
        Vector3 f = transform.forward; f.y = 0f; f.Normalize();

        Gizmos.color = Color.red;
        Vector3 left = Quaternion.Euler(0f, -viewAngle * 0.5f, 0f) * f;
        Vector3 right = Quaternion.Euler(0f, viewAngle * 0.5f, 0f) * f;
        Gizmos.DrawLine(pos, pos + left * viewDistance);
        Gizmos.DrawLine(pos, pos + right * viewDistance);

        Gizmos.color = Color.green;
        Vector3 back = -f;
        Gizmos.DrawLine(pos, pos + Quaternion.Euler(0f, -60f, 0f) * back * 2f);
        Gizmos.DrawLine(pos, pos + Quaternion.Euler(0f, 60f, 0f) * back * 2f);

        if (waypoints != null)
        {
            Gizmos.color = Color.yellow;
            for (int i = 0; i < waypoints.Length; i++)
            {
                if (waypoints[i] == null) continue;
                Gizmos.DrawWireSphere(waypoints[i].position, 0.25f);
                Transform next = waypoints[(i + 1) % waypoints.Length];
                if (next != null) Gizmos.DrawLine(waypoints[i].position, next.position);
            }
        }
    }
}
