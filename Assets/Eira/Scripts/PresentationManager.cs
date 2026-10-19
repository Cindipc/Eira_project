using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;

namespace EiraGame
{
    public class PresentationManager : MonoBehaviour
    {
        Text titleText;
        Text storyText;
        Button playButton;
        Font cachedFont;

        void Start()
        {
            BuildUi();
        }

        void BuildUi()
        {
            if (FindAnyObjectByType<EventSystem>() == null)
            {
                var es = new GameObject("EventSystem", typeof(EventSystem), typeof(InputSystemUIInputModule));
                try { es.GetComponent<InputSystemUIInputModule>().AssignDefaultActions(); } catch { }
            }

            var canvasGo = new GameObject("PresentationCanvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            canvasGo.transform.SetParent(transform, false);
            var canvas = canvasGo.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 10;
            var scaler = canvasGo.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);

            var root = canvasGo.transform;
            cachedFont = TryFont();

            var center = new GameObject("Center", typeof(RectTransform));
            center.transform.SetParent(root, false);
            var crt = center.GetComponent<RectTransform>();
            crt.anchorMin = Vector2.zero;
            crt.anchorMax = Vector2.one;
            crt.offsetMin = Vector2.zero;
            crt.offsetMax = Vector2.zero;

            titleText = MakeText(center.transform, "E I R A", 120, TextAnchor.MiddleCenter, new Vector2(0.5f, 0.7f), new Vector2(1500, 200), new Color(0.85f, 0.92f, 1f), cachedFont);
            
            storyText = MakeText(center.transform, "Año 3000. Despierta en un mundo de metal y silencio.\nDescubre la verdad sobre tu pasado y escapa de las máquinas.", 34, TextAnchor.MiddleCenter, new Vector2(0.5f, 0.5f), new Vector2(1200, 300), new Color(0.93f, 0.95f, 1f), cachedFont);
            
            playButton = MakeButton(center.transform, "J U G A R", new Vector2(0.5f, 0.3f), () =>
            {
                SceneManager.LoadScene(EiraConst.Level1Scene);
            }, cachedFont);
        }

        Font TryFont()
        {
            try { return Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf"); } catch { }
            try { return Resources.GetBuiltinResource<Font>("Arial.ttf"); } catch { }
            return null;
        }

        Text MakeText(Transform parent, string text, int size, TextAnchor align, Vector2 anchor, Vector2 sizeDelta, Color color, Font font)
        {
            var go = new GameObject("Text", typeof(RectTransform), typeof(Text));
            go.transform.SetParent(parent, false);
            var rt = go.GetComponent<RectTransform>();
            rt.anchorMin = anchor;
            rt.anchorMax = anchor;
            rt.pivot = anchor;
            rt.sizeDelta = sizeDelta;
            var t = go.GetComponent<Text>();
            t.font = font;
            t.text = text;
            t.fontSize = size;
            t.alignment = align;
            t.color = color;
            t.raycastTarget = false;
            t.horizontalOverflow = HorizontalWrapMode.Wrap;
            t.verticalOverflow = VerticalWrapMode.Overflow;
            return t;
        }

        Button MakeButton(Transform parent, string label, Vector2 anchor, UnityEngine.Events.UnityAction onClick, Font font)
        {
            var go = new GameObject("Button", typeof(RectTransform), typeof(Button), typeof(Image));
            go.transform.SetParent(parent, false);
            var rt = go.GetComponent<RectTransform>();
            rt.anchorMin = anchor;
            rt.anchorMax = anchor;
            rt.pivot = anchor;
            rt.sizeDelta = new Vector2(400, 80);
            var image = go.GetComponent<Image>();
            image.color = new Color(0.08f, 0.32f, 0.62f, 1f);
            var btn = go.GetComponent<Button>();
            btn.targetGraphic = image;
            btn.onClick.AddListener(onClick);

            var child = new GameObject("Label", typeof(RectTransform), typeof(Text));
            child.transform.SetParent(go.transform, false);
            var crt = child.GetComponent<RectTransform>();
            crt.anchorMin = Vector2.zero;
            crt.anchorMax = Vector2.one;
            crt.offsetMin = Vector2.zero;
            crt.offsetMax = Vector2.zero;
            var t = child.GetComponent<Text>();
            t.font = font;
            t.text = label;
            t.fontSize = 32;
            t.alignment = TextAnchor.MiddleCenter;
            t.color = Color.white;
            return btn;
        }
    }
}
