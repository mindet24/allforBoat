using System;
using System.Collections.Generic;

namespace Trouble.Sim
{
    /// <summary>One appliance type. Mirrors an entry of Resources/Config/catalog.json.</summary>
    [Serializable]
    public sealed class ApplianceDef
    {
        public string id = "";
        public string name = "";
        public int load;
        /// <summary>true = progress drops to 0 whenever power is lost (R7).</summary>
        public bool fragile;
        public string shortLabel = "";
        /// <summary>Height of the sprite in the house, in metres. Read by the view.</summary>
        public float worldHeight = 0.6f;
        /// <summary>Day mode: how heavy it is to carry (0 = light); each point takes thrust from the jetpack.</summary>
        public int weight;
    }

    /// <summary>The whole catalog.json file.</summary>
    [Serializable]
    public sealed class CatalogData
    {
        public List<ApplianceDef> appliances = new List<ApplianceDef>();
    }

    /// <summary>Lookup over every appliance type.</summary>
    public sealed class Catalog
    {
        private readonly Dictionary<string, ApplianceDef> _byId = new Dictionary<string, ApplianceDef>(StringComparer.Ordinal);

        public IReadOnlyList<ApplianceDef> All { get; }

        public Catalog(CatalogData data)
        {
            All = data.appliances;
            foreach (var def in data.appliances) _byId[def.id] = def;
        }

        /// <summary>The type with this id, or null.</summary>
        public ApplianceDef Find(string id)
        {
            return id != null && _byId.TryGetValue(id, out var def) ? def : null;
        }
    }
}
