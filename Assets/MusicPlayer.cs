using System.Collections;
using UnityEngine;
using UnityEngine.Audio;

/// <summary>
/// Riproduce la musica di sottofondo. Persiste tra le scene, quindi la musica
/// non si interrompe quando si passa dal menu al gioco.
/// Per cambiare brano da un altro script: MusicPlayer.Instance.PlayMusic(nuovaClip);
/// </summary>
public class MusicPlayer : MonoBehaviour
{
    public static MusicPlayer Instance { get; private set; }

    [Header("Mixer")]
    [Tooltip("Trascina qui il gruppo Music dell'AudioMixer")]
    [SerializeField] private AudioMixerGroup musicGroup;

    [Header("Musica")]
    [Tooltip("Brano che parte all'avvio (può essere lasciato vuoto)")]
    [SerializeField] private AudioClip startingMusic;
    [SerializeField, Range(0f, 1f)] private float musicVolume = 1f;
    [Tooltip("Durata della dissolvenza quando si cambia brano (secondi)")]
    [SerializeField] private float fadeDuration = 1f;

    private AudioSource source;
    private Coroutine fadeRoutine;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
        DontDestroyOnLoad(gameObject);

        source = gameObject.AddComponent<AudioSource>();
        source.outputAudioMixerGroup = musicGroup;
        source.loop = true;
        source.playOnAwake = false;
        source.volume = musicVolume;

        if (musicGroup == null)
            Debug.LogWarning("[MusicPlayer] Nessun gruppo Music assegnato: lo slider Musica non avrà effetto.", this);
    }

    private void Start()
    {
        if (startingMusic != null)
            PlayMusic(startingMusic);
    }

    /// <summary>Avvia un brano. Se è già in riproduzione non fa nulla.</summary>
    public void PlayMusic(AudioClip clip)
    {
        if (clip == null) return;
        if (source.clip == clip && source.isPlaying) return;

        if (fadeRoutine != null) StopCoroutine(fadeRoutine);
        fadeRoutine = StartCoroutine(SwitchTrack(clip));
    }

    /// <summary>Ferma la musica con dissolvenza.</summary>
    public void StopMusic()
    {
        if (fadeRoutine != null) StopCoroutine(fadeRoutine);
        fadeRoutine = StartCoroutine(SwitchTrack(null));
    }

    private IEnumerator SwitchTrack(AudioClip newClip)
    {
        // Dissolvenza in uscita del brano attuale
        if (source.isPlaying)
            yield return Fade(source.volume, 0f);

        source.Stop();
        source.clip = newClip;

        if (newClip == null) yield break;

        // Dissolvenza in entrata del nuovo brano
        source.volume = 0f;
        source.Play();
        yield return Fade(0f, musicVolume);
        fadeRoutine = null;
    }

    private IEnumerator Fade(float from, float to)
    {
        if (fadeDuration <= 0f)
        {
            source.volume = to;
            yield break;
        }

        float t = 0f;
        while (t < fadeDuration)
        {
            // unscaledDeltaTime: la dissolvenza funziona anche col gioco in pausa
            t += Time.unscaledDeltaTime;
            source.volume = Mathf.Lerp(from, to, t / fadeDuration);
            yield return null;
        }
        source.volume = to;
    }
}