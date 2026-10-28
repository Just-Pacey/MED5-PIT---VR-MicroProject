using UnityEngine;
using UnityEngine.Events;

// Put this on the SAME GameObject as Screwdriver.cs (the grab root with the Rigidbody).
//
// Hierarchy it expects:
//   Screwdriver root   <- Rigidbody, XRGrabInteractable, grip collider, Screwdriver.cs, ScrewdriverSnap.cs
//     Visuals          <- empty child, position 0, rotation 0, scale 1. Assign it to Visual Root below.
//       Handle, Shaft  <- the imported meshes
//       HeadMount      <- with the ScrewdriverTip trigger sphere as its child
//
// The root keeps following your hand (so twisting is still measured from your real wrist).
// Only the Visuals child swings onto the screw, like a magnet.
[RequireComponent(typeof(Screwdriver))]
public class ScrewdriverSnap : MonoBehaviour
{
    [Header("References")]
    [Tooltip("The child that holds everything you see: handle, shaft, HeadMount and the tip trigger. This is what swings onto the screw.")]
    [SerializeField] private Transform visualRoot;

    [Tooltip("The point that should land on the screw head. Auto-filled from the ScrewdriverTip trigger if left empty. Must be under Visual Root.")]
    [SerializeField] private Transform tipPoint;

    [Tooltip("Direction the shaft points toward the tip, in Visual Root's local space. The generated screwdriver points along +Y.")]
    [SerializeField] private Vector3 shaftAxisLocal = Vector3.up;

    [Header("Range")]
    [Tooltip("Snaps when your hand-driven tip comes within this distance (metres) of a screw's snap point.")]
    [SerializeField] private float snapDistance = 0.03f;

    [Tooltip("Lets go once your hand moves further than this from the screw. Keep it larger than Snap Distance so it feels magnetic and doesn't flicker.")]
    [SerializeField] private float releaseDistance = 0.05f;

    [Tooltip("The screwdriver must be pointing within this many degrees of the screw's axis to snap.")]
    [Range(5f, 90f)]
    [SerializeField] private float snapAngle = 40f;

    [Tooltip("Lets go if the angle gets worse than this.")]
    [Range(5f, 120f)]
    [SerializeField] private float releaseAngle = 70f;

    [Header("Behaviour")]
    [Tooltip("Only snap while the screwdriver is being held. If you turn this on, hook the XRGrabInteractable's Select Entered event to SetHeld(true) and Select Exited to SetHeld(false) on this component.")]
    [SerializeField] private bool onlySnapWhenHeld = false;

    [Tooltip("Only snap to a screw whose Required Bit is the bit currently equipped. A wrong bit won't seat, which is a useful cue for the player.")]
    [SerializeField] private bool onlySnapWithMatchingBit = true;

    [Tooltip("Also rotate the screwdriver so its shaft lines up with the screw. The twist around the shaft still comes from your hand.")]
    [SerializeField] private bool alignRotation = true;

    [Tooltip("Seconds the visuals take to swing onto, or off, the screw.")]
    [SerializeField] private float blendTime = 0.08f;
    [SerializeField] private bool debugLogging = false;

    [Header("Events (hook up a click sound or haptics here)")]
    public UnityEvent OnSnapped;
    public UnityEvent OnSnapReleased;

    /// <summary>The screw the bit is currently snapped to, or null.</summary>
    public Screw CurrentScrew => snapped ? target : null;

    private Screwdriver screwdriver;
    private Vector3 restLocalPosition;
    private Quaternion restLocalRotation;
    private Vector3 tipLocalInVisual;
    private Screw target;
    private bool snapped;
    private float blend;
    private bool poseOffset;
    private bool ready;
    private bool held;

    /// <summary>Call with true/false from the grab interactable's Select Entered/Exited events (only needed if Only Snap When Held is on).</summary>
    public void SetHeld(bool value) => held = value;

    private void Awake()
    {
        screwdriver = GetComponent<Screwdriver>();

        if (visualRoot == null || visualRoot == transform || visualRoot.parent == null)
        {
            Debug.LogWarning($"[ScrewdriverSnap {name}] Visual Root must be a child of the screwdriver root (not the root itself). Snapping is disabled.", this);
            enabled = false;
            return;
        }

        if (tipPoint == null)
        {
            var tip = GetComponentInChildren<ScrewdriverTip>();
            if (tip != null) tipPoint = tip.transform;
        }

        if (tipPoint == null)
        {
            Debug.LogWarning($"[ScrewdriverSnap {name}] No Tip Point assigned and no ScrewdriverTip found. Snapping is disabled.", this);
            enabled = false;
            return;
        }

        if (!tipPoint.IsChildOf(visualRoot))
            Debug.LogWarning($"[ScrewdriverSnap {name}] Tip Point is not under Visual Root, so the tip will not move with the snap.", this);

        // Capture the rest pose now, while the visuals are lined up with the root.
        restLocalPosition = visualRoot.localPosition;
        restLocalRotation = visualRoot.localRotation;
        tipLocalInVisual = visualRoot.InverseTransformPoint(tipPoint.position);
        ready = true;
    }

    private void OnEnable()
    {
        // XR poses can update right before rendering, so re-apply the snap then too to avoid a frame of lag.
        Application.onBeforeRender += OnBeforeRender;
    }

    private void OnDisable()
    {
        Application.onBeforeRender -= OnBeforeRender;

        if (ready && visualRoot != null)
        {
            visualRoot.localPosition = restLocalPosition;
            visualRoot.localRotation = restLocalRotation;
        }
        snapped = false;
        target = null;
        blend = 0f;
        poseOffset = false;
    }

    private void LateUpdate()
    {
        if (!ready) return;

        UpdateTarget();
        UpdateBlend();
        ApplyPose();
    }

    private void OnBeforeRender()
    {
        if (ready) ApplyPose();
    }

    // The pose the visuals have when they simply follow the hand.
    private void GetHandPose(out Vector3 position, out Quaternion rotation)
    {
        Transform parent = visualRoot.parent;
        position = parent.TransformPoint(restLocalPosition);
        rotation = parent.rotation * restLocalRotation;
    }

    private bool BitMatches(Screw screw) => !onlySnapWithMatchingBit || screw.RequiredBit == screwdriver.CurrentBit;

    private void UpdateTarget()
    {
        GetHandPose(out Vector3 handPosition, out Quaternion handRotation);
        Vector3 handTip = handPosition + handRotation * Vector3.Scale(tipLocalInVisual, visualRoot.lossyScale);
        Vector3 handAxis = handRotation * shaftAxisLocal.normalized;
        bool heldOk = !onlySnapWhenHeld || held;

        if (snapped)
        {
            if (target == null || !heldOk || target.IsUnscrewed || !target.isActiveAndEnabled || !BitMatches(target))
            {
                Release("no longer valid");
                return;
            }

            float distance = Vector3.Distance(handTip, target.SnapPointWorld);
            float angle = Vector3.Angle(handAxis, -target.OutwardAxisWorld);
            if (distance > releaseDistance || (alignRotation && angle > releaseAngle))
                Release($"moved away (distance {distance * 1000f:F0} mm, angle {angle:F0} deg)");
            return;
        }

        if (!heldOk) return;

        Screw best = null;
        float bestDistance = snapDistance;
        foreach (Screw screw in Screw.Active)
        {
            if (screw.IsUnscrewed || !BitMatches(screw)) continue;

            float distance = Vector3.Distance(handTip, screw.SnapPointWorld);
            if (distance > bestDistance) continue;

            float angle = Vector3.Angle(handAxis, -screw.OutwardAxisWorld);
            if (alignRotation && angle > snapAngle) continue;

            best = screw;
            bestDistance = distance;
        }

        if (best != null)
        {
            target = best;
            snapped = true;
            if (debugLogging) Debug.Log($"[ScrewdriverSnap {name}] snapped to {best.name}", this);
            OnSnapped?.Invoke();
        }
    }

    private void Release(string reason)
    {
        snapped = false;
        if (debugLogging) Debug.Log($"[ScrewdriverSnap {name}] released: {reason}", this);
        OnSnapReleased?.Invoke();
    }

    private void UpdateBlend()
    {
        float step = blendTime > 0.0001f ? Time.deltaTime / blendTime : 1f;
        blend = Mathf.MoveTowards(blend, snapped ? 1f : 0f, step);

        // Fully released and back on the hand: forget the screw.
        if (!snapped && blend <= 0f)
            target = null;
    }

    private void ApplyPose()
    {
        if (target == null || blend <= 0f)
        {
            if (poseOffset)
            {
                visualRoot.localPosition = restLocalPosition;
                visualRoot.localRotation = restLocalRotation;
                poseOffset = false;
            }
            return;
        }

        GetHandPose(out Vector3 handPosition, out Quaternion handRotation);

        // Minimal rotation that points the shaft at the screw. A minimal rotation keeps
        // the twist around the shaft, so the visual still turns with your wrist.
        Quaternion snappedRotation = handRotation;
        if (alignRotation)
        {
            Vector3 handAxis = handRotation * shaftAxisLocal.normalized;
            snappedRotation = Quaternion.FromToRotation(handAxis, -target.OutwardAxisWorld) * handRotation;
        }

        // Slide so the tip point lands exactly on the screw's snap point.
        Vector3 tipOffset = snappedRotation * Vector3.Scale(tipLocalInVisual, visualRoot.lossyScale);
        Vector3 snappedPosition = target.SnapPointWorld - tipOffset;

        visualRoot.SetPositionAndRotation(
            Vector3.Lerp(handPosition, snappedPosition, blend),
            Quaternion.Slerp(handRotation, snappedRotation, blend));
        poseOffset = true;
    }
}