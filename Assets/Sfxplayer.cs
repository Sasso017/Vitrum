using UnityEngine;
using UnityEngine.Audio;

/// <summary>
/// Riproduce effetti sonori "2D" (UI, notifiche, suoni non posizionati nello spazio).
/// Da qualsiasi script: SFXPlayer.Instance.Play(clip);
/// </summary>
public class SFXPlayer : MonoBehaviour
{
    public static SFXPlayer Instance { get; private set; }

    [Tooltip("Trascina qui il gruppo SFX dell'AudioMixer")]
    [SerializeField] private AudioMixerGroup sfxGroup;

    private AudioSource source;

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
        source.outputAudioMixerGroup = sfxGroup;
        source.playOnAwake = false;
        source.loop = false;

        if (sfxGroup == null)
            Debug.LogWarning("[SFXPlayer] Nessun gruppo SFX assegnato.", this);
    }

    /// <summary>Riproduce un effetto sonoro. Più effetti possono sovrapporsi.</summary>
    public void Play(AudioClip clip, float volume = 1f)
    {
        if (clip == null) return;
        source.PlayOneShot(clip, Mathf.Clamp01(volume));
    }
}