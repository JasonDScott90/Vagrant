using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

/// Hunger for the player. Max 100, drains slowly (a full bar lasts about 30 minutes).
///
/// Eating: open the inventory (Tab), click a slot to select it (it turns blue), then press F
/// to eat one item from that slot. Anything with hunger > 0 in ItemDB is edible
/// (Raw Meat +8, Cooked Meat +20). With nothing selected, F eats the first edible item found.
/// Builds its own bar UI in the bottom-left corner. Add this to the Player.
public class HungerSystem : MonoBehaviour
{
    [Header("Hunger")]
    public float maxHunger = 100f;
    public float startHunger = 55f;
    public float drainMinutes = 30f;      // a full bar empties after this many minutes

    public float Current { get; private set; }

    PlayerInventory inventory;

    // UI (built in code)
    const float BarWidth = 320f;
    RectTransform fill;
    Text valueText;
    Text feedbackText;
    float feedbackTimer;
    int shownPercent = -1;

    void Awake()
    {
        inventory = GetComponent<PlayerInventory>();
        Current = Mathf.Clamp(startHunger, 0f, maxHunger);
        BuildUI();
        Refresh();
    }

    void Update()
    {
        // slow drain: maxHunger lost over `drainMinutes` minutes
        float drainPerSecond = maxHunger / Mathf.Max(1f, drainMinutes * 60f);
        if (Current > 0f)
        {
            Current = Mathf.Max(0f, Current - drainPerSecond * Time.deltaTime);
            Refresh();
        }

        Keyboard kb = Keyboard.current;
        if (kb != null && kb.fKey.wasPressedThisFrame) Eat();

        if (feedbackTimer > 0f)
        {
            feedbackTimer -= Time.deltaTime;
            if (feedbackTimer <= 0f && feedbackText != null) feedbackText.text = string.Empty;
        }
    }

    // ---------------------------------------------------------------- eating

    /// Eat one item: from the inventory slot selected with a click (Tab screen), or - if
    /// nothing is selected - the first edible item in the inventory.
    public bool Eat()
    {
        if (inventory == null) return false;

        int slot = InventoryUI.SelectedSlotIndex;
        if (slot >= 0 && slot < inventory.Container.SlotCount)
            return EatFromSlot(slot);

        // nothing selected: fall back to the first edible item
        for (int i = 0; i < inventory.Container.SlotCount; i++)
        {
            ItemStack stack = inventory.Container.GetSlot(i);
            ItemDef def = stack.IsEmpty ? null : ItemDB.Get(stack.id);
            if (def != null && def.hunger > 0) return EatFromSlot(i);
        }

        ShowFeedback("Select an item in your inventory and press [F] to eat");
        return false;
    }

    /// Eats one item from a specific inventory slot and restores its hunger value.
    public bool EatFromSlot(int index)
    {
        if (inventory == null) return false;

        ItemStack stack = inventory.Container.GetSlot(index);
        ItemDef def = stack.IsEmpty ? null : ItemDB.Get(stack.id);

        if (def == null || def.hunger <= 0)
        {
            if (stack.IsEmpty) ShowFeedback("That slot is empty");
            else ShowFeedback("You can't eat " + (def != null ? def.displayName : stack.id));
            return false;
        }

        if (Current >= maxHunger - 0.01f)
        {
            ShowFeedback("You are not hungry");
            return false;
        }

        // take one from that exact slot
        stack.amount -= 1;
        inventory.Container.SetSlot(index, stack.amount > 0 ? stack : ItemStack.Empty);

        Current = Mathf.Min(maxHunger, Current + def.hunger);
        Refresh();
        ShowFeedback("Ate " + def.displayName + "  (+" + def.hunger + " hunger)");
        return true;
    }

    /// Direct hunger change (positive or negative).
    public void AddHunger(float amount)
    {
        Current = Mathf.Clamp(Current + amount, 0f, maxHunger);
        Refresh();
    }

    void ShowFeedback(string message)
    {
        if (feedbackText != null) feedbackText.text = message;
        feedbackTimer = 2.5f;
    }

    // ---------------------------------------------------------------- UI (built in code)

    void BuildUI()
    {
        GameObject canvasGO = new GameObject("HungerUI");
        canvasGO.transform.SetParent(transform, false);

        Canvas canvas = canvasGO.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 40;

        CanvasScaler scaler = canvasGO.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = 0.5f;

        Font font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");

        // title in the bottom-left corner
        Text title = NewText("Title", canvasGO.transform, font, new Vector2(30f, 66f), new Vector2(220f, 24f), 18);
        title.alignment = TextAnchor.MiddleLeft;
        title.text = "HUNGER  ([F] to eat selected item)";
        title.color = new Color(1f, 1f, 1f, 0.8f);

        // bar background (anchored bottom-left)
        GameObject barGO = new GameObject("Bar", typeof(RectTransform));
        RectTransform bar = barGO.GetComponent<RectTransform>();
        bar.SetParent(canvasGO.transform, false);
        bar.anchorMin = bar.anchorMax = bar.pivot = new Vector2(0f, 0f);
        bar.anchoredPosition = new Vector2(30f, 34f);
        bar.sizeDelta = new Vector2(BarWidth, 22f);
        Image barBg = barGO.AddComponent<Image>();
        barBg.color = new Color(0f, 0f, 0f, 0.55f);

        // fill (width is driven by hunger)
        GameObject fillGO = new GameObject("Fill", typeof(RectTransform));
        fill = fillGO.GetComponent<RectTransform>();
        fill.SetParent(bar, false);
        fill.anchorMin = Vector2.zero;
        fill.anchorMax = new Vector2(0f, 1f);
        fill.offsetMin = new Vector2(2f, 2f);
        fill.offsetMax = new Vector2(BarWidth - 2f, -2f);
        Image fillImg = fillGO.AddComponent<Image>();
        fillImg.color = new Color(0.85f, 0.55f, 0.18f);

        // value text over the bar
        valueText = NewText("Value", bar, font, Vector2.zero, new Vector2(BarWidth, 22f), 15);
        valueText.text = "100 / 100";

        // feedback line above the bar
        feedbackText = NewText("Feedback", canvasGO.transform, font, new Vector2(30f, 92f), new Vector2(700f, 24f), 17);
        feedbackText.alignment = TextAnchor.MiddleLeft;
        feedbackText.color = new Color(1f, 1f, 1f, 0.85f);
        feedbackText.text = string.Empty;
    }

    void Refresh()
    {
        float pct = maxHunger > 0f ? Mathf.Clamp01(Current / maxHunger) : 0f;

        int percentInt = Mathf.RoundToInt(pct * 100f);
        if (percentInt == shownPercent) return;
        shownPercent = percentInt;

        if (fill != null)
            fill.offsetMax = new Vector2(2f + pct * (BarWidth - 4f), -2f);
        if (valueText != null)
            valueText.text = Mathf.CeilToInt(Current) + " / " + Mathf.CeilToInt(maxHunger);
    }

    Text NewText(string name, Transform parent, Font font, Vector2 anchored, Vector2 size, int fontSize)
    {
        GameObject go = new GameObject(name, typeof(RectTransform));
        RectTransform rt = go.GetComponent<RectTransform>();
        rt.SetParent(parent, false);
        rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0f, 0f);
        rt.anchoredPosition = anchored;
        rt.sizeDelta = size;

        Text t = go.AddComponent<Text>();
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