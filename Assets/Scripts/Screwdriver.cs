using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

// Put this on the screwdriver's grabbable body (alongside your XRGrabInteractable).
public class Screwdriver : MonoBehaviour
{
    [Header("Bits")]
    [Tooltip("The 4 bit profiles this screwdriver can cycle through.")]
    [SerializeField] private List<ScrewdriverBitProfile> carriedBits = new List<ScrewdriverBitProfile>();

    [Tooltip("Empty child transform at the very tip of the shaft where the head visual attaches.")]
    [SerializeField] private Transform headMount;

    [Header("Input")]
    [Tooltip("Bind this to whichever button should cycle heads (e.g. secondary button on the holding hand).")]
    [SerializeField] private InputActionReference cycleBitAction;

    private int currentIndex;
    private GameObject currentHeadVisualInstance;

    /// <summary>The bit profile currently equipped.</summary>
    public ScrewdriverBitProfile CurrentBit => carriedBits.Count > 0 ? carriedBits[currentIndex] : null;

    private void Awake()
    {
        if (carriedBits.Count > 0)
            SwapVisual(carriedBits[currentIndex]);
    }

    private void OnEnable()
    {
        if (cycleBitAction != null)
        {
            cycleBitAction.action.performed += OnCyclePerformed;
            cycleBitAction.action.Enable();
        }
    }

    private void OnDisable()
    {
        if (cycleBitAction != null)
            cycleBitAction.action.performed -= OnCyclePerformed;
    }

    private void OnCyclePerformed(InputAction.CallbackContext ctx) => CycleBit();

    /// <summary>Advances to the next carried bit and swaps the visual head. Also callable directly (e.g. from a UnityEvent).</summary>
    public void CycleBit()
    {
        if (carriedBits.Count == 0) return;

        currentIndex = (currentIndex + 1) % carriedBits.Count;
        SwapVisual(carriedBits[currentIndex]);

        // ============================================================
        // ADD LOGIC FOR WHEN THE HEAD CHANGES HERE (e.g. a click sound,
        // haptic pulse, or updating a UI icon for the equipped bit).
        // ============================================================
    }

    private void SwapVisual(ScrewdriverBitProfile bit)
    {
        if (currentHeadVisualInstance != null)
            Destroy(currentHeadVisualInstance);

        if (bit != null && bit.headVisualPrefab != null && headMount != null)
        {
            currentHeadVisualInstance = Instantiate(bit.headVisualPrefab, headMount);
            currentHeadVisualInstance.transform.localPosition = Vector3.zero;
            currentHeadVisualInstance.transform.localRotation = Quaternion.identity;
        }
    }
}
