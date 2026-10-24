using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

public enum HotbarItem { None, Hammer, Bow, Gun }

/// An 8-slot action bar along the bottom of the screen.
///  - Keys 1-8 select a slot. Pressing the key of the selected slot puts the item away.
///  - The mouse wheel also cycles through the slots.
///  - Slot contents are set in the Inspector (Slots list). Slot 1 = Hammer, slot 2 = Bow by default.
/// Add this to the Player object. It takes over the 1 / 2 keys, so the old Hammer and Bow input actions are no longer used.
public class ActionBar : MonoBehaviour
{
    [Header("Slots (key 1 = first entry)")]
    public HotbarItem[] slots =
    {
        HotbarItem.Hammer, HotbarItem.Bow, HotbarItem.Gun,
        HotbarItem.None, HotbarItem.None, HotbarItem.None, HotbarItem.None, HotbarItem.None
    };

    [Header("Behaviour")]
    public bool scrollToSelect = true;

    [Header("Layout")]
    public float slotSize = 80f;
    public float gap = 8f;
    public float bottomMargin = 20f;

    BuildingSystem building;
    Bow bow;
    Gun gun;
    int selected = -1;

    Image[] backgrounds;
    Image[] borders;
    Font font;

    static readonly Color normalColor = new Color(0f, 0f, 0f, 0.55f);
    static readonly Color selectedColor = new Color(0.20f, 0.38f, 0.55f, 0.9f);
    static readonly Color borderOff = new Color(1f, 1f, 1f, 0f);
    static readonly Color borderOn = new Color(1f, 1f, 1f, 0.95f);

    public int SelectedSlot { get { return selected; } }

    void Awake()
    {
        building = GetComponent<BuildingSystem>();
        bow = GetComponent<Bow>();
        gun = GetComponent<Gun>();
        font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        BuildUI();
    }

    void Start()
    {
        // Wait until Start so BuildingSystem and Bow have finished their own setup.
        ApplySelection();
    }

    void Update()
    {
        if (InventoryUI.IsOpen) return; // don't switch items while a menu is open
        if (building != null && building.WheelOpen) return; // don't switch items while the build wheel is open

        Keyboard kb = Keyboard.current;
        if (kb != null)
        {
            int count = Mathf.Min(slots.Length, 8);
            for (int i = 0; i < count; i++)
            {
                Key key = (Key)((int)Key.Digit1 + i);
                if (kb[key].wasPressedThisFrame) PressSlot(i);
            }
        }

        if (scrollToSelect && Mouse.current != null)
        {
            float scroll = Mouse.current.scroll.ReadValue().y;
            if (Mathf.Abs(scroll) > 0.1f) CycleSlot(scroll > 0f ? -1 : 1);
        }
    }

    // ---------------------------------------------------------------- selection

    /// Number key: select the slot, or put the item away if it's already selected.
    void PressSlot(int index)
    {
        selected = (selected == index) ? -1 : index;
        ApplySelection();
    }

    void CycleSlot(int dir)
    {
        int n = slots.Length;
        if (n == 0) return;
        selected = selected < 0 ? (dir > 0 ? 0 : n - 1) : (selected + dir + n) % n;
        ApplySelection();
    }

    void ApplySelection()
    {
        HotbarItem item = (selected >= 0 && selected < slots.Length) ? slots[selected] : HotbarItem.None;

        if (building != null) building.SetHammer(item == HotbarItem.Hammer);
        if (bow != null) bow.SetEquipped(item == HotbarItem.Bow);
        if (gun != null) gun.SetEquipped(item == HotbarItem.Gun);

        RefreshVisuals();
    }

    void RefreshVisuals()
    {
        for (int i = 0; i < backgrounds.Length; i++)
        {
            bool on = i == selected;
            backgrounds[i].color = on ? selectedColor : normalColor;
            borders[i].color = on ? borderOn : borderOff;
        }
    }

    // ---------------------------------------------------------------- UI (built in code)

    void BuildUI()
    {
        GameObject canvasGO = new GameObject("ActionBarUI");
        Canvas canvas = canvasGO.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 40;
        CanvasScaler scaler = canvasGO.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = 0.5f;

        int n = slots.Length;
        backgrounds = new Image[n];
        borders = new Image[n];

        float totalWidth = n * slotSize + (n - 1) * gap;
        float startX = -totalWidth * 0.5f + slotSize * 0.5f;

        for (int i = 0; i < n; i++)
        {
            // slot container, anchored to the bottom center
            RectTransform slot = NewRect("Slot" + (i + 1), canvasGO.transform, new Vector2(slotSize, slotSize));
            slot.anchorMin = slot.anchorMax = new Vector2(0.5f, 0f);
            slot.pivot = new Vector2(0.5f, 0f);
            slot.anchoredPosition = new Vector2(startX + i * (slotSize + gap), bottomMargin);

            Image border = NewImage("Border", slot, new Vector2(slotSize + 6f, slotSize + 6f), borderOff);
            Image bg = NewImage("Background", slot, new Vector2(slotSize, slotSize), normalColor);
            borders[i] = border;
            backgrounds[i] = bg;

            BuildIcon(slots[i], slot);

            Text number = NewText("Number", slot, new Vector2(-slotSize * 0.5f + 14f, slotSize * 0.5f - 14f), new Vector2(24f, 24f), 18);
            number.text = (i + 1).ToString();
            number.color = new Color(1f, 1f, 1f, 0.85f);

            if (slots[i] != HotbarItem.None)
            {
                Text label = NewText("Name", slot, new Vector2(0f, -slotSize * 0.5f + 12f), new Vector2(slotSize, 20f), 14);
                label.text = slots[i].ToString();
            }
        }

        RefreshVisuals();
    }

    /// Simple icons drawn from rectangles. Replace with your own sprites whenever you have them.
    void BuildIcon(HotbarItem item, RectTransform slot)
    {
        if (item == HotbarItem.None) return;

        RectTransform icon = NewRect("Icon", slot, new Vector2(slotSize, slotSize));

        if (item == HotbarItem.Hammer)
        {
            icon.localRotation = Quaternion.Euler(0f, 0f, 35f);
            Rect(icon, new Vector2(0f, -4f), new Vector2(8f, 46f), 0f, new Color(0.55f, 0.38f, 0.2f));   // handle
            Rect(icon, new Vector2(0f, 20f), new Vector2(30f, 15f), 0f, new Color(0.7f, 0.7f, 0.74f));   // head
        }
        else if (item == HotbarItem.Bow)
        {
            Rect(icon, new Vector2(-4f, 17f), new Vector2(5f, 24f), -22f, new Color(0.6f, 0.42f, 0.25f)); // upper limb
            Rect(icon, new Vector2(-8f, 0f), new Vector2(5f, 18f), 0f, new Color(0.6f, 0.42f, 0.25f));   // grip
            Rect(icon, new Vector2(-4f, -17f), new Vector2(5f, 24f), 22f, new Color(0.6f, 0.42f, 0.25f)); // lower limb
            Rect(icon, new Vector2(2f, 0f), new Vector2(2f, 52f), 0f, new Color(0.9f, 0.9f, 0.85f));     // string
            Rect(icon, new Vector2(8f, 0f), new Vector2(36f, 3f), 0f, new Color(0.75f, 0.65f, 0.5f));    // arrow
        }
        else if (item == HotbarItem.Gun)
        {
            Rect(icon, new Vector2(-18f, -2f), new Vector2(12f, 9f), 0f, new Color(0.32f, 0.22f, 0.13f));   // stock
            Rect(icon, new Vector2(-2f, 0f), new Vector2(22f, 11f), 0f, new Color(0.16f, 0.17f, 0.19f));   // body
            Rect(icon, new Vector2(13f, 3f), new Vector2(30f, 5f), 0f, new Color(0.07f, 0.07f, 0.08f));    // barrel
            Rect(icon, new Vector2(-8f, -13f), new Vector2(7f, 16f), 15f, new Color(0.07f, 0.07f, 0.08f)); // grip
            Rect(icon, new Vector2(2f, -12f), new Vector2(7f, 13f), -10f, new Color(0.30f, 0.30f, 0.33f)); // magazine
        }
    }

    static void Rect(RectTransform parent, Vector2 pos, Vector2 size, float rotation, Color color)
    {
        GameObject go = new GameObject("Part", typeof(RectTransform));
        RectTransform rt = go.GetComponent<RectTransform>();
        rt.SetParent(parent, false);
        rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = pos;
        rt.sizeDelta = size;
        rt.localRotation = Quaternion.Euler(0f, 0f, rotation);
        Image img = go.AddComponent<Image>();
        img.color = color;
        img.raycastTarget = false;
    }

    static RectTransform NewRect(string name, Transform parent, Vector2 size)
    {
        GameObject go = new GameObject(name, typeof(RectTransform));
        RectTransform rt = go.GetComponent<RectTransform>();
        rt.SetParent(parent, false);
        rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = Vector2.zero;
        rt.sizeDelta = size;
        return rt;
    }

    static Image NewImage(string name, Transform parent, Vector2 size, Color color)
    {
        RectTransform rt = NewRect(name, parent, size);
        Image img = rt.gameObject.AddComponent<Image>();
        img.color = color;
        img.raycastTarget = false;
        return img;
    }

    Text NewText(string name, Transform parent, Vector2 pos, Vector2 size, int fontSize)
    {
        RectTransform rt = NewRect(name, parent, size);
        rt.anchoredPosition = pos;
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
}
