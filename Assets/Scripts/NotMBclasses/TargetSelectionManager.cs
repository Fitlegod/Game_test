using UnityEngine;

public class TargetSelectionManager : MonoBehaviour
{
    public static TargetSelectionManager Instance { get; private set; }

    private Card pendingCard;

    void Awake()
    {
        Instance = this;
    }

    public void SelectCard(Card card)
    {
        if (card.RequiresTarget)
        {
            pendingCard = card;
            Debug.Log(card.data.cardName + ": выбери цель");
        }
        else
        {
            pendingCard = null;
            card.Play(null);
        }
    }

    public void SelectTarget(Combatant target)
    {
        if (pendingCard != null && pendingCard.IsValidTarget(target))
        {
            pendingCard.Play(target);
            pendingCard = null;
        }
    }
}