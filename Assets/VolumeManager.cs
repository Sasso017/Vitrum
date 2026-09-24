using UnityEngine;
using UnityEngine.Audio;

/// <summary>
/// Gestisce il volume generale (Master) e quello della Musica tramite un AudioMixer.
/// I valori (0-1) vengono salvati in PlayerPrefs e persistono tra le scene.
/// Mettilo su un GameObject nella prima scena del gioco (es. il Main Menu).
/// </summary>
[DefaultExecutionOrder(-100)] // Si inizializza prima degli slider
public class VolumeManager : MonoBehaviour
{
    public static VolumeManager Instance { get; private set; }

    [Header("Audio Mixer")]
    [SerializeField] private AudioMixer audioMixer;
    [Tooltip("Nome del parametro esposto per il gruppo Master")]
    [SerializeField] private string masterParameter = "MasterVolume";
    [Tooltip("Nome del parametro esposto per il gruppo Music")]
    [SerializeField] private string musicParameter = "MusicVolume";

    [Header("Valori di default (0-1)")]
    [SerializeField, Range(0f, 1f)] private float defaultMasterVolume = 1f;
    [SerializeField, Range(0f, 1f)] private float defaultMusicVolume = 0.8f;

    private const string MasterKey = "Volume_Master";
    private const string MusicKey = "Volume_Music";
    private const float MinDecibel = -80f;

    public float MasterVolume { get; private set; }
    public float MusicVolume { get; private set; }

    private void Awake()
    {
        // Singleton persistente tra le scene
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
        DontDestroyOnLoad(gameObject);

        if (audioMixer == null)
            Debug.LogError("[VolumeManager] Nessun AudioMixer assegnato!", this);

        // Carica i valori salvati (o i default)
        MasterVolume = PlayerPrefs.GetFloat(MasterKey, defaultMasterVolume);
        MusicVolume = PlayerPrefs.GetFloat(MusicKey, defaultMusicVolume);
    }

    private void Start()
    {
        // AudioMixer.SetFloat chiamato in Awake a volte viene ignorato da Unity,
        // quindi applichiamo i valori in Start.
        ApplyToMixer(masterParameter, MasterVolume);
        ApplyToMixer(musicParameter, MusicVolume);
    }

    public void SetMasterVolume(float value)
    {
        MasterVolume = Mathf.Clamp01(value);
        ApplyToMixer(masterParameter, MasterVolume);
        PlayerPrefs.SetFloat(MasterKey, MasterVolume);
    }

    public void SetMusicVolume(float value)
    {
        MusicVolume = Mathf.Clamp01(value);
        ApplyToMixer(musicParameter, MusicVolume);
        PlayerPrefs.SetFloat(MusicKey, MusicVolume);
    }

    /// <summary>Scrive su disco i PlayerPrefs (chiamalo alla chiusura del menu impostazioni).</summary>
    public void Save()
    {
        PlayerPrefs.Save();
    }

    /// <summary>Ripristina i valori di default.</summary>
    public void ResetToDefaults()
    {
        SetMasterVolume(defaultMasterVolume);
        SetMusicVolume(defaultMusicVolume);
        Save();
    }

    private void ApplyToMixer(string parameter, float linearValue)
    {
        if (audioMixer == null) return;

        if (!audioMixer.SetFloat(parameter, LinearToDecibel(linearValue)))
            Debug.LogWarning($"[VolumeManager] Parametro '{parameter}' non trovato nell'AudioMixer. Controlla di averlo esposto.", this);
    }

    // Lo slider è lineare (0-1), il mixer lavora in decibel (logaritmico)
    private static float LinearToDecibel(float value)
    {
        return value <= 0.0001f ? MinDecibel : Mathf.Log10(value) * 20f;
    }

    private void OnApplicationQuit()
    {
        Save();
    }
}