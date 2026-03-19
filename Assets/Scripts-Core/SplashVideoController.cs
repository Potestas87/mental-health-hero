using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.Video;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

public class SplashVideoController : MonoBehaviour
{
    [Header("References")]
    public VideoPlayer videoPlayer;
    public CanvasGroup fadeOverlay;

    [Header("Flow")]
    public string nextSceneName = "BootstrapScene";
    [Min(0f)] public float minimumSplashSeconds = 1.5f;
    [Min(0.05f)] public float fadeOutSeconds = 0.6f;
    public bool allowTapToSkip = true;

    private bool _loading;
    private bool _videoFinished;
    private float _startTime;

    private void Start()
    {
        _startTime = Time.unscaledTime;

        if (fadeOverlay != null)
        {
            fadeOverlay.alpha = 0f;
            fadeOverlay.blocksRaycasts = false;
            fadeOverlay.interactable = false;
        }

        if (videoPlayer != null)
        {
            videoPlayer.loopPointReached += OnVideoFinished;
            if (!videoPlayer.isPlaying)
            {
                videoPlayer.Play();
            }
        }
        else
        {
            // No video assigned: continue after minimum delay.
            _videoFinished = true;
        }
    }

    private void Update()
    {
        if (_loading)
        {
            return;
        }

        if (allowTapToSkip && WasSkipPressed())
        {
            _videoFinished = true;
        }

        var minReached = Time.unscaledTime - _startTime >= minimumSplashSeconds;
        if (_videoFinished && minReached)
        {
            StartCoroutine(FadeAndLoadNextScene());
        }
    }


    private static bool WasSkipPressed()
    {
#if ENABLE_INPUT_SYSTEM
        if (Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame)
        {
            return true;
        }

        if (Touchscreen.current != null && Touchscreen.current.primaryTouch.press.wasPressedThisFrame)
        {
            return true;
        }

        if (Keyboard.current != null && Keyboard.current.anyKey.wasPressedThisFrame)
        {
            return true;
        }

        return false;
#else
        return Input.GetMouseButtonDown(0) || Input.touchCount > 0;
#endif
    }

    private void OnVideoFinished(VideoPlayer source)
    {
        _videoFinished = true;
    }

    private IEnumerator FadeAndLoadNextScene()
    {
        _loading = true;

        if (fadeOverlay != null)
        {
            fadeOverlay.blocksRaycasts = true;
            var elapsed = 0f;
            while (elapsed < fadeOutSeconds)
            {
                elapsed += Time.unscaledDeltaTime;
                fadeOverlay.alpha = Mathf.Clamp01(elapsed / fadeOutSeconds);
                yield return null;
            }

            fadeOverlay.alpha = 1f;
        }

        if (videoPlayer != null)
        {
            videoPlayer.Stop();
        }

        SceneManager.LoadScene(nextSceneName);
    }

    private void OnDestroy()
    {
        if (videoPlayer != null)
        {
            videoPlayer.loopPointReached -= OnVideoFinished;
        }
    }
}
