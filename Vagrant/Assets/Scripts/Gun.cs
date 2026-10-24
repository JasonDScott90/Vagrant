using UnityEngine;

/// An automatic rifle. Put it on the action bar (slot 3 by default), hold left mouse to fire,
/// hold right mouse to aim down sights (a slight FOV zoom).
/// Every shot takes one "bullet" out of the PlayerInventory: no bullets in the inventory, no shooting.
/// Add this to the Player object (next to Bow). Bullets fly from the center of the screen.
public class Gun : MonoBehaviour
{
    [Header("References (auto-found if empty)")]
    public Camera cam;

    [Header("Shooting")]
    public float damage = 20f;             // 5 hits kill a deer (100 health)
    public float roundsPerMinute = 650f;   // automatic fire rate
    public float bulletSpeed = 110f;       // m/s
    public string ammoItemId = "bullet";   // inventory item consumed per shot

    [Header("Aim down sights")]
    public float adsZoom = 12f;            // field of view reduction while aiming
    public float adsSpeed = 10f;           // how fast the zoom and gun position blend

    [Header("Recoil")]
    public float recoilPitch = 1.1f;       // camera kick upward per shot (degrees)
    public float recoilYawSpread = 0.6f;   // random sideways kick per shot (degrees)
    public float kickRecoverSpeed = 14f;   // how fast the viewmodel kick settles

    [Header("Effects")]
    public GameObject muzzleFlashPrefab;   // spawned at the muzzle on every shot

    [Header("Sound")]
    public AudioClip shotSound;            // optional recording; a synthesized gunshot is used when empty
    [Range(0f, 1f)] public float shotVolume = 0.65f;

    public bool Equipped => equipped;

    bool equipped;
    float nextFire;
    float baseFov;
    float aim;                             // 0 = hip fire, 1 = fully aimed

    Transform gunRoot;
    Transform muzzle;
    PlayerInventory inventory;
    PlayerLook look;
    AudioSource audioSource;

    float kick;                            // viewmodel recoil kick, settles back to zero

    static readonly Vector3 hipPos = new Vector3(0.30f, -0.24f, 0.55f);
    static readonly Vector3 adsPos = new Vector3(0f, -0.085f, 0.45f);

    void Awake()
    {
        if (cam == null) cam = GetComponentInChildren<Camera>();
        inventory = GetComponent<PlayerInventory>();
        look = GetComponent<PlayerLook>();

        audioSource = gameObject.AddComponent<AudioSource>();
        audioSource.playOnAwake = false;
        audioSource.spatialBlend = 0f;      // weapon fire is heard by the player only
        baseFov = cam.fieldOfView;
        BuildGun();
        gunRoot.gameObject.SetActive(false);
    }

    // ---------------------------------------------------------------- called by ActionBar / InputManager

    public void SetEquipped(bool value)
    {
        if (equipped == value) return;
        equipped = value;
        aim = 0f;
        gunRoot.gameObject.SetActive(value);
        if (!value) cam.fieldOfView = baseFov;
    }

    /// Call every frame. fireHeld = left mouse, aimHeld = right mouse.
    public void Tick(bool fireHeld, bool aimHeld)
    {
        if (!equipped) return;

        // aim down sights: a smooth, slight zoom
        aim = Mathf.Lerp(aim, aimHeld ? 1f : 0f, adsSpeed * Time.deltaTime);
        cam.fieldOfView = Mathf.Lerp(cam.fieldOfView, baseFov - adsZoom * aim, adsSpeed * Time.deltaTime);
        // viewmodel recoil: the gun jerks back and up, then settles
        kick = Mathf.Lerp(kick, 0f, kickRecoverSpeed * Time.deltaTime);
        gunRoot.localPosition = Vector3.Lerp(hipPos, adsPos, aim) + new Vector3(0f, 0f, -kick * 0.055f);
        gunRoot.localRotation = Quaternion.Euler(-kick * 3.5f, 0f, 0f);

        // fully automatic while the button is held
        if (fireHeld && Time.time >= nextFire) TryFire();
    }

    // ---------------------------------------------------------------- shooting

    void TryFire()
    {
        // one bullet per shot straight out of the inventory: no bullets, no bang
        if (inventory == null || inventory.Container == null) return;
        if (inventory.Container.Remove(ammoItemId, 1) != 1) return;

        nextFire = Time.time + 60f / Mathf.Max(1f, roundsPerMinute);

        // camera + viewmodel recoil
        kick = Mathf.Min(1f, kick + 0.45f);
        if (look != null)
            look.AddRecoil(recoilPitch, Random.Range(-recoilYawSpread, recoilYawSpread));

        Vector3 dir = cam.transform.forward;
        Vector3 origin = cam.transform.position + dir * 0.6f;
        Bullet.Spawn(origin, dir * bulletSpeed, damage, transform);

        SpawnMuzzleFlash();
        PlayShot();
    }

    void SpawnMuzzleFlash()
    {
        if (muzzleFlashPrefab == null || muzzle == null) return;
        Quaternion rot = muzzle.rotation * Quaternion.Euler(0f, 0f, Random.Range(0f, 360f));
        GameObject flash = Instantiate(muzzleFlashPrefab, muzzle.position, rot, gunRoot);
        Destroy(flash, 0.12f);   // a real flash is a blink, not a glow that lingers
    }

    void PlayShot()
    {
        if (audioSource == null) return;
        AudioClip clip = shotSound != null ? shotSound : ProceduralAudio.Gunshot();
        audioSource.pitch = Random.Range(0.94f, 1.06f);   // slight variation so rapid fire doesn't sound cloned
        audioSource.PlayOneShot(clip, shotVolume);
    }

    // ---------------------------------------------------------------- placeholder model

    void BuildGun()
    {
        GameObject root = new GameObject("Gun");
        gunRoot = root.transform;
        gunRoot.SetParent(cam.transform, false);
        gunRoot.localPosition = hipPos;
        gunRoot.localRotation = Quaternion.identity;

        Material metal = MakeMaterial(new Color(0.16f, 0.17f, 0.19f));
        Material dark = MakeMaterial(new Color(0.07f, 0.07f, 0.08f));
        Material wood = MakeMaterial(new Color(0.32f, 0.22f, 0.13f));

        AddPart(gunRoot, "Receiver", new Vector3(0f, 0f, 0f), Quaternion.identity, new Vector3(0.06f, 0.09f, 0.34f), metal);
        AddPart(gunRoot, "Barrel", new Vector3(0f, 0.01f, 0.32f), Quaternion.identity, new Vector3(0.025f, 0.025f, 0.36f), dark);
        muzzle = AddPart(gunRoot, "Muzzle", new Vector3(0f, 0.01f, 0.52f), Quaternion.identity, new Vector3(0.035f, 0.035f, 0.07f), dark);
        AddPart(gunRoot, "Grip", new Vector3(0f, -0.11f, -0.06f), Quaternion.Euler(15f, 0f, 0f), new Vector3(0.04f, 0.13f, 0.05f), dark);
        AddPart(gunRoot, "Stock", new Vector3(0f, -0.02f, -0.26f), Quaternion.identity, new Vector3(0.05f, 0.1f, 0.2f), wood);
        AddPart(gunRoot, "Magazine", new Vector3(0f, -0.13f, 0.06f), Quaternion.Euler(-10f, 0f, 0f), new Vector3(0.04f, 0.15f, 0.07f), metal);
        AddPart(gunRoot, "Sight", new Vector3(0f, 0.08f, 0.1f), Quaternion.identity, new Vector3(0.015f, 0.03f, 0.02f), dark);
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