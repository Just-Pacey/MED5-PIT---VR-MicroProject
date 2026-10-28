using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;

// Builds a wall with two holes out of boxes, and checks whether the player has got through.
//
// Place this object on the floor. The wall spreads along its local X (width) and Y (height).
// The player starts on the -Z side (the BACK of the blue arrow) and must end up on the +Z side.
// Make the wall span the whole room, or the player can simply walk around it.
//
// Right-click the component header and choose "Rebuild Wall" to preview it in the editor.
// The coloured outlines in the Scene view show the holes and the goal area.
[DefaultExecutionOrder(10001)]
public class HoleWall : MonoBehaviour
{
    /// <summary>All solid pieces of every wall in the scene. SplitBody pushes the player out of these.</summary>
    public static readonly List<Collider> Colliders = new List<Collider>();

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics() => Colliders.Clear();

    [Header("Wall (metres)")]
    [SerializeField] private float wallWidth = 6f;
    [SerializeField] private float wallHeight = 3f;
    [SerializeField] private float thickness = 0.3f;

    [Header("Legs hole (open down to the floor)")]
    [SerializeField] private float legsCenterX = -0.5f;
    [SerializeField] private float legsWidth = 0.6f;

    [Tooltip("Top of the legs hole. Keep it a bit ABOVE the Waist Height on SplitBody so the legs fit under it but the torso can't.")]
    [SerializeField] private float legsHeight = 1.05f;

    [Header("Torso window")]
    [SerializeField] private float torsoCenterX = 0.5f;
    [SerializeField] private float torsoWidth = 0.8f;

    [Tooltip("Bottom of the torso window. Keep it a bit BELOW the Waist Height on SplitBody so the torso fits and the legs can't.")]
    [SerializeField] private float torsoBottom = 0.85f;

    [SerializeField] private float torsoTop = 2.2f;

    [Header("Look")]
    [Tooltip("Without a material the pieces use the default, which is pink in URP.")]
    [SerializeField] private Material wallMaterial;

    [Header("Goal")]
    [SerializeField] private SplitBody body;

    [Tooltip("How far behind the wall the goal area reaches.")]
    [SerializeField] private float goalDepth = 3f;

    [Tooltip("Both halves must stay in the goal area this long to win.")]
    [SerializeField] private float holdSeconds = 1f;

    public UnityEvent OnCompleted;

    public bool IsCompleted => completed;

    private const string ContainerName = "WallPieces";
    private BoxCollider goal;
    private float insideTimer;
    private bool completed;

    private void Awake()
    {
        if (Application.isPlaying)
            Rebuild();
    }

    private void OnEnable() => RegisterColliders();

    private void OnDisable() => UnregisterColliders();

    [ContextMenu("Rebuild Wall")]
    public void Rebuild()
    {
        UnregisterColliders();

        Transform old = transform.Find(ContainerName);
        if (old != null)
        {
            if (Application.isPlaying)
            {
                old.name = "Old";
                old.gameObject.SetActive(false); // Destroy happens at end of frame, so switch it off now
                Destroy(old.gameObject);
            }
            else
            {
                DestroyImmediate(old.gameObject);
            }
        }

        var container = new GameObject(ContainerName).transform;
        container.SetParent(transform, false);

        float legsX0 = legsCenterX - legsWidth * 0.5f, legsX1 = legsCenterX + legsWidth * 0.5f;
        float torsoX0 = torsoCenterX - torsoWidth * 0.5f, torsoX1 = torsoCenterX + torsoWidth * 0.5f;
        float left = -wallWidth * 0.5f, right = wallWidth * 0.5f;

        if (legsX1 > torsoX0 && torsoX1 > legsX0)
            Debug.LogWarning($"[HoleWall {name}] The two holes overlap horizontally, which this builder doesn't support. Move them apart.", this);

        // Slice the wall into vertical strips at every hole edge, then fill what is left of each strip.
        var xs = new List<float> { left, right };
        foreach (float x in new[] { legsX0, legsX1, torsoX0, torsoX1 })
            xs.Add(Mathf.Clamp(x, left, right));
        xs.Sort();

        for (int i = 0; i < xs.Count - 1; i++)
        {
            float xa = xs[i], xb = xs[i + 1];
            if (xb - xa < 0.001f) continue;

            float mid = (xa + xb) * 0.5f;
            if (mid > legsX0 && mid < legsX1)
            {
                AddPiece(container, xa, xb, legsHeight, wallHeight); // lintel above the legs hole
            }
            else if (mid > torsoX0 && mid < torsoX1)
            {
                AddPiece(container, xa, xb, 0f, torsoBottom);        // sill under the torso window
                AddPiece(container, xa, xb, torsoTop, wallHeight);   // wall above the torso window
            }
            else
            {
                AddPiece(container, xa, xb, 0f, wallHeight);
            }
        }

        // Goal area: everything behind the wall.
        var goalObject = new GameObject("Goal");
        goalObject.transform.SetParent(container, false);
        goal = goalObject.AddComponent<BoxCollider>();
        goal.isTrigger = true;
        goal.center = new Vector3(0f, (wallHeight - 0.5f) * 0.5f, thickness * 0.5f + goalDepth * 0.5f);
        goal.size = new Vector3(wallWidth, wallHeight + 0.5f, goalDepth);

        RegisterColliders();
    }

    private void AddPiece(Transform parent, float xa, float xb, float ya, float yb)
    {
        if (yb - ya < 0.001f) return;

        GameObject piece = GameObject.CreatePrimitive(PrimitiveType.Cube);
        piece.name = "WallPiece";
        piece.transform.SetParent(parent, false);
        piece.transform.localPosition = new Vector3((xa + xb) * 0.5f, (ya + yb) * 0.5f, 0f);
        piece.transform.localScale = new Vector3(xb - xa, yb - ya, thickness);

        if (wallMaterial != null)
            piece.GetComponent<Renderer>().sharedMaterial = wallMaterial;
    }

    private void RegisterColliders()
    {
        Transform container = transform.Find(ContainerName);
        if (container == null) return;

        goal = null;
        foreach (BoxCollider box in container.GetComponentsInChildren<BoxCollider>())
        {
            if (box.isTrigger) goal = box;
            else if (!Colliders.Contains(box)) Colliders.Add(box);
        }
    }

    private void UnregisterColliders()
    {
        Transform container = transform.Find(ContainerName);
        if (container == null) return;

        foreach (BoxCollider box in container.GetComponentsInChildren<BoxCollider>())
            Colliders.Remove(box);
    }

    private void LateUpdate()
    {
        if (completed || goal == null || body == null || body.UpperPoints == null) return;

        bool allThrough = AllInsideGoal(body.UpperPoints) && AllInsideGoal(body.LowerPoints);
        insideTimer = allThrough ? insideTimer + Time.deltaTime : 0f;

        if (insideTimer >= holdSeconds)
        {
            completed = true;

            // ============================================================
            // ADD YOUR "GAME COMPLETE" LOGIC HERE, e.g. play a fanfare,
            // fade out, or load the ending. You can also use the
            // OnCompleted event in the Inspector.
            // ============================================================

            OnCompleted?.Invoke();
        }
    }

    private bool AllInsideGoal(Transform[] points)
    {
        foreach (Transform point in points)
        {
            if (point == null) continue;

            Vector3 position = point.position;
            if ((goal.ClosestPoint(position) - position).sqrMagnitude > 0.0001f)
                return false;
        }
        return true;
    }

    public void ResetGame()
    {
        completed = false;
        insideTimer = 0f;
    }

    // Preview of the wall, holes and goal area in the Scene view.
    private void OnDrawGizmosSelected()
    {
        Gizmos.matrix = transform.localToWorldMatrix;

        Gizmos.color = Color.white;
        Gizmos.DrawWireCube(new Vector3(0f, wallHeight * 0.5f, 0f), new Vector3(wallWidth, wallHeight, thickness));

        Gizmos.color = Color.green;
        Gizmos.DrawWireCube(new Vector3(legsCenterX, legsHeight * 0.5f, 0f), new Vector3(legsWidth, legsHeight, thickness * 1.05f));

        Gizmos.color = Color.yellow;
        Gizmos.DrawWireCube(new Vector3(torsoCenterX, (torsoBottom + torsoTop) * 0.5f, 0f), new Vector3(torsoWidth, torsoTop - torsoBottom, thickness * 1.05f));

        Gizmos.color = new Color(0.3f, 0.6f, 1f, 0.8f);
        Gizmos.DrawWireCube(new Vector3(0f, (wallHeight - 0.5f) * 0.5f, thickness * 0.5f + goalDepth * 0.5f), new Vector3(wallWidth, wallHeight + 0.5f, goalDepth));
    }
}