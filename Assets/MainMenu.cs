using UnityEngine;
using UnityEngine.SceneManagement;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

/// <summary>
/// Menu principale: GIOCA avvia la partita con dissolvenza, OPZIONI apre il pannello delle impostazioni,
/// ESCI chiude il gioco. Collega le funzioni PlayGame, ApriOpzioni, ChiudiOpzioni e QuitGame
/// all'On Click dei rispettivi pulsanti.
/// </summary>
public class MainMenu : MonoBehaviour
{
    [Header("Scene")]
    [Tooltip("Scena da caricare premendo GIOCA. Se vuoto, carica la scena successiva nelle Build Settings")]
    [SerializeField] private string nomeScenaDaCaricare = "IntroCutscene";

    [Header("Pannelli (opzionali)")]
    [Tooltip("Il pannello con i pulsanti GIOCA / OPZIONI / ESCI")]
    [SerializeField] private GameObject pannelloPrincipale;
    [Tooltip("Il pannello delle impostazioni (slider del volume, ecc.)")]
    [SerializeField] private GameObject pannelloOpzioni;

    [Header("Audio (opzionale)")]
    [Tooltip("Suono riprodotto premendo GIOCA")]
    [SerializeField] private AudioClip suonoAvvio;

    private bool caricamentoAvviato = false;

    private void Start()
    {
        // Tornando al menu dalla partita: cursore libero e tempo di gioco normale
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
        Time.timeScale = 1f;

        if (pannelloPrincipale != null) pannelloPrincipale.SetActive(true);
        if (pannelloOpzioni != null) pannelloOpzioni.SetActive(false);
    }

    private void Update()
    {
        // Esc dalle opzioni riporta al menu principale
        if (pannelloOpzioni != null && pannelloOpzioni.activeSelf && EscPremuto())
            ChiudiOpzioni();
    }

    // ---------- Pulsanti ----------

    /// <summary>GIOCA: avvia la partita.</summary>
    public void PlayGame()
    {
        // Evita doppi click durante la dissolvenza
        if (caricamentoAvviato || SceneFader.InTransizione) return;

        string scena = ScenaDaCaricare();
        if (string.IsNullOrEmpty(scena))
        {
            Debug.LogError("[MainMenu] Nessuna scena da caricare: controlla il campo 'Nome Scena Da Caricare' e le Build Settings.", this);
            return;
        }

        caricamentoAvviato = true;

        if (suonoAvvio != null && SFXPlayer.Instance != null)
            SFXPlayer.Instance.Play(suonoAvvio);

        // Dissolvenza al nero, caricamento in sottofondo, dissolvenza in entrata
        SceneFader.Instance.CaricaScena(scena);
    }

    /// <summary>OPZIONI: apre il pannello delle impostazioni.</summary>
    public void ApriOpzioni()
    {
        if (pannelloOpzioni == null) return;
        if (pannelloPrincipale != null) pannelloPrincipale.SetActive(false);
        pannelloOpzioni.SetActive(true);
    }

    /// <summary>INDIETRO (dalle opzioni): torna al menu principale.</summary>
    public void ChiudiOpzioni()
    {
        if (pannelloOpzioni != null) pannelloOpzioni.SetActive(false);
        if (pannelloPrincipale != null) pannelloPrincipale.SetActive(true);

        // Salva subito le impostazioni del volume
        if (VolumeManager.Instance != null) VolumeManager.Instance.Save();
    }

    /// <summary>ESCI: chiude il gioco.</summary>
    public void QuitGame()
    {
        Debug.Log("Quit!");
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }

    // ---------- Utilità ----------

    private string ScenaDaCaricare()
    {
        if (!string.IsNullOrEmpty(nomeScenaDaCaricare))
            return nomeScenaDaCaricare;

        // Nessun nome indicato: la scena successiva nelle Build Settings
        int indice = SceneManager.GetActiveScene().buildIndex + 1;
        if (indice >= SceneManager.sceneCountInBuildSettings) return null;
        return System.IO.Path.GetFileNameWithoutExtension(SceneUtility.GetScenePathByBuildIndex(indice));
    }

    private bool EscPremuto()
    {
#if ENABLE_INPUT_SYSTEM
        return Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame;
#else
        return Input.GetKeyDown(KeyCode.Escape);
#endif
    }
}