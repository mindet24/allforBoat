using System;
using System.Collections.Generic;

namespace Trouble.Sim
{
    [Serializable]
    public sealed class StringEntry
    {
        public string key = "";
        public string text = "";
    }

    /// <summary>The whole strings.json file. A list because JsonUtility cannot read dictionaries.</summary>
    [Serializable]
    public sealed class StringsData
    {
        public List<StringEntry> entries = new List<StringEntry>();
    }

    /// <summary>
    /// Every player-facing text, looked up by key. Text may contain {name} placeholders.
    /// A missing key returns the key itself, so a gap is visible on screen instead of blank.
    /// </summary>
    public sealed class Strings
    {
        private readonly Dictionary<string, string> _byKey = new Dictionary<string, string>(StringComparer.Ordinal);

        public Strings(StringsData data)
        {
            if (data == null) return;
            foreach (var e in data.entries) _byKey[e.key] = e.text;
        }

        public string Get(string key)
        {
            return key != null && _byKey.TryGetValue(key, out var text) ? text : key;
        }

        /// <summary>Text for <paramref name="key"/> with each {name} replaced by its value.</summary>
        public string Format(string key, params (string name, object value)[] args)
        {
            var text = Get(key);
            foreach (var (name, value) in args)
                text = text.Replace("{" + name + "}", Convert.ToString(value, System.Globalization.CultureInfo.InvariantCulture));
            return text;
        }
    }
}
