using UnityEngine;
using UnityEngine.UI;
using TMPro;

public enum PileKind { CharacterDeck, Draw, Discard, Exhaust }

public class DeckPileCountDisplay : MonoBehaviour
{
    public DeckManager deckManager;
    public PileKind pile;
    public TMP_Text countLabel;
    public Button button;
    public DeckPileScreen screen;

    void Start()
    {
        button.onClick.AddListener(() => screen.OpenToPile(pile));
    }

    void Update()
    {
        int count = pile switch
        {
            PileKind.CharacterDeck => deckManager.CharacterDeck.Count,
            PileKind.Draw => deckManager.DrawPile.Count,
            PileKind.Discard => deckManager.DiscardPile.Count,
            PileKind.Exhaust => deckManager.ExhaustPile.Count,
            _ => 0
        };
        if (countLabel != null) countLabel.text = count.ToString();
    }
}
