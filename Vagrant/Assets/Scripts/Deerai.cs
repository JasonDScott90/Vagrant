using System.Collections;
using UnityEngine;
using UnityEngine.AI;

/// Wildlife AI for a deer: grazes, wanders, gets alert when it sees or hears the player, flees when scared or hurt,
/// and dies when its health runs out. Needs a baked NavMesh. Put this on the deer root next to a NavMeshAgent.
[RequireComponent(typeof(NavMeshAgent))]
public class DeerAI : MonoBehaviour
{
    enum State { Graze, Wander, Alert, Flee }

    [Header("Health")]
    public float maxHealth = 100f;
    public float corpseLifetime = 60f;    // seconds before the body disappears
    public bool autoCollider = true;      // adds a hit box if the deer has no collider (so arrows can hit it)

    [Header("Meat drop")]
    public string meatItemId = "raw_meat"; // item spawned on the ground when it dies
    public int meatPerChunk = 3;           // meat per pickup lying on the ground
    public int meatChunks = 2;             // pickups scattered around the corpse

    [Header("Speeds")]
    public float walkSpeed = 1.8f;
    public float runSpeed = 8f;

    [Header("Senses")]
    public float sightRange = 30f;
    public float sightAngle = 140f;       // total field of view in front of the deer
    public float hearingBase = 6f;        // how far it hears a standing player
    public float hearingPerSpeed = 1.5f;  // extra hearing distance per m/s the player moves (running is louder)
    public float panicRange = 8f;         // closer than this and it bolts immediately
    public float eyeHeight = 1.2f;

    [Header("Behaviour")]
    public float wanderRadius = 25f;
    public Vector2 grazeTime = new Vector2(4f, 10f);
    public float alertTime = 2.5f;        // how long it watches you before running
    public float safeDistance = 40f;      // stops fleeing once it's this far away
    public float minFleeTime = 4f;

    [Header("Optional animation")]
    public Animator animator;             // if set: float "Speed" is updated, trigger "Die" is fired on death (if they exist)
    public string speedParameter = "Speed";
    public string dieTrigger = "Die";

    NavMeshAgent agent;
    Health health;
    Transform player;
    CharacterController playerController;

    State state;
    float stateTimer;
    float repathTimer;
    float loseTimer;
    bool warned;
    bool dead;
    bool hasSpeedParam;

    Vector3 scarePoint;
    bool usingScarePoint;

    void Awake()
    {
        agent = GetComponent<NavMeshAgent>();
        if (animator == null) animator = GetComponentInChildren<Animator>();

        health = GetComponent<Health>();
        if (health == null)
        {
            health = gameObject.AddComponent<Health>();
            health.SetMax(maxHealth);
        }
        health.Damaged += OnDamaged;
        health.Died += OnDied;

        if (autoCollider && GetComponentInChildren<Collider>() == null)
        {
            BoxCollider box = gameObject.AddComponent<BoxCollider>();
            box.center = new Vector3(0f, 0.95f, 0.1f);
            box.size = new Vector3(0.6f, 1.5f, 1.7f);
        }
    }

    void Start()
    {
        GameObject p = GameObject.FindGameObjectWithTag("Player");
        if (p != null)
        {
            player = p.transform;
            playerController = p.GetComponent<CharacterController>();
        }
        else
        {
            Debug.LogWarning("DeerAI: no object tagged 'Player' found, the deer will ignore you.");
        }

        hasSpeedParam = AnimatorHasParameter(speedParameter, AnimatorControllerParameterType.Float);

        EnterState(State.Graze);
    }

    void Update()
    {
        if (dead) return;

        if (!agent.isOnNavMesh)
        {
            if (!warned)
            {
                Debug.LogWarning("DeerAI: " + name + " is not on a NavMesh. Bake one on your terrain (see setup steps).");
                warned = true;
            }
            return;
        }

        stateTimer -= Time.deltaTime;
        bool noticed = PlayerNoticed(out float distance);

        switch (state)
        {
            case State.Graze:
                if (noticed) { EnterState(State.Alert); break; }
                if (stateTimer <= 0f) EnterState(State.Wander);
                break;

            case State.Wander:
                if (noticed) { EnterState(State.Alert); break; }
                if (!agent.pathPending && agent.remainingDistance <= agent.stoppingDistance + 0.5f)
                    EnterState(State.Graze);
                break;

            case State.Alert:
                FacePlayer();
                if (distance < panicRange) { EnterState(State.Flee); break; }
                if (noticed)
                {
                    loseTimer = 0f;
                    if (stateTimer <= 0f) EnterState(State.Flee);
                }
                else
                {
                    loseTimer += Time.deltaTime;
                    if (loseTimer > 3f) EnterState(State.Graze); // false alarm
                }
                break;

            case State.Flee:
                repathTimer -= Time.deltaTime;
                bool arrived = !agent.pathPending && agent.remainingDistance <= agent.stoppingDistance + 0.5f;
                if (repathTimer <= 0f || arrived)
                {
                    PickFleePoint();
                    repathTimer = 0.6f;
                }
                if (stateTimer <= 0f && distance > safeDistance) EnterState(State.Wander);
                break;
        }

        if (hasSpeedParam) animator.SetFloat(speedParameter, agent.velocity.magnitude);
    }

    // ---------------------------------------------------------------- states

    void EnterState(State next)
    {
        state = next;
        loseTimer = 0f;
        if (next != State.Flee) usingScarePoint = false;

        switch (next)
        {
            case State.Graze:
                agent.isStopped = true;
                stateTimer = Random.Range(grazeTime.x, grazeTime.y);
                break;

            case State.Wander:
                agent.isStopped = false;
                agent.speed = walkSpeed;
                if (RandomNavPoint(transform.position, wanderRadius, out Vector3 dest))
                    agent.SetDestination(dest);
                else
                    EnterState(State.Graze);
                break;

            case State.Alert:
                agent.isStopped = true;
                stateTimer = alertTime;
                break;

            case State.Flee:
                agent.isStopped = false;
                agent.speed = runSpeed;
                stateTimer = minFleeTime;
                repathTimer = 0f;
                break;
        }
    }

    /// Make the deer run away from a point (gunshots, damage, etc.).
    public void Scare(Vector3 fromPosition)
    {
        if (dead || !agent.isOnNavMesh) return;
        EnterState(State.Flee);
        scarePoint = fromPosition;
        usingScarePoint = true;
    }

    void PickFleePoint()
    {
        Vector3 threat = (usingScarePoint || player == null) ? scarePoint : player.position;
        Vector3 away = transform.position - threat;
        away.y = 0f;
        if (away.sqrMagnitude < 0.01f) away = transform.forward;
        away.Normalize();

        // try a few angles so it can run around obstacles
        for (int i = 0; i < 6; i++)
        {
            float angle = Random.Range(-45f, 45f) * (1f + i * 0.4f);
            Vector3 dir = Quaternion.Euler(0f, angle, 0f) * away;
            if (RandomNavPoint(transform.position + dir * 25f, 8f, out Vector3 dest))
            {
                agent.SetDestination(dest);
                return;
            }
        }
    }

    bool RandomNavPoint(Vector3 around, float radius, out Vector3 result)
    {
        for (int i = 0; i < 10; i++)
        {
            Vector2 c = Random.insideUnitCircle;
            Vector3 candidate = around + new Vector3(c.x, 0f, c.y) * radius;
            if (NavMesh.SamplePosition(candidate, out NavMeshHit hit, 6f, NavMesh.AllAreas))
            {
                result = hit.position;
                return true;
            }
        }
        result = transform.position;
        return false;
    }

    // ---------------------------------------------------------------- damage and death

    void OnDamaged(float amount, Vector3 fromPosition)
    {
        if (dead) return;
        Scare(fromPosition); // getting hit sends it running away from the attacker
    }

    void OnDied()
    {
        if (dead) return;
        dead = true;

        if (agent.isOnNavMesh) agent.isStopped = true;
        agent.enabled = false;

        DropMeat();
        StartCoroutine(DeathRoutine());
        Destroy(gameObject, corpseLifetime);
    }

        /// Drops chunks of meat on the ground around the corpse (walk over them to pick them up).
    void DropMeat()
    {
        if (meatChunks <= 0 || meatPerChunk <= 0 || string.IsNullOrEmpty(meatItemId)) return;

        for (int i = 0; i < meatChunks; i++)
        {
            Vector2 c = Random.insideUnitCircle * 1.4f;
            Vector3 pos = transform.position + new Vector3(c.x, 0.15f, c.y);
            MeatPickup.Spawn(pos, meatItemId, meatPerChunk);
        }
    }

IEnumerator DeathRoutine()
    {
        // If you have a real model with a "Die" trigger in its Animator, use that.
        if (AnimatorHasParameter(dieTrigger, AnimatorControllerParameterType.Trigger))
        {
            animator.SetTrigger(dieTrigger);
            yield break;
        }

        // Otherwise: collapse onto its side (works for the placeholder body).
        Quaternion startRot = transform.rotation;
        Quaternion endRot = startRot * Quaternion.Euler(0f, 0f, Random.value < 0.5f ? 90f : -90f);
        Vector3 startPos = transform.position;
        Vector3 endPos = startPos + Vector3.up * 0.28f; // its body is thick, so lift it so it doesn't sink into the ground

        const float duration = 0.9f;
        float t = 0f;
        while (t < 1f)
        {
            t += Time.deltaTime / duration;
            float e = Mathf.Clamp01(t);
            e = e * e; // starts slow, then drops
            transform.rotation = Quaternion.Slerp(startRot, endRot, e);
            transform.position = Vector3.Lerp(startPos, endPos, e);
            yield return null;
        }
    }

    bool AnimatorHasParameter(string parameterName, AnimatorControllerParameterType type)
    {
        if (animator == null || animator.runtimeAnimatorController == null || string.IsNullOrEmpty(parameterName)) return false;
        foreach (AnimatorControllerParameter p in animator.parameters)
            if (p.name == parameterName && p.type == type) return true;
        return false;
    }

    // ---------------------------------------------------------------- senses

    bool PlayerNoticed(out float distance)
    {
        distance = float.MaxValue;
        if (player == null) return false;

        Vector3 toPlayer = player.position - transform.position;
        distance = new Vector2(toPlayer.x, toPlayer.z).magnitude;

        // hearing: a moving player is louder than a standing one (crouching is quieter than sprinting)
        float speed = 0f;
        if (playerController != null)
        {
            Vector3 v = playerController.velocity;
            speed = new Vector2(v.x, v.z).magnitude;
        }
        if (distance <= hearingBase + speed * hearingPerSpeed) return true;

        // sight: in front of the deer, within range, nothing in the way
        if (distance > sightRange) return false;
        if (Vector3.Angle(transform.forward, toPlayer) > sightAngle * 0.5f) return false;

        Vector3 eye = transform.position + Vector3.up * eyeHeight;
        if (Physics.Linecast(eye, player.position, out RaycastHit hit, ~0, QueryTriggerInteraction.Ignore))
        {
            if (hit.transform.root != player.root && hit.transform.root != transform.root) return false;
        }
        return true;
    }

    void FacePlayer()
    {
        if (player == null) return;
        Vector3 d = player.position - transform.position;
        d.y = 0f;
        if (d.sqrMagnitude < 0.01f) return;
        transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.LookRotation(d), 6f * Time.deltaTime);
    }

    // ---------------------------------------------------------------- placeholder body

    /// Right-click the component header > "Build Placeholder Body" to get a quick box deer until you have a real model.
    [ContextMenu("Build Placeholder Body")]
    void BuildPlaceholderBody()
    {
        Color brown = new Color(0.5f, 0.35f, 0.2f);
        AddPart("Body", new Vector3(0f, 1.0f, 0f), new Vector3(0.55f, 0.6f, 1.3f), brown);
        AddPart("Neck", new Vector3(0f, 1.45f, 0.65f), new Vector3(0.22f, 0.6f, 0.22f), brown);
        AddPart("Head", new Vector3(0f, 1.75f, 0.85f), new Vector3(0.22f, 0.22f, 0.4f), brown);
        float[] xs = { -0.2f, 0.2f };
        float[] zs = { -0.5f, 0.5f };
        foreach (float x in xs)
            foreach (float z in zs)
                AddPart("Leg", new Vector3(x, 0.35f, z), new Vector3(0.12f, 0.7f, 0.12f), brown * 0.8f);
    }

    void AddPart(string partName, Vector3 localPos, Vector3 scale, Color color)
    {
        GameObject part = GameObject.CreatePrimitive(PrimitiveType.Cube);
        part.name = partName;
        DestroyImmediate(part.GetComponent<Collider>());
        part.transform.SetParent(transform, false);
        part.transform.localPosition = localPos;
        part.transform.localScale = scale;
        part.GetComponent<Renderer>().sharedMaterial.color = color;
    }

    void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, hearingBase);
        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(transform.position, panicRange);
        Gizmos.color = Color.cyan;
        Gizmos.DrawWireSphere(transform.position, sightRange);
    }
}

