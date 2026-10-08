using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class DeckPileScreen : MonoBehaviour
{
    public Transform cardContainer;
    public GameObject cardPreviewPrefab;
    public DeckManager deckManager;

    public Button characterDeckTab;
    public Button drawPileTab;
    public Button discardPileTab;
    public Button exhaustPileTab;
    public Button closeButton;

    void Awake()
    {
        characterDeckTab.onClick.AddListener(() => ShowPile(deckManager.CharacterDeck));
        drawPileTab.onClick.AddListener(() => ShowPile(deckManager.DrawPile));
        discardPileTab.onClick.AddListener(() => ShowPile(deckManager.DiscardPile));
        exhaustPileTab.onClick.AddListener(() => ShowPile(deckManager.ExhaustPile));
        closeButton.onClick.AddListener(Close);
    }

    public void Open() => OpenToPile(PileKind.CharacterDeck);

    public void OpenToPile(PileKind pile)
    {
        switch (pile)
        {
            case PileKind.CharacterDeck: ShowPile(deckManager.CharacterDeck); break;
            case PileKind.Draw: ShowPile(deckManager.DrawPile); break;
            case PileKind.Discard: ShowPile(deckManager.DiscardPile); break;
            case PileKind.Exhaust: ShowPile(deckManager.ExhaustPile); break;
        }
        ScreenManager.Instance.OpenOverlay(gameObject);
    }

    public void Close() => ScreenManager.Instance.CloseOverlay(gameObject);

    private void ShowPile(IReadOnlyList<CardInstance> pile)
    {
        foreach (Transform child in cardContainer)
            Destroy(child.gameObject);
        foreach (var instance in pile)
        {
            GameObject obj = Instantiate(cardPreviewPrefab, cardContainer);
            obj.GetComponent<CardPreviewDisplay>().Show(instance);
        }
    }
}
