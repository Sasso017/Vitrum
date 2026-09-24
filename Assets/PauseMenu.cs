using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

/// <summary>
/// Menu di pausa: ferma il gioco, disattiva i controlli del giocatore (movimento e visuale),
/// sblocca il cursore per navigare nel menu e ripristina tutto alla ripresa.
/// Mettilo su un GameObject sempre attivo nella scena di gioco (es. sul Canvas del menu di pausa).
/// I pulsanti vengono collegati automaticamente: basta trascinarli nei campi dell'Inspector.
/// Altri script possono controllare PauseMenu.IsPaused per sapere se il gioco è in pausa.
/// </summary>
public class PauseMenu : MonoBehaviour
{
    public static bool IsPaused { get; private set; }

    [Header("Pannelli")]
    [Tooltip("Il pannello principale del menu di pausa")]
    [SerializeField] private GameObject pausePanel;
    [Tooltip("Il pannello impostazioni (con gli slider del volume)")]
    [SerializeField] private GameObject settingsPanel;

    [Header("Pulsanti del menu di pausa")]
    [Tooltip("CONTINUA: riprende il gioco")]
    [SerializeField] private Button continueButton;
    [Tooltip("OPZIONI: apre il pannello impostazioni")]
    [SerializeField] private Button optionsButton;
    [Tooltip("ESCI: torna al menu principale")]
    [SerializeField] private Button exitButton;

    [Header("Pulsanti del pannello impostazioni")]
    [Tooltip("Opzionale: pulsante INDIETRO dentro il SettingsPanel, torna al menu di pausa")]
    [SerializeField] private Button settingsBackButton;

    [Header("Controlli del giocatore")]
    [Tooltip("Trascina qui gli script che muovono il giocatore e la visuale: verranno disattivati durante la pausa")]
    [SerializeField] private Behaviour[] playerControls;

    [Header("Cursore")]
    [Tooltip("Se attivo, durante il gioco il cursore è bloccato al centro e nascosto (tipico della prima persona)")]
    [SerializeField] private bool lockCursorDuringGameplay = true;

    [Header("Scene")]
    [Tooltip("Nome esatto della scena del menu principale (deve essere nelle Build Settings)")]
    [SerializeField] private string mainMenuScene = "StartMenu";

#if !ENABLE_INPUT_SYSTEM
    [SerializeField] private KeyCode pauseKey = KeyCode.Escape;
#endif

    private void Awake()
    {
        // Collega i pulsanti alle funzioni via codice
        if (continueButton != null) continueButton.onClick.AddListener(Resume);
        if (optionsButton != null) optionsButton.onClick.AddListener(OpenSettings);
        if (exitButton != null) exitButton.onClick.AddListener(GoToMainMenu);
        if (settingsBackButton != null) settingsBackButton.onClick.AddListener(CloseSettings);
    }

    private void Start()
    {
        // Stato iniziale pulito, anche se si arriva da una scena lasciata in pausa
        IsPaused = false;
        Time.timeScale = 1f;

        if (pausePanel != null) pausePanel.SetActive(false);
        if (settingsPanel != null) settingsPanel.SetActive(false);

        SetPlayerControls(true);
        SetCursorForGameplay(true);
    }

    private void Update()
    {
        if (!PausePressed()) return;

        if (!IsPaused)
            Pause();
        else if (settingsPanel != null && settingsPanel.activeSelf)
            CloseSettings(); // Esc dalle impostazioni torna al menu di pausa
        else
            Resume();
    }

    // ---------- Azioni del menu ----------

    /// <summary>Mette il gioco in pausa e mostra il menu.</summary>
    public void Pause()
    {
        IsPaused = true;
        Time.timeScale = 0f;

        if (pausePanel != null) pausePanel.SetActive(true);
        if (settingsPanel != null) settingsPanel.SetActive(false);
        SetPlayerControls(false);
        SetCursorForGameplay(false);
    }

    /// <summary>CONTINUA: chiude il menu e riprende il gioco.</summary>
    public void Resume()
    {
        IsPaused = false;
        Time.timeScale = 1f;

        if (pausePanel != null) pausePanel.SetActive(false);
        if (settingsPanel != null) settingsPanel.SetActive(false);
        SetPlayerControls(true);
        SetCursorForGameplay(true);
    }

    /// <summary>OPZIONI: nasconde il menu di pausa e apre il pannello impostazioni.</summary>
    public void OpenSettings()
    {
        if (settingsPanel == null)
        {
            Debug.LogWarning("[PauseMenu] Nessun SettingsPanel assegnato.", this);
            return;
        }
        if (pausePanel != null) pausePanel.SetActive(false);
        settingsPanel.SetActive(true);
    }

    /// <summary>INDIETRO (dalle impostazioni): torna al menu di pausa.</summary>
    public void CloseSettings()
    {
        if (settingsPanel != null) settingsPanel.SetActive(false);
        if (pausePanel != null) pausePanel.SetActive(true);
    }

    /// <summary>ESCI: esce dalla scena di gioco e torna al menu principale.</summary>
    public void GoToMainMenu()
    {
        // Ripristina il tempo PRIMA di cambiare scena, altrimenti il menu resta "congelato"
        Time.timeScale = 1f;
        IsPaused = false;
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;

        if (!Application.CanStreamedLevelBeLoaded(mainMenuScene))
        {
            Debug.LogError($"[PauseMenu] La scena '{mainMenuScene}' non esiste o non è nelle Build Settings.", this);
            return;
        }
        SceneManager.LoadScene(mainMenuScene);
    }

    // ---------- Interni ----------

    private void SetPlayerControls(bool enabled)
    {
        if (playerControls == null) return;
        foreach (Behaviour control in playerControls)
        {
            if (control != null) control.enabled = enabled;
        }
    }

    private void SetCursorForGameplay(bool gameplay)
    {
        bool locked = gameplay && lockCursorDuringGameplay;
        Cursor.lockState = locked ? CursorLockMode.Locked : CursorLockMode.None;
        Cursor.visible = !locked;
    }

    private bool PausePressed()
    {
#if ENABLE_INPUT_SYSTEM
        return Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame;
#else
        return Input.GetKeyDown(pauseKey);
#endif
    }

    private void OnDestroy()
    {
        // Scollega i pulsanti
        if (continueButton != null) continueButton.onClick.RemoveListener(Resume);
        if (optionsButton != null) optionsButton.onClick.RemoveListener(OpenSettings);
        if (exitButton != null) exitButton.onClick.RemoveListener(GoToMainMenu);
        if (settingsBackButton != null) settingsBackButton.onClick.RemoveListener(CloseSettings);

        // Sicurezza: se la scena viene scaricata mentre è in pausa, il tempo non resta a 0
        if (IsPaused)
        {
            Time.timeScale = 1f;
            IsPaused = false;
        }
    }
}