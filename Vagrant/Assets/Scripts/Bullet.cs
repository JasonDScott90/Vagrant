using UnityEngine;

/// A bullet fired by the Gun. Fast projectile: raycasts every frame so it never passes through
/// things. Drops slightly under gravity, damages anything with a Health component and briefly
/// sticks into whatever it hit.
public class Bullet : MonoBehaviour
{
    public float gravity = 5f;
    public float maxFlightTime = 6f;
    public float stuckLifetime = 4f;

    Vector3 velocity;
    float damage;
    Transform shooterRoot;
    Vector3 shooterPosition;
    bool stuck;
    float flightTime;

    static Material tracerMat;

    public static Bullet Spawn(Vector3 position, Vector3 velocity, float damage, Transform shooter)
    {
        GameObject go = new GameObject("Bullet");
        go.transform.position = position;
        go.transform.rotation = Quaternion.LookRotation(velocity);

        // thin tracer streak, pivot at the tip
        AddPart(go.transform, new Vector3(0f, 0f, -0.15f), new Vector3(0.02f, 0.02f, 0.32f), GetMaterial(ref tracerMat, new Color(0.9f, 0.75f, 0.35f)));

        Bullet b = go.AddComponent<Bullet>();
        b.velocity = velocity;
        b.damage = damage;
        b.shooterRoot = shooter != null ? shooter.root : null;
        b.shooterPosition = shooter != null ? shooter.position : position;
        return b;
    }

    void Update()
    {
        if (stuck) return;

        float dt = Time.deltaTime;
        flightTime += dt;
        if (flightTime > maxFlightTime) { Destroy(gameObject); return; }

        Vector3 start = transform.position;
        velocity += Vector3.down * gravity * dt;
        Vector3 step = velocity * dt;
        float dist = step.magnitude;

        if (dist > 0.0001f)
        {
            RaycastHit[] hits = Physics.RaycastAll(start, step / dist, dist, ~(1 << 2), QueryTriggerInteraction.Ignore);
            float best = float.MaxValue;
            RaycastHit bestHit = default;
            bool found = false;
            foreach (RaycastHit h in hits)
            {
                if (shooterRoot != null && h.collider.transform.root == shooterRoot) continue; // don't shoot yourself
                if (h.distance < best) { best = h.distance; bestHit = h; found = true; }
            }

            if (found)
            {
                OnHit(bestHit);
                return;
            }
        }

        transform.position = start + step;
        if (velocity.sqrMagnitude > 0.01f) transform.rotation = Quaternion.LookRotation(velocity);
    }

    void OnHit(RaycastHit hit)
    {
        stuck = true;
        transform.position = hit.point + velocity.normalized * 0.03f; // sink the tip in a little

        Health health = hit.collider.GetComponentInParent<Health>();
        if (health != null && !health.IsDead)
            health.TakeDamage(damage, shooterPosition);

        transform.SetParent(hit.collider.transform, true);
        Destroy(gameObject, stuckLifetime);
    }

    static void AddPart(Transform parent, Vector3 localPos, Vector3 scale, Material mat)
    {
        GameObject p = GameObject.CreatePrimitive(PrimitiveType.Cube);
        Destroy(p.GetComponent<Collider>());
        p.transform.SetParent(parent, false);
        p.transform.localPosition = localPos;
        p.transform.localScale = scale;
        p.GetComponent<Renderer>().sharedMaterial = mat;
    }

    static Material GetMaterial(ref Material cache, Color color)
    {
        if (cache != null) return cache;
        GameObject tmp = GameObject.CreatePrimitive(PrimitiveType.Cube);
        cache = new Material(tmp.GetComponent<Renderer>().sharedMaterial);
        cache.color = color;
        Destroy(tmp);
        return cache;
    }
}