using System.Collections;
using UnityEngine;
using UnityEngine.UI;

/// Health for the player: red bar above the hunger bar, damage flash, and respawn.
/// Put this on the Player - it wires itself to the Health component.
[RequireComponent(typeof(Health))]
public class PlayerHealthUI : MonoBehaviour
{
    [Header("Respawn")]
    public float respawnDelay = 3f;

    Health health;
    Vector3 spawnPos;
    Quaternion spawnRot;

    // UI (built in code)
    const float BarWidth = 320f;
    RectTransform fill;
    Text valueText;
    Text feedbackText;
    Image flash;
    float flashTimer;
    float feedbackTimer;
    int shownPercent = -1;

    void Awake()
    {
        health = GetComponent<Health>();
        spawnPos = transform.position;
        spawnRot = transform.rotation;
        BuildUI();
        Refresh();
    }

    void OnEnable()
    {
        if (health != null)
        {
            health.Damaged += OnDamaged;
            health.Died += OnDied;
        }
    }

    void OnDisable()
    {
        if (health != null)
        {
            health.Damaged -= OnDamaged;
            health.Died -= OnDied;
        }
    }

    void Update()
    {
        if (flashTimer > 0f)
        {
            flashTimer -= Time.deltaTime * 2f;
            if (flash != null)
                flash.color = new Color(1f, 0.05f, 0.05f, Mathf.Clamp01(flashTimer) * 0.35f);
        }

        if (feedbackTimer > 0f)
        {
            feedbackTimer -= Time.deltaTime;
            if (feedbackTimer <= 0f && feedbackText != null) feedbackText.text = string.Empty;
        }
    }

    // ---------------------------------------------------------------- damage

    void OnDamaged(float amount, Vector3 fromPosition)
    {
        flashTimer = 1f;
        ShowFeedback("-" + Mathf.CeilToInt(amount) + " health");
        Refresh();
    }

    void OnDied()
    {
        flashTimer = 1f;
        ShowFeedback("You died - respawning...");
        StartCoroutine(RespawnRoutine());
    }

    IEnumerator RespawnRoutine()
    {
        yield return new WaitForSeconds(respawnDelay);

        CharacterController cc = GetComponent<CharacterController>();
        if (cc != null) cc.enabled = false;
        transform.SetPositionAndRotation(spawnPos, spawnRot);
        if (cc != null) cc.enabled = true;

        health.SetMax(health.maxHealth); // back to full
        Refresh();
        ShowFeedback("You are back on your feet");
    }

    /// Heal the player directly.
    public void Heal(float amount)
    {
        if (health == null || health.IsDead) return;
        health.Heal(amount);
        Refresh();
        ShowFeedback("+" + Mathf.CeilToInt(amount) + " health");
    }

    void ShowFeedback(string message)
    {
        if (feedbackText != null) feedbackText.text = message;
        feedbackTimer = 2.5f;
    }

    // ---------------------------------------------------------------- UI (built in code)

    void BuildUI()
    {
        GameObject canvasGO = new GameObject("PlayerHealthUI");
        canvasGO.transform.SetParent(transform, false);

        Canvas canvas = canvasGO.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 41;

        CanvasScaler scaler = canvasGO.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = 0.5f;

        Font font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");

        // fullscreen red flash on damage
        GameObject flashGO = new GameObject("Flash", typeof(RectTransform));
        RectTransform flashRect = flashGO.GetComponent<RectTransform>();
        flashRect.SetParent(canvasGO.transform, false);
        flashRect.anchorMin = Vector2.zero;
        flashRect.anchorMax = Vector2.one;
        flashRect.offsetMin = flashRect.offsetMax = Vector2.zero;
        flash = flashGO.AddComponent<Image>();
        flash.color = new Color(1f, 0f, 0f, 0f);
        flash.raycastTarget = false;

        // title above the hunger UI block
        Text title = NewText("Title", canvasGO.transform, font, new Vector2(30f, 154f), new Vector2(220f, 24f), 18);
        title.alignment = TextAnchor.MiddleLeft;
        title.text = "HEALTH";
        title.color = new Color(1f, 1f, 1f, 0.8f);

        // bar background (anchored bottom-left, above the hunger bar)
        GameObject barGO = new GameObject("Bar", typeof(RectTransform));
        RectTransform bar = barGO.GetComponent<RectTransform>();
        bar.SetParent(canvasGO.transform, false);
        bar.anchorMin = bar.anchorMax = bar.pivot = new Vector2(0f, 0f);
        bar.anchoredPosition = new Vector2(30f, 122f);
        bar.sizeDelta = new Vector2(BarWidth, 22f);
        Image barBg = barGO.AddComponent<Image>();
        barBg.color = new Color(0f, 0f, 0f, 0.55f);

        // fill (width is driven by health)
        GameObject fillGO = new GameObject("Fill", typeof(RectTransform));
        fill = fillGO.GetComponent<RectTransform>();
        fill.SetParent(bar, false);
        fill.anchorMin = Vector2.zero;
        fill.anchorMax = new Vector2(0f, 1f);
        fill.offsetMin = new Vector2(2f, 2f);
        fill.offsetMax = new Vector2(BarWidth - 2f, -2f);
        Image fillImg = fillGO.AddComponent<Image>();
        fillImg.color = new Color(0.80f, 0.16f, 0.14f);

        // value text over the bar
        valueText = NewText("Value", bar, font, Vector2.zero, new Vector2(BarWidth, 22f), 15);
        valueText.text = "100 / 100";

        // feedback line under the bar
        feedbackText = NewText("Feedback", canvasGO.transform, font, new Vector2(30f, 96f), new Vector2(700f, 24f), 17);
        feedbackText.alignment = TextAnchor.MiddleLeft;
        feedbackText.color = new Color(1f, 0.75f, 0.75f, 0.9f);
        feedbackText.text = string.Empty;
    }

    void Refresh()
    {
        if (health == null) return;
        float max = health.maxHealth;
        float pct = max > 0f ? Mathf.Clamp01(health.Current / max) : 0f;

        int percentInt = Mathf.RoundToInt(pct * 100);
        if (percentInt == shownPercent) return;
        shownPercent = percentInt;

        if (fill != null)
            fill.offsetMax = new Vector2(2f + pct * (BarWidth - 4f), -2f);
        if (valueText != null)
            valueText.text = Mathf.CeilToInt(health.Current) + " / " + Mathf.CeilToInt(max);
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
