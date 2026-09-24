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

    [Tooltip("The shaft axis to measure twist around, in the screwdriver's LOCAL space (usually Forward/Z or Up/Y depending on how you modeled it).")]
    [SerializeField] private Vector3 localShaftAxis = Vector3.forward;

    [Header("Events")]
    public UnityEvent OnUnscrewed;
    public UnityEvent<float> OnProgressChanged; // 0-1 normalized, handy for a progress bar or screw sliding out visually

    private ScrewdriverTip engagedTip;
    private Quaternion lastTipRotation;
    private float currentProgressDegrees;
    private bool isUnscrewed;

    private void OnTriggerEnter(Collider other)
    {
        if (isUnscrewed) return;

        var tip = other.GetComponent<ScrewdriverTip>();
        if (tip == null || tip.Owner == null) return;

        engagedTip = tip;
        lastTipRotation = tip.Owner.transform.rotation;
    }

    private void OnTriggerExit(Collider other)
    {
        var tip = other.GetComponent<ScrewdriverTip>();
        if (tip != null && tip == engagedTip)
            engagedTip = null; // progress is preserved, just paused, so repositioning the controller doesn't lose work
    }

    private void Update()
    {
        if (isUnscrewed || engagedTip == null || engagedTip.Owner == null) return;
        if (engagedTip.Owner.CurrentBit != requiredBit) return; // wrong head equipped

        Transform screwdriverTransform = engagedTip.Owner.transform;
        Quaternion currentRotation = screwdriverTransform.rotation;

        // Rotation since last frame, isolated to the shaft's own twist axis
        // (so tilting/wobbling the controller doesn't falsely add progress).
        Quaternion deltaRotation = currentRotation * Quaternion.Inverse(lastTipRotation);
        deltaRotation.ToAngleAxis(out float angle, out Vector3 axis);
        if (angle > 180f) angle -= 360f; // shortest-path angle

        Vector3 worldShaftAxis = screwdriverTransform.TransformDirection(localShaftAxis.normalized);
        float signedDelta = angle * Vector3.Dot(axis.normalized, worldShaftAxis);

        lastTipRotation = currentRotation;

        // Only counter-clockwise (unscrewing) direction adds progress;
        // screwing back in reduces it. Flip the sign here if it feels backwards in your setup.
        currentProgressDegrees = Mathf.Clamp(currentProgressDegrees - signedDelta, 0f, totalRotationDegreesRequired);

        OnProgressChanged?.Invoke(currentProgressDegrees / totalRotationDegreesRequired);

        if (currentProgressDegrees >= totalRotationDegreesRequired)
            Unscrew();
    }

    private void Unscrew()
    {
        isUnscrewed = true;
        engagedTip = null;

        // ============================================================
        // ADD YOUR "SCREW REMOVED" LOGIC HERE, e.g.:
        // - disable this collider so the screwdriver no longer engages it
        // - swap this screw's visual for a loose, grabbable screw prop
        // - play a pop/drop sound
        // ============================================================
        GetComponent<Collider>().enabled = false;

        OnUnscrewed?.Invoke();
    }
}
