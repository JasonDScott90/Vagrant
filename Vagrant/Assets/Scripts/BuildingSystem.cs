using UnityEngine;
using UnityEngine.UI;

/// Rust-style building with a hammer.
///  - Press the Hammer key to equip / holster.
///  - Hold the Wheel button (right mouse): a radial menu opens, move the mouse toward an entry, release to select it.
///  - Pieces: a green/red ghost shows where it will go and snaps to existing pieces. Place = left mouse. Rotate = R.
///  - Upgrade tool: aim at a piece, left mouse upgrades it to the chosen tier (R changes the tier).
///  - Demolish tool: aim at a piece, left mouse removes it (and anything that was only held up by it).
///  - Interact (E) opens/closes doors.
/// Add this to the Player object (next to PlayerMotor / PlayerLook / InputManager).
public class BuildingSystem : MonoBehaviour
{
    [Header("References (auto-found / auto-generated if empty)")]
    public Camera cam;
    public Material pieceMaterial;
    [Tooltip("Optional: 5 materials for Twig, Wood, Stone, Metal, Armored. Leave empty to use generated colors.")]
    public Material[] tierMaterials;

    [Header("Placement")]
    public float buildRange = 7f;
    public float snapRange = 2.8f;
    public float foundationRaise = 0.25f;
    public float maxGroundVariation = 1.2f;
    public float interactRange = 3.5f;

    [Header("Wheel")]
    public float wheelOuterRadius = 290f;
    public float wheelHoleRadius = 85f;
    public float wheelDeadzone = 40f;
    public float wheelSensitivity = 1f;

    public bool WheelOpen => wheelOpen;
    public bool HammerEquipped => hammerEquipped;

    // state
    bool hammerEquipped;
    bool wheelOpen;
    int selectedIndex;               // 0..PieceCount-1 = pieces, then Upgrade, then Demolish
    int rotationStep;
    int upgradeTier = 1;             // tier the Upgrade tool applies (1 = Wood ... 4 = Armored)

    bool ToolMode => selectedIndex >= PieceDefs.PieceCount;
    bool IsUpgradeTool => selectedIndex == PieceDefs.PieceCount;
    bool IsDemolishTool => selectedIndex == PieceDefs.PieceCount + 1;
    PieceType SelectedPiece => (PieceType)selectedIndex;

    // ghost
    GameObject ghost;
    PieceType ghostType;
    Material ghostMat;
    Material highlightMat;
    bool ghostValid;
    Vector3 targetPos;
    Quaternion targetRot;
    BuildPiece highlighted;

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
            Destroy(tmp);
        }

        BuildTierMaterials();

        Shader overlay = Shader.Find("Sprites/Default");
        ghostMat = overlay != null ? new Material(overlay) : new Material(pieceMaterial);
        highlightMat = overlay != null ? new Material(overlay) : new Material(pieceMaterial);
        ghostMat.color = validColor;

        BuildHammer();
        BuildUI();
        SetHammer(false);
    }

    void BuildTierMaterials()
    {
        int n = PieceDefs.TierNames.Length;
        if (tierMaterials != null && tierMaterials.Length == n)
        {
            bool ok = true;
            foreach (Material m in tierMaterials) if (m == null) ok = false;
            if (ok) return;
        }

        Color[] colors =
        {
            new Color(0.62f, 0.52f, 0.33f),  // Twig
            new Color(0.50f, 0.32f, 0.16f),  // Wood
            new Color(0.62f, 0.62f, 0.60f),  // Stone
            new Color(0.42f, 0.47f, 0.52f),  // Metal
            new Color(0.16f, 0.18f, 0.21f),  // Armored
        };

        tierMaterials = new Material[n];
        for (int i = 0; i < n; i++)
        {
            Material m = new Material(pieceMaterial);
            m.color = colors[i];
            bool shiny = i >= 3;
            if (m.HasProperty("_Metallic")) m.SetFloat("_Metallic", shiny ? 0.85f : 0f);
            if (m.HasProperty("_Smoothness")) m.SetFloat("_Smoothness", shiny ? 0.6f : 0.12f);
            tierMaterials[i] = m;
        }
    }

    // ---------------------------------------------------------------- input entry points (called by InputManager)

    public void ToggleHammer() { SetHammer(!hammerEquipped); }

    public void RotatePiece()
    {
        if (!hammerEquipped || wheelOpen) return;

        if (IsUpgradeTool)
            upgradeTier = upgradeTier % (PieceDefs.TierNames.Length - 1) + 1; // cycles 1..4
        else if (!ToolMode)
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
        wheelCursor = Vector2.ClampMagnitude(wheelCursor + delta * wheelSensitivity, wheelOuterRadius - 20f);

        int n = PieceDefs.Names.Length;
        if (wheelCursor.magnitude >= wheelDeadzone)
        {
            float ang = Mathf.Atan2(wheelCursor.x, wheelCursor.y) * Mathf.Rad2Deg; // clockwise from the top
            if (ang < 0f) ang += 360f;
            hovered = Mathf.RoundToInt(ang / (360f / n)) % n;
        }
        UpdateWheelVisuals();
    }

    /// Left mouse: place a piece, upgrade the aimed piece, or demolish the aimed piece, depending on the selected entry.
    public void Place()
    {
        if (!hammerEquipped || wheelOpen) return;

        if (IsDemolishTool)
        {
            BuildPiece p = AimedPiece();
            if (p == null) return;
            if (highlighted == p) highlighted = null;
            BuildPiece.Demolish(p);
            swingTimer = swingDuration;
            return;
        }

        if (IsUpgradeTool)
        {
            BuildPiece p = AimedPiece();
            if (p == null || p.tier >= upgradeTier) return;
            p.SetTier(upgradeTier, tierMaterials[upgradeTier]);
            swingTimer = swingDuration;
            return;
        }

        if (ghost == null || !ghost.activeSelf || !ghostValid) return;

        int startTier = ghostType == PieceType.Door ? 1 : 0; // doors start as wood, everything else as twig
        GameObject go = PieceFactory.Build(ghostType, tierMaterials[startTier], true);
        go.transform.SetPositionAndRotation(targetPos, targetRot);
        go.AddComponent<BuildPiece>().Init(ghostType, startTier, tierMaterials[startTier]);
        if (ghostType == PieceType.Door) go.AddComponent<DoorController>();
        swingTimer = swingDuration;
    }

    /// E: open / close the door you're looking at.
    public void Interact()
    {
        Ray ray = new Ray(cam.transform.position, cam.transform.forward);
        if (Physics.Raycast(ray, out RaycastHit hit, interactRange, ~(1 << 2), QueryTriggerInteraction.Ignore))
        {
            DoorController door = hit.collider.GetComponentInParent<DoorController>();
            if (door != null) door.Toggle(transform.position);
        }
    }

    /// Called every frame by InputManager after the camera has been updated.
    public void Refresh()
    {
        UpdateHammerSwing();

        if (!hammerEquipped || wheelOpen)
        {
            HideGhost();
            SetHighlighted(null);
            return;
        }

        if (ToolMode)
        {
            HideGhost();
            RefreshTool();
            return;
        }

        SetHighlighted(null);
        hint.text = "[1] Holster    [LMB] Place    [Hold RMB] Menu    [R] Rotate    [E] Doors        Selected: "
                    + PieceDefs.Names[selectedIndex];

        if (!TryGetTarget(out targetPos, out targetRot, out ghostValid))
        {
            HideGhost();
            ghostValid = false;
            return;
        }

        EnsureGhost();
        ghost.SetActive(true);
        ghost.transform.SetPositionAndRotation(targetPos, targetRot);
        ghostMat.color = ghostValid ? validColor : invalidColor;
    }

    // ---------------------------------------------------------------- upgrade / demolish tools

    void RefreshTool()
    {
        BuildPiece aimed = AimedPiece();
        SetHighlighted(aimed);
        string looking = aimed != null ? "    Looking at: " + aimed.Label : "";

        if (IsDemolishTool)
        {
            highlightMat.color = new Color(1f, 0.2f, 0.2f, 0.55f);
            hint.text = "[1] Holster    [LMB] Demolish    [Hold RMB] Menu" + looking;
        }
        else
        {
            bool can = aimed != null && aimed.tier < upgradeTier;
            highlightMat.color = can ? new Color(0.3f, 1f, 0.4f, 0.55f) : new Color(0.7f, 0.7f, 0.7f, 0.45f);
            hint.text = "[1] Holster    [LMB] Upgrade to " + PieceDefs.TierNames[upgradeTier]
                        + "    [R] Change tier    [Hold RMB] Menu" + looking;
        }
    }

    BuildPiece AimedPiece()
    {
        Ray ray = new Ray(cam.transform.position, cam.transform.forward);
        if (Physics.Raycast(ray, out RaycastHit hit, buildRange, ~(1 << 2), QueryTriggerInteraction.Ignore))
            return hit.collider.GetComponentInParent<BuildPiece>();
        return null;
    }

    void SetHighlighted(BuildPiece piece)
    {
        if (highlighted == piece) return;
        if (highlighted != null) highlighted.SetHighlight(null);
        highlighted = piece;
        if (highlighted != null) highlighted.SetHighlight(highlightMat);
    }

    void HideGhost()
    {
        if (ghost != null) ghost.SetActive(false);
    }

    // ---------------------------------------------------------------- hammer / wheel state

    public void SetHammer(bool equipped)
    {
        hammerEquipped = equipped;
        hammer.gameObject.SetActive(equipped);
        hint.gameObject.SetActive(equipped);
        if (!equipped)
        {
            if (wheelOpen) CloseWheel(false);
            HideGhost();
            SetHighlighted(null);
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
            selectedIndex = hovered;
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
            if (i == selectedIndex) c = selectedColor;
            if (i == hovered) c = hoverColor;
            sliceImages[i].color = c;
        }
        centerText.text = hovered >= 0 ? PieceDefs.Names[hovered] : PieceDefs.Names[selectedIndex];
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

        PieceType type = SelectedPiece;
        SocketDef[] needs = PieceDefs.Needs(type);
        float extraYaw = PieceDefs.RotationOffset(type, rotationStep);

        float best = float.MaxValue;
        bool found = false;

        foreach (BuildPiece piece in BuildPiece.All)
        {
            foreach (WorldSocket s in piece.GetWorldSockets())
            {
                for (int i = 0; i < needs.Length; i++)
                {
                    if (needs[i].kind != s.kind) continue;

                    float yaw = s.yaw + 180f - needs[i].yaw + extraYaw;
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
        PieceType wanted = SelectedPiece;
        if (ghost != null && ghostType == wanted) return;
        if (ghost != null) Destroy(ghost);
        ghost = PieceFactory.Build(wanted, ghostMat, false);
        SetLayerRecursive(ghost, 2); // Ignore Raycast, so the ghost never blocks your aim
        ghostType = wanted;
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
        hint = NewText("Hint", canvasGO.transform, Vector2.zero, new Vector2(1600f, 40f), 22);
        hint.rectTransform.anchorMin = hint.rectTransform.anchorMax = new Vector2(0.5f, 0f);
        hint.rectTransform.anchoredPosition = new Vector2(0f, 125f); // sits above the action bar

        // wheel
        float size = wheelOuterRadius * 2f;
        wheelRoot = NewRect("Wheel", canvasGO.transform, Vector2.zero, new Vector2(size, size)).gameObject;
        NewImage("Background", wheelRoot.transform, new Vector2(size, size), new Color(0f, 0f, 0f, 0.35f));

        int n = PieceDefs.Names.Length;
        sliceImages = new Image[n];
        float labelRadius = (wheelOuterRadius + wheelHoleRadius) * 0.5f;

        for (int i = 0; i < n; i++)
        {
            Image img = NewImage("Slice" + i, wheelRoot.transform, new Vector2(size, size), baseColor);
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
            NewText("Label" + i, wheelRoot.transform, p, new Vector2(150f, 40f), 22).text = PieceDefs.Names[i];
        }

        NewImage("Hole", wheelRoot.transform, new Vector2(wheelHoleRadius * 2f, wheelHoleRadius * 2f), new Color(0.04f, 0.04f, 0.04f, 0.92f));
        centerText = NewText("CenterText", wheelRoot.transform, Vector2.zero, new Vector2(wheelHoleRadius * 2f, 40f), 22);
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
