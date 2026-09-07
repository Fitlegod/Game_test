using UnityEngine;
using UnityEngine.InputSystem;

public class ClickManager : MonoBehaviour
{
    private Camera mainCamera;

    void Start()
    {
        mainCamera = Camera.main;
    }

    void Update()
    {
        if (Mouse.current.leftButton.wasPressedThisFrame)
        {
            Vector2 worldPoint = mainCamera.ScreenToWorldPoint(Mouse.current.position.ReadValue());
            Collider2D hit = Physics2D.OverlapPoint(worldPoint);
            if (hit != null)
            {
                Card card = hit.GetComponent<Card>();
                if (card != null)
                {
                    card.OnClicked();
                }
            }
        }
    }
}