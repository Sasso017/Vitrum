using System.Collections;
using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

/// <summary>
/// Chiave ricompensa in stile Super Mario 64: alla vittoria di un enigma emerge da un punto
/// (es. il centro del tavolo) ruotando velocemente, poi resta sospesa girando e ondeggiando.
/// Il giocatore la raccoglie avvicinandosi e premendo E: solo in quel momento la chiave
/// viene assegnata nel GameManager.
///
/// Struttura consigliata:
///     ChiaveRicompensa (questo script, GameObject vuoto posizionato al centro del tavolo)
///     └── Modello (il modello 3D della chiave, orientato in piedi)
/// </summary>
public class ChiaveRicompensa : MonoBehaviour
{
    private enum Stato { Nascosta, Comparsa, Sospesa, Raccolta }

    [Header("Enigma")]
    [Tooltip("L'enigma di cui questa chiave è la ricompensa")]
    [SerializeField] private Enigma enigma = Enigma.Gioco15;

    [Header("Modello")]
    [Tooltip("Il modello 3D della chiave (figlio di questo oggetto)")]
    [SerializeField] private Transform modello;

    [Header("Comparsa")]
    [Tooltip("Attesa prima della comparsa (lascia finire la dissolvenza di ritorno in prima persona)")]
    [SerializeField] private float ritardoComparsa = 0.8f;
    [Tooltip("Di quanto sale la chiave rispetto al punto di partenza (metri)")]
    [SerializeField] private float altezzaSalita = 0.4f;
    [SerializeField] private float durataSalita = 1.5f;
    [Tooltip("Velocità di rotazione iniziale, mentre emerge (gradi al secondo)")]
    [SerializeField] private float rotazioneIniziale = 1080f;
    [Tooltip("Velocità di rotazione mentre resta sospesa (gradi al secondo)")]
    [SerializeField] private float rotazioneSospesa = 120f;
    [SerializeField] private AudioClip suonoComparsa;

    [Header("Galleggiamento")]
    [Tooltip("Ampiezza dell'oscillazione su e giù (metri)")]
    [SerializeField] private float ampiezzaOndeggio = 0.03f;
    [SerializeField] private float velocitaOndeggio = 2f;

    [Header("Luce (opzionale)")]
    [Tooltip("Una Point Light figlia della chiave: si accende durante la comparsa")]
    [SerializeField] private Light luce;
    [SerializeField] private float intensitaLuce = 1.5f;

    [Header("Raccolta")]
    [SerializeField] private float distanzaRaccolta = 2f;
    [Tooltip("Il giocatore deve guardare verso la chiave entro questo angolo (gradi)")]
    [SerializeField] private float angoloVisuale = 50f;
    [Tooltip("Camera del giocatore (Main Camera di Carlo): la chiave vola verso di lei")]
    [SerializeField] private Camera cameraGiocatore;
    [Tooltip("Suggerimento mostrato in basso quando il giocatore è vicino (lascia vuoto per non mostrarlo)")]
    [SerializeField] private string testoSuggerimento = "Premi E per raccogliere la chiave";
    [Tooltip("Messaggio mostrato al centro dopo la raccolta (lascia vuoto per non mostrarlo)")]
    [SerializeField] private string testoRaccolta = "Hai ottenuto la chiave!";
    [SerializeField] private float durataMessaggio = 3f;
    [SerializeField] private AudioClip suonoRaccolta;
#if !ENABLE_INPUT_SYSTEM
    [SerializeField] private KeyCode tastoRaccogli = KeyCode.E;
#endif

    private Stato stato = Stato.Nascosta;
    private Transform giocatore;
    private Vector3 posizioneBase;       // Posizione di partenza (sul tavolo)
    private Vector3 posizioneSospesa;    // Posizione a fine salita
    private Vector3 scalaModello;
    private float velocitaRotazioneAttuale;
    private float tempoOndeggio;

    private void Start()
    {
        GameObject playerObj = GameObject.FindGameObjectWithTag("Player");
        if (playerObj != null) giocatore = playerObj.transform;

        posizioneBase = transform.position;
        posizioneSospesa = posizioneBase + Vector3.up * altezzaSalita;

        if (modello != null)
        {
            scalaModello = modello.localScale;
            modello.gameObject.SetActive(false);
        }
        else
        {
            Debug.LogError("[ChiaveRicompensa] Nessun modello della chiave assegnato.", this);
        }

        if (luce != null) { luce.intensity = 0f; luce.enabled = false; }
    }

    private void Update()
    {
        if (stato == Stato.Nascosta || modello == null) return;

        // Rotazione continua sull'asse verticale
        transform.Rotate(Vector3.up, velocitaRotazioneAttuale * Time.deltaTime, Space.World);

        if (stato != Stato.Sospesa) return;

        // Ondeggia su e giù
        tempoOndeggio += Time.deltaTime * velocitaOndeggio;
        transform.position = posizioneSospesa + Vector3.up * Mathf.Sin(tempoOndeggio) * ampiezzaOndeggio;

        // Raccolta
        bool puoInteragire = !PauseMenu.IsPaused && !SceneFader.InTransizione && !StazioneMinigioco.InUso;
        bool vicino = puoInteragire && GiocatoreVicino();

        if (vicino) MessaggiSchermo.MostraSuggerimento(this, testoSuggerimento);
        else MessaggiSchermo.NascondiSuggerimento(this);

        if (vicino && RaccogliPremuto())
            StartCoroutine(Raccogli());
    }

    // ---------- Comparsa ----------

    /// <summary>
    /// Fa emergere la chiave. Chiamato dalla StazioneMinigioco al ritorno in prima persona dopo la vittoria.
    /// </summary>
    public void Compari()
    {
        if (stato != Stato.Nascosta) return;

        // Se la chiave è già stata raccolta in precedenza, non ricompare
        if (GameManager.instance != null && GameManager.instance.IsCompletato(enigma)) return;

        StartCoroutine(SequenzaComparsa());
    }

    private IEnumerator SequenzaComparsa()
    {
        stato = Stato.Comparsa;
        yield return new WaitForSeconds(ritardoComparsa);

        transform.position = posizioneBase;
        modello.localScale = Vector3.zero;
        modello.gameObject.SetActive(true);

        if (luce != null) luce.enabled = true;
        if (suonoComparsa != null && SFXPlayer.Instance != null) SFXPlayer.Instance.Play(suonoComparsa);

        float t = 0f;
        while (t < durataSalita)
        {
            t += Time.deltaTime;
            float k = Mathf.Clamp01(t / durataSalita);

            // Sale rallentando verso la fine
            float salita = 1f - Mathf.Pow(1f - k, 3f);
            transform.position = Vector3.Lerp(posizioneBase, posizioneSospesa, salita);

            // Cresce con un piccolo rimbalzo nel primo terzo della salita
            float crescita = EaseOutBack(Mathf.Clamp01(k * 3f));
            modello.localScale = scalaModello * crescita;

            // La rotazione rallenta dalla velocità iniziale a quella costante
            velocitaRotazioneAttuale = Mathf.Lerp(rotazioneIniziale, rotazioneSospesa, salita);

            if (luce != null) luce.intensity = intensitaLuce * k;
            yield return null;
        }

        modello.localScale = scalaModello;
        velocitaRotazioneAttuale = rotazioneSospesa;
        tempoOndeggio = 0f;
        stato = Stato.Sospesa;
    }

    // ---------- Raccolta ----------

    private IEnumerator Raccogli()
    {
        stato = Stato.Raccolta;
        MessaggiSchermo.NascondiSuggerimento(this);

        // Adesso la chiave è davvero del giocatore
        if (GameManager.instance != null)
            GameManager.instance.CompletaEnigma(enigma);

        if (suonoRaccolta != null && SFXPlayer.Instance != null) SFXPlayer.Instance.Play(suonoRaccolta);

        // La chiave accelera e vola verso la camera rimpicciolendosi
        Vector3 partenza = transform.position;
        float durata = 0.6f;
        float t = 0f;
        float intensitaIniziale = luce != null ? luce.intensity : 0f;

        while (t < durata)
        {
            t += Time.deltaTime;
            float k = Mathf.Clamp01(t / durata);

            Vector3 destinazione = cameraGiocatore != null
                ? cameraGiocatore.transform.position + cameraGiocatore.transform.forward * 0.3f
                : partenza + Vector3.up * 0.5f;

            transform.position = Vector3.Lerp(partenza, destinazione, k * k);
            modello.localScale = scalaModello * (1f - k);
            velocitaRotazioneAttuale = Mathf.Lerp(rotazioneSospesa, rotazioneIniziale * 1.5f, k);
            if (luce != null) luce.intensity = intensitaIniziale * (1f - k);
            yield return null;
        }

        modello.gameObject.SetActive(false);
        if (luce != null) luce.enabled = false;
        velocitaRotazioneAttuale = 0f;

        MessaggiSchermo.MostraMessaggio(testoRaccolta, durataMessaggio);

        stato = Stato.Nascosta;
        enabled = false; // Chiave raccolta: lo script non serve più
    }

    // ---------- Utilità ----------

    private void OnDisable()
    {
        MessaggiSchermo.NascondiSuggerimento(this);
    }

    private bool GiocatoreVicino()
    {
        if (giocatore == null) return false;
        if (Vector3.Distance(giocatore.position, transform.position) > distanzaRaccolta) return false;

        Transform occhi = cameraGiocatore != null ? cameraGiocatore.transform : giocatore;
        Vector3 direzione = (transform.position - occhi.position).normalized;
        return Vector3.Angle(occhi.forward, direzione) <= angoloVisuale;
    }

    private bool RaccogliPremuto()
    {
#if ENABLE_INPUT_SYSTEM
        return Keyboard.current != null && Keyboard.current.eKey.wasPressedThisFrame;
#else
        return Input.GetKeyDown(tastoRaccogli);
#endif
    }

    private static float EaseOutBack(float x)
    {
        const float c1 = 1.70158f;
        const float c3 = c1 + 1f;
        return 1f + c3 * Mathf.Pow(x - 1f, 3f) + c1 * Mathf.Pow(x - 1f, 2f);
    }

    private void OnDrawGizmosSelected()
    {
        // Nell'Editor mostra il percorso di salita
        Vector3 inizio = Application.isPlaying ? posizioneBase : transform.position;
        Gizmos.color = new Color(1f, 0.85f, 0.2f);
        Gizmos.DrawLine(inizio, inizio + Vector3.up * altezzaSalita);
        Gizmos.DrawWireSphere(inizio + Vector3.up * altezzaSalita, 0.05f);
    }
}