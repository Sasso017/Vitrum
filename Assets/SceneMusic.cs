using UnityEngine;

/// <summary>
/// Imposta la musica della scena corrente.
/// Mettilo su un GameObject in ogni scena che deve avere una musica propria:
/// quando la scena si carica, il MusicPlayer passa a questo brano con una dissolvenza.
/// Se il brano è lo stesso di quello già in riproduzione, la musica continua senza ripartire.
/// </summary>
public class SceneMusic : MonoBehaviour
{
    [Tooltip("Brano da riprodurre in questa scena")]
    [SerializeField] private AudioClip music;

    [Tooltip("Se attivo, in questa scena la musica viene fermata (utile per cutscene silenziose)")]
    [SerializeField] private bool stopMusicInThisScene = false;

    private void Start()
    {
        if (MusicPlayer.Instance == null)
        {
            Debug.LogWarning("[SceneMusic] Nessun MusicPlayer trovato. " +
                             "Avvia il gioco dalla scena StartMenu, dove si trova il MusicPlayer.", this);
            return;
        }

        if (stopMusicInThisScene)
        {
            MusicPlayer.Instance.StopMusic();
            return;
        }

        if (music == null)
        {
            Debug.LogWarning("[SceneMusic] Nessun brano assegnato.", this);
            return;
        }

        MusicPlayer.Instance.PlayMusic(music);
    }
}