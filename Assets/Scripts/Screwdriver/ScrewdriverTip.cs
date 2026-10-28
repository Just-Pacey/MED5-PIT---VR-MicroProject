using UnityEngine;

// Put this on a small trigger Collider (e.g. a tiny sphere) at the very tip
// of the screwdriver's head mount. This is purely an identity marker +
// a way for Screw.cs to reach back to the owning Screwdriver.
[RequireComponent(typeof(Collider))]
public class ScrewdriverTip : MonoBehaviour
{
    [Tooltip("Auto-filled from a parent if left empty.")]
    [SerializeField] private Screwdriver owner;

    public Screwdriver Owner => owner;

    private void Awake()
    {
        if (owner == null)
            owner = GetComponentInParent<Screwdriver>();
    }
}
