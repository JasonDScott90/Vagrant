#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

/// One-click setup for the Rust-style storage boxes.
/// Builds the small (12 slots) and large (30 slots) wooden storage boxes in the open scene,
/// wires up the storage scripts on the Player, saves prefabs and the scene.
/// Menu: Tools > Storage > Build Storage Boxes In Scene
public static class StorageBoxSetup
{
    const string SmallRoot = "Small Storage Box";
    const string LargeRoot = "Large Storage Box";

    [MenuItem("Tools/Storage/Build Storage Boxes In Scene")]
    public static void Build()
    {
        GameObject player = GameObject.Find("Player");
        if (player == null)
        {
            Debug.LogError("StorageBoxSetup: no object named 'Player' found in the scene.");
            return;
        }

        Material wood = AssetDatabase.LoadAssetAtPath<Material>("Assets/Materials/StorageBoxWood.mat");
        Material trim = AssetDatabase.LoadAssetAtPath<Material>("Assets/Materials/StorageBoxTrim.mat");
        Material metal = AssetDatabase.LoadAssetAtPath<Material>("Assets/Materials/StorageBoxMetal.mat");
        if (wood == null || trim == null || metal == null)
        {
            Debug.LogError("StorageBoxSetup: missing materials in Assets/Materials.");
            return;
        }

        // Replace any previous setup so the menu item can be run repeatedly.
        foreach (string name in new[] { SmallRoot, LargeRoot })
        {
            GameObject old = GameObject.Find(name);
            if (old != null) Object.DestroyImmediate(old);
        }

        Transform t = player.transform;
        Vector3 fwd = t.forward; fwd.y = 0f; fwd.Normalize();
        Vector3 right = t.right; right.y = 0f; right.Normalize();
        float yaw = t.eulerAngles.y + 180f; // face the player

        GameObject small = BuildBox(SmallRoot, "Small Wood Box", 12, false,
            t.position + fwd * 2.2f + right * 1.0f, yaw, wood, trim, metal);
        GameObject large = BuildBox(LargeRoot, "Large Wood Box", 30, true,
            t.position + fwd * 2.2f - right * 1.3f, yaw, wood, trim, metal);

        // Player-side storage components.
        if (player.GetComponent<PlayerInventory>() == null) Undo.AddComponent<PlayerInventory>(player);
        if (player.GetComponent<StorageInteractor>() == null) Undo.AddComponent<StorageInteractor>(player);

        // Prefabs for reuse (deployables later).
        EnsureFolder("Assets/Prefabs");
        PrefabUtility.SaveAsPrefabAssetAndConnect(small, "Assets/Prefabs/SmallStorageBox.prefab", InteractionMode.AutomatedAction);
        PrefabUtility.SaveAsPrefabAssetAndConnect(large, "Assets/Prefabs/LargeStorageBox.prefab", InteractionMode.AutomatedAction);

        EditorSceneManager.MarkSceneDirty(player.scene);
        EditorSceneManager.SaveOpenScenes();
        Debug.Log("StorageBoxSetup: small and large storage boxes built, prefabs saved, scene saved.");
    }

    static GameObject BuildBox(string rootName, string boxName, int slots, bool large,
        Vector3 pos, float yaw, Material wood, Material trim, Material metal)
    {
        GameObject root = new GameObject(rootName);
        root.transform.SetPositionAndRotation(Grounded(pos), Quaternion.Euler(0f, yaw, 0f));

        if (large)
        {
            Part(root, "Body", new Vector3(0f, 0.275f, 0f), new Vector3(1.15f, 0.55f, 0.62f), wood);
            Part(root, "Lid", new Vector3(0f, 0.585f, 0f), new Vector3(1.22f, 0.07f, 0.68f), trim);
            Part(root, "TrimL", new Vector3(-0.5975f, 0.275f, 0f), new Vector3(0.055f, 0.59f, 0.64f), trim);
            Part(root, "TrimR", new Vector3(0.5975f, 0.275f, 0f), new Vector3(0.055f, 0.59f, 0.64f), trim);
            Part(root, "Latch", new Vector3(0f, 0.5f, 0.335f), new Vector3(0.18f, 0.12f, 0.03f), metal);
        }
        else
        {
            Part(root, "Body", new Vector3(0f, 0.18f, 0f), new Vector3(0.6f, 0.36f, 0.45f), wood);
            Part(root, "Lid", new Vector3(0f, 0.395f, 0f), new Vector3(0.66f, 0.07f, 0.5f), trim);
            Part(root, "TrimL", new Vector3(-0.3225f, 0.18f, 0f), new Vector3(0.045f, 0.4f, 0.47f), trim);
            Part(root, "TrimR", new Vector3(0.3225f, 0.18f, 0f), new Vector3(0.045f, 0.4f, 0.47f), trim);
            Part(root, "Latch", new Vector3(0f, 0.33f, 0.245f), new Vector3(0.12f, 0.1f, 0.03f), metal);
        }

        BoxCollider col = root.AddComponent<BoxCollider>();
        col.center = large ? new Vector3(0f, 0.31f, 0f) : new Vector3(0f, 0.215f, 0f);
        col.size = large ? new Vector3(1.22f, 0.62f, 0.68f) : new Vector3(0.66f, 0.43f, 0.5f);

        StorageBox box = root.AddComponent<StorageBox>();
        box.boxName = boxName;
        box.slotCount = slots;
        box.gridColumns = 6;
        box.prefillDemoItems = true;
        return root;
    }

    static void Part(GameObject root, string name, Vector3 localPos, Vector3 scale, Material mat)
    {
        GameObject p = GameObject.CreatePrimitive(PrimitiveType.Cube);
        p.name = name;
        Object.DestroyImmediate(p.GetComponent<Collider>());
        p.transform.SetParent(root.transform, false);
        p.transform.localPosition = localPos;
        p.transform.localScale = scale;
        p.GetComponent<MeshRenderer>().sharedMaterial = mat;
    }

    /// Snaps the box to the ground (terrain or any collider) so it never floats or sinks.
    static Vector3 Grounded(Vector3 pos)
    {
        Vector3 origin = new Vector3(pos.x, pos.y + 5f, pos.z);
        if (Physics.Raycast(origin, Vector3.down, out RaycastHit hit, 20f, ~0, QueryTriggerInteraction.Ignore))
            return new Vector3(pos.x, hit.point.y, pos.z);

        Terrain terrain = Terrain.activeTerrain;
        if (terrain != null)
            return new Vector3(pos.x, terrain.SampleHeight(pos) + terrain.transform.position.y, pos.z);

        return pos;
    }

    static void EnsureFolder(string path)
    {
        if (AssetDatabase.IsValidFolder(path)) return;
        string parent = System.IO.Path.GetDirectoryName(path).Replace('\\', '/');
        string leaf = System.IO.Path.GetFileName(path);
        if (!AssetDatabase.IsValidFolder(parent)) EnsureFolder(parent);
        AssetDatabase.CreateFolder(parent, leaf);
    }
}
#endif
