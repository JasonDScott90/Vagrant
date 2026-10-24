using UnityEngine;

/// A world ammo pickup: walk over it and the bullets go straight into the player's inventory.
/// Builds its own placeholder model (a small crate with brass rounds) at runtime.
public class AmmoPickup : MonoBehaviour
{
    [Header("Contents")]
    public string itemId = "bullet";
    public int amount = 30;

    [Header("Visual")]
    public float spinSpeed = 45f;
    public float bobHeight = 0.08f;

    Vector3 basePos;
    float phase;
    Light beacon;

    /// Places a ready-to-collect pickup in the world.
    public static AmmoPickup Spawn(Vector3 position, int amount)
    {
        GameObject go = new GameObject("AmmoPickup");
        go.transform.position = position;
        AmmoPickup pickup = go.AddComponent<AmmoPickup>();
        pickup.amount = amount;
        return pickup;
    }

    void Awake()
    {
        SphereCollider trigger = gameObject.AddComponent<SphereCollider>();
        trigger.isTrigger = true;
        trigger.radius = 1.3f;
        trigger.center = new Vector3(0f, 0.4f, 0f);

        BuildVisual();
    }

    void Start()
    {
        basePos = transform.position;
        phase = Random.value * Mathf.PI * 2f;
    }

    void Update()
    {
        transform.Rotate(0f, spinSpeed * Time.deltaTime, 0f, Space.World);
        transform.position = basePos + Vector3.up * (Mathf.Sin(Time.time * 2f + phase) * bobHeight);

        // pulse the beacon so the pickup catches the eye
        if (beacon != null)
            beacon.intensity = 1.3f + Mathf.Sin(Time.time * 3f + phase) * 0.5f;
    }

    void OnTriggerEnter(Collider other)
    {
        if (amount <= 0) return;
        PlayerInventory inventory = other.GetComponentInParent<PlayerInventory>();
        if (inventory == null) return;

        int added = inventory.Container.Add(itemId, amount);
        if (added <= 0) return; // inventory full: leave it lying here

        amount -= added;
        if (amount <= 0) Destroy(gameObject);
    }

    // ---------------------------------------------------------------- placeholder model

    void BuildVisual()
    {
        Material olive = MakeMaterial(new Color(0.24f, 0.28f, 0.15f));
        Material trim = MakeMaterial(new Color(0.12f, 0.14f, 0.08f));
        Material brass = MakeMaterial(new Color(0.88f, 0.70f, 0.28f));

        // a chunky olive ammo crate with darker bands and lid
        AddPart(transform, "Crate", new Vector3(0f, 0.22f, 0f), new Vector3(0.62f, 0.36f, 0.42f), olive);
        AddPart(transform, "Lid", new Vector3(0f, 0.43f, 0f), new Vector3(0.66f, 0.08f, 0.46f), trim);
        AddPart(transform, "BandL", new Vector3(-0.22f, 0.22f, 0f), new Vector3(0.06f, 0.38f, 0.44f), trim);
        AddPart(transform, "BandR", new Vector3(0.22f, 0.22f, 0f), new Vector3(0.06f, 0.38f, 0.44f), trim);

        // brass rounds sticking out of the top
        for (int i = 0; i < 4; i++)
        {
            float x = -0.21f + i * 0.14f;
            AddPart(transform, "Round" + i, new Vector3(x, 0.56f, 0f), new Vector3(0.09f, 0.22f, 0.09f), brass);
        }

        // warm beacon light so the pickup reads clearly against the terrain
        beacon = gameObject.AddComponent<Light>();
        beacon.type = LightType.Point;
        beacon.color = new Color(1f, 0.82f, 0.45f);
        beacon.intensity = 1.4f;
        beacon.range = 3.2f;
        beacon.shadows = LightShadows.None;
    }

    static Transform AddPart(Transform parent, string name, Vector3 pos, Vector3 scale, Material mat)
    {
        GameObject p = GameObject.CreatePrimitive(PrimitiveType.Cube);
        p.name = name;
        Destroy(p.GetComponent<Collider>());
        p.transform.SetParent(parent, false);
        p.transform.localPosition = pos;
        p.transform.localScale = scale;
        p.GetComponent<Renderer>().sharedMaterial = mat;
        p.layer = 2;
        return p.transform;
    }

    static Material MakeMaterial(Color color)
    {
        GameObject tmp = GameObject.CreatePrimitive(PrimitiveType.Cube);
        Material m = new Material(tmp.GetComponent<Renderer>().sharedMaterial);
        m.color = color;
        Destroy(tmp);
        return m;
    }
}