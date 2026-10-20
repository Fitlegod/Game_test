using UnityEngine;

public class TargetSelectionManager : MonoBehaviour
{
    public static TargetSelectionManager Instance { get; private set; }

    public CombatManager combatManager;
    public Card PendingCard { get; private set; }
    private Combatant hoveredTarget;
    // Превью числа — только для живой цели, на которую выбранная карта вообще может быть сыграна (наведение на игрока превью не меняет)
    public Combatant HoveredTarget =>
        hoveredTarget != null && hoveredTarget.CurrentHP > 0 && (PendingCard == null || PendingCard.IsValidTarget(hoveredTarget)) ? hoveredTarget : null;

    private bool inputLocked;

    void Awake()
    {
        Instance = this;
    }

    public void LockInput()
    {
        inputLocked = true;
    }

    public void UnlockInput()
    {
        inputLocked = false;
    }

    public void SetHoveredTarget(Combatant target) => hoveredTarget = target;
    public void ClearHoveredTarget(Combatant target)
    {
        if (hoveredTarget == target) hoveredTarget = null;
    }

    public void SelectCard(Card card)
    {
        if (inputLocked) return;

        if (card.RequiresTarget)
        {
            PendingCard = card;
            Debug.Log(card.data.DisplayName + ": выбери цель");
        }
        else
        {
            PendingCard = null;
            card.Play(null);
        }
    }

    // «Подождать» — единая точка входа для клавиши и кнопки
    public void Wait()
    {
        if (inputLocked || PendingCard != null) return;
        combatManager.Wait();
    }

    public void CancelSelection()
    {
        PendingCard = null;
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
