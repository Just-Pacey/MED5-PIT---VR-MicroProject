using UnityEngine;

public class CableSegment : MonoBehaviour
{
    [SerializeField] private Renderer cableRenderer;

    [SerializeField] private Color normalColor = Color.black;
    [SerializeField] private Color poweredColor = Color.green;

    private void Awake()
    {
        if (cableRenderer == null)
            cableRenderer = GetComponent<Renderer>();

        SetPowered(false);
    }

    public void SetPowered(bool powered)
    {
        cableRenderer.material.color = powered
            ? poweredColor
            : normalColor;
    }
}