using Trouble.Sim;
using UnityEngine;

namespace Trouble.EditorTools
{
    /// <summary>Wraps rules.json, catalog.json and strings.json so the Level Editor can draw and undo them.</summary>
    public sealed class GlobalHolder : ScriptableObject
    {
        public RulesConfig rules = new RulesConfig();
        public CatalogData catalog = new CatalogData();
        public StringsData strings = new StringsData();
    }
}
