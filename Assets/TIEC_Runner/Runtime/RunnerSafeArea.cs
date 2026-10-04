using UnityEngine;

namespace TIEC.Runner
{
    /// <summary>Constrains all screen UI to the tablet's safe area, including after rotation.</summary>
    [ExecuteAlways, DisallowMultipleComponent, RequireComponent(typeof(RectTransform))]
    public sealed class RunnerSafeArea : MonoBehaviour
    {
        private RectTransform rectTransform;
        private Rect previousSafeArea;
        private Vector2Int previousScreen;

        private void OnEnable()
        {
            rectTransform = GetComponent<RectTransform>();
            Apply();
        }

        private void Update()
        {
            if (previousSafeArea != Screen.safeArea || previousScreen.x != Screen.width || previousScreen.y != Screen.height)
                Apply();
        }

        private void Apply()
        {
            if (rectTransform == null || Screen.width <= 0 || Screen.height <= 0) return;
            previousSafeArea = Screen.safeArea;
            previousScreen = new Vector2Int(Screen.width, Screen.height);
            rectTransform.anchorMin = new Vector2(previousSafeArea.xMin / Screen.width, previousSafeArea.yMin / Screen.height);
            rectTransform.anchorMax = new Vector2(previousSafeArea.xMax / Screen.width, previousSafeArea.yMax / Screen.height);
            rectTransform.offsetMin = Vector2.zero;
            rectTransform.offsetMax = Vector2.zero;
        }
    }
}
