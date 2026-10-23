using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;

namespace Trouble.Game
{
    /// <summary>Tells whether a screen point is over HUD graphics, so clicks on the UI never reach the house.</summary>
    public static class UiHit
    {
        private static readonly List<RaycastResult> Hits = new List<RaycastResult>();
        private static PointerEventData _pointer;
        private static EventSystem _pointerOwner;

        /// <summary>True if any raycast-target UI graphic is under <paramref name="screenPosition"/>.</summary>
        public static bool IsOverUi(Vector2 screenPosition)
        {
            var es = EventSystem.current;
            if (es == null) return false;
            if (_pointer == null || _pointerOwner != es)
            {
                _pointer = new PointerEventData(es);
                _pointerOwner = es;
            }
            _pointer.position = screenPosition;
            Hits.Clear();
            es.RaycastAll(_pointer, Hits);
            return Hits.Count > 0;
        }
    }
}
