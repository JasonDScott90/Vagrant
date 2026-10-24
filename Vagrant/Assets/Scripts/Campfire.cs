using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

/// A campfire you can cook meat on: stand close and press E to turn one Raw Meat into
/// Cooked Meat. Put this on the campfire root. It adds its own trigger sphere (cookRadius)
/// if none exists, shows a prompt while the player is near, and flickers the fire light.
public class Campfire : MonoBehaviour
{
    [Header("Cooking")]
    public string inputItemId = "raw_meat";
    public string outputItemId = "cooked_meat";
    public float cookRadius = 3f;

    [Header("Fire")]
    public Light fireLight;                 // auto-found on this object if empty
    public float flickerAmount = 0.35f;

    PlayerInventory cook;                   // player currently standing near the fire
    Text prompt;
    float baseIntensity;

    void Awake()
    {
        if (fireLight == null) fireLight = GetComponentInChildren<Light>();
        if (fireLight != null) baseIntensity = fireLight.intensity;

        SphereCollider trigger = GetComponent<SphereCollider>();
        if (trigger == null) trigger = gameObject.AddComponent<SphereCollider>();
        trigger.isTrigger = true;
        trigger.radius = cookRadius;
        trigger.center = new Vector3(0f, 0.5f, 0f);

        BuildPromptUI();
    }

    void Update()
    {
        // gentle fire flicker
        if (fireLight != null)
            fireLight.intensity = baseIntensity + Mathf.Sin(Time.time * 9f) * flickerAmount * 0.5f
                                             + Mathf.Sin(Time.time * 23f) * flickerAmount * 0.5f;

        if (cook == null || InventoryUI.IsOpen || StorageUI.IsOpen)
        {
            SetPrompt(false);
            return;
        }

        int raw = CountOf(cook.Container, inputItemId);
        int cooked = CountOf(cook.Container, outputItemId);

        if (raw > 0)
        {
            SetPrompt(true, "[E] Cook " + DisplayName(inputItemId) + "     " + raw + " raw / " + cooked + " cooked");

            Keyboard kb = Keyboard.current;
            if (kb != null && kb.eKey.wasPressedThisFrame) CookOne();
        }
        else
        {
            SetPrompt(true, "Bring " + DisplayName(inputItemId) + " here to cook it  (" + cooked + " cooked)");
        }
    }

    void CookOne()
    {
        if (cook == null) return;
        if (cook.Container.Remove(inputItemId, 1) <= 0) return;
        cook.Container.Add(outputItemId, 1);
    }

    // ---------------------------------------------------------------- player detection

    void OnTriggerEnter(Collider other)
    {
        PlayerInventory inventory = other.GetComponentInParent<PlayerInventory>();
        if (inventory != null) cook = inventory;
    }

    void OnTriggerExit(Collider other)
    {
        PlayerInventory inventory = other.GetComponentInParent<PlayerInventory>();
        if (inventory != null && inventory == cook) cook = null;
    }

    // ---------------------------------------------------------------- helpers

    static int CountOf(ItemContainer container, string id)
    {
        if (container == null || string.IsNullOrEmpty(id)) return 0;

        int total = 0;
        for (int i = 0; i < container.SlotCount; i++)
        {
            ItemStack stack = container.GetSlot(i);
            if (stack.id == id) total += stack.amount;
        }
        return total;
    }

    static string DisplayName(string id)
    {
        ItemDef def = ItemDB.Get(id);
        return def != null ? def.displayName : id;
    }

    // ---------------------------------------------------------------- prompt UI

    void BuildPromptUI()
    {
        GameObject canvasGO = new GameObject("CampfirePrompt");
        canvasGO.transform.SetParent(transform, false);

        Canvas canvas = canvasGO.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 40;

        CanvasScaler scaler = canvasGO.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = 0.5f;

        Font font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");

        GameObject textGO = new GameObject("Prompt", typeof(RectTransform));
        RectTransform rt = textGO.GetComponent<RectTransform>();
        rt.SetParent(canvasGO.transform, false);
        rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0f);
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = new Vector2(0f, 170f);   // above the storage prompt
        rt.sizeDelta = new Vector2(1400f, 40f);

        prompt = textGO.AddComponent<Text>();
        prompt.font = font;
        prompt.fontSize = 22;
        prompt.alignment = TextAnchor.MiddleCenter;
        prompt.color = new Color(1f, 0.92f, 0.75f);
        prompt.raycastTarget = false;
        prompt.text = string.Empty;

        Outline outline = textGO.AddComponent<Outline>();
        outline.effectColor = new Color(0f, 0f, 0f, 0.8f);

        SetPrompt(false);
    }

    void SetPrompt(bool visible, string text = "")
    {
        if (prompt == null) return;
        if (visible && !prompt.gameObject.activeSelf) prompt.gameObject.SetActive(true);
        else if (!visible && prompt.gameObject.activeSelf) prompt.gameObject.SetActive(false);
        if (visible && prompt.text != text) prompt.text = text;
    }
}