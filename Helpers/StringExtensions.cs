using System.IO;
using System;
using System.Collections.Generic;
using PEAKLevelLoader.Core;

public static class StringExtensions
{
    public static string ReplaceInvalidFileNameChars(this string s, char replaceWith = '_')
    {
        if (string.IsNullOrEmpty(s)) return s ?? string.Empty;
        var invalid = Path.GetInvalidFileNameChars();
        var arr = s.ToCharArray();
        for (int i = 0; i < arr.Length; i++)
            if (Array.IndexOf(invalid, arr[i]) >= 0) arr[i] = replaceWith;
        return new string(arr);
    }
    public static string SanitizeJson(string txt)
    {
        if (string.IsNullOrEmpty(txt)) return txt;
        txt = txt.TrimStart('\uFEFF', '\u200B', '\u200E', '\u200F', '\u0000');
        int i = 0;
        while (i < txt.Length && char.IsControl(txt[i]) && txt[i] != '\r' && txt[i] != '\n' && txt[i] != '\t')
            i++;
        if (i > 0) txt = txt.Substring(i);
        return txt;
    }
    public static string ExtractTopLevelArray(string json, string key)
    {
        if (string.IsNullOrEmpty(json)) return string.Empty;
        int keyIndex = json.IndexOf($"\"{key}\"", StringComparison.OrdinalIgnoreCase);
        if (keyIndex < 0) return string.Empty;
        int bracketIndex = json.IndexOf('[', keyIndex);
        if (bracketIndex < 0) return string.Empty;

        int depth = 0;
        for (int i = bracketIndex; i < json.Length; i++)
        {
            if (json[i] == '[') depth++;
            else if (json[i] == ']') depth--;
            if (depth == 0) return json.Substring(bracketIndex, i - bracketIndex + 1);
        }
        return string.Empty;
    }
    public static List<string> SplitTopLevelObjects(string arrayText)
    {
        var outList = new List<string>();
        if (string.IsNullOrWhiteSpace(arrayText)) return outList;

        int i = 0;
        while (i < arrayText.Length && arrayText[i] != '[') i++;
        if (i >= arrayText.Length) return outList;
        i++;

        while (i < arrayText.Length)
        {
            while (i < arrayText.Length && (char.IsWhiteSpace(arrayText[i]) || arrayText[i] == ',')) i++;
            if (i >= arrayText.Length) break;
            if (arrayText[i] != '{') break;

            int start = i;
            int depth = 0;
            for (; i < arrayText.Length; i++)
            {
                if (arrayText[i] == '{') depth++;
                else if (arrayText[i] == '}') depth--;
                if (depth == 0) { i++; break; }
            }
            if (depth == 0)
                outList.Add(arrayText.Substring(start, i - start));
            else
                break;
        }

        return outList;
    }
}
