using UnityEngine;
using UnityEngine.InputSystem;

public class ClickManager : MonoBehaviour
{
    private Camera mainCamera;
    private Card pendingCard;

    void Start()
    {
        mainCamera = Camera.main;
    }

    void Update()
    {
        if (!Mouse.current.leftButton.wasPressedThisFrame) return;

        Vector2 worldPoint = mainCamera.ScreenToWorldPoint(Mouse.current.position.ReadValue());
        Collider2D hit = Physics2D.OverlapPoint(worldPoint);
        if (hit == null) return;

        if (pendingCard != null)
        {
            Combatant target = hit.GetComponent<Combatant>();
            if (target != null && pendingCard.IsValidTarget(target))
            {
                pendingCard.Play(target);
                pendingCard = null;
                return;
            }
            // клик мимо валидной цели - выбор остаётся активным, пробуем ниже как клик по новой карте
        }

        Card card = hit.GetComponent<Card>();
        if (card != null)
        {
            if (card.RequiresTarget)
            {
                pendingCard = card;
                Debug.Log(card.cardName + ": выбери цель");
            }
            else
            {
                pendingCard = null;
                card.Play(null); // карты только с Block цель не используют
            }
        }
    }
}