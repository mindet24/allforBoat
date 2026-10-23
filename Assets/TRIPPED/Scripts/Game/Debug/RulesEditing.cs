using System;
using System.Collections.Generic;
using System.Globalization;
using System.Reflection;
using Trouble.Sim;

namespace Trouble.Game
{
    /// <summary>
    /// Reading and writing <see cref="RulesConfig"/> fields by name for the F1 panel (GDD v1.1 §4.4). Every public
    /// instance field is a rule, so a field added to RulesConfig appears in the panel without any change here.
    /// </summary>
    public static class RulesEditing
    {
        /// <summary>Every rule field of <see cref="RulesConfig"/>, in declaration order.</summary>
        public static FieldInfo[] Fields()
        {
            return typeof(RulesConfig).GetFields(BindingFlags.Public | BindingFlags.Instance);
        }

        /// <summary>The value of a field as text: numbers with '.', booleans as true/false.</summary>
        public static string ValueText(RulesConfig rules, FieldInfo field)
        {
            return ToText(field.GetValue(rules));
        }

        /// <summary>Sets a field from text. Returns false and an error text if the text does not fit the field type.</summary>
        public static bool TrySet(RulesConfig rules, string key, string text, out string error)
        {
            error = "";
            var field = typeof(RulesConfig).GetField(key, BindingFlags.Public | BindingFlags.Instance);
            if (field == null)
            {
                error = $"Unknown rule '{key}'.";
                return false;
            }
            try
            {
                field.SetValue(rules, Convert.ChangeType(text.Trim(), field.FieldType, CultureInfo.InvariantCulture));
                return true;
            }
            catch (Exception)
            {
                error = $"'{text}' is not a valid {field.FieldType.Name} for '{key}'.";
                return false;
            }
        }

        /// <summary>
        /// The overrides that turn <paramref name="baseline"/> into <paramref name="edited"/>: one entry for each
        /// field whose value differs. Used as a level's rulesOverride, so a level keeps the global rules it did not touch.
        /// </summary>
        public static List<RuleOverrideData> Diff(RulesConfig edited, RulesConfig baseline)
        {
            var list = new List<RuleOverrideData>();
            foreach (var f in Fields())
            {
                var a = f.GetValue(edited);
                var b = f.GetValue(baseline);
                if (!Equals(a, b)) list.Add(new RuleOverrideData { key = f.Name, value = ToText(a) });
            }
            return list;
        }

        private static string ToText(object value)
        {
            if (value is bool b) return b ? "true" : "false";
            if (value is float f) return f.ToString("R", CultureInfo.InvariantCulture);
            return Convert.ToString(value, CultureInfo.InvariantCulture);
        }
    }
}
