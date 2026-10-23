using UnityEngine;

/// A flying arrow. Moves with its own gravity and raycasts every frame, so fast arrows never pass through things.
/// On impact it damages anything with a Health component and sticks into whatever it hit.
public class Arrow : MonoBehaviour
{
    public float gravity = 9.81f;
    public float maxFlightTime = 12f;
    public float stuckLifetime = 25f;

    Vector3 velocity;
    float damage;
    Transform shooterRoot;
    Vector3 shooterPosition;
    bool stuck;
    float flightTime;

    static Material woodMat;
    static Material tipMat;

    public static Arrow Spawn(Vector3 position, Vector3 velocity, float damage, Transform shooter)
    {
        GameObject go = new GameObject("Arrow");
        go.transform.position = position;
        go.transform.rotation = Quaternion.LookRotation(velocity);

        // pivot is the arrow tip, the shaft trails behind it
        AddPart(go.transform, new Vector3(0f, 0f, -0.35f), new Vector3(0.015f, 0.015f, 0.7f), GetMaterial(ref woodMat, new Color(0.55f, 0.4f, 0.25f)));
        AddPart(go.transform, new Vector3(0f, 0f, -0.02f), new Vector3(0.035f, 0.035f, 0.08f), GetMaterial(ref tipMat, new Color(0.3f, 0.3f, 0.32f)));

        Arrow a = go.AddComponent<Arrow>();
        a.velocity = velocity;
        a.damage = damage;
        a.shooterRoot = shooter != null ? shooter.root : null;
        a.shooterPosition = shooter != null ? shooter.position : position;
        return a;
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
        transform.position = hit.point + velocity.normalized * 0.05f; // sink the tip in a little

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
