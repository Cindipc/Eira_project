using UnityEngine;
using UnityEngine.Video;
using UnityEngine.SceneManagement;

public class IntroVideo : MonoBehaviour
{
    public VideoPlayer videoPlayer;
    public string nextScene = "MainMenu";

    void Start()
    {
        videoPlayer.loopPointReached += VideoFinished;
    }

    void Update()
    {
        if (Input.GetKeyDown(KeyCode.Escape) ||
            Input.GetKeyDown(KeyCode.Space))
        {
            SkipVideo();
        }
    }

    void VideoFinished(VideoPlayer vp)
    {
        SceneManager.LoadScene(nextScene);
    }

    void SkipVideo()
    {
        SceneManager.LoadScene(nextScene);
    }
}