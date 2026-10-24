using System.Collections;
using UnityEngine;
using UnityEngine.AI;
#if UNITY_EDITOR
using UnityEditor;
#endif

/// Demon-hunter boss: prowls around its spawn point, chases the player on sight (or when
/// shot at) and cuts them down in melee. Needs a baked NavMesh, like the deer.
/// The body is built from primitives - right-click the component > "Build Illidan Model".
[RequireComponent(typeof(NavMeshAgent))]
public class IllidanAI : MonoBehaviour
{
    enum State { Prowl, Chase, Attack, Return }

    [Header("Health")]
    public float maxHealth = 400f;
    public float corpseLifetime = 30f;

    [Header("Speeds")]
    public float prowlSpeed = 2.5f;
    public float chaseSpeed = 7.5f;

    [Header("Senses")]
    public float sightRange = 30f;
    public float sightAngle = 160f;
    public float loseRange = 45f;        // gives up the chase once the player is this far

    [Header("Combat")]
    public float attackRange = 2.4f;
    public float attackDamage = 15f;
    public float attackCooldown = 1.4f;
    public float windup = 0.35f;         // seconds between raising the glaive and the hit

    [Header("Prowl")]
    public float prowlRadius = 25f;
    public float leashRange = 70f;       // never wanders this far from its spawn

    [Header("Model (set by Build Illidan Model)")]
    public Transform model;
    public Transform rightArm;           // swings its warglaive on attack

    NavMeshAgent agent;
    Health health;
    Transform player;
    Health playerHealth;

    State state;
    float repathTimer;
    float attackTimer;
    bool swinging;
    bool warned;
    bool dead;

    Vector3 spawnPoint;
    Quaternion armRest;

    void Awake()
    {
        agent = GetComponent<NavMeshAgent>();
        spawnPoint = transform.position;

        health = GetComponent<Health>();
        if (health == null) health = gameObject.AddComponent<Health>();
        health.SetMax(maxHealth);
        health.Died += OnDied;
        health.Damaged += OnDamaged;

        if (GetComponentInChildren<Collider>() == null)
        {
            BoxCollider box = gameObject.AddComponent<BoxCollider>();
            box.center = new Vector3(0f, 1.1f, 0f);
            box.size = new Vector3(1.1f, 2.2f, 0.8f);
        }
    }

    void Start()
    {
        GameObject p = GameObject.FindGameObjectWithTag("Player");
        if (p != null)
        {
            player = p.transform;
            playerHealth = p.GetComponent<Health>();
        }

        // snap onto the NavMesh in case he was placed slightly off it
        if (!agent.isOnNavMesh && NavMesh.SamplePosition(transform.position, out NavMeshHit snap, 10f, NavMesh.AllAreas))
            agent.Warp(snap.position);

        if (rightArm != null) armRest = rightArm.localRotation;
        EnterState(State.Prowl);
    }

    void Update()
    {
        if (dead) return;

        if (!agent.isOnNavMesh)
        {
            if (!warned)
            {
                Debug.LogWarning("IllidanAI: " + name + " is not on a NavMesh. Bake one on your terrain.");
                warned = true;
            }
            return;
        }

        attackTimer -= Time.deltaTime;
        float distance = player != null ? Vector3.Distance(transform.position, player.position) : float.MaxValue;

        switch (state)
        {
            case State.Prowl:
                repathTimer -= Time.deltaTime;
                if (repathTimer <= 0f) { PickProwlPoint(); repathTimer = Random.Range(3f, 6f); }
                if (CanSeePlayer(distance)) EnterState(State.Chase);
                break;

            case State.Chase:
                agent.SetDestination(player.position);
                if (distance <= attackRange) EnterState(State.Attack);
                else if (distance > loseRange ||
                         Vector3.Distance(transform.position, spawnPoint) > leashRange)
                    EnterState(State.Return);
                break;

            case State.Attack:
                FacePlayer();
                if (distance > attackRange * 1.25f) { EnterState(State.Chase); break; }
                if (attackTimer <= 0f && !swinging)
                {
                    attackTimer = attackCooldown;
                    StartCoroutine(AttackRoutine());
                }
                break;

            case State.Return:
                agent.SetDestination(spawnPoint);
                if (!agent.pathPending && agent.remainingDistance <= agent.stoppingDistance + 0.5f)
                {
                    if (CanSeePlayer(distance)) EnterState(State.Chase);
                    else EnterState(State.Prowl);
                }
                break;
        }
    }

    // ---------------------------------------------------------------- states

    void EnterState(State next)
    {
        state = next;
        switch (next)
        {
            case State.Prowl:
            case State.Return:
                agent.isStopped = false;
                agent.speed = prowlSpeed;
                break;
            case State.Chase:
                agent.isStopped = false;
                agent.speed = chaseSpeed;
                break;
            case State.Attack:
                agent.isStopped = true;
                break;
        }
    }

    void PickProwlPoint()
    {
        for (int i = 0; i < 10; i++)
        {
            Vector2 c = Random.insideUnitCircle;
            Vector3 candidate = spawnPoint + new Vector3(c.x, 0f, c.y) * prowlRadius;
            if (NavMesh.SamplePosition(candidate, out NavMeshHit hit, 6f, NavMesh.AllAreas))
            {
                agent.isStopped = false;
                agent.SetDestination(hit.position);
                return;
            }
        }
    }

    // ---------------------------------------------------------------- combat

    IEnumerator AttackRoutine()
    {
        swinging = true;

        // windup: raise the glaive
        if (rightArm != null)
        {
            Quaternion raised = armRest * Quaternion.Euler(-70f, 0f, 0f);
            for (float t = 0f; t < windup; t += Time.deltaTime)
            {
                rightArm.localRotation = Quaternion.Slerp(armRest, raised, t / windup);
                yield return null;
            }
        }
        else
        {
            yield return new WaitForSeconds(windup);
        }

        // swing down
        if (rightArm != null)
        {
            Quaternion strike = armRest * Quaternion.Euler(55f, 0f, 0f);
            for (float t = 0f; t < 0.12f; t += Time.deltaTime)
            {
                rightArm.localRotation = Quaternion.Slerp(rightArm.localRotation, strike, t / 0.12f);
                yield return null;
            }
        }

        // connect if the player is still in reach
        if (!dead && player != null && playerHealth != null && !playerHealth.IsDead)
        {
            float d = Vector3.Distance(transform.position, player.position);
            if (d <= attackRange * 1.4f)
                playerHealth.TakeDamage(attackDamage, transform.position);
        }

        // return the arm to rest
        if (rightArm != null)
        {
            Quaternion start = rightArm.localRotation;
            for (float t = 0f; t < 0.35f; t += Time.deltaTime)
            {
                rightArm.localRotation = Quaternion.Slerp(start, armRest, t / 0.35f);
                yield return null;
            }
            rightArm.localRotation = armRest;
        }

        swinging = false;
    }

    void OnDamaged(float amount, Vector3 fromPosition)
    {
        if (dead) return;
        // getting shot always draws his attention
        if (player != null && state != State.Attack) EnterState(State.Chase);
    }

    void OnDied()
    {
        if (dead) return;
        dead = true;
        StopAllCoroutines();
        if (agent.isOnNavMesh) agent.isStopped = true;
        agent.enabled = false;

        StartCoroutine(DeathRoutine());
        Destroy(gameObject, corpseLifetime);
    }

    IEnumerator DeathRoutine()
    {
        Quaternion startRot = transform.rotation;
        Quaternion endRot = startRot * Quaternion.Euler(0f, 0f, 90f);
        for (float t = 0f; t < 1f; t += Time.deltaTime)
        {
            float e = t * t;
            transform.rotation = Quaternion.Slerp(startRot, endRot, e);
            yield return null;
        }
    }

    // ---------------------------------------------------------------- senses

    bool CanSeePlayer(float distance)
    {
        if (player == null) return false;
        if (distance > sightRange) return false;

        Vector3 to = player.position - transform.position;
        if (Vector3.Angle(transform.forward, to) > sightAngle * 0.5f) return false;

        Vector3 eye = transform.position + Vector3.up * 1.8f;
        Vector3 target = player.position + Vector3.up * 1.2f;
        if (Physics.Linecast(eye, target, out RaycastHit hit, ~0, QueryTriggerInteraction.Ignore))
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
        transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.LookRotation(d), 8f * Time.deltaTime);
    }

    // ---------------------------------------------------------------- model

    /// Right-click the component header > "Build Illidan Model" to (re)build his body.
    [ContextMenu("Build Illidan Model")]
    public void BuildIllidanModel()
    {
        Transform old = transform.Find("Model");
        if (old != null) DestroyGO(old.gameObject);

        Material skin  = LoadMat("Assets/Materials/Illidan/IllidanSkin.mat", new Color(0.26f, 0.21f, 0.46f));
        Material hair  = LoadMat("Assets/Materials/Illidan/IllidanHair.mat", new Color(0.85f, 0.86f, 0.92f));
        Material horn  = LoadMat("Assets/Materials/Illidan/IllidanHorn.mat", new Color(0.14f, 0.11f, 0.20f));
        Material cloth = LoadMat("Assets/Materials/Illidan/IllidanCloth.mat", new Color(0.10f, 0.09f, 0.11f));
        Material metal = LoadMat("Assets/Materials/Illidan/IllidanGlaive.mat", new Color(0.16f, 0.18f, 0.21f));
        Material glow  = LoadMat("Assets/Materials/Illidan/IllidanGlow.mat", new Color(0.15f, 1f, 0.45f));

        GameObject rootGO = new GameObject("Model");
        Transform root = rootGO.transform;
        root.SetParent(transform, false);
        model = root;

        // torso and hips
        Part(root, "Hips", new Vector3(0f, 1.02f, 0f), new Vector3(0.42f, 0.28f, 0.28f), Vector3.zero, skin);
        Part(root, "Torso", new Vector3(0f, 1.42f, 0f), new Vector3(0.52f, 0.62f, 0.30f), Vector3.zero, skin);
        Part(root, "Loincloth", new Vector3(0f, 0.90f, 0.13f), new Vector3(0.30f, 0.38f, 0.10f), Vector3.zero, cloth);
        Part(root, "Belt", new Vector3(0f, 1.12f, 0f), new Vector3(0.46f, 0.08f, 0.32f), Vector3.zero, cloth);

        // glowing demon-hunter tattoos
        Part(root, "Tattoo1", new Vector3(-0.13f, 1.48f, 0.155f), new Vector3(0.05f, 0.42f, 0.01f), new Vector3(0f, 0f, 12f), glow);
        Part(root, "Tattoo2", new Vector3(0.13f, 1.48f, 0.155f), new Vector3(0.05f, 0.42f, 0.01f), new Vector3(0f, 0f, -12f), glow);
        Part(root, "Tattoo3", new Vector3(0f, 1.30f, 0.158f), new Vector3(0.30f, 0.04f, 0.01f), Vector3.zero, glow);

        // head, blindfold, hair, horns, ears
        Part(root, "Head", new Vector3(0f, 1.95f, 0f), new Vector3(0.32f, 0.34f, 0.32f), Vector3.zero, skin);
        Part(root, "Blindfold", new Vector3(0f, 2.02f, 0.15f), new Vector3(0.34f, 0.09f, 0.06f), Vector3.zero, glow);
        Part(root, "HairTop", new Vector3(0f, 2.12f, -0.06f), new Vector3(0.34f, 0.16f, 0.30f), Vector3.zero, hair);
        Part(root, "HairBack", new Vector3(0f, 1.92f, -0.19f), new Vector3(0.32f, 0.50f, 0.14f), Vector3.zero, hair);
        Part(root, "HairTail", new Vector3(0f, 1.55f, -0.22f), new Vector3(0.26f, 0.42f, 0.12f), new Vector3(-8f, 0f, 0f), hair);
        Part(root, "EarL", new Vector3(-0.21f, 1.96f, -0.02f), new Vector3(0.05f, 0.18f, 0.04f), new Vector3(0f, 0f, 32f), skin);
        Part(root, "EarR", new Vector3(0.21f, 1.96f, -0.02f), new Vector3(0.05f, 0.18f, 0.04f), new Vector3(0f, 0f, -32f), skin);
        Part(root, "HornL1", new Vector3(-0.17f, 2.22f, -0.03f), new Vector3(0.09f, 0.28f, 0.09f), new Vector3(-10f, 0f, 25f), horn);
        Part(root, "HornR1", new Vector3(0.17f, 2.22f, -0.03f), new Vector3(0.09f, 0.28f, 0.09f), new Vector3(-10f, 0f, -25f), horn);
        Part(root, "HornL2", new Vector3(-0.30f, 2.38f, -0.06f), new Vector3(0.07f, 0.24f, 0.07f), new Vector3(-20f, 0f, 55f), horn);
        Part(root, "HornR2", new Vector3(0.30f, 2.38f, -0.06f), new Vector3(0.07f, 0.24f, 0.07f), new Vector3(-20f, 0f, -55f), horn);

        // shoulders
        Part(root, "ShoulderL", new Vector3(-0.34f, 1.68f, 0f), new Vector3(0.20f, 0.16f, 0.22f), Vector3.zero, skin);
        Part(root, "ShoulderR", new Vector3(0.34f, 1.68f, 0f), new Vector3(0.20f, 0.16f, 0.22f), Vector3.zero, skin);

        // arms (right arm gets a pivot so the AI can swing it)
        Transform armL = BuildArm(root, "ArmL", new Vector3(-0.34f, 1.66f, 0f), skin, glow, -1f);
        Transform armR = BuildArm(root, "ArmR", new Vector3(0.34f, 1.66f, 0f), skin, glow, 1f);
        rightArm = armR;
        armRest = armR.localRotation;

        // warglaives in both hands
        BuildWarglaive(armL, new Vector3(0f, -0.78f, 0.10f), metal, glow, -1f);
        BuildWarglaive(armR, new Vector3(0f, -0.78f, 0.10f), metal, glow, 1f);

        // legs and hooves
        for (int s = -1; s <= 1; s += 2)
        {
            Part(root, "Thigh" + s, new Vector3(0.15f * s, 0.78f, 0f), new Vector3(0.17f, 0.50f, 0.19f), Vector3.zero, skin);
            Part(root, "Shin" + s, new Vector3(0.15f * s, 0.36f, 0f), new Vector3(0.14f, 0.42f, 0.16f), Vector3.zero, skin);
            Part(root, "Hoof" + s, new Vector3(0.15f * s, 0.09f, 0.04f), new Vector3(0.15f, 0.17f, 0.26f), Vector3.zero, cloth);
            Part(root, "LegTattoo" + s, new Vector3(0.15f * s, 0.52f, 0.10f), new Vector3(0.05f, 0.30f, 0.01f), new Vector3(0f, 0f, 6f * s), glow);
        }

#if UNITY_EDITOR
        EditorUtility.SetDirty(this);
#endif
    }

    Transform BuildArm(Transform parent, string name, Vector3 shoulder, Material skin, Material glow, float side)
    {
        GameObject pivotGO = new GameObject(name);
        Transform pivot = pivotGO.transform;
        pivot.SetParent(parent, false);
        pivot.localPosition = shoulder;

        Part(pivot, "Upper", new Vector3(0f, -0.20f, 0f), new Vector3(0.13f, 0.42f, 0.13f), Vector3.zero, skin);
        Part(pivot, "Fore", new Vector3(0f, -0.52f, 0.02f), new Vector3(0.11f, 0.36f, 0.11f), Vector3.zero, skin);
        Part(pivot, "Hand", new Vector3(0f, -0.74f, 0.02f), new Vector3(0.12f, 0.12f, 0.12f), Vector3.zero, skin);
        Part(pivot, "Bracer", new Vector3(0f, -0.52f, 0.02f), new Vector3(0.13f, 0.10f, 0.13f), Vector3.zero, glow);
        Part(pivot, "ArmTattoo", new Vector3(0.09f * side, -0.34f, 0.02f), new Vector3(0.01f, 0.26f, 0.06f), Vector3.zero, glow);
        return pivot;
    }

    void BuildWarglaive(Transform hand, Vector3 pos, Material metal, Material glow, float side)
    {
        GameObject g = new GameObject("Warglaive" + (side > 0 ? "R" : "L"));
        Transform t = g.transform;
        t.SetParent(hand, false);
        t.localPosition = pos;

        Part(t, "Handle", Vector3.zero, new Vector3(0.05f, 0.30f, 0.05f), Vector3.zero, metal);
        Part(t, "BladeLow", new Vector3(0f, -0.22f, 0.28f), new Vector3(0.05f, 0.16f, 0.52f), new Vector3(28f, 0f, 0f), metal);
        Part(t, "BladeUp", new Vector3(0f, 0.24f, 0.28f), new Vector3(0.05f, 0.16f, 0.52f), new Vector3(-28f, 0f, 0f), metal);
        Part(t, "EdgeLow", new Vector3(0f, -0.33f, 0.46f), new Vector3(0.02f, 0.05f, 0.34f), new Vector3(28f, 0f, 0f), glow);
        Part(t, "EdgeUp", new Vector3(0f, 0.35f, 0.46f), new Vector3(0.02f, 0.05f, 0.34f), new Vector3(-28f, 0f, 0f), glow);
    }

    static GameObject Part(Transform parent, string partName, Vector3 pos, Vector3 scale, Vector3 euler, Material mat)
    {
        GameObject go = GameObject.CreatePrimitive(PrimitiveType.Cube);
        go.name = partName;

        Collider col = go.GetComponent<Collider>();
        if (col != null) DestroyGO(col);

        go.transform.SetParent(parent, false);
        go.transform.localPosition = pos;
        go.transform.localEulerAngles = euler;
        go.transform.localScale = scale;

        MeshRenderer renderer = go.GetComponent<MeshRenderer>();
        if (renderer != null) renderer.sharedMaterial = mat;
        return go;
    }

    // Destroy helpers so the same code works in edit mode (model build) and play mode.
    static void DestroyGO(Object o)
    {
        if (o == null) return;
        if (Application.isPlaying) Destroy(o);
        else DestroyImmediate(o);
    }

    static Material LoadMat(string path, Color fallback)
    {
#if UNITY_EDITOR
        Material m = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (m != null) return m;
#endif
        Material n = new Material(Shader.Find("Universal Render Pipeline/Lit"));
        n.color = fallback;
        return n;
    }

    void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(transform.position, attackRange);
        Gizmos.color = Color.cyan;
        Gizmos.DrawWireSphere(transform.position, sightRange);
    }
}
