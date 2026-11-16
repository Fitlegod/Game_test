[System.Serializable]
public class CardInstance
{
    public CardData data;
    public CardPropertyFlags instanceProperties;

    public CardInstance(CardData data)
    {
        this.data = data;
    }

    public CardPropertyFlags EffectiveProperties =>
        data.properties | instanceProperties;

    public bool CanUpgrade =>
        data != null && data.upgradedVersion != null;

    public bool TryUpgrade()
    {
        if (!CanUpgrade)
            return false;

        data = data.upgradedVersion;
        return true;
    }

    public CardInstance CreateCopy()
    {
        return new CardInstance(data)
        {
            instanceProperties = instanceProperties
        };
    }
}
