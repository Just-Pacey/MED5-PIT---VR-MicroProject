using System.Collections;
using UnityEngine;

public class ScrewdriverBox : MonoBehaviour
{
    [SerializeField] private Transform lid;
    [SerializeField] private float openAngle = 90f;
    [SerializeField] private float openDuration = 1f;

    private bool isOpen = false;

    public void Open()
    {
        if (isOpen)
            return;

        isOpen = true;

        StartCoroutine(OpenLid());
    }

    private IEnumerator OpenLid()
    {
        Quaternion startRotation = lid.localRotation;
        Quaternion endRotation =
            startRotation * Quaternion.Euler(openAngle, 0f, 0f);

        float elapsed = 0f;

        while (elapsed < openDuration)
        {
            elapsed += Time.deltaTime;

            float t = elapsed / openDuration;

            lid.localRotation =
                Quaternion.Slerp(startRotation, endRotation, t);

            yield return null;
        }

        lid.localRotation = endRotation;
    }
}