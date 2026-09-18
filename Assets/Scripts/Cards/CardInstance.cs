[System.Serializable]
public class CardInstance
{
    public CardData data;
    public CardPropertyFlags instanceProperties;

    public CardInstance(CardData data)
    {
        this.data = data;
    }

    public CardPropertyFlags EffectiveProperties => data.properties | instanceProperties;
}