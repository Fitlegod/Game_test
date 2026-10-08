using UnityEngine;

// Правила контекста для горячих клавиш (подписчик GameInput).
public class CombatHotkeys : MonoBehaviour
{
    public GameInput input;
    public HandManager handManager;
    public MapManager mapManager;
    public DeckPileScreen deckPileScreen;

    void OnEnable()
    {
        input.OnSlot += HandleSlot;
        input.OnCancel += HandleCancel;
        input.OnToggleMap += HandleToggleMap;
        input.OnToggleDeck += HandleToggleDeck;
    }

    void OnDisable()
    {
        input.OnSlot -= HandleSlot;
        input.OnCancel -= HandleCancel;
        input.OnToggleMap -= HandleToggleMap;
        input.OnToggleDeck -= HandleToggleDeck;
    }

    private static bool InCombat => ScreenManager.Instance.CurrentScreenId == ScreenManager.Combat;

    private void HandleSlot(int slot)
    {
        if (!InCombat) return;
        Card card = handManager.GetCardInSlot(slot);
        if (card != null) TargetSelectionManager.Instance.SelectCard(card);
    }

    private void HandleCancel()
    {
        if (TargetSelectionManager.Instance.PendingCard != null)
            TargetSelectionManager.Instance.CancelSelection();
        else
            ScreenManager.Instance.CloseTopOverlay();
    }

    private void HandleToggleMap()
    {
        if (InCombat) mapManager.ToggleMapView();
    }

    private void HandleToggleDeck()
    {
        if (!InCombat) return;
        if (ScreenManager.Instance.IsOverlayOpen(deckPileScreen.gameObject)) deckPileScreen.Close();
        else deckPileScreen.Open();
    }
}
