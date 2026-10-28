using UnityEngine;
using UnityEngine.Events;
using UnityEngine.InputSystem;

// Splits the avatar at the waist. While split:
//  - the LOWER body is pinned to the XR Origin, so the thumbstick moves it but walking around your room does not
//  - the UPPER body keeps following your headset and hands as normal
//
// It also pushes the XR Origin out of the HoleWall using two invisible capsules (legs and torso),
// so each half only fits through the hole made for it.
//
// Put this on an empty "Body Manager" object and fill in the references.
[DefaultExecutionOrder(10000)] // after the avatar's IK and after locomotion
public class SplitBody : MonoBehaviour
{
    [Header("Rig")]
    [Tooltip("The XR Origin: the object the thumbstick moves.")]
    [SerializeField] private Transform xrOrigin;

    [Tooltip("The headset camera. Auto-filled from Camera.main if left empty.")]
    [SerializeField] private Transform head;

    [Tooltip("The CharacterController on the XR Origin (optional but recommended). It stops colliding with the wall, because this script handles the wall per body half instead.")]
    [SerializeField] private CharacterController originController;

    [Header("Avatar bones")]
    [SerializeField] private Transform hips;

    [Tooltip("The Spine bone. It must be a direct child of Hips.")]
    [SerializeField] private Transform spine;

    [SerializeField] private Transform leftFoot;
    [SerializeField] private Transform rightFoot;

    [Header("Input")]
    [Tooltip("A button action that splits and rejoins the body. Example binding: <XRController>{LeftHand}/primaryButton")]
    [SerializeField] private InputActionReference splitAction;

    [Header("Split")]
    [Tooltip("Seconds the lower body takes to blend between following you and staying put.")]
    [SerializeField] private float blendTime = 0.25f;

    [Tooltip("You can only rejoin when the lower body is within this horizontal distance of your head. Stops the legs jumping through the wall.")]
    [SerializeField] private float mergeMaxDistance = 0.25f;

    [Header("Collision with the hole wall")]
    [SerializeField] private bool collideWithWall = true;

    [Tooltip("Height of the waist above the floor, in metres. The legs capsule is below this and the torso capsule above it.")]
    [SerializeField] private float waistHeight = 0.95f;

    [Tooltip("Radius of the legs capsule. Must be smaller than half the legs hole width.")]
    [SerializeField] private float lowerRadius = 0.18f;

    [Tooltip("Radius of the torso and head capsule. Must be smaller than half the torso window width.")]
    [SerializeField] private float upperRadius = 0.2f;

    [Tooltip("Extra height above the headset, so the top of your head fits too.")]
    [SerializeField] private float headTopMargin = 0.1f;

    [Header("Events")]
    public UnityEvent OnSplit;
    public UnityEvent OnMerged;
    public UnityEvent OnMergeRefused;
    [Tooltip("Fires when you first touch the wall.")]
    public UnityEvent OnWallHit;

    public bool IsSplit => isSplit;

    /// <summary>Points that must be through the wall for the upper body to count as through.</summary>
    public Transform[] UpperPoints { get; private set; }

    /// <summary>Points that must be through the wall for the lower body to count as through.</summary>
    public Transform[] LowerPoints { get; private set; }

    private bool ready;
    private bool isSplit;
    private float blend;
    private Vector3 anchorLocalPosition;
    private Quaternion anchorLocalRotation;

    // Used to undo our own changes when the rig doesn't rewrite a bone each frame.
    private Vector3 restHipsLocalPosition, restSpineLocalPosition;
    private Quaternion restHipsLocalRotation, restSpineLocalRotation;
    private Vector3 lastHipsLocalPosition, lastSpineLocalPosition;
    private Quaternion lastHipsLocalRotation, lastSpineLocalRotation;
    private bool wroteLastFrame;

    private CapsuleCollider lowerCollider, upperCollider;
    private Collider[] bodyParts;
    private bool wasBlocked;

    private void OnEnable()
    {
        if (splitAction != null)
        {
            splitAction.action.performed += OnSplitPerformed;
            splitAction.action.Enable();
        }
    }

    private void OnDisable()
    {
        if (splitAction != null)
            splitAction.action.performed -= OnSplitPerformed;
    }

    private void OnSplitPerformed(InputAction.CallbackContext ctx) => Toggle();

    private void Start()
    {
        if (head == null && Camera.main != null)
            head = Camera.main.transform;

        if (xrOrigin == null || head == null || hips == null || spine == null)
        {
            Debug.LogWarning($"[SplitBody {name}] XR Origin, Head, Hips and Spine must all be assigned. Splitting is disabled.", this);
            enabled = false;
            return;
        }

        if (spine.parent != hips)
            Debug.LogWarning($"[SplitBody {name}] Spine is not a direct child of Hips. The split may look wrong.", this);

        restHipsLocalPosition = hips.localPosition;
        restHipsLocalRotation = hips.localRotation;
        restSpineLocalPosition = spine.localPosition;
        restSpineLocalRotation = spine.localRotation;

        UpperPoints = new[] { head, spine };
        var lower = new System.Collections.Generic.List<Transform> { hips };
        if (leftFoot != null) lower.Add(leftFoot);
        if (rightFoot != null) lower.Add(rightFoot);
        LowerPoints = lower.ToArray();

        lowerCollider = CreateCapsule("LowerBodyCollider");
        upperCollider = CreateCapsule("UpperBodyCollider");
        bodyParts = new Collider[] { lowerCollider, upperCollider };

        if (originController != null)
        {
            foreach (Collider wall in HoleWall.Colliders)
                if (wall != null) Physics.IgnoreCollision(originController, wall, true);
        }

        ready = true;
    }

    private void OnDestroy()
    {
        if (lowerCollider != null) Destroy(lowerCollider.gameObject);
        if (upperCollider != null) Destroy(upperCollider.gameObject);
    }

    private CapsuleCollider CreateCapsule(string objectName)
    {
        var go = new GameObject(objectName);
        go.layer = 2; // Ignore Raycast, so XR ray interactors don't hit it
        var capsule = go.AddComponent<CapsuleCollider>();
        capsule.isTrigger = true; // only used for geometry queries, so it never blocks anything physically
        capsule.direction = 1;    // Y axis
        return capsule;
    }

    public void Toggle()
    {
        if (!ready) return;
        if (isSplit) TryMerge(); else Split();
    }

    private void Split()
    {
        // Pin the lower body where it is right now, in the XR Origin's space.
        anchorLocalPosition = xrOrigin.InverseTransformPoint(hips.position);
        anchorLocalRotation = Quaternion.Inverse(xrOrigin.rotation) * hips.rotation;
        isSplit = true;
        OnSplit?.Invoke();
    }

    private void TryMerge()
    {
        Vector3 offset = xrOrigin.TransformPoint(anchorLocalPosition) - head.position;
        offset.y = 0f;
        if (offset.magnitude > mergeMaxDistance)
        {
            OnMergeRefused?.Invoke();
            return;
        }

        isSplit = false;
        OnMerged?.Invoke();
    }

    // Runs after locomotion, so the wall can push back against thumbstick and walking movement.
    private void Update()
    {
        if (ready && collideWithWall)
            ResolveWallCollision();
    }

    private void ResolveWallCollision()
    {
        if (HoleWall.Colliders.Count == 0) return;

        PositionCapsules();

        // Push the XR Origin out of the wall, one deepest overlap at a time.
        Vector3 total = Vector3.zero;
        for (int i = 0; i < 4; i++)
        {
            if (!TryGetDeepestPenetration(total, out Vector3 push)) break;

            push.y = 0f; // only push sideways, never up or down
            if (push.sqrMagnitude < 1e-8f) break;
            total += push * 1.01f;
        }

        if (total != Vector3.zero)
        {
            xrOrigin.position += total;
            Physics.SyncTransforms(); // keeps a CharacterController on the origin from snapping back

            if (!wasBlocked)
            {
                wasBlocked = true;
                OnWallHit?.Invoke();
            }
        }
        else
        {
            wasBlocked = false;
        }
    }

    private void PositionCapsules()
    {
        float floorY = xrOrigin.position.y;

        Vector3 lowerPosition = head.position;
        if (blend > 0f)
            lowerPosition = Vector3.Lerp(head.position, xrOrigin.TransformPoint(anchorLocalPosition), blend);

        float lowerBottom = floorY + 0.02f;
        float lowerTop = floorY + waistHeight;
        lowerCollider.radius = lowerRadius;
        lowerCollider.height = Mathf.Max(lowerTop - lowerBottom, lowerRadius * 2f);
        lowerCollider.transform.position = new Vector3(lowerPosition.x, (lowerBottom + lowerTop) * 0.5f, lowerPosition.z);

        float upperBottom = floorY + waistHeight;
        float upperTop = head.position.y + headTopMargin;
        upperCollider.radius = upperRadius;
        upperCollider.height = Mathf.Max(upperTop - upperBottom, upperRadius * 2f);
        upperCollider.transform.position = new Vector3(head.position.x, upperBottom + upperCollider.height * 0.5f, head.position.z);
    }

    private bool TryGetDeepestPenetration(Vector3 offset, out Vector3 push)
    {
        push = Vector3.zero;
        float deepest = 0f;

        foreach (Collider part in bodyParts)
        {
            Vector3 position = part.transform.position + offset;
            foreach (Collider wall in HoleWall.Colliders)
            {
                if (wall == null) continue;

                if (Physics.ComputePenetration(part, position, part.transform.rotation,
                        wall, wall.transform.position, wall.transform.rotation,
                        out Vector3 direction, out float distance) && distance > deepest)
                {
                    deepest = distance;
                    push = direction * distance;
                }
            }
        }

        return deepest > 0f;
    }

    private void LateUpdate()
    {
        if (!ready) return;

        UndoOwnWrite();

        blend = Mathf.MoveTowards(blend, isSplit ? 1f : 0f, blendTime > 0.0001f ? Time.deltaTime / blendTime : 1f);
        if (blend <= 0f)
        {
            wroteLastFrame = false;
            return;
        }

        // The upper body keeps the pose the rig gave it.
        Vector3 spinePosition = spine.position;
        Quaternion spineRotation = spine.rotation;
        Vector3 riggedHipsPosition = hips.position;
        Quaternion riggedHipsRotation = hips.rotation;

        // Move the hips (and so the legs) to the pinned position...
        Vector3 anchoredPosition = xrOrigin.TransformPoint(anchorLocalPosition);
        Quaternion anchoredRotation = xrOrigin.rotation * anchorLocalRotation;
        hips.SetPositionAndRotation(
            Vector3.Lerp(riggedHipsPosition, anchoredPosition, blend),
            Quaternion.Slerp(riggedHipsRotation, anchoredRotation, blend));

        // ...then put the spine (and so the whole upper body) back where the rig had it.
        spine.SetPositionAndRotation(spinePosition, spineRotation);

        lastHipsLocalPosition = hips.localPosition;
        lastHipsLocalRotation = hips.localRotation;
        lastSpineLocalPosition = spine.localPosition;
        lastSpineLocalRotation = spine.localRotation;
        wroteLastFrame = true;
    }

    // If the rig/animator didn't rewrite a bone since our last change, our change would still be there.
    // Put it back to its rest pose first, so we always start from the real rigged pose.
    private void UndoOwnWrite()
    {
        if (!wroteLastFrame) return;

        if ((hips.localPosition - lastHipsLocalPosition).sqrMagnitude < 1e-10f &&
            Quaternion.Angle(hips.localRotation, lastHipsLocalRotation) < 0.01f)
        {
            hips.localPosition = restHipsLocalPosition;
            hips.localRotation = restHipsLocalRotation;
        }

        if ((spine.localPosition - lastSpineLocalPosition).sqrMagnitude < 1e-10f &&
            Quaternion.Angle(spine.localRotation, lastSpineLocalRotation) < 0.01f)
        {
            spine.localPosition = restSpineLocalPosition;
            spine.localRotation = restSpineLocalRotation;
        }
    }

    // Shows the two collision capsules in the Scene view while playing, so you can compare them with the holes.
    private void OnDrawGizmos()
    {
        if (!Application.isPlaying) return;

        Gizmos.color = new Color(1f, 0.5f, 0f, 0.9f);
        DrawBounds(lowerCollider);
        Gizmos.color = new Color(0.2f, 0.7f, 1f, 0.9f);
        DrawBounds(upperCollider);
    }

    private static void DrawBounds(Collider c)
    {
        if (c != null) Gizmos.DrawWireCube(c.bounds.center, c.bounds.size);
    }
}