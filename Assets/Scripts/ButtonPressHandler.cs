using UnityEngine;
using UnityEngine.Events;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;

[RequireComponent(typeof(XRSimpleInteractable))]
public class ButtonPressHandler : MonoBehaviour
{
    [Header("References")]
    [Tooltip("Auto-filled from this GameObject if left empty.")]
    [SerializeField] private XRSimpleInteractable interactable;

    [Header("Events")]
    public UnityEvent OnButtonPressed;
    public UnityEvent OnButtonReleased;

    /// <summary>True while the button is currently held down.</summary>
    public bool IsPressed { get; private set; }

    private void Awake()
    {
        if (interactable == null)
            interactable = GetComponent<XRSimpleInteractable>();
    }

    private void OnEnable()
    {
        interactable.selectEntered.AddListener(HandleSelectEntered);
        interactable.selectExited.AddListener(HandleSelectExited);
    }

    private void OnDisable()
    {
        interactable.selectEntered.RemoveListener(HandleSelectEntered);
        interactable.selectExited.RemoveListener(HandleSelectExited);
    }

    private void HandleSelectEntered(SelectEnterEventArgs args)
    {
        IsPressed = true;
        HandleButtonPressed();
        OnButtonPressed?.Invoke();
    }

    private void HandleSelectExited(SelectExitEventArgs args)
    {
        IsPressed = false;
        HandleButtonReleased();
        OnButtonReleased?.Invoke();
    }

    private void HandleButtonPressed()
    {
        Debug.Log($"{name} was pressed.");

        // Unlock ScrewDriver
    }

    private void HandleButtonReleased()
    {
        Debug.Log($"{name} was released.");

        // TODO: your release logic here
    }
}