using UnityEngine;
using UnityEngine.UI;

/// Rust-style building with a hammer.
///  - Press the Hammer key to equip / holster.
///  - Hold the Wheel button (right mouse): a radial menu opens, move the mouse toward a piece, release to select it.
///  - A green/red ghost shows where the piece will go and snaps to existing pieces. Place = left mouse. Rotate = R.
/// Add this to the Player object (next to PlayerMotor / PlayerLook / InputManager).
public class BuildingSystem : MonoBehaviour
{
    [Header("References (auto-found if empty)")]
    public Camera cam;
    public Material pieceMaterial;

    [Header("Placement")]
    public float buildRange = 7f;
    public float snapRange = 2.8f;             // how close your aim must be to a snap point
    public float foundationRaise = 0.25f;      // how far a free-placed foundation sticks out of the ground
    public float maxGroundVariation = 1.2f;    // foundations can't be placed on ground steeper than this across the 3m

    [Header("Wheel")]
    public float wheelRadius = 240f;
    public float wheelInnerRadius = 70f;
    public float wheelDeadzone = 40f;
    public float wheelSensitivity = 1f;

    public bool WheelOpen => wheelOpen;
    public bool HammerEquipped => hammerEquipped;

    // state
    bool hammerEquipped;
    bool wheelOpen;
    PieceType selected = PieceType.Foundation;
    int rotationStep;

    // ghost
    GameObject ghost;
    PieceType ghostType;
    Material ghostMat;
    bool ghostValid;
    Vector3 targetPos;
    Quaternion targetRot;

    // hammer visual
    Transform hammer;
    float swingTimer;
    const float swingDuration = 0.25f;

    // wheel UI
    Canvas canvas;
    GameObject wheelRoot;
    Image[] sliceImages;
    Image dot;
    Text centerText;
    Text hint;
    Sprite circleSprite;
    Font font;
    Vector2 wheelCursor;
    int hovered = -1;

    static readonly Color baseColor = new Color(0.12f, 0.12f, 0.12f, 0.75f);
    static readonly Color hoverColor = new Color(0.30f, 0.55f, 0.85f, 0.9f);
    static readonly Color selectedColor = new Color(0.20f, 0.35f, 0.50f, 0.85f);
    static readonly Color validColor = new Color(0.2f, 1f, 0.3f, 0.45f);
    static readonly Color invalidColor = new Color(1f, 0.2f, 0.2f, 0.45f);

    void Awake()
    {
        if (cam == null) cam = GetComponentInChildren<Camera>();

        if (pieceMaterial == null)
        {
            // Borrow the project's default material (URP Lit) from a temporary cube.
            GameObject tmp = GameObject.CreatePrimitive(PrimitiveType.Cube);
            pieceMaterial = new Material(tmp.GetComponent<Renderer>().sharedMaterial);
            pieceMaterial.color = new Color(0.55f, 0.42f, 0.30f);
            Destroy(tmp);
        }

        Shader ghostShader = Shader.Find("Sprites/Default");
        ghostMat = ghostShader != null ? new Material(ghostShader) : new Material(pieceMaterial);
        ghostMat.color = validColor;

        BuildHammer();
        BuildUI();
        SetHammer(false);
    }

    // ---------------------------------------------------------------- input entry points (called by InputManager)

    public void ToggleHammer() { SetHammer(!hammerEquipped); }

    public void RotatePiece()
    {
        if (!hammerEquipped || wheelOpen) return;
        rotationStep = (rotationStep + 1) % 4;
    }

    public void UpdateWheel(bool held)
    {
        if (!hammerEquipped) { if (wheelOpen) CloseWheel(false); return; }
        if (held && !wheelOpen) OpenWheel();
        else if (!held && wheelOpen) CloseWheel(true);
    }

    public void FeedWheelInput(Vector2 delta)
    {
        if (!wheelOpen) return;
        wheelCursor = Vector2.ClampMagnitude(wheelCursor + delta * wheelSensitivity, wheelRadius - 20f);

        int n = PieceDefs.Names.Length;
        if (wheelCursor.magnitude >= wheelDeadzone)
        {
            float ang = Mathf.Atan2(wheelCursor.x, wheelCursor.y) * Mathf.Rad2Deg; // clockwise from the top
            if (ang < 0f) ang += 360f;
            hovered = Mathf.RoundToInt(ang / (360f / n)) % n;
        }
        UpdateWheelVisuals();
    }

    public void Place()
    {
        if (!hammerEquipped || wheelOpen || ghost == null || !ghost.activeSelf || !ghostValid) return;

        GameObject go = PieceFactory.Build(ghostType, pieceMaterial, true);
        go.transform.SetPositionAndRotation(targetPos, targetRot);
        go.AddComponent<BuildPiece>().type = ghostType;
        swingTimer = swingDuration;
    }

    /// Called every frame by InputManager after the camera has been updated.
    public void Refresh()
    {
        UpdateHammerSwing();

        if (!hammerEquipped || wheelOpen)
        {
            if (ghost != null) ghost.SetActive(false);
            return;
        }

        hint.text = "[1] Holster    [LMB] Place    [Hold RMB] Build menu    [R] Rotate        Selected: " + PieceDefs.Names[(int)selected];

        if (!TryGetTarget(out targetPos, out targetRot, out ghostValid))
        {
            if (ghost != null) ghost.SetActive(false);
            ghostValid = false;
            return;
        }

        EnsureGhost();
        ghost.SetActive(true);
        ghost.transform.SetPositionAndRotation(targetPos, targetRot);
        ghostMat.color = ghostValid ? validColor : invalidColor;
    }

    // ---------------------------------------------------------------- hammer / wheel state

    void SetHammer(bool equipped)
    {
        hammerEquipped = equipped;
        hammer.gameObject.SetActive(equipped);
        hint.gameObject.SetActive(equipped);
        if (!equipped)
        {
            if (wheelOpen) CloseWheel(false);
            if (ghost != null) ghost.SetActive(false);
        }
    }

    void OpenWheel()
    {
        wheelOpen = true;
        wheelCursor = Vector2.zero;
        hovered = -1;
        wheelRoot.SetActive(true);
        UpdateWheelVisuals();
    }

    void CloseWheel(bool apply)
    {
        if (apply && hovered >= 0)
        {
            selected = (PieceType)hovered;
            rotationStep = 0;
        }
        wheelOpen = false;
        wheelRoot.SetActive(false);
    }

    void UpdateWheelVisuals()
    {
        for (int i = 0; i < sliceImages.Length; i++)
        {
            Color c = baseColor;
            if (i == (int)selected) c = selectedColor;
            if (i == hovered) c = hoverColor;
            sliceImages[i].color = c;
        }
        centerText.text = hovered >= 0 ? PieceDefs.Names[hovered] : PieceDefs.Names[(int)selected];
        dot.rectTransform.anchoredPosition = wheelCursor;
    }

    // ---------------------------------------------------------------- targeting / snapping

    bool TryGetTarget(out Vector3 pos, out Quaternion rot, out bool valid)
    {
        pos = Vector3.zero;
        rot = Quaternion.identity;
        valid = false;

        Ray ray = new Ray(cam.transform.position, cam.transform.forward);
        if (!Physics.Raycast(ray, out RaycastHit hit, buildRange, ~(1 << 2), QueryTriggerInteraction.Ignore))
            return false;

        PieceType type = selected;
        SocketDef[] needs = PieceDefs.Needs(type);
        bool rotatable = PieceDefs.CanRotateWhenSnapped(type);

        float best = float.MaxValue;
        bool found = false;

        foreach (BuildPiece piece in BuildPiece.All)
        {
            foreach (WorldSocket s in piece.GetWorldSockets())
            {
                for (int i = 0; i < needs.Length; i++)
                {
                    if (needs[i].kind != s.kind) continue;

                    float yaw = s.yaw + 180f - needs[i].yaw + (rotatable ? 90f * rotationStep : 0f);
                    Quaternion q = Quaternion.Euler(0f, yaw, 0f);
                    Vector3 p = s.pos - q * needs[i].pos;

                    float score = Vector3.Distance(hit.point, p);
                    if (score < best)
                    {
                        best = score;
                        pos = p;
                        rot = q;
                        found = true;
                    }
                }
            }
        }

        if (found && best <= snapRange)
        {
            valid = !BuildPiece.IsOccupied(type, pos);
            return true;
        }

        // Nothing to snap to: only foundations can be placed freely, on the ground.
        if (type == PieceType.Foundation && hit.collider.GetComponentInParent<BuildPiece>() == null)
        {
            valid = FreeFoundation(hit.point, out pos, out rot);
            return true;
        }

        // Show a red ghost where you're aiming so it's clear it can't go there.
        pos = hit.point;
        rot = Quaternion.Euler(0f, Mathf.Round(transform.eulerAngles.y / 90f) * 90f, 0f);
        valid = false;
        return true;
    }

    bool FreeFoundation(Vector3 point, out Vector3 pos, out Quaternion rot)
    {
        float yaw = Mathf.Round(transform.eulerAngles.y / 90f) * 90f + 90f * rotationStep;
        rot = Quaternion.Euler(0f, yaw, 0f);
        pos = point;

        float h = PieceDefs.Half - 0.1f;
        Vector3[] samples =
        {
            Vector3.zero,
            new Vector3(h, 0f, h), new Vector3(-h, 0f, h),
            new Vector3(h, 0f, -h), new Vector3(-h, 0f, -h)
        };

        float maxY = float.MinValue, minY = float.MaxValue;
        foreach (Vector3 o in samples)
        {
            if (!TryGroundHeight(point + rot * o, out float y)) return false;
            maxY = Mathf.Max(maxY, y);
            minY = Mathf.Min(minY, y);
        }

        pos = new Vector3(point.x, maxY + foundationRaise, point.z);
        if (maxY - minY > maxGroundVariation) return false;

        foreach (BuildPiece p in BuildPiece.All)
        {
            if (p.type != PieceType.Foundation) continue;
            Vector3 d = p.transform.position - pos;
            if (new Vector2(d.x, d.z).magnitude < 2.9f) return false; // would overlap
        }
        return true;
    }

    bool TryGroundHeight(Vector3 p, out float y)
    {
        Terrain t = Terrain.activeTerrain;
        if (t != null)
        {
            Vector3 tp = t.transform.position;
            Vector3 size = t.terrainData.size;
            if (p.x >= tp.x && p.x <= tp.x + size.x && p.z >= tp.z && p.z <= tp.z + size.z)
            {
                y = t.SampleHeight(p) + tp.y;
                return true;
            }
        }
        if (Physics.Raycast(p + Vector3.up * 5f, Vector3.down, out RaycastHit h, 20f, ~(1 << 2), QueryTriggerInteraction.Ignore)
            && h.collider.GetComponentInParent<BuildPiece>() == null)
        {
            y = h.point.y;
            return true;
        }
        y = 0f;
        return false;
    }

    void EnsureGhost()
    {
        if (ghost != null && ghostType == selected) return;
        if (ghost != null) Destroy(ghost);
        ghost = PieceFactory.Build(selected, ghostMat, false);
        SetLayerRecursive(ghost, 2); // Ignore Raycast, so the ghost never blocks your aim
        ghostType = selected;
    }

    static void SetLayerRecursive(GameObject go, int layer)
    {
        go.layer = layer;
        foreach (Transform c in go.transform) SetLayerRecursive(c.gameObject, layer);
    }

    // ---------------------------------------------------------------- hammer visual (placeholder)

    void BuildHammer()
    {
        GameObject root = new GameObject("Hammer");
        hammer = root.transform;
        hammer.SetParent(cam.transform, false);
        hammer.localPosition = new Vector3(0.35f, -0.45f, 0.7f);
        hammer.localRotation = Quaternion.Euler(10f, 0f, 0f);

        Material wood = new Material(pieceMaterial) { color = new Color(0.45f, 0.30f, 0.18f) };
        Material metal = new Material(pieceMaterial) { color = new Color(0.35f, 0.35f, 0.38f) };

        AddHammerPart(hammer, new Vector3(0f, 0.2f, 0f), new Vector3(0.05f, 0.4f, 0.05f), wood);
        AddHammerPart(hammer, new Vector3(0f, 0.42f, 0f), new Vector3(0.22f, 0.12f, 0.12f), metal);
    }

    static void AddHammerPart(Transform parent, Vector3 pos, Vector3 scale, Material mat)
    {
        GameObject p = GameObject.CreatePrimitive(PrimitiveType.Cube);
        Destroy(p.GetComponent<Collider>());
        p.transform.SetParent(parent, false);
        p.transform.localPosition = pos;
        p.transform.localScale = scale;
        p.GetComponent<Renderer>().sharedMaterial = mat;
        p.layer = 2;
    }

    void UpdateHammerSwing()
    {
        if (!hammerEquipped) return;
        float swing = 0f;
        if (swingTimer > 0f)
        {
            swingTimer -= Time.deltaTime;
            float t = 1f - Mathf.Clamp01(swingTimer / swingDuration);
            swing = Mathf.Sin(t * Mathf.PI) * 70f;
        }
        hammer.localRotation = Quaternion.Euler(10f + swing, 0f, 0f);
    }

    // ---------------------------------------------------------------- wheel UI (built in code, no setup needed)

    void BuildUI()
    {
        circleSprite = MakeCircleSprite();
        font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");

        GameObject canvasGO = new GameObject("BuildingUI");
        canvas = canvasGO.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 50;
        CanvasScaler scaler = canvasGO.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = 0.5f;

        // hint line (bottom of screen)
        hint = NewText("Hint", canvasGO.transform, Vector2.zero, new Vector2(1400f, 40f), 22);
        hint.rectTransform.anchorMin = hint.rectTransform.anchorMax = new Vector2(0.5f, 0f);
        hint.rectTransform.anchoredPosition = new Vector2(0f, 50f);

        // wheel
        wheelRoot = NewRect("Wheel", canvasGO.transform, Vector2.zero, new Vector2(wheelRadius * 2f, wheelRadius * 2f)).gameObject;
        NewImage("Background", wheelRoot.transform, new Vector2(wheelRadius * 2f, wheelRadius * 2f), new Color(0f, 0f, 0f, 0.35f));

        int n = PieceDefs.Names.Length;
        sliceImages = new Image[n];
        float labelRadius = (wheelRadius + wheelInnerRadius) * 0.5f;

        for (int i = 0; i < n; i++)
        {
            Image img = NewImage("Slice" + i, wheelRoot.transform, new Vector2(wheelRadius * 2f, wheelRadius * 2f), baseColor);
            img.type = Image.Type.Filled;
            img.fillMethod = Image.FillMethod.Radial360;
            img.fillOrigin = (int)Image.Origin360.Top;
            img.fillClockwise = true;
            img.fillAmount = 1f / n - 0.008f; // thin gap between slices

            float start = i * 360f / n - 180f / n + 1.4f;
            img.rectTransform.localRotation = Quaternion.Euler(0f, 0f, -start);
            sliceImages[i] = img;

            float a = i * 360f / n * Mathf.Deg2Rad;
            Vector2 p = new Vector2(Mathf.Sin(a), Mathf.Cos(a)) * labelRadius;
            NewText("Label" + i, wheelRoot.transform, p, new Vector2(170f, 40f), 26).text = PieceDefs.Names[i];
        }

        NewImage("Hole", wheelRoot.transform, new Vector2(wheelInnerRadius * 2f, wheelInnerRadius * 2f), new Color(0.04f, 0.04f, 0.04f, 0.92f));
        centerText = NewText("CenterText", wheelRoot.transform, Vector2.zero, new Vector2(wheelInnerRadius * 2f, 40f), 24);
        dot = NewImage("Cursor", wheelRoot.transform, new Vector2(14f, 14f), Color.white);

        wheelRoot.SetActive(false);
    }

    RectTransform NewRect(string name, Transform parent, Vector2 anchored, Vector2 size)
    {
        GameObject go = new GameObject(name, typeof(RectTransform));
        RectTransform rt = go.GetComponent<RectTransform>();
        rt.SetParent(parent, false);
        rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = anchored;
        rt.sizeDelta = size;
        return rt;
    }

    Image NewImage(string name, Transform parent, Vector2 size, Color color)
    {
        RectTransform rt = NewRect(name, parent, Vector2.zero, size);
        Image img = rt.gameObject.AddComponent<Image>();
        img.sprite = circleSprite;
        img.color = color;
        img.raycastTarget = false;
        return img;
    }

    Text NewText(string name, Transform parent, Vector2 anchored, Vector2 size, int fontSize)
    {
        RectTransform rt = NewRect(name, parent, anchored, size);
        Text t = rt.gameObject.AddComponent<Text>();
        t.font = font;
        t.fontSize = fontSize;
        t.alignment = TextAnchor.MiddleCenter;
        t.horizontalOverflow = HorizontalWrapMode.Overflow;
        t.verticalOverflow = VerticalWrapMode.Overflow;
        t.color = Color.white;
        t.raycastTarget = false;
        return t;
    }

    static Sprite MakeCircleSprite()
    {
        const int size = 256;
        Texture2D tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
        tex.wrapMode = TextureWrapMode.Clamp;
        Color32[] cols = new Color32[size * size];
        float r = size * 0.5f;
        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float dx = x + 0.5f - r, dy = y + 0.5f - r;
                float a = Mathf.Clamp01(r - Mathf.Sqrt(dx * dx + dy * dy));
                cols[y * size + x] = new Color32(255, 255, 255, (byte)(a * 255f));
            }
        }
        tex.SetPixels32(cols);
        tex.Apply();
        return Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 100f);
    }
}
