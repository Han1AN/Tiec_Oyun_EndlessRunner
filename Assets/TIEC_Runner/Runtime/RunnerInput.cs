using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;

namespace TIEC.Runner
{
    public sealed class RunnerInput : MonoBehaviour
    {
        [SerializeField] RunnerSession session;
        [SerializeField] RunnerHud hud;
        readonly Dictionary<int, float> touchAxes = new Dictionary<int, float>();
        readonly List<RaycastResult> uiHits = new List<RaycastResult>();
        public float Steer { get; private set; }
        public void Configure(RunnerSession owner, RunnerHud view) { session = owner; hud = view; }
        public void SetTouchSteer(int pointerId, float axis) { touchAxes[pointerId] = Mathf.Clamp(axis, -1, 1); }
        public void ReleaseTouchSteer(int pointerId) { touchAxes.Remove(pointerId); }
        public void ClearSteering() { touchAxes.Clear(); Steer = 0; }
        void Update()
        {
            if (session == null) return;
            var keyboard = Keyboard.current;
            float axis = 0;
            if (keyboard != null)
            {
                if (keyboard.aKey.isPressed || keyboard.leftArrowKey.isPressed) axis -= 1;
                if (keyboard.dKey.isPressed || keyboard.rightArrowKey.isPressed) axis += 1;
            }
            foreach (var value in touchAxes.Values) axis += value;
            Steer = session.State == RunState.Running ? Mathf.Clamp(axis, -1, 1) : 0;
            if (session.State == RunState.Running || session.ResultAge < .65f && session.State == RunState.Result) return;
            if (hud != null && hud.IsEditingName) return;
            bool key = keyboard != null && keyboard.anyKey.wasPressedThisFrame;
            bool pointer = Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame;
            bool touch = Touchscreen.current != null && Touchscreen.current.primaryTouch.press.wasPressedThisFrame;
            if (session.State == RunState.Ready && (key || pointer || touch)) session.StartRun();
            else if (session.State == RunState.Result)
            {
                bool nameSelected = EventSystem.current != null && EventSystem.current.currentSelectedGameObject != null
                    && EventSystem.current.currentSelectedGameObject.GetComponent<TMPro.TMP_InputField>() != null;
                Vector2 position = touch ? Touchscreen.current.primaryTouch.position.ReadValue()
                    : Mouse.current != null ? Mouse.current.position.ReadValue() : Vector2.zero;
                if ((key && !nameSelected) || ((pointer || touch) && !HitsInteractiveUI(position)))
                {
                    if (hud != null) hud.SaveAndRestart(); else session.StartRun();
                }
            }
        }
        bool HitsInteractiveUI(Vector2 position)
        {
            if (EventSystem.current == null) return false;
            uiHits.Clear();
            EventSystem.current.RaycastAll(new PointerEventData(EventSystem.current) { position = position }, uiHits);
            return uiHits.Count > 0;
        }
        void OnApplicationFocus(bool focus) { if (!focus) ClearSteering(); }
        void OnDisable() { ClearSteering(); }
    }
}
