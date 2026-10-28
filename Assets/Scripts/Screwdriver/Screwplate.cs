using System.Collections;
using UnityEngine;
using UnityEngine.Events;

// Put this on the plate's root object.
//
// It counts the screws holding the plate on. When the last one is out, the plate lets go
// and falls off the wall, and everything in "Reveal When Removed" (your split button) is switched on.
//
// Setup notes:
//  - Do NOT add a Rigidbody to the plate yourself. A Rigidbody on a parent object takes over the
//    trigger events of the screws below it, and the screws would stop reacting to the screwdriver.
//    This script adds a Rigidbody at the moment the plate comes off.
//  - The plate needs a normal (non-trigger) collider, ideally a Box Collider, or it falls through the floor.
//  - The objects in "Reveal When Removed" must NOT be children of the plate, or they fall off with it.
public class ScrewPlate : MonoBehaviour
{
    [Header("Screws holding the plate on")]
    [Tooltip("Leave empty to use every Screw found under this object. If your screws are not children of the plate, drag them in here.")]
    [SerializeField] private Screw[] screws;

    [Header("Behind the plate")]
    [Tooltip("Objects that should only exist once the plate is off, e.g. the split button. They are switched off when the scene starts and switched on when the plate comes off.")]
    [SerializeField] private GameObject[] revealWhenRemoved;

    [Header("When the last screw is out")]
    [Tooltip("Seconds to wait after the last screw before the plate lets go, so the last screw can finish sliding out.")]
    [SerializeField] private float releaseDelay = 0.6f;

    [Tooltip("On = the plate falls off the wall. Off = it just disappears.")]
    [SerializeField] private bool fallOff = true;

    [Tooltip("A small push toward the player, so the plate swings off the wall instead of dropping straight down. 0 = no push.")]
    [SerializeField] private float popOutSpeed = 0.5f;

    [Tooltip("Mass of the plate once it is loose.")]
    [SerializeField] private float mass = 2f;

    [Tooltip("Seconds after falling before the plate is removed. 0 = it stays on the floor.")]
    [SerializeField] private float removeAfterSeconds = 0f;

    [Header("Events")]
    [Tooltip("Fires each time a screw comes out. Passes how many are out so far (handy for a sound that gets more rattly).")]
    public UnityEvent<int> OnScrewRemoved;

    [Tooltip("Fires the moment the plate lets go.")]
    public UnityEvent OnPlateReleased;

    public int RemovedCount => removedCount;
    public int TotalScrews => totalScrews;
    public bool IsReleased => released;

    private int removedCount;
    private int totalScrews;
    private bool released;

    private void Awake()
    {
        if (screws == null || screws.Length == 0)
            screws = GetComponentsInChildren<Screw>(true);

        foreach (Screw screw in screws)
            if (screw != null) totalScrews++;

        if (totalScrews == 0)
            Debug.LogWarning($"[ScrewPlate {name}] No screws found. Add the Screw objects to the Screws list, or make them children of the plate. The plate will never come off.", this);

        if (GetComponent<Rigidbody>() != null)
            Debug.LogWarning($"[ScrewPlate {name}] The plate has a Rigidbody. Remove it: it would take over the screws' trigger events. This script adds one when the plate comes off.", this);

        if (fallOff)
            CheckColliders();

        if (revealWhenRemoved != null)
        {
            foreach (GameObject hidden in revealWhenRemoved)
            {
                if (hidden == null) continue;

                if (hidden.transform.IsChildOf(transform))
                    Debug.LogWarning($"[ScrewPlate {name}] '{hidden.name}' is a child of the plate, so it would fall off with it. Move it out, next to the plate.", this);

                hidden.SetActive(false);
            }
        }
    }

    private void OnEnable()
    {
        foreach (Screw screw in screws)
            if (screw != null && screw.OnUnscrewed != null)
                screw.OnUnscrewed.AddListener(HandleScrewRemoved);
    }

    private void OnDisable()
    {
        foreach (Screw screw in screws)
            if (screw != null && screw.OnUnscrewed != null)
                screw.OnUnscrewed.RemoveListener(HandleScrewRemoved);
    }

    private void CheckColliders()
    {
        bool hasSolid = false;
        foreach (Collider col in GetComponentsInChildren<Collider>(true))
        {
            if (col.isTrigger) continue; // the screws' trigger zones don't count

            hasSolid = true;
            if (col is MeshCollider mesh && !mesh.convex)
                Debug.LogWarning($"[ScrewPlate {name}] '{col.name}' has a non-convex Mesh Collider, which can't be used on a falling object. Tick Convex, or use a Box Collider.", this);
        }

        if (!hasSolid)
            Debug.LogWarning($"[ScrewPlate {name}] The plate has no solid (non-trigger) collider, so it will fall through the floor. Add a Box Collider to the plate.", this);
    }

    private void HandleScrewRemoved()
    {
        if (released) return;

        removedCount++;
        OnScrewRemoved?.Invoke(removedCount);

        if (removedCount >= totalScrews)
            StartCoroutine(ReleaseRoutine());
    }

    // Lets you test everything behind the plate without unscrewing four screws. Play mode only.
    [ContextMenu("Test: release the plate now")]
    private void TestRelease()
    {
        if (Application.isPlaying && !released)
            StartCoroutine(ReleaseRoutine());
    }

    private IEnumerator ReleaseRoutine()
    {
        released = true;

        if (releaseDelay > 0f)
            yield return new WaitForSeconds(releaseDelay);

        // ============================================================
        // ADD YOUR "PLATE REMOVED" LOGIC HERE, e.g. a clang sound,
        // a light switching on behind the plate, or a hint that
        // something has appeared. You can also use the OnPlateReleased
        // event in the Inspector.
        // ============================================================

        if (revealWhenRemoved != null)
        {
            foreach (GameObject hidden in revealWhenRemoved)
                if (hidden != null) hidden.SetActive(true);
        }

        OnPlateReleased?.Invoke();

        if (!fallOff)
        {
            gameObject.SetActive(false);
            yield break;
        }

        // Let go: from now on the plate is a normal physics object.
        Rigidbody body = gameObject.AddComponent<Rigidbody>();
        body.mass = mass;
        body.useGravity = true;
        body.isKinematic = false;

        Camera cam = Camera.main;
        if (cam != null && popOutSpeed > 0f)
        {
            Vector3 towardPlayer = cam.transform.position - transform.position;
            towardPlayer.y = 0f;
            if (towardPlayer.sqrMagnitude > 0.0001f)
                body.AddForce(towardPlayer.normalized * popOutSpeed, ForceMode.VelocityChange);
        }

        if (removeAfterSeconds > 0f)
        {
            yield return new WaitForSeconds(removeAfterSeconds);
            gameObject.SetActive(false);
        }
    }
}