using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

/// Rust-style box interaction: look at a storage box and press E to open it.
/// Add this to the Player (next to InputManager). Reads the keyboard directly so the
/// shared input actions asset needs no new binding.
public class StorageInteractor : MonoBehaviour
{
    [Header("References (auto-found if empty)")]
    public Camera cam;

    [Header("Interaction")]
    public float range = 3.5f;

    PlayerInventory inventory;
    StorageBox current;

    // prompt UI (built in code)
    Text prompt;

    void Awake()
    {
        if (cam == null) cam = GetComponentInChildren<Camera>();
        inventory = GetComponent<PlayerInventory>();

        if (inventory == null)
            Debug.LogError("StorageInteractor: no PlayerInventory component found on this object. Add it to the Player.");

        BuildPromptUI();
    }

    void Update()
    {
        if (StorageUI.IsOpen)
        {
            SetPrompt(false);
            if (ClosePressed() || !StillInRange()) CloseBox();
            return;
        }

        StorageBox box = FindBox();
        if (box == null)
        {
            current = null;
            SetPrompt(false);
            return;
        }

        current = box;
        SetPrompt(true, "[E] Open " + box.boxName);
        if (InteractPressed()) OpenBox(box);
    }

    // ---------------------------------------------------------------- boxes

    StorageBox FindBox()
    {
        if (cam == null) return null;

        Ray ray = new Ray(cam.transform.position, cam.transform.forward);
        if (!Physics.Raycast(ray, out RaycastHit hit, range, ~(1 << 2), QueryTriggerInteraction.Ignore))
            return null;

        return hit.collider.GetComponentInParent<StorageBox>();
    }

    bool StillInRange()
    {
        return current != null && cam != null
            && Vector3.Distance(cam.transform.position, current.transform.position) <= range + 1.5f;
    }

    void OpenBox(StorageBox box)
    {
        if (inventory == null) return;
        StorageUI.Open(inventory.Container, box.boxName, box.Container);
    }

    void CloseBox()
    {
        StorageUI.Close();
        current = null;
    }

    // ---------------------------------------------------------------- input

    static bool InteractPressed()
    {
        Keyboard kb = Keyboard.current;
        return kb != null && kb.eKey.wasPressedThisFrame;
    }

    static bool ClosePressed()
    {
        Keyboard kb = Keyboard.current;
        return kb != null && (kb.eKey.wasPressedThisFrame || kb.escapeKey.wasPressedThisFrame);
    }

    // ---------------------------------------------------------------- prompt UI

    void BuildPromptUI()
    {
        GameObject canvasGO = new GameObject("StoragePrompt");
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
        rt.anchoredPosition = new Vector2(0f, 120f);
        rt.sizeDelta = new Vector2(1400f, 40f);

        prompt = textGO.AddComponent<Text>();
        prompt.font = font;
        prompt.fontSize = 24;
        prompt.alignment = TextAnchor.MiddleCenter;
        prompt.color = Color.white;
        prompt.raycastTarget = false;
        prompt.text = string.Empty;

        Outline outline = textGO.AddComponent<Outline>();
        outline.effectColor = new Color(0f, 0f, 0f, 0.8f);
    }

    void SetPrompt(bool visible, string text = "")
    {
        if (prompt == null) return;
        if (visible && !prompt.gameObject.activeSelf) prompt.gameObject.SetActive(true);
        else if (!visible && prompt.gameObject.activeSelf) prompt.gameObject.SetActive(false);
        if (visible && prompt.text != text) prompt.text = text;
    }
}
