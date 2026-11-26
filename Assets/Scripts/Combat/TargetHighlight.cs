using UnityEngine;

public class TargetHighlight : MonoBehaviour
{
    [SerializeField] private GameObject highlightVisual;

    private void Awake()
    {
        Hide();
    }

    public void Show()
    {
        if (highlightVisual != null)
            highlightVisual.SetActive(true);
    }

    public void Hide()
    {
        if (highlightVisual != null)
            highlightVisual.SetActive(false);
    }
}