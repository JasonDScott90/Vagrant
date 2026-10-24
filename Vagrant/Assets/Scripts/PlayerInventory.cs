using UnityEngine;

/// The player's own inventory (6x4 = 24 slots). Shown on the left side of the storage screen.
public class PlayerInventory : MonoBehaviour
{
    public int slotCount = 24;
    public bool prefillDemoItems = true;

    public ItemContainer Container { get; private set; }

    void Awake()
    {
        Container = new ItemContainer(slotCount);

        if (prefillDemoItems)
        {
            Container.Add("wood", 1000);
            Container.Add("stone", 500);
            Container.Add("metal_fragments", 250);
            Container.Add("scrap", 40);
            Container.Add("cloth", 100);
            Container.Add("low_grade_fuel", 50);
        }

        // ammo for the gun: every game starts with 100 bullets
        Container.Add("bullet", 100);
    }
}
