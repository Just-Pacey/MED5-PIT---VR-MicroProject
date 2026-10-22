using UnityEngine;

public class FollowActiveSource : MonoBehaviour
{
    public Transform controller; // e.g. Left Controller
    public Transform hand;       // e.g. Left Hand wrist/palm (optional)

    void LateUpdate()
    {
        Transform src = controller.gameObject.activeInHierarchy ? controller
                      : (hand != null && hand.gameObject.activeInHierarchy ? hand : null);
        if (src == null) return;
        transform.SetPositionAndRotation(src.position, src.rotation);
    }
}