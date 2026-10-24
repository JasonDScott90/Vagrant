using UnityEngine;

/// A piece of dropped meat in the world: walk over it and it goes straight into the player's
/// inventory. DeerAI spawns these when a deer dies. Builds its own placeholder model
/// (a slab of meat with a bone) at runtime, like AmmoPickup does.
public class MeatPickup : MonoBehaviour
{
    [Header("Contents")]
    public string itemId = "raw_meat";
    public int amount = 3;

    [Header("Visual")]
    public float spinSpeed = 15f;

    Light beacon;

    /// Places a ready-to-collect piece of meat on the ground.
    public static MeatPickup Spawn(Vector3 position, string itemId, int amount)
    {
        GameObject go = new GameObject("MeatPickup");
        go.transform.position = position;
        go.transform.rotation = Quaternion.Euler(0f, Random.Range(0f, 360f), 0f);
        MeatPickup pickup = go.AddComponent<MeatPickup>();
        pickup.itemId = itemId;
        pickup.amount = amount;
        return pickup;
    }

    void Awake()
    {
        SphereCollider trigger = gameObject.AddComponent<SphereCollider>();
        trigger.isTrigger = true;
        trigger.radius = 1.1f;
        trigger.center = new Vector3(0f, 0.25f, 0f);

        BuildVisual();
    }

    void Update()
    {
        // slow spin so the meat is easy to spot on the ground
        transform.Rotate(0f, spinSpeed * Time.deltaTime, 0f, Space.World);

        if (beacon != null)
            beacon.intensity = 0.7f + Mathf.Sin(Time.time * 2.5f) * 0.2f;
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
        bool cooked = itemId == "cooked_meat";

        Material meat = MakeMaterial(cooked ? new Color(0.42f, 0.24f, 0.12f) : new Color(0.62f, 0.15f, 0.14f));
        Material meatDark = MakeMaterial(cooked ? new Color(0.30f, 0.16f, 0.08f) : new Color(0.45f, 0.10f, 0.10f));
        Material fat = MakeMaterial(cooked ? new Color(0.72f, 0.58f, 0.36f) : new Color(0.92f, 0.86f, 0.78f));
        Material bone = MakeMaterial(new Color(0.90f, 0.88f, 0.80f));

        // slab of meat resting on the ground
        AddPart(transform, "Slab", new Vector3(0f, 0.14f, 0f), new Vector3(0.55f, 0.20f, 0.38f), meat);
        AddPart(transform, "Top", new Vector3(0.02f, 0.27f, 0f), new Vector3(0.44f, 0.10f, 0.30f), meatDark);
        AddPart(transform, "Fat1", new Vector3(-0.12f, 0.30f, 0.06f), new Vector3(0.20f, 0.05f, 0.07f), fat);
        AddPart(transform, "Fat2", new Vector3(0.14f, 0.30f, -0.08f), new Vector3(0.16f, 0.05f, 0.06f), fat);

        // a bone sticking out of one end
        GameObject b = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        b.name = "Bone";
        Destroy(b.GetComponent<Collider>());
        b.transform.SetParent(transform, false);
        b.transform.localPosition = new Vector3(0.42f, 0.17f, 0f);
        b.transform.localRotation = Quaternion.Euler(0f, 0f, 70f);
        b.transform.localScale = new Vector3(0.09f, 0.22f, 0.09f);
        b.GetComponent<Renderer>().sharedMaterial = bone;
        b.layer = 2;

        AddPart(transform, "Knuckle", new Vector3(0.58f, 0.10f, 0f), new Vector3(0.13f, 0.13f, 0.13f), bone);

        // faint warm glow so it catches the eye on the ground
        beacon = gameObject.AddComponent<Light>();
        beacon.type = LightType.Point;
        beacon.color = cooked ? new Color(1f, 0.75f, 0.45f) : new Color(1f, 0.5f, 0.4f);
        beacon.intensity = 0.8f;
        beacon.range = 2.2f;
        beacon.shadows = LightShadows.None;
    }

    static Transform AddPart(Transform parent, string partName, Vector3 pos, Vector3 scale, Material mat)
    {
        GameObject p = GameObject.CreatePrimitive(PrimitiveType.Cube);
        p.name = partName;
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