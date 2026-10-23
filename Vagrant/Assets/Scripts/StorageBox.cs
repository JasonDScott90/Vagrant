using UnityEngine;

/// Rust-style storage box. The visual is authored as child meshes under this object;
/// this component owns the item storage and the name shown on the storage screen.
/// Small box = 12 slots (6x2), Large box = 30 slots (6x5) - same as Rust.
public class StorageBox : MonoBehaviour
{
    [Header("Box")]
    public string boxName = "Small Wood Box";
    public int slotCount = 12;
    public int gridColumns = 6;

    [Header("Demo contents (fill the box when the game starts)")]
    public bool prefillDemoItems = true;

    public ItemContainer Container { get; private set; }

    void Awake()
    {
        Container = new ItemContainer(slotCount);
        if (prefillDemoItems) FillDemoItems();
    }

    void FillDemoItems()
    {
        if (slotCount >= 24)
        {
            Container.Add("wood", 1200);
            Container.Add("stone", 800);
            Container.Add("metal_fragments", 400);
            Container.Add("cloth", 150);
            Container.Add("low_grade_fuel", 60);
            Container.Add("sulfur", 200);
            Container.Add("charcoal", 300);
            Container.Add("scrap", 75);
        }
        else
        {
            Container.Add("wood", 500);
            Container.Add("stone", 250);
            Container.Add("scrap", 30);
        }
    }
}
