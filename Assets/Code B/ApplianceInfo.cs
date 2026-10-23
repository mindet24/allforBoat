using System.Collections.Generic;
using UnityEngine;

namespace Trouble.Game
{
    public static class ApplianceInfo
    {
        public readonly struct Info
        {
            public readonly string Name, Short;
            public Info(string n, string s) { Name = n; Short = s; }
        }

        static readonly Dictionary<string, Info> map = new Dictionary<string, Info>
        {
            { "kettle", new Info("Kettle", "KTL") },
            { "washer", new Info("Washing machine", "WSH") },
            { "microwave", new Info("Microwave", "MWV") },
            { "dryer", new Info("Hair dryer", "DRY") },
            { "iron", new Info("Iron", "IRN") },
            { "toaster", new Info("Toaster", "TST") },
            { "console", new Info("Game console", "GAME") },
            { "fridge", new Info("Fridge", "FRG") },
            { "phone", new Info("Phone charger", "PHN") },
            { "lamp", new Info("Lamp", "LMP") },
            { "heater", new Info("Space heater", "HTR") },
            { "tv", new Info("TV", "TV") },
            { "ac", new Info("Air conditioner", "AC") },
        };

        public static Info Get(object type)
        {
            string k = (type == null ? "" : type.ToString()).ToLowerInvariant().TrimEnd('0', '1', '2', '3', '4', '5', '6', '7', '8', '9');
            if (map.TryGetValue(k, out var i)) return i;
            return new Info(k, k.ToUpperInvariant().Substring(0, Mathf.Min(3, k.Length)));
        }
    }
}