using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

[RequireComponent(typeof(AudioSource))]
public class HomeMusicPlayer : MonoBehaviour
{
    [Header("Music")]
    public AudioClip musicClip;
    [Range(0f, 1f)] public float volume = 0.5f;

    [Header("Scene Rules")]
    public string[] excludedScenes = { "DungeonScene", "OnboardingScene", "SplashScene", "BootstrapScene" };

    private static HomeMusicPlayer _instance;

    private AudioSource _audioSource;
    private HashSet<string> _excludedSceneSet;

    private void Awake()
    {
        if (_instance != null && _instance != this)
        {
            Destroy(gameObject);
            return;
        }

        _instance = this;
        DontDestroyOnLoad(gameObject);

        _audioSource = GetComponent<AudioSource>();
        _audioSource.playOnAwake = false;
        _audioSource.loop = true;
        _audioSource.volume = Mathf.Clamp01(volume);

        BuildExcludedSceneSet();
        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    private void Start()
    {
        ApplySceneRule(SceneManager.GetActiveScene().name);
    }

    private void OnDestroy()
    {
        if (_instance == this)
        {
            SceneManager.sceneLoaded -= OnSceneLoaded;
            _instance = null;
        }
    }

    private void OnValidate()
    {
        if (_audioSource != null)
        {
            _audioSource.volume = Mathf.Clamp01(volume);
        }

        BuildExcludedSceneSet();
    }

    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        ApplySceneRule(scene.name);
    }

    private void ApplySceneRule(string sceneName)
    {
        if (string.IsNullOrWhiteSpace(sceneName) || IsSceneExcluded(sceneName))
        {
            Stop();
            return;
        }

        Play();
    }

    private bool IsSceneExcluded(string sceneName)
    {
        if (_excludedSceneSet == null)
        {
            BuildExcludedSceneSet();
        }

        return _excludedSceneSet.Contains(sceneName.Trim());
    }

    private void BuildExcludedSceneSet()
    {
        _excludedSceneSet = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        if (excludedScenes == null)
        {
            return;
        }

        for (var i = 0; i < excludedScenes.Length; i++)
        {
            var name = excludedScenes[i];
            if (!string.IsNullOrWhiteSpace(name))
            {
                _excludedSceneSet.Add(name.Trim());
            }
        }
    }

    public void Play()
    {
        if (musicClip == null)
        {
            return;
        }

        _audioSource.volume = Mathf.Clamp01(volume);
        _audioSource.clip = musicClip;
        if (!_audioSource.isPlaying)
        {
            _audioSource.Play();
        }
    }

    public void Stop()
    {
        if (_audioSource != null && _audioSource.isPlaying)
        {
            _audioSource.Stop();
        }
    }
}
