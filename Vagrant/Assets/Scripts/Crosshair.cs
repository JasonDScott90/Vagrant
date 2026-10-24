using UnityEngine;
using UnityEngine.UI;

/// A simple screen-center crosshair drawn with code-built uGUI (like the action bar).
/// Hides itself while a menu (inventory, storage, build wheel) is open.
/// Add this to the Player object.
public class Crosshair : MonoBehaviour
{
    [Header("Layout")]
    public float gap = 7f;        // distance from center to the start of each tick
    public float length = 11f;    // tick length
    public float thickness = 2.5f;

    Canvas canvas;
    BuildingSystem building;

    void Awake()
    {
        building = GetComponent<BuildingSystem>();
        BuildUI();
    }

    void Update()
    {
        if (canvas == null) return;

        bool hide = StorageUI.IsOpen || InventoryUI.IsOpen
            || (building != null && building.WheelOpen);

        if (canvas.gameObject.activeSelf == hide)
            canvas.gameObject.SetActive(!hide);
    }

    void BuildUI()
    {
        GameObject canvasGO = new GameObject("CrosshairUI");
        canvasGO.transform.SetParent(transform, false);

        canvas = canvasGO.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 50;

        CanvasScaler scaler = canvasGO.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = 0.5f;

        // center dot + four ticks, all with a dark outline so they read on any background
        NewRect("Dot", canvasGO.transform, Vector2.zero, new Vector2(thickness + 1f, thickness + 1f), Color.white);

        Vector2[] dirs = { Vector2.up, Vector2.down, Vector2.left, Vector2.right };
        for (int i = 0; i < dirs.Length; i++)
        {
            Vector2 pos = dirs[i] * (gap + length * 0.5f);
            Vector2 size = (i < 2)
                ? new Vector2(thickness, length)      // vertical ticks
                : new Vector2(length, thickness);     // horizontal ticks
            NewRect("Tick" + i, canvasGO.transform, pos, size, Color.white);
        }
    }

    static RectTransform NewRect(string name, Transform parent, Vector2 pos, Vector2 size, Color color)
    {
        GameObject go = new GameObject(name, typeof(RectTransform));
        RectTransform rt = go.GetComponent<RectTransform>();
        rt.SetParent(parent, false);
        rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = pos;
        rt.sizeDelta = size;

        Image img = go.AddComponent<Image>();
        img.color = color;
        img.raycastTarget = false;

        Outline outline = go.AddComponent<Outline>();
        outline.effectColor = new Color(0f, 0f, 0f, 0.85f);
        outline.effectDistance = new Vector2(1f, -1f);

        return rt;
    }
}
