using UnityEngine;

public class HandManager : MonoBehaviour
{
    public const int MaxHandSize = 7;
    public const int InitialHandSize = 4;
    public const float DrawIntervalSeconds = 1.5f;

    public static HandManager Instance { get; private set; }

    public RectTransform[] slots;
    public GameObject cardPrefab;
    public CombatManager combatManager;
    public DeckManager deckManager;

    private Card[] cardsInSlots = new Card[MaxHandSize];

    void Awake()
    {
        Instance = this;
    }

    void Start()
    {
        deckManager.StartCombat();

        for (int i = 0; i < InitialHandSize; i++)
            TryDrawToHand();

        var drawEvent = new PeriodicEffectEvent(combatManager.CurrentTime, DrawIntervalSeconds, () => TryDrawToHand());
        combatManager.RegisterScheduledEvent(drawEvent);
    }

    public bool TryDrawToHand()
    {
        CardInstance instance = deckManager.DrawCard();
        if (instance == null)
            return false;

        int emptySlot = FindFirstEmptySlot();
        if (emptySlot == -1)
        {
            Debug.Log("Рука полна — " + instance.data.cardName + " сразу уходит в сброс");
            deckManager.Discard(instance);
            return false;
        }

        GameObject cardObject = Instantiate(cardPrefab, slots[emptySlot]);
        cardObject.transform.localPosition = Vector3.zero;

        Card card = cardObject.GetComponent<Card>();
        card.instance = instance;
        card.combatManager = combatManager;
        if (card.nameLabel != null)
            card.nameLabel.text = instance.data.cardName;

        cardsInSlots[emptySlot] = card;
        return true;
    }

    public void OnCardPlayed(Card card)
    {
        deckManager.Discard(card.instance);
        RemoveFromHand(card);
    }

    public void RemoveFromHand(Card card)
    {
        int index = System.Array.IndexOf(cardsInSlots, card);
        if (index == -1) return;

        Destroy(card.gameObject);
        cardsInSlots[index] = null;

        for (int i = index; i < MaxHandSize - 1; i++)
        {
            if (cardsInSlots[i + 1] == null) continue;

            Card moving = cardsInSlots[i + 1];
            moving.transform.SetParent(slots[i], false);
            moving.transform.localPosition = Vector3.zero;

            cardsInSlots[i] = moving;
            cardsInSlots[i + 1] = null;
        }
    }

    private int FindFirstEmptySlot()
    {
        for (int i = 0; i < MaxHandSize; i++)
            if (cardsInSlots[i] == null)
                return i;
        return -1;
    }

    [ContextMenu("Тест: добрать в руку")]
    private void TestDraw() => TryDrawToHand();

    [ContextMenu("Тест: сыграть первую карту в руке")]
    private void TestPlayFirst()
    {
        foreach (var card in cardsInSlots)
        {
            if (card != null)
            {
                OnCardPlayed(card);
                break;
            }
        }
    }
}