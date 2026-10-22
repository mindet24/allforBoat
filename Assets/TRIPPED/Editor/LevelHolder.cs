using Trouble.Sim;
using UnityEngine;

namespace Trouble.EditorTools
{
    /// <summary>Wraps one level so the Level Editor can draw and undo it with SerializedObject.</summary>
    public sealed class LevelHolder : ScriptableObject
    {
        public LevelData level = new LevelData();
    }
}
