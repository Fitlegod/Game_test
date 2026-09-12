using UnityEngine;
using TMPro;

public class EffectStacksDisplay : MonoBehaviour
{
    public Combatant target;
    public TMP_Text label;

    void Update()
    {
        string text = "";
        AppendIfPresent(ref text, "Сила", StatusEffectType.Strength);
        AppendIfPresent(ref text, "Слабость", StatusEffectType.Weak);
        AppendIfPresent(ref text, "Лечение", StatusEffectType.Regen);
        label.text = text;
    }

    private void AppendIfPresent(ref string text, string name, StatusEffectType type)
    {
        int stacks = target.GetStacks(type);
        if (stacks <= 0) return;
        if (text.Length > 0) text += " | ";
        text += name + " x" + stacks;
    }
}