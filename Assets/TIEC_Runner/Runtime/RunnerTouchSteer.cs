using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;

namespace TIEC.Runner
{
    /// <summary>Tracks each finger separately so releasing one finger never cancels another.</summary>
    [DisallowMultipleComponent]
    public sealed class RunnerTouchSteer : MonoBehaviour, IPointerDownHandler, IPointerUpHandler
    {
        [SerializeField] private RunnerInput input;
        [SerializeField, Range(-1f, 1f)] private float axis;
        [SerializeField] private UnityEngine.UI.Image background;
        [SerializeField] private Color idleColor = new Color(0.03f, 0.03f, 0.03f, 0.7f);
        [SerializeField] private Color heldColor = new Color(0.92f, 0.56f, 0.16f, 0.9f);
        private readonly HashSet<int> heldPointers = new HashSet<int>();

        public void Configure(RunnerInput controls, float direction, UnityEngine.UI.Image image)
        {
            input = controls;
            axis = Mathf.Clamp(direction, -1f, 1f);
            background = image;
            RefreshColor();
        }

        public void OnPointerDown(PointerEventData eventData)
        {
            if (input == null) return;
            heldPointers.Add(eventData.pointerId);
            input.SetTouchSteer(eventData.pointerId, axis);
            RefreshColor();
        }

        public void OnPointerUp(PointerEventData eventData)
        {
            heldPointers.Remove(eventData.pointerId);
            if (input != null) input.ReleaseTouchSteer(eventData.pointerId);
            RefreshColor();
        }

        private void OnDisable() => ReleaseAll();
        private void OnApplicationFocus(bool focus) { if (!focus) ReleaseAll(); }
        private void OnApplicationPause(bool paused) { if (paused) ReleaseAll(); }

        private void ReleaseAll()
        {
            if (input != null)
                foreach (int pointer in heldPointers) input.ReleaseTouchSteer(pointer);
            heldPointers.Clear();
            RefreshColor();
        }

        private void RefreshColor()
        {
            if (background != null) background.color = heldPointers.Count > 0 ? heldColor : idleColor;
        }
    }
}
