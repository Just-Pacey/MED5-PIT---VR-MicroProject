using System.Collections;
using UnityEngine;

public class CablePowerFlow : MonoBehaviour
{
    [Header("Cable")]
    [SerializeField] private CableSegment[] cableSegments;

    [Header("Timing")]
    [SerializeField] private float timePerSegment = 0.25f;

    [Header("Box")]
    [SerializeField] private ScrewdriverBox screwdriverBox;

    private bool hasActivated = false;

    public void Activate()
    {
        if (hasActivated)
            return;

        hasActivated = true;

        StartCoroutine(PowerFlow());
    }

    private IEnumerator PowerFlow()
    {
        for (int i = 0; i < cableSegments.Length; i++)
        {
            // Turn previous segment off
            if (i > 0)
                cableSegments[i - 1].SetPowered(false);

            // Turn current segment on
            cableSegments[i].SetPowered(true);

            yield return new WaitForSeconds(timePerSegment);
        }

        // Turn off the final segment
        cableSegments[cableSegments.Length - 1].SetPowered(false);

        // Open the screwdriver box
        if (screwdriverBox != null)
            screwdriverBox.Open();
    }
}