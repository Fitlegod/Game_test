using UnityEngine;
using UnityEngine.UI;

[RequireComponent(typeof(Slider))]
public class HPBarDisplay : MonoBehaviour
{
    public Combatant target;
    private Slider slider;

    void Start()
    {
        slider = GetComponent<Slider>();
        slider.maxValue = target.maxHP;
    }

    void Update()
    {
        slider.value = target.CurrentHP;
    }
}