using UnityEngine;
using UnityEngine.Video;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using System.Collections;

namespace EiraGame
{
    public class IntroVideo : MonoBehaviour
{
    public VideoPlayer videoPlayer;
    public string nextScene = EiraGame.EiraConst.Level1Scene;

    [Header("UI Skip Button")]
    public bool showSkipButton = true;
    public float buttonFadeInTime = 2f;

    private Button skipButton;
    private CanvasGroup buttonCanvasGroup;
    private bool buttonVisible = false;
    private bool menuShown = false;

    void Start()
    {
        EiraGame.IntroGate.IntroPlayed = true;
        videoPlayer.loopPointReached += VideoFinished;

        if (showSkipButton)
        {
            CreateSkipButton();
            StartCoroutine(ShowButtonAfterDelay());
        }
    }

    System.Collections.IEnumerator ShowButtonAfterDelay()
    {
        yield return new WaitForSecondsRealtime(1.5f);
        buttonVisible = true;
    }

    void CreateSkipButton()
    {
        var canvasGo = new GameObject("SkipButtonCanvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        var canvas = canvasGo.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 100;

        var scaler = canvasGo.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);

        var buttonGo = new GameObject("SkipButton", typeof(RectTransform), typeof(Button), typeof(Image));
        buttonGo.transform.SetParent(canvasGo.transform, false);

        var rt = buttonGo.GetComponent<RectTransform>();
        rt.anchorMin = new Vector2(1f, 0f);
        rt.anchorMax = new Vector2(1f, 0f);
        rt.pivot = new Vector2(1f, 0f);
        rt.anchoredPosition = new Vector2(-30f, 30f);
        rt.sizeDelta = new Vector2(180f, 50f);

        var image = buttonGo.GetComponent<Image>();
        image.color = new Color(0f, 0f, 0f, 0.5f);

        buttonCanvasGroup = buttonGo.AddComponent<CanvasGroup>();
        buttonCanvasGroup.alpha = 0f;

        skipButton = buttonGo.GetComponent<Button>();
        skipButton.onClick.AddListener(SkipVideo);

        var textGo = new GameObject("Text", typeof(RectTransform), typeof(Text));
        textGo.transform.SetParent(buttonGo.transform, false);

        var textRt = textGo.GetComponent<RectTransform>();
        textRt.anchorMin = Vector2.zero;
        textRt.anchorMax = Vector2.one;
        textRt.offsetMin = Vector2.zero;
        textRt.offsetMax = Vector2.zero;

        var text = textGo.GetComponent<Text>();
        text.text = "SALTAR INTRO";
        text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        text.fontSize = 20;
        text.alignment = TextAnchor.MiddleCenter;
        text.color = Color.white;
        text.raycastTarget = false;
    }

    void Update()
    {
        if (Input.GetKeyDown(KeyCode.Escape) || Input.GetKeyDown(KeyCode.Space))
        {
            SkipVideo();
        }

        if (showSkipButton && buttonCanvasGroup != null)
        {
            float targetAlpha = buttonVisible ? 1f : 0f;
            buttonCanvasGroup.alpha = Mathf.MoveTowards(buttonCanvasGroup.alpha, targetAlpha, Time.unscaledDeltaTime / buttonFadeInTime);
        }
    }

    void VideoFinished(VideoPlayer vp)
    {
        ShowLevelMenu();
    }

    public void SkipVideo()
    {
        ShowLevelMenu();
    }

    void LoadNext()
    {
        if (string.IsNullOrEmpty(nextScene))
            return;

        SceneManager.LoadScene(nextScene);
    }

    /// <summary>
    /// Muestra un menú de selección de nivel al terminar el tráiler (o al
    /// saltarlo). Permite elegir entre el Nivel 1 y el Nivel 2.
    /// </summary>
    void ShowLevelMenu()
    {
        if (menuShown)
            return;

        menuShown = true;

        if (skipButton != null)
            Destroy(skipButton.gameObject);

        if (videoPlayer != null)
            videoPlayer.Stop();

        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;

        if (FindAnyObjectByType<EventSystem>() == null)
        {
            var es = new GameObject("EventSystem", typeof(EventSystem), typeof(InputSystemUIInputModule));
            try { es.GetComponent<InputSystemUIInputModule>().AssignDefaultActions(); } catch { }
        }

        var canvasGo = new GameObject("LevelMenuCanvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        var canvas = canvasGo.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 150;

        var scaler = canvasGo.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);

        var font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");

        MakeLabel(canvasGo.transform, "E I R A", 96, new Vector2(0.5f, 0.8f), new Vector2(1400, 160), new Color(0.85f, 0.92f, 1f), font);
        MakeLabel(canvasGo.transform, "SELECCIONA UN NIVEL", 30, new Vector2(0.5f, 0.66f), new Vector2(1400, 60), new Color(0.6f, 0.72f, 0.88f), font);

        MakeMenuButton(canvasGo.transform, "NIVEL 1 · EL DESPERTAR", new Vector2(0.5f, 0.48f),
            () => SceneManager.LoadScene(EiraConst.Level1Scene), font);
        MakeMenuButton(canvasGo.transform, "NIVEL 2", new Vector2(0.5f, 0.36f),
            () => SceneManager.LoadScene(EiraConst.Level2Scene), font);
    }

    static Text MakeLabel(Transform parent, string text, int size, Vector2 anchor, Vector2 sizeDelta, Color color, Font font)
    {
        var go = new GameObject("Label", typeof(RectTransform), typeof(Text));
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
        t.alignment = TextAnchor.MiddleCenter;
        t.color = color;
        t.raycastTarget = false;

        return t;
    }

    void MakeMenuButton(Transform parent, string label, Vector2 anchor, UnityEngine.Events.UnityAction onClick, Font font)
    {
        var go = new GameObject("Button", typeof(RectTransform), typeof(Button), typeof(Image));
        go.transform.SetParent(parent, false);

        var rt = go.GetComponent<RectTransform>();
        rt.anchorMin = anchor;
        rt.anchorMax = anchor;
        rt.pivot = anchor;
        rt.sizeDelta = new Vector2(620, 80);

        var image = go.GetComponent<Image>();
        image.color = new Color(0.08f, 0.32f, 0.62f, 1f);

        var btn = go.GetComponent<Button>();
        btn.targetGraphic = image;
        btn.onClick.AddListener(onClick);

        MakeLabel(go.transform, label, 30, new Vector2(0.5f, 0.5f), new Vector2(620, 80), Color.white, font);
    }

    public void ShowSkipButton()
    {
        buttonVisible = true;
    }
}
}