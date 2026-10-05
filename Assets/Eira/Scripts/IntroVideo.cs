using UnityEngine;
using UnityEngine.Video;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
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
        LoadNext();
    }

    public void SkipVideo()
    {
        LoadNext();
    }

    void LoadNext()
    {
        if (string.IsNullOrEmpty(nextScene))
            return;

        SceneManager.LoadScene(nextScene);
    }

    public void ShowSkipButton()
    {
        buttonVisible = true;
    }
}
}