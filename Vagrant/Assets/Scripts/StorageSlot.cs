using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// One interactive slot in the storage screen. Handles hover highlighting, click-to-quick-move
/// and drag & drop, forwarding all real work to the StorageUI that owns it.
public class StorageSlot : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler,
    IPointerClickHandler, IBeginDragHandler, IDragHandler, IEndDragHandler
{
    [HideInInspector] public StorageUI ui;
    [HideInInspector] public bool playerSide;
    [HideInInspector] public int index;

    public Image bg;
    public Image icon;
    public Text amount;
    public Text label;

    public void OnPointerEnter(PointerEventData eventData) { if (ui != null) ui.OnSlotHover(this, true); }
    public void OnPointerExit(PointerEventData eventData) { if (ui != null) ui.OnSlotHover(this, false); }

    public void OnPointerClick(PointerEventData eventData)
    {
        if (ui != null && eventData.button == PointerEventData.InputButton.Left)
            ui.OnSlotClicked(this);
    }

    public void OnBeginDrag(PointerEventData eventData) { if (ui != null) ui.OnSlotBeginDrag(this, eventData); }
    public void OnDrag(PointerEventData eventData) { if (ui != null) ui.OnSlotDrag(eventData); }
    public void OnEndDrag(PointerEventData eventData) { if (ui != null) ui.OnSlotEndDrag(this, eventData); }
}
