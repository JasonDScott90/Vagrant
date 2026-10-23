using UnityEngine;

/// A bow. Press the Bow key to equip, hold left mouse to draw, release to shoot.
/// Add this to the Player object (next to BuildingSystem). Arrows fly from the center of the screen.
public class Bow : MonoBehaviour
{
    [Header("References (auto-found if empty)")]
    public Camera cam;

    [Header("Shooting")]
    public float damage = 50f;            // 2 hits kill a deer (100 health)
    public float minArrowSpeed = 25f;     // m/s at the shortest draw
    public float maxArrowSpeed = 65f;     // m/s at full draw
    public float drawTime = 0.8f;         // seconds to a full draw
    public float minDrawToFire = 0.25f;   // releasing before this just lowers the bow
    public float cooldown = 0.5f;         // time before you can draw again

    [Header("Zoom")]
    public float zoomAmount = 8f;         // field of view reduction at full draw

    public bool Equipped => equipped;

    bool equipped;
    float draw;                           // 0..1
    float nextFire;
    float baseFov;

    Transform bowRoot;
    Transform stringT;
    Transform nockedArrow;

    void Awake()
    {
        if (cam == null) cam = GetComponentInChildren<Camera>();
        baseFov = cam.fieldOfView;
        BuildBow();
        bowRoot.gameObject.SetActive(false);
    }

    // ---------------------------------------------------------------- called by InputManager

    public void Toggle() { SetEquipped(!equipped); }
    public void Holster() { SetEquipped(false); }

    public void SetEquipped(bool value)
    {
        if (equipped == value) return;
        equipped = value;
        draw = 0f;
        bowRoot.gameObject.SetActive(value);
        if (!value) cam.fieldOfView = baseFov;
    }

    /// Call every frame. fireHeld = is the left mouse button down.
    public void Tick(bool fireHeld)
    {
        if (!equipped) return;

        bool ready = Time.time >= nextFire;

        if (fireHeld && ready)
        {
            draw = Mathf.Min(draw + Time.deltaTime / drawTime, 1f);
        }
        else if (!fireHeld && draw > 0f)
        {
            if (draw * drawTime >= minDrawToFire) Fire(draw);
            draw = 0f;
        }

        UpdateVisuals(ready);
    }

    // ---------------------------------------------------------------- shooting

    void Fire(float drawAmount)
    {
        float speed = Mathf.Lerp(minArrowSpeed, maxArrowSpeed, drawAmount);
        Vector3 dir = cam.transform.forward;
        Vector3 origin = cam.transform.position + dir * 0.8f;
        Arrow.Spawn(origin, dir * speed, damage, transform);
        nextFire = Time.time + cooldown;
    }

    void UpdateVisuals(bool ready)
    {
        // string and nocked arrow pull back as you draw
        stringT.localPosition = new Vector3(0f, 0f, -0.12f - 0.22f * draw);
        nockedArrow.localPosition = new Vector3(0f, 0f, 0.15f - 0.22f * draw);
        nockedArrow.gameObject.SetActive(ready);

        float targetFov = baseFov - zoomAmount * draw;
        cam.fieldOfView = Mathf.Lerp(cam.fieldOfView, targetFov, 12f * Time.deltaTime);
    }

    // ---------------------------------------------------------------- placeholder model

    void BuildBow()
    {
        GameObject root = new GameObject("Bow");
        bowRoot = root.transform;
        bowRoot.SetParent(cam.transform, false);
        bowRoot.localPosition = new Vector3(0.28f, -0.2f, 0.6f);
        bowRoot.localRotation = Quaternion.identity;

        Material wood = MakeMaterial(new Color(0.45f, 0.30f, 0.18f));
        Material cord = MakeMaterial(new Color(0.85f, 0.85f, 0.8f));
        Material shaft = MakeMaterial(new Color(0.6f, 0.45f, 0.3f));

        AddPart(bowRoot, "Grip", new Vector3(0f, 0f, 0f), Quaternion.identity, new Vector3(0.04f, 0.14f, 0.04f), wood);
        AddPart(bowRoot, "UpperLimb", new Vector3(0f, 0.22f, -0.025f), Quaternion.Euler(-15f, 0f, 0f), new Vector3(0.03f, 0.32f, 0.03f), wood);
        AddPart(bowRoot, "LowerLimb", new Vector3(0f, -0.22f, -0.025f), Quaternion.Euler(15f, 0f, 0f), new Vector3(0.03f, 0.32f, 0.03f), wood);

        stringT = AddPart(bowRoot, "String", new Vector3(0f, 0f, -0.12f), Quaternion.identity, new Vector3(0.006f, 0.8f, 0.006f), cord);
        nockedArrow = AddPart(bowRoot, "NockedArrow", new Vector3(0f, 0f, 0.15f), Quaternion.identity, new Vector3(0.012f, 0.012f, 0.6f), shaft);
    }

    static Transform AddPart(Transform parent, string name, Vector3 pos, Quaternion rot, Vector3 scale, Material mat)
    {
        GameObject p = GameObject.CreatePrimitive(PrimitiveType.Cube);
        p.name = name;
        Destroy(p.GetComponent<Collider>());
        p.transform.SetParent(parent, false);
        p.transform.localPosition = pos;
        p.transform.localRotation = rot;
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
