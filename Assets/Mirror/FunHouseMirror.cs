using UnityEngine;

public class FunhouseMirror : MonoBehaviour
{
    [Header("Mirror")]
    [SerializeField] private Renderer mirrorRenderer;

    [Header("Reflection Camera")]
    [SerializeField] private Camera reflectionCamera;
    [SerializeField] private RenderTexture reflectionTexture;

    [Header("Distortion")]
    [SerializeField, Range(1f, 3f)]
    private float verticalStretch = 1.5f;

    private Material mirrorMaterial;

    private Camera mainCamera;

    private void Start()
    {
        mainCamera = Camera.main;

        if (mirrorRenderer == null)
            mirrorRenderer = GetComponent<Renderer>();

        mirrorMaterial = mirrorRenderer.material;

        // Give the mirror shader the RenderTexture.
        mirrorMaterial.SetTexture("_MainTex", reflectionTexture);

        // Set the initial distortion amount.
        mirrorMaterial.SetFloat("_VerticalStretch", verticalStretch);

        // Make sure the reflection camera uses our RenderTexture.
        reflectionCamera.targetTexture = reflectionTexture;
    }

    private void LateUpdate()
    {
        if (mainCamera == null)
            return;

        UpdateReflectionCamera();
    }

    private void UpdateReflectionCamera()
    {
        Transform mirrorTransform = transform;

        Vector3 mirrorNormal = mirrorTransform.forward;
        Vector3 mirrorPosition = mirrorTransform.position;

        // Position of the player camera relative to the mirror.
        Vector3 cameraOffset =
            mainCamera.transform.position - mirrorPosition;

        // Reflect the camera position across the mirror plane.
        float distance =
            Vector3.Dot(cameraOffset, mirrorNormal);

        Vector3 reflectedPosition =
            mainCamera.transform.position -
            2f * distance * mirrorNormal;

        reflectionCamera.transform.position = reflectedPosition;

        // Reflect the camera's forward direction.
        Vector3 forward = mainCamera.transform.forward;

        float forwardDistance =
            Vector3.Dot(forward, mirrorNormal);

        Vector3 reflectedForward =
            forward -
            2f * forwardDistance * mirrorNormal;

        // Reflect the camera's up direction.
        Vector3 up = mainCamera.transform.up;

        float upDistance =
            Vector3.Dot(up, mirrorNormal);

        Vector3 reflectedUp =
            up -
            2f * upDistance * mirrorNormal;

        reflectionCamera.transform.rotation =
            Quaternion.LookRotation(
                reflectedForward,
                reflectedUp
            );
    }
}