using System.Collections.Generic;
using UnityEngine;
using TMPro;

public class CardPreviewDisplay : MonoBehaviour
{
    public TMP_Text nameLabel;
    public TMP_Text effectLabel;
    public TMP_Text propertiesLabel;

    public void Show(CardInstance instance)
    {
        CardData data = instance.data;
        if (nameLabel != null) nameLabel.text = data.DisplayName;

        var lines = new List<string>();
        foreach (var action in data.instantActions)
            lines.Add(action.Describe(action.amount)); // статичное базовое число, БЕЗ пересчёта под игрока
        foreach (var effect in data.appliedEffects)
            lines.Add(effect.Describe());
        if (data.drawCardsOnPlay > 0)
            lines.Add(Loc.Format("card.draw", data.drawCardsOnPlay));
        foreach (var action in data.parryActions)
            lines.Add(Loc.Format("card.parry", action.Describe(action.amount)));
        foreach (var effect in data.parryEffects)
            lines.Add(Loc.Format("card.parry", effect.Describe()));
        if (effectLabel != null) effectLabel.text = string.Join("\n", lines);

        if (propertiesLabel != null)
        {
            var props = new List<string>();
            if ((instance.EffectiveProperties & CardPropertyFlags.Exhaust) != 0) props.Add(Loc.Get("card.property.exhaust"));
            if ((instance.EffectiveProperties & CardPropertyFlags.FrontOfDraw) != 0) props.Add(Loc.Get("card.property.frontOfDraw"));
            propertiesLabel.text = string.Join(", ", props);
        }
    }
}
