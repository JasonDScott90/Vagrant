using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;

/// Tab-screen for the player's own inventory: a single grid of slots you can drag
/// to sort (merge, fill or swap stacks). Built entirely in code, like the storage screen.
/// Opened with Tab (see InputManager), closed with Tab or Esc.
public class InventoryUI : MonoBehaviour
{
    public static InventoryUI Instance { get; private set; }
    public static bool IsOpen { get; private set; }

    public float slotSize = 86f;
    public float slotGap = 8f;

    ItemContainer playerInv;

    Text titleText;
    RectTransform grid;
    InventorySlot[] slots;

    RectTransform ghost;
    Image ghostIcon;
    Text ghostAmount;
    InventorySlot dragFrom;

    Font font;
    Sprite white;
    RectTransform canvasRt;

    const float PanelW = 700f;
    const float PanelH = 640f;
    const int Columns = 6;

    static readonly Color SlotNormal = new Color(0.16f, 0.15f, 0.13f, 0.95f);
    static readonly Color SlotHover = new Color(0.28f, 0.27f, 0.23f, 1f);

    // ---------------------------------------------------------------- public API

    /// Tab handler: opens the screen, or closes it if it is already showing.
    public static void Toggle(ItemContainer player)
    {
        if (IsOpen) Close();
        else if (!StorageUI.IsOpen) Open(player);   // don't stack on top of a storage box
    }

    public static void Open(ItemContainer player)
    {
        if (player == null) return;

        if (Instance == null)
        {
            GameObject go = new GameObject("InventoryUI");
            Instance = go.AddComponent<InventoryUI>();   // builds its UI in Awake
            go.SetActive(false);
        }

        Instance.playerInv = player;
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

        GameObject canvasGO = new GameObject("InventoryCanvas");
        canvasGO.transform.SetParent(transform, false);

        Canvas canvas = canvasGO.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 55;

        canvasGO.AddComponent<GraphicRaycaster>();

        CanvasScaler scaler = canvasGO.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = 0.5f;

        canvasRt = canvasGO.GetComponent<RectTransform>();

        // dim background so the world goes dark while sorting items (also swallows stray clicks)
        NewStretchImage("Dim", canvasGO.transform, new Color(0f, 0f, 0f, 0.5f));

        RectTransform panel = NewRect("Panel", canvasGO.transform, Vector2.zero, new Vector2(PanelW, PanelH));
        Image panelImg = panel.gameObject.AddComponent<Image>();
        panelImg.sprite = white;
        panelImg.color = new Color(0.09f, 0.085f, 0.075f, 0.97f);

        titleText = NewText("Title", panel, new Vector2(0f, 268f), new Vector2(640f, 40f), 28);
        titleText.text = "INVENTORY";

        grid = NewRect("Grid", panel, new Vector2(0f, -18f), Vector2.zero);

        Text hint = NewText("Hint", panel, new Vector2(0f, -282f), new Vector2(640f, 30f), 20);
        hint.text = "[TAB] / [ESC] Close      -      Drag stacks to sort your items";
        hint.color = new Color(1f, 1f, 1f, 0.7f);

        CreateGhost();
    }

    void Update()
    {
        Keyboard kb = Keyboard.current;
        if (kb != null && (kb.tabKey.wasPressedThisFrame || kb.escapeKey.wasPressedThisFrame))
            Close();
    }

    void OnDisable()
    {
        dragFrom = null;
        if (ghost != null) ghost.gameObject.SetActive(false);
    }

    void RebuildGrids()
    {
        int count = playerInv != null ? playerInv.SlotCount : 24;
        if (slots == null || slots.Length != count)
            slots = BuildGrid(grid, count);
    }

    InventorySlot[] BuildGrid(RectTransform gridRt, int count)
    {
        for (int i = gridRt.childCount - 1; i >= 0; i--) Destroy(gridRt.GetChild(i).gameObject);

        InventorySlot[] result = new InventorySlot[count];
        float totalW = Columns * slotSize + (Columns - 1) * slotGap;
        int rows = Mathf.CeilToInt(count / (float)Columns);

        for (int i = 0; i < count; i++)
        {
            int row = i / Columns;
            int col = i % Columns;
            Vector2 p = new Vector2(
                -totalW * 0.5f + slotSize * 0.5f + col * (slotSize + slotGap),
                (rows - 1) * 0.5f * (slotSize + slotGap) - row * (slotSize + slotGap));
            result[i] = BuildSlot(gridRt, "Slot" + i, p, i);
        }
        return result;
    }

    InventorySlot BuildSlot(RectTransform gridRt, string name, Vector2 pos, int index)
    {
        RectTransform rt = NewRect(name, gridRt, pos, new Vector2(slotSize, slotSize));

        InventorySlot slot = rt.gameObject.AddComponent<InventorySlot>();
        slot.ui = this;
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

    public void OnSlotHover(InventorySlot slot, bool enter)
    {
        if (slot.bg != null) slot.bg.color = enter ? SlotHover : SlotNormal;
    }

    public void OnSlotBeginDrag(InventorySlot slot, PointerEventData e)
    {
        if (e.button != PointerEventData.InputButton.Left) return;

        ItemStack stack = (playerInv != null && slot.index < playerInv.SlotCount)
            ? playerInv.GetSlot(slot.index)
            : ItemStack.Empty;
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

    public void OnSlotEndDrag(InventorySlot slot, PointerEventData e)
    {
        if (ghost != null) ghost.gameObject.SetActive(false);
        if (dragFrom == null) return;

        InventorySlot target = FindSlotAt(e.position);
        if (target != null && target != dragFrom && playerInv != null)
            playerInv.TryMoveSlotToSlot(dragFrom.index, playerInv, target.index);

        dragFrom = null;
        Refresh();
    }

    InventorySlot FindSlotAt(Vector2 screenPos)
    {
        if (EventSystem.current == null) return null;

        PointerEventData ped = new PointerEventData(EventSystem.current) { position = screenPos };
        List<RaycastResult> results = new List<RaycastResult>();
        EventSystem.current.RaycastAll(ped, results);

        for (int i = 0; i < results.Count; i++)
        {
            InventorySlot s = results[i].gameObject.GetComponent<InventorySlot>();
            if (s != null) return s;
        }
        return null;
    }

    // ---------------------------------------------------------------- data

    void Refresh()
    {
        if (slots == null) return;

        for (int i = 0; i < slots.Length; i++)
        {
            ItemStack stack = (playerInv != null && i < playerInv.SlotCount)
                ? playerInv.GetSlot(i)
                : ItemStack.Empty;

            ItemDef def = stack.IsEmpty ? null : ItemDB.Get(stack.id);
            bool empty = def == null;

            slots[i].icon.enabled = !empty;
            slots[i].icon.color = empty ? Color.clear : def.color;
            slots[i].amount.text = empty ? string.Empty : stack.amount.ToString("N0");
            slots[i].label.text = empty ? string.Empty : def.displayName;
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

    void NewStretchImage(string name, Transform parent, Color color)
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

/// One slot in the inventory screen. Created at runtime by InventoryUI.
public class InventorySlot : MonoBehaviour,
    IPointerEnterHandler, IPointerExitHandler,
    IBeginDragHandler, IDragHandler, IEndDragHandler
{
    public InventoryUI ui;
    public int index;
    public Image bg;
    public Image icon;
    public Text amount;
    public Text label;

    public void OnPointerEnter(PointerEventData e) { if (ui != null) ui.OnSlotHover(this, true); }
    public void OnPointerExit(PointerEventData e) { if (ui != null) ui.OnSlotHover(this, false); }
    public void OnBeginDrag(PointerEventData e) { if (ui != null) ui.OnSlotBeginDrag(this, e); }
    public void OnDrag(PointerEventData e) { if (ui != null) ui.OnSlotDrag(e); }
    public void OnEndDrag(PointerEventData e) { if (ui != null) ui.OnSlotEndDrag(this, e); }
}
