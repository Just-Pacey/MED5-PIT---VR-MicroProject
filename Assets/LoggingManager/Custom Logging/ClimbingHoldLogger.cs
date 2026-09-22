using System.Collections.Generic;
using System.Diagnostics;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
using UnityEngine.XR.Interaction.Toolkit.Interactors;

public class ClimbingHoldLogger : MonoBehaviour
{
    private XRBaseInteractable interactable;
    private LoggingManager loggingManager;

    private float grabTime;
    private bool isBeingClimbed;
    private Stopwatch stopwatch;

    private void Awake()
    {
        interactable = GetComponent<XRBaseInteractable>();
        loggingManager = GameObject.Find("Logging").GetComponent<LoggingManager>();
        loggingManager.CreateLog("Event", headers: new List<string>() {"ClimbingHoldName", "OnGrab", "OnRelease", "ClimbDuration"});
    }

    private void OnEnable()
    {
        interactable.selectEntered.AddListener(OnGrab);
        interactable.selectExited.AddListener(OnRelease);
    }

    private void OnDisable()
    {
        interactable.selectEntered.RemoveListener(OnGrab);
        interactable.selectExited.RemoveListener(OnRelease);
    }

    private void OnGrab(SelectEnterEventArgs args)
    {
        if (isBeingClimbed)
            return;
        
        isBeingClimbed = true;
        grabTime = Time.time;

        stopwatch = Stopwatch.StartNew();

        Dictionary<string, object> grabData = new Dictionary<string, object>()
        {
            {"ClimbingHoldName", gameObject.name},
            {"OnGrab", grabTime}
        };
        loggingManager.Log("Event", grabData);
    }

    private void OnRelease(SelectExitEventArgs args)
    {
        if (interactable.interactorsSelecting.Count > 0)
            return;

        if (!isBeingClimbed)
            return;

        stopwatch.Stop();

        float releaseTime = Time.time;
        float duration = stopwatch.ElapsedMilliseconds / 1000.0f;

        isBeingClimbed = false;

        Dictionary<string, object> releaseData = new Dictionary<string, object>()
        {
            {"ClimbingHoldName", gameObject.name},
            {"OnRelease", releaseTime},
            {"ClimbDuration", duration}
        };
        loggingManager.Log("Event", releaseData);
    }
}