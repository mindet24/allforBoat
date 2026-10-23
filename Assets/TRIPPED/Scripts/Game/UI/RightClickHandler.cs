using UnityEngine;
using UnityEngine.EventSystems;

namespace Trouble.Game
{
    /// <summary>Calls <see cref="OnRight"/> when a UI element is right-clicked (uGUI buttons only react to the left button).</summary>
    public sealed class RightClickHandler : MonoBehaviour, IPointerClickHandler
    {
        /// <summary>What to do on a right click.</summary>
        public System.Action OnRight;

        public void OnPointerClick(PointerEventData eventData)
        {
            if (eventData.button == PointerEventData.InputButton.Right) OnRight?.Invoke();
        }
    }
}
