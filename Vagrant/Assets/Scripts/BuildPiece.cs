using System.Collections.Generic;
using UnityEngine;

public enum PieceType { Foundation, Floor, Wall, Doorway, Window, Stairs, Door }
public enum SocketKind { FoundationEdge, FloorEdge, WallBase, WallTop, CellCenter, DoorFrame }

/// A connection point in a piece's local space. yaw = the direction the socket "faces" (0 = +Z).
public struct SocketDef
{
    public SocketKind kind;
    public Vector3 pos;
    public float yaw;
    public SocketDef(SocketKind kind, Vector3 pos, float yaw) { this.kind = kind; this.pos = pos; this.yaw = yaw; }
}

public struct WorldSocket
{
    public SocketKind kind;
    public Vector3 pos;
    public float yaw;
}

/// Everything that describes the pieces: sizes, which sockets they offer, which sockets they can snap to.
public static class PieceDefs
{
    public const float Cell = 3f;
    public const float Half = 1.5f;
    public const float WallHeight = 3f;
    public const float Thickness = 0.2f;
    public const float FoundationDepth = 2f;

    /// Number of real building pieces (the PieceType values). The wheel adds the two tools after them.
    public const int PieceCount = 7;

    // Wheel order, clockwise from the top. First PieceCount entries match the PieceType enum.
    public static readonly string[] Names =
    {
        "Foundation", "Floor", "Wall", "Doorway", "Window", "Stairs", "Door", "Upgrade", "Demolish"
    };

    public static readonly string[] TierNames = { "Twig", "Wood", "Stone", "Metal", "Armored" };

    static SocketDef[] Edges(SocketKind kind)
    {
        return new[]
        {
            new SocketDef(kind, new Vector3(0f, 0f, Half), 0f),
            new SocketDef(kind, new Vector3(Half, 0f, 0f), 90f),
            new SocketDef(kind, new Vector3(0f, 0f, -Half), 180f),
            new SocketDef(kind, new Vector3(-Half, 0f, 0f), 270f),
        };
    }

    static SocketDef[] Join(params SocketDef[][] lists)
    {
        var r = new List<SocketDef>();
        foreach (var l in lists) r.AddRange(l);
        return r.ToArray();
    }

    static readonly SocketDef[] none = new SocketDef[0];
    static readonly SocketDef[] centerSocket = { new SocketDef(SocketKind.CellCenter, Vector3.zero, 0f) };

    // What each piece OFFERS to other pieces
    static readonly SocketDef[] foundationProvides = Join(Edges(SocketKind.FoundationEdge), Edges(SocketKind.WallBase), centerSocket);
    static readonly SocketDef[] floorProvides = Join(Edges(SocketKind.FloorEdge), Edges(SocketKind.WallBase), centerSocket);
    static readonly SocketDef[] wallProvides =
    {
        new SocketDef(SocketKind.WallTop, new Vector3(0f, WallHeight, 0f), 0f),
        new SocketDef(SocketKind.WallTop, new Vector3(0f, WallHeight, 0f), 180f),
    };
    static readonly SocketDef[] doorwayProvides =
        Join(wallProvides, new[] { new SocketDef(SocketKind.DoorFrame, Vector3.zero, 0f) });

    // What each piece NEEDS to snap onto (the point on the piece that gets lined up with a socket)
    static readonly SocketDef[] foundationNeeds = { new SocketDef(SocketKind.FoundationEdge, new Vector3(0f, 0f, -Half), 180f) };
    static readonly SocketDef[] floorNeeds =
    {
        new SocketDef(SocketKind.FloorEdge, new Vector3(0f, 0f, -Half), 180f),
        new SocketDef(SocketKind.WallTop, new Vector3(0f, 0f, -Half), 180f),
    };
    static readonly SocketDef[] wallNeeds = { new SocketDef(SocketKind.WallBase, Vector3.zero, 180f) };
    static readonly SocketDef[] stairsNeeds = { new SocketDef(SocketKind.CellCenter, Vector3.zero, 180f) };
    static readonly SocketDef[] doorNeeds = { new SocketDef(SocketKind.DoorFrame, Vector3.zero, 180f) };

    public static SocketDef[] Provides(PieceType t)
    {
        switch (t)
        {
            case PieceType.Foundation: return foundationProvides;
            case PieceType.Floor: return floorProvides;
            case PieceType.Doorway: return doorwayProvides;
            case PieceType.Wall:
            case PieceType.Window: return wallProvides;
            default: return none;
        }
    }

    public static SocketDef[] Needs(PieceType t)
    {
        switch (t)
        {
            case PieceType.Foundation: return foundationNeeds;
            case PieceType.Floor: return floorNeeds;
            case PieceType.Wall:
            case PieceType.Doorway:
            case PieceType.Window: return wallNeeds;
            case PieceType.Door: return doorNeeds;
            default: return stairsNeeds;
        }
    }

    /// Pieces in the same "slot class" can't occupy the same spot (a doorway can't go where a wall already is).
    public static int SlotClass(PieceType t)
    {
        switch (t)
        {
            case PieceType.Foundation: return 0;
            case PieceType.Floor: return 1;
            case PieceType.Stairs: return 3;
            case PieceType.Door: return 4;
            default: return 2; // wall, doorway, window
        }
    }

    /// Extra turn the player can add with R when a piece is snapped (stairs: 90° steps, door: flips the hinge side).
    public static float RotationOffset(PieceType t, int step)
    {
        if (t == PieceType.Stairs) return 90f * step;
        if (t == PieceType.Door) return 180f * (step % 2);
        return 0f;
    }
}

/// Put on every placed piece. Keeps a global list so the ghost can look for sockets to snap to.
public class BuildPiece : MonoBehaviour
{
    public static readonly List<BuildPiece> All = new List<BuildPiece>();

    public PieceType type;
    public int tier;

    Material baseMaterial;
    bool highlighted;

    void OnEnable() { All.Add(this); }
    void OnDisable() { All.Remove(this); }

    public string Label { get { return PieceDefs.Names[(int)type] + " (" + PieceDefs.TierNames[tier] + ")"; } }

    public void Init(PieceType t, int tierIndex, Material mat)
    {
        type = t;
        SetTier(tierIndex, mat);
    }

    public void SetTier(int t, Material mat)
    {
        tier = t;
        baseMaterial = mat;
        if (!highlighted) Apply(mat);
    }

    /// Pass a material to tint the whole piece (used for the aim highlight), or null to restore it.
    public void SetHighlight(Material m)
    {
        if (m == null)
        {
            if (highlighted) { highlighted = false; Apply(baseMaterial); }
        }
        else
        {
            highlighted = true;
            Apply(m);
        }
    }

    void Apply(Material m)
    {
        if (m == null) return;
        foreach (Renderer r in GetComponentsInChildren<Renderer>()) r.sharedMaterial = m;
    }

    public IEnumerable<WorldSocket> GetWorldSockets()
    {
        SocketDef[] defs = PieceDefs.Provides(type);
        for (int i = 0; i < defs.Length; i++)
        {
            yield return new WorldSocket
            {
                kind = defs[i].kind,
                pos = transform.TransformPoint(defs[i].pos),
                yaw = transform.eulerAngles.y + defs[i].yaw
            };
        }
    }

    public static bool IsOccupied(PieceType t, Vector3 pos)
    {
        int cls = PieceDefs.SlotClass(t);
        foreach (BuildPiece p in All)
        {
            if (PieceDefs.SlotClass(p.type) != cls) continue;
            Vector3 d = p.transform.position - pos;
            if (new Vector2(d.x, d.z).magnitude < 0.6f && Mathf.Abs(d.y) < 0.6f) return true;
        }
        return false;
    }

    // ------------------------------------------------------------ demolishing

    /// Removes a piece, then removes anything that was only held up by it (walls on a demolished foundation, etc).
    public static void Demolish(BuildPiece piece)
    {
        Kill(piece);
        PruneUnsupported();
    }

    static void Kill(BuildPiece p)
    {
        p.gameObject.SetActive(false); // removes it from All immediately
        Destroy(p.gameObject);
    }

    static void PruneUnsupported()
    {
        // Foundations rest on the ground. Everything else must be connected to one through its sockets.
        var supported = new HashSet<BuildPiece>();
        foreach (BuildPiece p in All)
            if (p.type == PieceType.Foundation) supported.Add(p);

        bool changed = true;
        while (changed)
        {
            changed = false;
            foreach (BuildPiece p in All)
            {
                if (supported.Contains(p)) continue;
                if (HasSupport(p, supported)) { supported.Add(p); changed = true; }
            }
        }

        var doomed = new List<BuildPiece>();
        foreach (BuildPiece p in All)
            if (!supported.Contains(p)) doomed.Add(p);
        foreach (BuildPiece p in doomed) Kill(p);
    }

    static bool HasSupport(BuildPiece p, HashSet<BuildPiece> supported)
    {
        SocketDef[] needs = PieceDefs.Needs(p.type);
        foreach (BuildPiece other in supported)
        {
            if (other == p) continue;
            foreach (WorldSocket s in other.GetWorldSockets())
            {
                for (int i = 0; i < needs.Length; i++)
                {
                    if (needs[i].kind != s.kind) continue;
                    if (Vector3.Distance(s.pos, p.transform.TransformPoint(needs[i].pos)) < 0.2f) return true;
                }
            }
        }
        return false;
    }
}

/// Swings the door open/closed around its hinge. Interact (E) toggles it.
public class DoorController : MonoBehaviour
{
    public float openAngle = 100f;
    public float speed = 240f;

    Transform hinge;
    bool isOpen;
    float currentAngle;
    float targetAngle;

    void Awake() { hinge = transform.Find("Hinge"); }

    public void Toggle(Vector3 playerPosition)
    {
        if (!isOpen)
        {
            // swing away from whoever is opening it
            float side = Vector3.Dot(playerPosition - transform.position, transform.forward);
            targetAngle = side >= 0f ? openAngle : -openAngle;
            isOpen = true;
        }
        else
        {
            targetAngle = 0f;
            isOpen = false;
        }
    }

    void Update()
    {
        if (hinge == null) return;
        currentAngle = Mathf.MoveTowardsAngle(currentAngle, targetAngle, speed * Time.deltaTime);
        hinge.localRotation = Quaternion.Euler(0f, currentAngle, 0f);
    }
}

/// Builds each piece out of simple boxes. Swap this for your own prefabs/models whenever you have them.
public static class PieceFactory
{
    public static GameObject Build(PieceType type, Material mat, bool colliders)
    {
        var root = new GameObject(type.ToString());

        void Box(Vector3 center, Vector3 size, Transform parent = null)
        {
            GameObject b = GameObject.CreatePrimitive(PrimitiveType.Cube);
            b.transform.SetParent(parent != null ? parent : root.transform, false);
            b.transform.localPosition = center;
            b.transform.localScale = size;
            if (mat != null) b.GetComponent<Renderer>().sharedMaterial = mat;
            if (!colliders) Object.Destroy(b.GetComponent<Collider>());
        }

        const float t = PieceDefs.Thickness;

        switch (type)
        {
            case PieceType.Foundation:
                Box(new Vector3(0f, -PieceDefs.FoundationDepth * 0.5f, 0f), new Vector3(3f, PieceDefs.FoundationDepth, 3f));
                break;

            case PieceType.Floor:
                Box(new Vector3(0f, -t * 0.5f, 0f), new Vector3(3f, t, 3f));
                break;

            case PieceType.Wall:
                Box(new Vector3(0f, 1.5f, 0f), new Vector3(3f, 3f, t));
                break;

            case PieceType.Doorway:
                {
                    const float open = 1.3f, openH = 2.3f;
                    float post = (3f - open) * 0.5f;
                    float px = open * 0.5f + post * 0.5f;
                    Box(new Vector3(-px, 1.5f, 0f), new Vector3(post, 3f, t));
                    Box(new Vector3(px, 1.5f, 0f), new Vector3(post, 3f, t));
                    Box(new Vector3(0f, openH + (3f - openH) * 0.5f, 0f), new Vector3(open, 3f - openH, t));
                    break;
                }

            case PieceType.Window:
                {
                    const float open = 1.4f, bottom = 1f, top = 2f;
                    float post = (3f - open) * 0.5f;
                    float px = open * 0.5f + post * 0.5f;
                    Box(new Vector3(-px, 1.5f, 0f), new Vector3(post, 3f, t));
                    Box(new Vector3(px, 1.5f, 0f), new Vector3(post, 3f, t));
                    Box(new Vector3(0f, bottom * 0.5f, 0f), new Vector3(open, bottom, t));
                    Box(new Vector3(0f, top + (3f - top) * 0.5f, 0f), new Vector3(open, 3f - top, t));
                    break;
                }

            case PieceType.Stairs:
                {
                    const int steps = 12;
                    const float rise = 0.25f;           // stays under the CharacterController Step Offset (0.3)
                    float run = 3f / steps;
                    for (int i = 0; i < steps; i++)
                    {
                        float h = rise * (i + 1);
                        Box(new Vector3(0f, h * 0.5f, -1.5f + run * 0.5f + run * i), new Vector3(3f, h, run));
                    }
                    break;
                }

            case PieceType.Door:
                {
                    // fits the doorway opening (1.3 x 2.3), hinged on its left edge
                    var hinge = new GameObject("Hinge").transform;
                    hinge.SetParent(root.transform, false);
                    hinge.localPosition = new Vector3(-0.65f, 0f, 0f);
                    Box(new Vector3(0.65f, 1.15f, 0f), new Vector3(1.26f, 2.26f, 0.08f), hinge);
                    break;
                }
        }

        return root;
    }
}

