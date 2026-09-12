using UnityEngine;

public class TargetSelectionManager : MonoBehaviour
{
    public static TargetSelectionManager Instance { get; private set; }

    public Card PendingCard { get; private set; }
    public Combatant HoveredTarget { get; private set; }

    private bool inputLocked;

    void Awake()
    {
        Instance = this;
    }

    public void LockInput()
    {
        inputLocked = true;
    }

    public void SetHoveredTarget(Combatant target) => HoveredTarget = target;
    public void ClearHoveredTarget(Combatant target)
    {
        if (HoveredTarget == target) HoveredTarget = null;
    }

    public void SelectCard(Card card)
    {
        if (inputLocked) return;

        if (card.RequiresTarget)
        {
            PendingCard = card;
            Debug.Log(card.data.cardName + ": выбери цель");
        }
        else
        {
            PendingCard = null;
            card.Play(null);
        }
    }

    public void SelectTarget(Combatant target)
    {
        if (inputLocked) return;

        if (PendingCard != null && PendingCard.IsValidTarget(target))
        {
            PendingCard.Play(target);
            PendingCard = null;
        }
    }
}
