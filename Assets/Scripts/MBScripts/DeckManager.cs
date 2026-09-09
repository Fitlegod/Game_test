using System.Collections.Generic;
using UnityEngine;

public class DeckManager : MonoBehaviour
{
    public List<CardInstance> characterDeck = new List<CardInstance>();

    private List<CardInstance> drawPile = new List<CardInstance>();
    private List<CardInstance> discardPile = new List<CardInstance>();
    private List<CardInstance> exhaustPile = new List<CardInstance>();

    public void StartCombat()
    {
        drawPile = new List<CardInstance>(characterDeck);
        Shuffle(drawPile);
        discardPile.Clear();
        exhaustPile.Clear();
        Debug.Log("Колода добора собрана: " + drawPile.Count + " карт");
    }

    private void Shuffle(List<CardInstance> list)
    {
        for (int i = list.Count - 1; i > 0; i--)
        {
            int j = Random.Range(0, i + 1);
            (list[i], list[j]) = (list[j], list[i]);
        }
    }

    public CardInstance DrawCard()
    {
        if (drawPile.Count == 0)
        {
            if (discardPile.Count == 0)
            {
                Debug.Log("Нечего добирать — и добор, и сброс пусты");
                return null;
            }
            drawPile.AddRange(discardPile);
            discardPile.Clear();
            Shuffle(drawPile);
            Debug.Log("Колода добора пуста — сброс перемешан в добор (" + drawPile.Count + " карт)");
        }

        CardInstance drawn = drawPile[drawPile.Count - 1];
        drawPile.RemoveAt(drawPile.Count - 1);
        Debug.Log("Добрана карта: " + drawn.data.cardName);
        return drawn;
    }

    public void Discard(CardInstance instance)
    {
        if ((instance.EffectiveProperties & CardPropertyFlags.Exhaust) != 0)
        {
            exhaustPile.Add(instance);
            Debug.Log(instance.data.cardName + " уходит в сжигание");
        }
        else
        {
            discardPile.Add(instance);
            Debug.Log(instance.data.cardName + " уходит в сброс");
        }
    }

    [ContextMenu("Тест: собрать колоду добора")]
    private void TestStartCombat() => StartCombat();

    [ContextMenu("Тест: добрать карту")]
    private void TestDrawCard() => DrawCard();
}