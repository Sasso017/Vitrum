using System.Collections;
using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

/// <summary>
/// Cutscene di ingresso nella Basilica: la camera mostra la porta principale che si chiude,
/// compare il messaggio con l'obiettivo, poi il controllo passa al giocatore.
/// Viene riprodotta solo la prima volta: tornando dai minigiochi la porta è già chiusa.
/// Mettilo su un GameObject vuoto nella ScenaBasilica.
/// </summary>
[DefaultExecutionOrder(100)] // Parte dopo PauseMenu e PlayerSceneRestore, che impostano controlli e cursore
public class IntroBasilica : MonoBehaviour
{
    /// <summary>True mentre la cutscene è in corso (il menu di pausa è disabilitato).</summary>
    public static bool InCorso { get; private set; }

    [Header("Giocatore")]
    [Tooltip("Script da disattivare durante la cutscene (es. FPSController di Carlo)")]
    [SerializeField] private Behaviour[] controlliGiocatore;
    [Tooltip("La camera del giocatore (Main Camera dentro Carlo)")]
    [SerializeField] private Camera cameraGiocatore;

    [Header("Camera della cutscene")]
    [Tooltip("Camera dedicata alla cutscene, già posizionata per inquadrare la porta")]
    [SerializeField] private Camera cameraCutscene;
    [Tooltip("Opzionale: se assegnato, durante la cutscene la camera si sposta lentamente verso questo punto")]
    [SerializeField] private Transform puntoFinaleCamera;

    [Header("Porta")]
    [Tooltip("Anta sinistra: il suo pivot deve trovarsi sui cardini")]
    [SerializeField] private Transform antaSinistra;
    [Tooltip("Anta destra: il suo pivot deve trovarsi sui cardini (lascia vuoto se la porta ha un'anta sola)")]
    [SerializeField] private Transform antaDestra;
    [SerializeField] private Vector3 rotazioneApertaSinistra = new Vector3(0f, -90f, 0f);
    [SerializeField] private Vector3 rotazioneChiusaSinistra = Vector3.zero;
    [SerializeField] private Vector3 rotazioneApertaDestra = new Vector3(0f, 90f, 0f);
    [SerializeField] private Vector3 rotazioneChiusaDestra = Vector3.zero;

    [Header("Tempi (secondi)")]
    [Tooltip("Pausa prima che la porta inizi a chiudersi")]
    [SerializeField] private float attesaIniziale = 1f;
    [SerializeField] private float durataChiusura = 2f;
    [Tooltip("Quanto resta visibile il messaggio prima di passare al giocatore")]
    [SerializeField] private float durataMessaggio = 3.5f;

    [Header("Scossa della camera alla chiusura")]
    [SerializeField] private float intensitaScossa = 0.08f;
    [SerializeField] private float durataScossa = 0.35f;

    [Header("Audio (usa lo SFXPlayer)")]
    [Tooltip("Suono mentre la porta si muove (cigolio)")]
    [SerializeField] private AudioClip suonoCigolio;
    [Tooltip("Suono quando la porta sbatte")]
    [SerializeField] private AudioClip suonoChiusura;

    [Header("Messaggio")]
    [Tooltip("Testo con l'obiettivo, es. 'La porta si è chiusa alle tue spalle. Trova le tre chiavi per fuggire.'")]
    [SerializeField] private GameObject messaggioObiettivo;

    [Header("Salta cutscene")]
    [Tooltip("Permette di saltare la cutscene con Spazio, Invio o click")]
    [SerializeField] private bool consentiSalta = true;

    private Vector3 posizioneInizialeCamera;
    private Quaternion rotazioneInizialeCamera;
    private Vector3 offsetScossa = Vector3.zero;

    private void Start()
    {
        InCorso = false;
        if (cameraCutscene != null) cameraCutscene.enabled = false;
        if (messaggioObiettivo != null) messaggioObiettivo.SetActive(false);

        // Cutscene già vista (es. ritorno da un minigioco): porta chiusa e nient'altro
        GameManager gm = GameManager.instance;
        if (gm != null && gm.introBasilicaVista)
        {
            ImpostaPorta(0f);
            return;
        }

        StartCoroutine(Sequenza());
    }

    private void Update()
    {
        if (InCorso && consentiSalta && SaltaPremuto())
        {
            StopAllCoroutines();
            Termina();
        }
    }

    // ---------- Sequenza ----------

    private IEnumerator Sequenza()
    {
        InCorso = true;

        // Porta aperta e giocatore bloccato
        ImpostaPorta(1f);
        SetControlli(false);
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;

        // Passa alla camera della cutscene
        if (cameraCutscene != null)
        {
            posizioneInizialeCamera = cameraCutscene.transform.position;
            rotazioneInizialeCamera = cameraCutscene.transform.rotation;
            if (cameraGiocatore != null) cameraGiocatore.enabled = false;
            cameraCutscene.enabled = true;

            float durataTotale = attesaIniziale + durataChiusura + durataMessaggio;
            StartCoroutine(MuoviCamera(durataTotale));
        }

        yield return new WaitForSeconds(attesaIniziale);

        // Chiusura della porta: parte lenta e accelera, come se fosse spinta dal vento
        Suona(suonoCigolio);
        float t = 0f;
        while (t < durataChiusura)
        {
            t += Time.deltaTime;
            float k = Mathf.Clamp01(t / durataChiusura);
            ImpostaPorta(1f - k * k);
            yield return null;
        }
        ImpostaPorta(0f);

        // Tonfo finale
        Suona(suonoChiusura);
        StartCoroutine(Scossa());

        // Messaggio con l'obiettivo
        if (messaggioObiettivo != null) messaggioObiettivo.SetActive(true);
        yield return new WaitForSeconds(durataMessaggio);

        Termina();
    }

    /// <summary>Riporta tutto allo stato di gioco normale (fine cutscene o cutscene saltata).</summary>
    private void Termina()
    {
        // Ferma tutte le coroutine ancora attive (movimento camera, scossa)
        StopAllCoroutines();
        offsetScossa = Vector3.zero;

        ImpostaPorta(0f);
        if (messaggioObiettivo != null) messaggioObiettivo.SetActive(false);

        if (cameraCutscene != null)
        {
            cameraCutscene.enabled = false;
            cameraCutscene.transform.SetPositionAndRotation(posizioneInizialeCamera, rotazioneInizialeCamera);
        }
        if (cameraGiocatore != null) cameraGiocatore.enabled = true;

        SetControlli(true);

        if (GameManager.instance != null)
            GameManager.instance.introBasilicaVista = true;

        InCorso = false;
    }

    // ---------- Camera ----------

    private IEnumerator MuoviCamera(float durata)
    {
        Transform cam = cameraCutscene.transform;
        float t = 0f;

        while (true)
        {
            t += Time.deltaTime;
            Vector3 posizione = posizioneInizialeCamera;
            Quaternion rotazione = rotazioneInizialeCamera;

            // Movimento lento verso il punto finale (se assegnato)
            if (puntoFinaleCamera != null && durata > 0f)
            {
                float k = Mathf.SmoothStep(0f, 1f, t / durata);
                posizione = Vector3.Lerp(posizioneInizialeCamera, puntoFinaleCamera.position, k);
                rotazione = Quaternion.Slerp(rotazioneInizialeCamera, puntoFinaleCamera.rotation, k);
            }

            cam.SetPositionAndRotation(posizione + offsetScossa, rotazione);
            yield return null;
        }
    }

    private IEnumerator Scossa()
    {
        float t = 0f;
        while (t < durataScossa)
        {
            t += Time.deltaTime;
            float forza = intensitaScossa * (1f - t / durataScossa); // Si attenua nel tempo
            offsetScossa = Random.insideUnitSphere * forza;
            yield return null;
        }
        offsetScossa = Vector3.zero;
    }

    // ---------- Porta ----------

    /// <summary>Imposta la porta: 1 = aperta, 0 = chiusa.</summary>
    private void ImpostaPorta(float apertura)
    {
        if (antaSinistra != null)
            antaSinistra.localRotation = Quaternion.Slerp(
                Quaternion.Euler(rotazioneChiusaSinistra), Quaternion.Euler(rotazioneApertaSinistra), apertura);

        if (antaDestra != null)
            antaDestra.localRotation = Quaternion.Slerp(
                Quaternion.Euler(rotazioneChiusaDestra), Quaternion.Euler(rotazioneApertaDestra), apertura);
    }

    // ---------- Utilità ----------

    private void SetControlli(bool attivi)
    {
        if (controlliGiocatore == null) return;
        foreach (Behaviour controllo in controlliGiocatore)
        {
            if (controllo != null) controllo.enabled = attivi;
        }
    }

    private void Suona(AudioClip clip)
    {
        if (clip != null && SFXPlayer.Instance != null)
            SFXPlayer.Instance.Play(clip);
    }

    private bool SaltaPremuto()
    {
#if ENABLE_INPUT_SYSTEM
        bool tastiera = Keyboard.current != null &&
                        (Keyboard.current.spaceKey.wasPressedThisFrame || Keyboard.current.enterKey.wasPressedThisFrame);
        bool mouse = Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame;
        return tastiera || mouse;
#else
        return Input.GetKeyDown(KeyCode.Space) || Input.GetKeyDown(KeyCode.Return) || Input.GetMouseButtonDown(0);
#endif
    }

    private void OnDestroy()
    {
        InCorso = false;
    }
}
