using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;

// Put this on each screw GameObject. It needs a trigger Collider sized as
// the "engagement zone" the screwdriver tip must sit inside while twisting.
[RequireComponent(typeof(Collider))]
public class Screw : MonoBehaviour
{
    [Header("Matching")]
    [Tooltip("The exact bit profile required to turn this screw. Wrong type OR wrong size = no progress.")]
    [SerializeField] private ScrewdriverBitProfile requiredBit;

    [Header("Unscrewing feel")]
    [Tooltip("Total degrees of rotation needed to fully remove the screw. 1440 = 4 full turns.")]
    [SerializeField] private float totalRotationDegreesRequired = 1440f;

    [Tooltip("The axis to measure twist around, in the SCREWDRIVER's local space (not the screw's). The generated screwdriver runs along its local Y, so use (0, 1, 0). Changing this default does not update screws that already exist, so set it on each screw.")]
    [SerializeField] private Vector3 localShaftAxis = Vector3.up;

    [Header("Back-out motion")]
    [Tooltip("Slide the screw outwards as it is unscrewed.")]
    [SerializeField] private bool moveWhileUnscrewing = true;

    [Tooltip("How far the screw slides out when fully unscrewed, in the parent's units (metres if nothing is scaled). The trigger zone moves with the screw, so with the default 10 mm zone keep this under about 15 mm, or enlarge the trigger radius.")]
    [SerializeField] private float backOutDistance = 0.012f;

    [Tooltip("Direction the screw slides, in the screw's OWN local space. The head of the generated screws points along local +Y.")]
    [SerializeField] private Vector3 backOutAxisLocal = Vector3.up;

    [Tooltip("Off = glides out continuously with progress. On = moves out in hops every (100 / Step Count) percent.")]
    [SerializeField] private bool steppedMovement = false;

    [Tooltip("Number of hops from start to fully out. 10 = one hop every 10 percent.")]
    [Min(1)]
    [SerializeField] private int stepCount = 10;

    [Tooltip("Smoothing time for the movement. Smaller feels tighter. In stepped mode this is roughly how long each hop takes.")]
    [SerializeField] private float moveSmoothTime = 0.1f;

    [Tooltip("Also rotate the screw as it comes out.")]
    [SerializeField] private bool spinWhileUnscrewing = true;

    [Tooltip("Tick this if the screw visually turns the wrong way.")]
    [SerializeField] private bool invertSpin = false;

    [Header("When finished")]
    [Tooltip("Seconds to wait after the screw has finished sliding out, before it disappears.")]
    [SerializeField] private float disappearDelay = 0.3f;

    [Tooltip("Off = the screw is just hidden (SetActive false). On = it is destroyed.")]
    [SerializeField] private bool destroyWhenDone = false;

    [Header("Snapping")]
    [Tooltip("Where the bit tip should seat, in the screw's local space. The default is the middle of the head top on the generated screws.")]
    [SerializeField] private Vector3 snapPointLocal = new Vector3(0f, 0.0237f, 0f);

    [Header("Debugging")]
    [Tooltip("Logs trigger enter/exit, bit mismatches and progress in 10% steps. Turn off once it works.")]
    [SerializeField] private bool debugLogging = true;

    [Header("Events")]
    public UnityEvent OnUnscrewed; // fires the moment the last degree of rotation is done
    public UnityEvent<float> OnProgressChanged; // 0-1 normalized, handy for a progress bar

    /// <summary>All screws currently enabled in the scene. ScrewdriverSnap searches this to find a screw to snap onto.</summary>
    public static readonly List<Screw> Active = new List<Screw>();

    public bool IsUnscrewed => isUnscrewed;
    public ScrewdriverBitProfile RequiredBit => requiredBit;

    /// <summary>World position where the bit tip should seat. Follows the screw as it slides out.</summary>
    public Vector3 SnapPointWorld => transform.TransformPoint(snapPointLocal);

    /// <summary>World direction the screw head faces (toward the person unscrewing it).</summary>
    public Vector3 OutwardAxisWorld => transform.TransformDirection(backOutAxisLocal.normalized);

    private ScrewdriverTip engagedTip;
    private Quaternion lastTipRotation;
    private float currentProgressDegrees;
    private bool isUnscrewed;
    private bool loggedMismatch;
    private int lastLoggedStep = -1;

    // Visual state
    private Vector3 startLocalPosition;
    private Quaternion startLocalRotation;
    private float smoothedMove01;
    private float moveVelocity;
    private float appliedMove01 = -1f;
    private float appliedSpinDegrees = float.NaN;

    private void OnEnable() => Active.Add(this);

    private void OnDisable() => Active.Remove(this);

    private void Awake()
    {
        startLocalPosition = transform.localPosition;
        startLocalRotation = transform.localRotation;

        // Setup checks: these catch the most common reasons nothing happens.
        Collider col = GetComponent<Collider>();
        if (!col.isTrigger)
            Debug.LogWarning($"[Screw {name}] Collider is not set to Is Trigger, so OnTriggerEnter will never fire.", this);
        if (col is MeshCollider mesh && !mesh.convex)
            Debug.LogWarning($"[Screw {name}] MeshCollider is not Convex, so it cannot work as a trigger. Tick Convex or use a Sphere/Capsule collider.", this);
        if (requiredBit == null)
            Debug.LogWarning($"[Screw {name}] Required Bit is not assigned, so no bit will ever match.", this);
        if (localShaftAxis.sqrMagnitude < 0.0001f)
            Debug.LogWarning($"[Screw {name}] Local Shaft Axis is (0,0,0), so no twist can be measured.", this);
        if (backOutAxisLocal.sqrMagnitude < 0.0001f)
            Debug.LogWarning($"[Screw {name}] Back Out Axis Local is (0,0,0), so the screw cannot move or spin.", this);
    }

    private void OnTriggerEnter(Collider other)
    {
        if (isUnscrewed) return;

        if (debugLogging)
            Debug.Log($"[Screw {name}] trigger entered by '{other.name}'", this);

        var tip = other.GetComponent<ScrewdriverTip>();
        if (tip == null || tip.Owner == null) return;

        engagedTip = tip;
        lastTipRotation = tip.Owner.transform.rotation;
        loggedMismatch = false;

        if (debugLogging)
            Debug.Log($"[Screw {name}] engaged. Equipped: '{BitName(tip.Owner.CurrentBit)}', required: '{BitName(requiredBit)}'", this);
    }

    private void OnTriggerExit(Collider other)
    {
        var tip = other.GetComponent<ScrewdriverTip>();
        if (tip != null && tip == engagedTip)
        {
            engagedTip = null; // progress is preserved, just paused, so repositioning the controller doesn't lose work
            if (debugLogging)
                Debug.Log($"[Screw {name}] tip left the zone (progress kept at {currentProgressDegrees:F0} degrees)", this);
        }
    }

    private void Update()
    {
        if (!isUnscrewed)
            UpdateTwist();

        UpdateVisuals();
    }

    private void UpdateTwist()
    {
        if (engagedTip == null || engagedTip.Owner == null) return;

        Transform screwdriverTransform = engagedTip.Owner.transform;
        Quaternion currentRotation = screwdriverTransform.rotation;

        if (engagedTip.Owner.CurrentBit != requiredBit)
        {
            // Wrong head equipped. Keep the baseline fresh so switching to the right bit
            // doesn't count all the rotation since entering as one big jump.
            lastTipRotation = currentRotation;
            if (debugLogging && !loggedMismatch)
            {
                loggedMismatch = true;
                Debug.Log($"[Screw {name}] wrong bit: equipped '{BitName(engagedTip.Owner.CurrentBit)}', required '{BitName(requiredBit)}'", this);
            }
            return;
        }
        loggedMismatch = false;

        // Rotation since last frame, isolated to the shaft's own twist axis
        // (so tilting/wobbling the controller doesn't falsely add progress).
        Quaternion deltaRotation = currentRotation * Quaternion.Inverse(lastTipRotation);
        deltaRotation.ToAngleAxis(out float angle, out Vector3 axis);
        if (angle > 180f) angle -= 360f; // shortest-path angle

        Vector3 worldShaftAxis = screwdriverTransform.TransformDirection(localShaftAxis.normalized);
        float signedDelta = angle * Vector3.Dot(axis.normalized, worldShaftAxis);

        lastTipRotation = currentRotation;

        // One twist direction unscrews, the other screws back in. If it feels backwards
        // in your setup, change "- signedDelta" to "+ signedDelta".
        currentProgressDegrees = Mathf.Clamp(currentProgressDegrees - signedDelta, 0f, totalRotationDegreesRequired);

        float normalized = currentProgressDegrees / totalRotationDegreesRequired;
        OnProgressChanged?.Invoke(normalized);

        if (debugLogging)
        {
            int step = Mathf.FloorToInt(normalized * 10f);
            if (step != lastLoggedStep)
            {
                lastLoggedStep = step;
                Debug.Log($"[Screw {name}] progress {normalized * 100f:F0}%", this);
            }
        }

        if (currentProgressDegrees >= totalRotationDegreesRequired)
            Unscrew();
    }

    // Slides and spins the screw to match progress. Only writes to the transform when something changed.
    private void UpdateVisuals()
    {
        float progress01 = totalRotationDegreesRequired > 0f
            ? Mathf.Clamp01(currentProgressDegrees / totalRotationDegreesRequired)
            : 1f;

        if (moveWhileUnscrewing && backOutAxisLocal.sqrMagnitude > 0.0001f)
        {
            float target;
            if (isUnscrewed)
                target = 1f;
            else if (steppedMovement)
                target = Mathf.Floor(progress01 * stepCount) / stepCount;
            else
                target = progress01;

            smoothedMove01 = Mathf.SmoothDamp(smoothedMove01, target, ref moveVelocity, Mathf.Max(0.0001f, moveSmoothTime));

            if (!Mathf.Approximately(smoothedMove01, appliedMove01))
            {
                appliedMove01 = smoothedMove01;
                Vector3 axisInParent = startLocalRotation * backOutAxisLocal.normalized;
                transform.localPosition = startLocalPosition + axisInParent * (smoothedMove01 * backOutDistance);
            }
        }

        if (spinWhileUnscrewing && backOutAxisLocal.sqrMagnitude > 0.0001f)
        {
            // Unscrewing turns counter-clockwise when looking at the head, which is a negative angle about +Y in Unity.
            float spinDegrees = (isUnscrewed ? totalRotationDegreesRequired : currentProgressDegrees) * (invertSpin ? 1f : -1f);

            if (float.IsNaN(appliedSpinDegrees) || !Mathf.Approximately(spinDegrees, appliedSpinDegrees))
            {
                appliedSpinDegrees = spinDegrees;
                transform.localRotation = startLocalRotation * Quaternion.AngleAxis(spinDegrees, backOutAxisLocal.normalized);
            }
        }
    }

    private void Unscrew()
    {
        isUnscrewed = true;
        engagedTip = null;
        GetComponent<Collider>().enabled = false;

        if (debugLogging)
            Debug.Log($"[Screw {name}] unscrewed", this);

        // ============================================================
        // ADD YOUR "SCREW REMOVED" LOGIC HERE, e.g. a pop sound, or
        // counting how many screws are out to open the box. (The screw
        // itself keeps sliding out and then disappears on its own.)
        // ============================================================

        OnUnscrewed?.Invoke();
        StartCoroutine(FinishRoutine());
    }

    private IEnumerator FinishRoutine()
    {
        // Let the screw finish sliding out before it vanishes (with a safety timeout).
        float waited = 0f;
        while (moveWhileUnscrewing && smoothedMove01 < 0.995f && waited < 2f)
        {
            waited += Time.deltaTime;
            yield return null;
        }

        if (disappearDelay > 0f)
            yield return new WaitForSeconds(disappearDelay);

        if (destroyWhenDone)
            Destroy(gameObject);
        else
            gameObject.SetActive(false);
    }

    private static string BitName(ScrewdriverBitProfile bit) => bit != null ? bit.bitName : "none";
}