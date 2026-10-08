using System.Collections.Generic;
using UnityEngine;

// Таблица строк: Assets/Resources/Strings/ru.txt, строка «ключ = текст», комментарии с #, "\n" в тексте — перенос.
public static class Loc
{
    public const string ResourcePath = "Strings/ru";

    private static Dictionary<string, string> table;
    private static readonly HashSet<string> warned = new HashSet<string>();

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void Reset()
    {
        table = null;
        warned.Clear();
    }

    public static string Get(string key)
    {
        if (EnsureLoaded() && table.TryGetValue(key ?? "", out string value)) return value;
        if (warned.Add(key ?? ""))
            Debug.LogWarning("Loc: нет строки для ключа [" + key + "]");
        return "[" + key + "]";
    }

    public static string Format(string key, params object[] args) => string.Format(Get(key), args);

    public static bool Has(string key) => EnsureLoaded() && table.ContainsKey(key ?? "");

    // Разбирает текст файла в таблицу. Возвращает список ошибок (неверный формат, повтор ключа).
    public static List<string> Parse(string text, Dictionary<string, string> into)
    {
        var errors = new List<string>();
        string[] lines = text.Split('\n');
        for (int i = 0; i < lines.Length; i++)
        {
            string line = lines[i].Trim();
            if (line.Length == 0 || line[0] == '#') continue;
            int eq = line.IndexOf('=');
            string key = eq > 0 ? line.Substring(0, eq).Trim() : "";
            if (key.Length == 0)
            {
                errors.Add("строка " + (i + 1) + ": неверный формат («ключ = текст»): " + line);
                continue;
            }
            if (into.ContainsKey(key))
            {
                errors.Add("строка " + (i + 1) + ": повтор ключа " + key);
                continue;
            }
            into[key] = line.Substring(eq + 1).Trim().Replace("\n", "\n");
        }
        return errors;
    }

    private static bool EnsureLoaded()
    {
        if (table != null) return true;
        var asset = Resources.Load<TextAsset>(ResourcePath);
        if (asset == null)
        {
            Debug.LogError("Loc: нет файла Resources/" + ResourcePath + ".txt");
            return false;
        }
        var loaded = new Dictionary<string, string>();
        foreach (string error in Parse(asset.text, loaded))
            Debug.LogError("Loc: ru.txt, " + error);
        table = loaded;
        return true;
    }
}
