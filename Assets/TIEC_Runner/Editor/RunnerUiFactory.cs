#if UNITY_EDITOR
using System;
using System.IO;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;

namespace TIEC.Runner
{
    /// <summary>Builds a fully bound uGUI hierarchy; runtime code never searches scene object names.</summary>
    public static class RunnerUiFactory
    {
        private static readonly Color White = new Color32(247, 244, 236, 255);
        private static readonly Color Orange = new Color32(234, 155, 60, 255);
        private static readonly Color Green = new Color32(89, 179, 101, 255);
        private static readonly Color Muted = new Color32(177, 184, 178, 255);
        private static Material titleMaterial;
        private static Material bodyMaterial;

        public static RunnerHud Build(Transform parent, RunnerSession session, RunnerInput input,
            TMP_FontAsset titleFont, TMP_FontAsset bodyFont)
        {
            if (parent == null || session == null || input == null)
                throw new ArgumentException("UI factory requires explicit parent, session and input references.");
            if (bodyFont == null) throw new ArgumentException("Assign the runner body TMP font before building UI.");
            if (titleFont == null) titleFont = bodyFont;
            titleMaterial = OutlineMaterial(titleFont, 0.2f);
            bodyMaterial = OutlineMaterial(bodyFont, 0.16f);
            EnsureEventSystem(parent);

            var canvasObject = new GameObject("RunnerCanvas", typeof(RectTransform), typeof(Canvas),
                typeof(UnityEngine.UI.CanvasScaler), typeof(UnityEngine.UI.GraphicRaycaster));
            canvasObject.SetActive(false);
            canvasObject.transform.SetParent(parent, false);
            var canvas = canvasObject.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 100;
            var scaler = canvasObject.GetComponent<UnityEngine.UI.CanvasScaler>();
            scaler.uiScaleMode = UnityEngine.UI.CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1600f, 1000f);
            scaler.screenMatchMode = UnityEngine.UI.CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = 0.5f;

            var safeArea = Rect("SafeArea", canvasObject.transform);
            Stretch(safeArea);
            safeArea.gameObject.AddComponent<RunnerSafeArea>();
            var ready = Screen("ReadyScreen", safeArea, 0.24f);
            var running = Screen("RunningScreen", safeArea, 0f);
            var results = Screen("ResultsScreen", safeArea, 0.4f);

            var readyCard = Card("ReadyCard", ready, new Vector2(760f, 610f));
            Text("Series", readyCard, bodyFont, "STREET CHALLENGE", 24f, Orange, 38f, 42f, 684f, 38f);
            Text("GameTitle", readyCard, titleFont, "Grove Run", 104f, White, 30f, 90f, 700f, 130f);
            var target = Text("Target", readyCard, bodyFont, "30 SANİYE / TEK HAK", 31f, Green, 38f, 236f, 684f, 50f);
            Text("Instructions", readyCard, bodyFont,
                "Bisiklet ileri gider. Sen yolu seç.\nEngellerden kaç. Yolun sonuna ulaş.\nHız yükselir; tek çarpışma oyunu bitirir.",
                24f, White, 44f, 300f, 672f, 112f);
            var readyStart = Button("StartGame", readyCard, bodyFont, "START GAME", new Rect(42f, 436f, 676f, 78f), Orange);
            Text("StartHint", readyCard, bodyFont, "Herhangi bir tuşa bas veya ekrana dokun", 21f, Muted, 30f, 534f, 700f, 48f);

            var topBar = Image("TopBar", running, new Color(0.02f, 0.025f, 0.02f, 0.62f));
            topBar.rectTransform.anchorMin = new Vector2(0f, 1f);
            topBar.rectTransform.anchorMax = new Vector2(1f, 1f);
            topBar.rectTransform.pivot = new Vector2(0.5f, 1f);
            topBar.rectTransform.sizeDelta = new Vector2(0f, 160f);
            var timer = Text("RunTime", topBar.transform, bodyFont, "00.00 / 30.00 s", 40f, White, 28f, 16f, 460f, 64f);
            timer.alignment = TextAlignmentOptions.Left;
            var speed = Text("Speed", topBar.transform, bodyFont, "0 KM/H", 40f, White, 0f, 16f, 350f, 64f);
            AnchorRight(speed.rectTransform, 28f, 16f, 350f, 64f);
            speed.alignment = TextAlignmentOptions.Right;
            var progress = Text("ProgressLabel", topBar.transform, bodyFont, "PARKUR %0", 19f, Orange, 30f, 86f, 700f, 25f);
            progress.alignment = TextAlignmentOptions.Left;
            var track = Image("ProgressTrack", topBar.transform, new Color(0f, 0f, 0f, 0.85f));
            TopStretch(track.rectTransform, 30f, 30f, 121f, 16f);
            var fill = Image("ProgressFill", track.transform, Green);
            Stretch(fill.rectTransform, new Vector2(3f, 3f), new Vector2(-3f, -3f));
            // A built-in white UI sprite makes Image.Type.Filled render consistently.
            fill.sprite = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/UISprite.psd");
            fill.type = UnityEngine.UI.Image.Type.Filled;
            fill.fillMethod = UnityEngine.UI.Image.FillMethod.Horizontal;
            fill.fillOrigin = 0;
            fill.fillAmount = 0f;

            TouchControl("SteerLeft", running, input, bodyFont, -1f, false);
            TouchControl("SteerRight", running, input, bodyFont, 1f, true);
            var controlsHint = Text("ControlsHint", running, bodyFont, "A / D   ·   ← / →\nveya yön tuşlarını basılı tut", 19f, White, 0f, 0f, 540f, 68f);
            controlsHint.rectTransform.anchorMin = controlsHint.rectTransform.anchorMax = new Vector2(0.5f, 0f);
            controlsHint.rectTransform.pivot = new Vector2(0.5f, 0f);
            controlsHint.rectTransform.anchoredPosition = new Vector2(0f, 27f);

            var resultCard = Card("ResultCard", results, new Vector2(760f, 790f));
            var title = Text("ResultTitle", resultCard, titleFont, "WASTED", 69f, Orange, 32f, 30f, 696f, 100f);
            var summary = Text("ResultSummary", resultCard, bodyFont, "ENGELE ÇARPTIN\n00.00 SANİYE", 27f, White, 36f, 142f, 688f, 87f);
            Text("ScoresTitle", resultCard, bodyFont, "EN İYİ 5 KOŞU", 25f, Green, 42f, 246f, 676f, 38f);
            var leaderboard = Text("ScoreTable", resultCard, bodyFont, "İlk skoru sen yaz.", 24f, White, 50f, 294f, 660f, 168f);
            leaderboard.alignment = TextAlignmentOptions.TopLeft;
            leaderboard.lineSpacing = 12f;
            Text("NameLabel", resultCard, bodyFont, "OYUNCU İSMİ", 18f, Muted, 42f, 479f, 676f, 28f).alignment = TextAlignmentOptions.Left;
            var name = NameInput(resultCard, bodyFont, new Rect(42f, 516f, 456f, 66f));
            var save = Button("SaveScore", resultCard, bodyFont, "KAYDET", new Rect(514f, 516f, 204f, 66f), Green);
            var status = Text("SaveStatus", resultCard, bodyFont, "Skor kaydedildi.", 18f, Muted, 42f, 594f, 676f, 28f);
            var retry = Button("RestartGame", resultCard, bodyFont, "START GAME", new Rect(42f, 643f, 676f, 76f), Orange);
            Text("RetryHint", resultCard, bodyFont, "Tekrar oynamak için tuşa bas veya ekrana dokun", 18f, Muted, 30f, 737f, 700f, 34f);

            var hud = canvasObject.AddComponent<RunnerHud>();
            hud.Configure(session, input, ready.gameObject, running.gameObject, results.gameObject,
                timer, speed, progress, fill, target, title, summary, leaderboard, name, status,
                readyStart, retry, save);
            ready.gameObject.SetActive(true);
            running.gameObject.SetActive(false);
            results.gameObject.SetActive(false);
            canvasObject.SetActive(true);
            return hud;
        }

        private static void EnsureEventSystem(Transform parent)
        {
            EventSystem primary = null;
            var systems = UnityEngine.Object.FindObjectsByType<EventSystem>(FindObjectsInactive.Include);
            foreach (var system in systems)
            {
                if (system.gameObject.scene != parent.gameObject.scene) continue;
                if (primary == null) primary = system;
                else system.enabled = false;
            }
            if (primary == null)
            {
                var go = new GameObject("RunnerEventSystem", typeof(EventSystem));
                go.transform.SetParent(parent, false);
                primary = go.GetComponent<EventSystem>();
            }
            primary.gameObject.SetActive(true);
            primary.enabled = true;
            primary.sendNavigationEvents = true;
            foreach (var oldModule in primary.GetComponents<BaseInputModule>()) oldModule.enabled = false;
            var uiModule = primary.GetComponent<InputSystemUIInputModule>();
            if (uiModule == null) uiModule = primary.gameObject.AddComponent<InputSystemUIInputModule>();
            uiModule.enabled = true;
            uiModule.AssignDefaultActions();
        }

        private static Material OutlineMaterial(TMP_FontAsset font, float width)
        {
            var sourcePath = AssetDatabase.GetAssetPath(font);
            var directory = string.IsNullOrEmpty(sourcePath) ? "Assets" : Path.GetDirectoryName(sourcePath).Replace('\\', '/');
            if (directory.StartsWith("Packages/", StringComparison.Ordinal)) directory = "Assets";
            var path = $"{directory}/{font.name}_RunnerOutline.mat";
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material == null)
            {
                material = new Material(font.material) { name = font.name + " Runner Outline" };
                AssetDatabase.CreateAsset(material, path);
            }
            material.SetColor(ShaderUtilities.ID_OutlineColor, Color.black);
            material.SetFloat(ShaderUtilities.ID_OutlineWidth, width);
            material.EnableKeyword("OUTLINE_ON");
            EditorUtility.SetDirty(material);
            return material;
        }

        private static RectTransform Rect(string name, Transform parent)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            return go.GetComponent<RectTransform>();
        }

        private static RectTransform Screen(string name, Transform parent, float shade)
        {
            var image = Image(name, parent, new Color(0.01f, 0.012f, 0.01f, shade));
            Stretch(image.rectTransform);
            return image.rectTransform;
        }

        private static RectTransform Card(string name, Transform parent, Vector2 size)
        {
            var image = Image(name, parent, new Color(0.015f, 0.02f, 0.017f, 0.88f));
            var rect = image.rectTransform;
            rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = Vector2.zero;
            rect.sizeDelta = size;
            var stripe = Image("AccentStripe", rect, Orange);
            TopStretch(stripe.rectTransform, 0f, 0f, 0f, 5f);
            return rect;
        }

        private static UnityEngine.UI.Image Image(string name, Transform parent, Color color)
        {
            var rect = Rect(name, parent);
            var image = rect.gameObject.AddComponent<UnityEngine.UI.Image>();
            image.color = color;
            image.raycastTarget = false;
            return image;
        }

        private static TextMeshProUGUI Text(string name, Transform parent, TMP_FontAsset font,
            string value, float size, Color color, float x, float y, float width, float height)
        {
            var rect = Rect(name, parent);
            Place(rect, new Rect(x, y, width, height));
            var text = rect.gameObject.AddComponent<TextMeshProUGUI>();
            text.font = font;
            text.fontSharedMaterial = size >= 60f ? titleMaterial : bodyMaterial;
            text.text = value;
            text.fontSize = size;
            text.fontStyle = FontStyles.Bold;
            text.color = color;
            text.alignment = TextAlignmentOptions.Center;
            text.enableAutoSizing = true;
            text.fontSizeMin = size * 0.75f;
            text.fontSizeMax = size;
            text.textWrappingMode = TextWrappingModes.NoWrap;
            text.overflowMode = TextOverflowModes.Ellipsis;
            text.richText = false;
            text.raycastTarget = false;
            text.extraPadding = true;
            return text;
        }

        private static UnityEngine.UI.Button Button(string name, Transform parent, TMP_FontAsset font,
            string label, Rect bounds, Color accent)
        {
            var image = Image(name, parent, accent);
            Place(image.rectTransform, bounds);
            image.raycastTarget = true;
            var button = image.gameObject.AddComponent<UnityEngine.UI.Button>();
            button.targetGraphic = image;
            var colors = button.colors;
            colors.normalColor = Color.white;
            colors.highlightedColor = new Color(1f, 1f, 1f, 0.88f);
            colors.pressedColor = new Color(0.66f, 0.66f, 0.66f, 1f);
            colors.selectedColor = Color.white;
            colors.disabledColor = new Color(0.35f, 0.35f, 0.35f, 1f);
            button.colors = colors;
            var text = Text("Label", image.transform, font, label, 31f, White, 12f, 0f, bounds.width - 24f, bounds.height);
            Stretch(text.rectTransform, new Vector2(12f, 0f), new Vector2(-12f, 0f));
            return button;
        }

        private static TMP_InputField NameInput(Transform parent, TMP_FontAsset font, Rect bounds)
        {
            var image = Image("PlayerNameInput", parent, new Color(0.08f, 0.09f, 0.08f, 0.98f));
            Place(image.rectTransform, bounds);
            image.raycastTarget = true;
            var viewport = Rect("TextArea", image.transform);
            Stretch(viewport, new Vector2(16f, 8f), new Vector2(-16f, -8f));
            viewport.gameObject.AddComponent<UnityEngine.UI.RectMask2D>();
            var value = Text("Value", viewport, font, "OYUNCU", 28f, White, 0f, 0f, bounds.width - 32f, bounds.height - 16f);
            Stretch(value.rectTransform);
            value.alignment = TextAlignmentOptions.Left;
            value.enableAutoSizing = false;
            var placeholder = Text("Placeholder", viewport, font, "İsmini yaz", 26f, Muted, 0f, 0f, bounds.width - 32f, bounds.height - 16f);
            Stretch(placeholder.rectTransform);
            placeholder.alignment = TextAlignmentOptions.Left;
            var field = image.gameObject.AddComponent<TMP_InputField>();
            field.targetGraphic = image;
            field.textViewport = viewport;
            field.textComponent = value;
            field.placeholder = placeholder;
            field.characterLimit = 16;
            field.contentType = TMP_InputField.ContentType.Standard;
            field.lineType = TMP_InputField.LineType.SingleLine;
            field.richText = false;
            field.caretWidth = 3;
            field.customCaretColor = true;
            field.caretColor = Orange;
            field.selectionColor = new Color(0.3f, 0.5f, 0.32f, 0.6f);
            field.text = "OYUNCU";
            return field;
        }

        private static void TouchControl(string name, Transform parent, RunnerInput input, TMP_FontAsset font,
            float axis, bool right)
        {
            var image = Image(name, parent, new Color(0.03f, 0.03f, 0.03f, 0.7f));
            var rect = image.rectTransform;
            rect.anchorMin = rect.anchorMax = new Vector2(right ? 1f : 0f, 0f);
            rect.pivot = new Vector2(right ? 1f : 0f, 0f);
            rect.sizeDelta = new Vector2(170f, 150f);
            rect.anchoredPosition = new Vector2(right ? -30f : 30f, 28f);
            image.raycastTarget = true;
            var stripe = Image("Accent", rect, Orange);
            TopStretch(stripe.rectTransform, 0f, 0f, 0f, 4f);
            var label = Text("Direction", rect, font, right ? ">\nSAĞ" : "<\nSOL", 42f, White, 0f, 0f, 170f, 150f);
            Stretch(label.rectTransform, new Vector2(8f, 7f), new Vector2(-8f, -7f));
            image.gameObject.AddComponent<RunnerTouchSteer>().Configure(input, axis, image);
        }

        private static void Place(RectTransform rect, Rect bounds)
        {
            rect.anchorMin = rect.anchorMax = new Vector2(0f, 1f);
            rect.pivot = new Vector2(0f, 1f);
            rect.anchoredPosition = new Vector2(bounds.x, -bounds.y);
            rect.sizeDelta = bounds.size;
        }

        private static void AnchorRight(RectTransform rect, float margin, float top, float width, float height)
        {
            rect.anchorMin = rect.anchorMax = new Vector2(1f, 1f);
            rect.pivot = new Vector2(1f, 1f);
            rect.anchoredPosition = new Vector2(-margin, -top);
            rect.sizeDelta = new Vector2(width, height);
        }

        private static void TopStretch(RectTransform rect, float left, float right, float top, float height)
        {
            rect.anchorMin = new Vector2(0f, 1f);
            rect.anchorMax = new Vector2(1f, 1f);
            rect.pivot = new Vector2(0.5f, 1f);
            rect.offsetMin = new Vector2(left, -top - height);
            rect.offsetMax = new Vector2(-right, -top);
        }

        private static void Stretch(RectTransform rect)
            => Stretch(rect, Vector2.zero, Vector2.zero);

        private static void Stretch(RectTransform rect, Vector2 minimum, Vector2 maximum)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.offsetMin = minimum;
            rect.offsetMax = maximum;
        }
    }
}
#endif
