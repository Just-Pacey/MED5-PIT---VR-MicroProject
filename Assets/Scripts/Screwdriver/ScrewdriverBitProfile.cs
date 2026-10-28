using UnityEngine;

// Create one asset per bit via: Assets > Create > Screwdriver > Bit Profile
// e.g. "Phillips_Small", "Phillips_Large", "Flathead_Small", "Hex_Medium"
[CreateAssetMenu(fileName = "NewBitProfile", menuName = "Screwdriver/Bit Profile")]
public class ScrewdriverBitProfile : ScriptableObject
{
    [Tooltip("Shown in any UI / debug logs.")]
    public string bitName = "New Bit";

    [Tooltip("The visual mesh prefab swapped onto the screwdriver's head mount when this bit is active.")]
    public GameObject headVisualPrefab;

    [Tooltip("Optional icon if you build a UI to show which bit is equipped.")]
    public Sprite icon;
}
