using TMPro;
using UnityEngine;

// Ставит в TMP_Text строку по ключу при включении объекта (для статичных текстов сцены и префабов).
[RequireComponent(typeof(TMP_Text))]
public class LocalizedText : MonoBehaviour
{
    public string locKey;

    void OnEnable()
    {
        GetComponent<TMP_Text>().text = Loc.Get(locKey);
    }
}
