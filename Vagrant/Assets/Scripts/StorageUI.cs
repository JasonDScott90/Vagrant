using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;

/// Rust-style storage screen: your inventory on the left, the open box on the right.
/// Drag stacks between any slots (merge, fill or swap), or click a stack to quick-move it
/// to the other side. Built entirely in code (like the build wheel).
public class StorageUI : MonoBehaviour
{
    public static StorageUI Instance { get; private set; }
    public static bool IsOpen { get; private set; }

    public float slotSize = 86f;
    public float slotGap = 8f;

    ItemContainer playerInv;
    ItemContainer boxInv;
    string boxTitle = "Storage";

    Text titleText;
    Text rightHeader;
    RectTransform playerGrid;
    RectTransform boxGrid;
    StorageSlot[] playerSlots;
    StorageSlot[] boxSlots;

    Font font;
    Sprite white;

    RectTransform canvasRt;
    RectTransform ghost;
    Image ghostIcon;
    Text ghostAmount;
    StorageSlot dragFrom;

    const float PanelW = 1240f;
    const float PanelH = 660f;
    const float GridTopY = 210f;
    const float SideX = 310f;
    const int Columns = 6;

    static readonly Color SlotNormal = new Color(0.16f, 0.15f, 0.13f, 0.95f);
    static readonly Color SlotHover = new Color(0.28f, 0.27f, 0.23f, 1f);

    // ---------------------------------------------------------------- public API

    public static void Open(ItemContainer player, string title, ItemContainer box)
    {
        if (Instance == null)
        {
            GameObject go = new GameObject("StorageUI");
            Instance = go.AddComponent<StorageUI>();   // builds its UI in Awake
            go.SetActive(false);
        }

        Instance.playerInv = player;
        Instance.boxInv = box;
        Instance.boxTitle = string.IsNullOrEmpty(title) ? "Storage" : title;
        Instance.RebuildGrids();
        Instance.Refresh();
        Instance.gameObject.SetActive(true);

        IsOpen = true;
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
    }

    public static void Close()
    {
        IsOpen = false;
        if (Instance != null) Instance.gameObject.SetActive(false);
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
    }

    // ---------------------------------------------------------------- build

    void Awake()
    {
        font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        white = MakeWhiteSprite();

        // UI pointer events need an EventSystem with an input module (the project uses
        // the new Input System, so the legacy StandaloneInputModule would not work).
        if (EventSystem.current == null)
        {
            GameObject es = new GameObject("EventSystem");
            es.AddComponent<EventSystem>();
            es.AddComponent<InputSystemUIInputModule>();
        }

        GameObject canvasGO = new GameObject("StorageCanvas");
        canvasGO.transform.SetParent(transform, false);

        Canvas canvas = canvasGO.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 60;

        // required for clicks / drags to hit the UI at all
        canvasGO.AddComponent<GraphicRaycaster>();

        CanvasScaler scaler = canvasGO.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = 0.5f;

        canvasRt = canvasGO.GetComponent<RectTransform>();

        // dim background so the world goes dark while sorting items (also swallows stray clicks)
        NewStretchImage("Dim", canvasGO.transform, new Color(0f, 0f, 0f, 0.55f));

        RectTransform panel = NewRect("Panel", canvasGO.transform, Vector2.zero, new Vector2(PanelW, PanelH));
        Image panelImg = panel.gameObject.AddComponent<Image>();
        panelImg.sprite = white;
        panelImg.color = new Color(0.09f, 0.085f, 0.075f, 0.97f);

        titleText = NewText("Title", panel, new Vector2(0f, 302f), new Vector2(1180f, 40f), 28);

        NewText("LeftHeader", panel, new Vector2(-SideX, 250f), new Vector2(560f, 26f), 20).text = "YOUR INVENTORY";
        rightHeader = NewText("RightHeader", panel, new Vector2(SideX, 250f), new Vector2(560f, 26f), 20);

        playerGrid = NewRect("PlayerGrid", panel, new Vector2(-SideX, 0f), Vector2.zero);
        boxGrid = NewRect("BoxGrid", panel, new Vector2(SideX, 0f), Vector2.zero);

        Text hint = NewText("Hint", panel, new Vector2(0f, -292f), new Vector2(1180f, 30f), 20);
        hint.text = "[E] / [ESC] Close      -      Drag stacks between slots      -      Click a stack to send it to the other side";
        hint.color = new Color(1f, 1f, 1f, 0.7f);

        CreateGhost();
    }

    void OnDisable()
    {
        dragFrom = null;
        if (ghost != null) ghost.gameObject.SetActive(false);
    }

    void RebuildGrids()
    {
        int pCount = playerInv != null ? playerInv.SlotCount : 24;
        int bCount = boxInv != null ? boxInv.SlotCount : 12;

        if (playerSlots == null || playerSlots.Length != pCount)
            playerSlots = BuildGrid(playerGrid, pCount, true, "PlayerSlot");
        if (boxSlots == null || boxSlots.Length != bCount)
            boxSlots = BuildGrid(boxGrid, bCount, false, "BoxSlot");

        titleText.text = boxTitle;
        rightHeader.text = boxTitle.ToUpper();
    }

    StorageSlot[] BuildGrid(RectTransform grid, int count, bool playerSide, string prefix)
    {
        for (int i = grid.childCount - 1; i >= 0; i--) Destroy(grid.GetChild(i).gameObject);

        StorageSlot[] slots = new StorageSlot[count];
        float totalW = Columns * slotSize + (Columns - 1) * slotGap;

        for (int i = 0; i < count; i++)
        {
            int row = i / Columns;
            int col = i % Columns;
            Vector2 p = new Vector2(
                -totalW * 0.5f + slotSize * 0.5f + col * (slotSize + slotGap),
                GridTopY - row * (slotSize + slotGap));
            slots[i] = BuildSlot(grid, prefix + i, p, playerSide, i);
        }
        return slots;
    }

    StorageSlot BuildSlot(RectTransform grid, string name, Vector2 pos, bool playerSide, int index)
    {
        RectTransform rt = NewRect(name, grid, pos, new Vector2(slotSize, slotSize));

        StorageSlot slot = rt.gameObject.AddComponent<StorageSlot>();
        slot.ui = this;
        slot.playerSide = playerSide;
        slot.index = index;

        Image bg = rt.gameObject.AddComponent<Image>();
        bg.sprite = white;
        bg.color = SlotNormal;
        slot.bg = bg;

        RectTransform iconRt = NewRect("Icon", rt, new Vector2(0f, 14f), new Vector2(52f, 52f));
        Image icon = iconRt.gameObject.AddComponent<Image>();
        icon.sprite = white;
        icon.raycastTarget = false;
        slot.icon = icon;

        Text amount = NewText("Amount", rt, new Vector2(22f, -2f), new Vector2(44f, 18f), 15);
        amount.alignment = TextAnchor.MiddleRight;
        slot.amount = amount;

        Text label = NewText("Label", rt, new Vector2(0f, -28f), new Vector2(82f, 16f), 11);
        label.color = new Color(1f, 1f, 1f, 0.75f);
        slot.label = label;

        return slot;
    }

    // ---------------------------------------------------------------- drag ghost

    void CreateGhost()
    {
        GameObject go = new GameObject("DragGhost", typeof(RectTransform));
        ghost = go.GetComponent<RectTransform>();
        ghost.SetParent(canvasRt, false);
        ghost.sizeDelta = new Vector2(64f, 64f);

        RectTransform iconRt = NewRect("Icon", ghost, new Vector2(0f, 8f), new Vector2(52f, 52f));
        ghostIcon = iconRt.gameObject.AddComponent<Image>();
        ghostIcon.sprite = white;
        ghostIcon.raycastTarget = false;

        ghostAmount = NewText("Amount", ghost, new Vector2(22f, -18f), new Vector2(44f, 18f), 15);
        ghostAmount.alignment = TextAnchor.MiddleRight;

        ghost.gameObject.SetActive(false);
    }

    void MoveGhost(PointerEventData e)
    {
        if (ghost == null) return;
        RectTransformUtility.ScreenPointToLocalPointInRectangle(
            canvasRt, e.position, e.pressEventCamera, out Vector2 local);
        ghost.localPosition = local;
        ghost.SetAsLastSibling();
    }

    // ---------------------------------------------------------------- slot interaction

    public void OnSlotHover(StorageSlot slot, bool enter)
    {
        if (slot.bg != null) slot.bg.color = enter ? SlotHover : SlotNormal;
    }

    public void OnSlotClicked(StorageSlot slot)
    {
        if (playerInv == null || boxInv == null) return;

        if (slot.playerSide) playerInv.TryMoveSlotTo(slot.index, boxInv);
        else boxInv.TryMoveSlotTo(slot.index, playerInv);

        Refresh();
    }

    public void OnSlotBeginDrag(StorageSlot slot, PointerEventData e)
    {
        if (e.button != PointerEventData.InputButton.Left) return;

        ItemContainer c = slot.playerSide ? playerInv : boxInv;
        ItemStack stack = (c != null && slot.index < c.SlotCount) ? c.GetSlot(slot.index) : ItemStack.Empty;
        if (stack.IsEmpty) return;   // nothing picked up, let the drag fizzle out

        dragFrom = slot;
        ItemDef def = ItemDB.Get(stack.id);
        ghostIcon.color = def != null ? def.color : Color.white;
        ghostAmount.text = stack.amount.ToString("N0");
        ghost.gameObject.SetActive(true);
        MoveGhost(e);
    }

    public void OnSlotDrag(PointerEventData e)
    {
        if (dragFrom != null) MoveGhost(e);
    }

    public void OnSlotEndDrag(StorageSlot slot, PointerEventData e)
    {
        if (ghost != null) ghost.gameObject.SetActive(false);
        if (dragFrom == null) return;

        StorageSlot target = FindSlotAt(e.position);
        if (target != null && target != dragFrom) MoveBetween(dragFrom, target);

        dragFrom = null;
        Refresh();
    }

    void MoveBetween(StorageSlot from, StorageSlot to)
    {
        ItemContainer fromC = from.playerSide ? playerInv : boxInv;
        ItemContainer toC = to.playerSide ? playerInv : boxInv;
        if (fromC == null || toC == null) return;

        fromC.TryMoveSlotToSlot(from.index, toC, to.index);
    }

    StorageSlot FindSlotAt(Vector2 screenPos)
    {
        if (EventSystem.current == null) return null;

        PointerEventData ped = new PointerEventData(EventSystem.current) { position = screenPos };
        List<RaycastResult> results = new List<RaycastResult>();
        EventSystem.current.RaycastAll(ped, results);

        for (int i = 0; i < results.Count; i++)
        {
            StorageSlot s = results[i].gameObject.GetComponent<StorageSlot>();
            if (s != null) return s;
        }
        return null;
    }

    // ---------------------------------------------------------------- data

    void Refresh()
    {
        Fill(playerSlots, playerInv);
        Fill(boxSlots, boxInv);
    }

    static void Fill(StorageSlot[] views, ItemContainer container)
    {
        if (views == null) return;

        for (int i = 0; i < views.Length; i++)
        {
            ItemStack stack = (container != null && i < container.SlotCount)
                ? container.GetSlot(i)
                : ItemStack.Empty;

            ItemDef def = stack.IsEmpty ? null : ItemDB.Get(stack.id);
            bool empty = def == null;

            views[i].icon.enabled = !empty;
            views[i].icon.color = empty ? Color.clear : def.color;
            views[i].amount.text = empty ? string.Empty : stack.amount.ToString("N0");
            views[i].label.text = empty ? string.Empty : def.displayName;
        }
    }

    // ---------------------------------------------------------------- helpers

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

    Image NewStretchImage(string name, Transform parent, Color color)
    {
        GameObject go = new GameObject(name, typeof(RectTransform));
        RectTransform rt = go.GetComponent<RectTransform>();
        rt.SetParent(parent, false);
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = rt.offsetMax = Vector2.zero;
        Image img = go.AddComponent<Image>();
        img.sprite = white;
        img.color = color;
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

    static Sprite MakeWhiteSprite()
    {
        Texture2D tex = new Texture2D(8, 8, TextureFormat.RGBA32, false);
        Color32[] cols = new Color32[64];
        for (int i = 0; i < cols.Length; i++) cols[i] = new Color32(255, 255, 255, 255);
        tex.SetPixels32(cols);
        tex.Apply();
        return Sprite.Create(tex, new Rect(0, 0, 8, 8), new Vector2(0.5f, 0.5f), 100f);
    }
}