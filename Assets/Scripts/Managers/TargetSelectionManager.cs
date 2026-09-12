using UnityEngine;

public class TargetSelectionManager : MonoBehaviour
{
    public static TargetSelectionManager Instance { get; private set; }

    private Card pendingCard;
    private bool inputLocked;

    void Awake()
    {
        Instance = this;
    }

    public void LockInput()
    {
        inputLocked = true;
    }

    public void SelectCard(Card card)
    {
        if (inputLocked) return;

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
        if (inputLocked) return;

        if (pendingCard != null && pendingCard.IsValidTarget(target))
        {
            pendingCard.Play(target);
            pendingCard = null;
        }
    }
}