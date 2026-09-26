using System.Collections;
using UnityEngine;
using UnityEngine.Audio;
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

    [Header("Audio della porta")]
    [Tooltip("Un unico audio per tutta la chiusura (cigolio + tonfo)")]
    [SerializeField] private AudioClip suonoPorta;
    [Tooltip("Dopo quanti secondi dall'inizio del file audio si sente il tonfo. L'audio parte in anticipo in modo che il tonfo coincida con la chiusura delle ante. Con 0 l'audio parte quando la porta inizia a muoversi")]
    [SerializeField] private float istanteTonfo = 0f;
    [SerializeField, Range(0f, 1f)] private float volumePorta = 1f;
    [Tooltip("Gruppo SFX dell'AudioMixer: così l'audio segue gli slider del volume e si può interrompere se la cutscene viene saltata")]
    [SerializeField] private AudioMixerGroup gruppoSFX;

    [Header("Messaggio")]
    [Tooltip("Testo con l'obiettivo, es. 'La porta si è chiusa alle tue spalle. Trova le due chiavi per fuggire.'")]
    [SerializeField] private GameObject messaggioObiettivo;

    [Header("Lucchetti")]
    [Tooltip("I lucchetti sulle ante (tienili figli delle ante, così si muovono con loro). Compaiono uno dopo l'altro appena la porta si chiude")]
    [SerializeField] private Transform[] lucchetti;
    [Tooltip("Prefab con il Particle System dell'effetto 'puf', creato sulla posizione di ogni lucchetto")]
    [SerializeField] private ParticleSystem prefabPuf;
    [Tooltip("Suono di ogni lucchetto che compare")]
    [SerializeField] private AudioClip suonoLucchetto;
    [Tooltip("Pausa tra il tonfo della porta e il primo lucchetto")]
    [SerializeField] private float attesaPrimaDeiLucchetti = 0.5f;
    [Tooltip("Tempo tra la comparsa di un lucchetto e il successivo")]
    [SerializeField] private float intervalloTraLucchetti = 0.4f;
    [Tooltip("Durata dell'effetto 'pop' con cui ogni lucchetto cresce")]
    [SerializeField] private float durataComparsa = 0.3f;

    [Header("Salta cutscene")]
    [Tooltip("Permette di saltare la cutscene con Spazio, Invio o click")]
    [SerializeField] private bool consentiSalta = true;

    private Vector3 posizioneInizialeCamera;
    private Quaternion rotazioneInizialeCamera;
    private Vector3 offsetScossa = Vector3.zero;
    private Vector3[] scaleOriginaliLucchetti;
    private AudioSource sorgentePorta;

    private void Start()
    {
        InCorso = false;
        if (cameraCutscene != null) cameraCutscene.enabled = false;
        if (messaggioObiettivo != null) messaggioObiettivo.SetActive(false);
        SalvaScaleLucchetti();

        // Cutscene già vista (es. ritorno da un minigioco): porta chiusa e nient'altro
        GameManager gm = GameManager.instance;
        if (gm != null && gm.introBasilicaVista)
        {
            ImpostaPorta(0f);
            MostraTuttiILucchetti();
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
        NascondiLucchetti();
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

            float durataTotale = attesaIniziale + durataChiusura + DurataFaseLucchetti() + durataMessaggio;
            StartCoroutine(MuoviCamera(durataTotale));
        }

        // Audio unico della porta, sincronizzato perché il tonfo cada sulla chiusura
        StartCoroutine(AudioPorta());

        yield return new WaitForSeconds(attesaIniziale);

        // Chiusura della porta: parte lenta e accelera, come se fosse spinta dal vento
        float t = 0f;
        while (t < durataChiusura)
        {
            t += Time.deltaTime;
            float k = Mathf.Clamp01(t / durataChiusura);
            ImpostaPorta(1f - k * k);
            yield return null;
        }
        ImpostaPorta(0f);

        // Tonfo finale: la scossa coincide con il colpo nell'audio
        StartCoroutine(Scossa());

        // I lucchetti compaiono uno dopo l'altro sulle ante
        yield return new WaitForSeconds(attesaPrimaDeiLucchetti);
        yield return ComparsaLucchetti();

        // Messaggio con l'obiettivo
        if (messaggioObiettivo != null) messaggioObiettivo.SetActive(true);
        yield return new WaitForSeconds(durataMessaggio);

        Termina();
    }

    /// <summary>Riporta tutto allo stato di gioco normale (fine cutscene o cutscene saltata).</summary>
    private void Termina()
    {
        // Ferma tutte le coroutine ancora attive (movimento camera, scossa, audio programmato)
        StopAllCoroutines();
        offsetScossa = Vector3.zero;

        // Se la cutscene è stata saltata mentre la porta suona, l'audio sfuma via
        if (sorgentePorta != null && sorgentePorta.isPlaying)
            StartCoroutine(SfumaAudioPorta(0.3f));

        ImpostaPorta(0f);
        MostraTuttiILucchetti(); // Anche se la cutscene è stata saltata a metà
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

    // ---------- Audio della porta ----------

    /// <summary>
    /// Fa partire l'audio della porta nel momento giusto: il tonfo (a "istanteTonfo" secondi dall'inizio
    /// del file) deve cadere quando le ante si chiudono, cioè dopo attesaIniziale + durataChiusura.
    /// </summary>
    private IEnumerator AudioPorta()
    {
        if (suonoPorta == null) yield break;

        float momentoChiusura = attesaIniziale + durataChiusura;
        float attesa = istanteTonfo > 0f ? momentoChiusura - istanteTonfo : attesaIniziale;

        if (attesa < 0f)
        {
            Debug.LogWarning($"[IntroBasilica] Il tonfo nell'audio arriva dopo {istanteTonfo}s, ma la porta si chiude dopo " +
                             $"{momentoChiusura}s: aumenta 'Attesa Iniziale' di {-attesa:0.0}s per sincronizzarli.", this);
            attesa = 0f;
        }

        if (attesa > 0f) yield return new WaitForSeconds(attesa);

        if (gruppoSFX != null)
        {
            // Sorgente dedicata: segue gli slider del volume e si può interrompere
            if (sorgentePorta == null)
            {
                sorgentePorta = gameObject.AddComponent<AudioSource>();
                sorgentePorta.playOnAwake = false;
                sorgentePorta.outputAudioMixerGroup = gruppoSFX;
            }
            sorgentePorta.clip = suonoPorta;
            sorgentePorta.volume = volumePorta;
            sorgentePorta.Play();
        }
        else if (SFXPlayer.Instance != null)
        {
            // Senza gruppo assegnato usa lo SFXPlayer (in questo caso non si può interrompere)
            SFXPlayer.Instance.Play(suonoPorta, volumePorta);
        }
    }

    private IEnumerator SfumaAudioPorta(float durata)
    {
        float volumeIniziale = sorgentePorta.volume;
        float t = 0f;
        while (t < durata)
        {
            t += Time.deltaTime;
            sorgentePorta.volume = Mathf.Lerp(volumeIniziale, 0f, t / durata);
            yield return null;
        }
        sorgentePorta.Stop();
        sorgentePorta.volume = volumePorta;
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

    // ---------- Lucchetti ----------

    private void SalvaScaleLucchetti()
    {
        if (lucchetti == null) return;
        scaleOriginaliLucchetti = new Vector3[lucchetti.Length];
        for (int i = 0; i < lucchetti.Length; i++)
        {
            if (lucchetti[i] != null)
                scaleOriginaliLucchetti[i] = lucchetti[i].localScale;
        }
    }

    private void NascondiLucchetti()
    {
        if (lucchetti == null) return;
        foreach (Transform l in lucchetti)
        {
            if (l != null) l.gameObject.SetActive(false);
        }
    }

    private void MostraTuttiILucchetti()
    {
        if (lucchetti == null || scaleOriginaliLucchetti == null) return;
        for (int i = 0; i < lucchetti.Length; i++)
        {
            if (lucchetti[i] == null) continue;
            lucchetti[i].gameObject.SetActive(true);
            lucchetti[i].localScale = scaleOriginaliLucchetti[i];
        }
    }

    private float DurataFaseLucchetti()
    {
        if (lucchetti == null || lucchetti.Length == 0) return 0f;
        return attesaPrimaDeiLucchetti + intervalloTraLucchetti * (lucchetti.Length - 1) + durataComparsa;
    }

    /// <summary>Fa comparire i lucchetti uno alla volta.</summary>
    private IEnumerator ComparsaLucchetti()
    {
        if (lucchetti == null || lucchetti.Length == 0) yield break;

        for (int i = 0; i < lucchetti.Length; i++)
        {
            if (lucchetti[i] == null) continue;
            StartCoroutine(ComparsaLucchetto(i));

            if (i < lucchetti.Length - 1)
                yield return new WaitForSeconds(intervalloTraLucchetti);
        }

        // Aspetta che anche l'ultimo abbia finito di crescere
        yield return new WaitForSeconds(durataComparsa);
    }

    /// <summary>Effetto "puf" + suono + il lucchetto cresce con un piccolo rimbalzo.</summary>
    private IEnumerator ComparsaLucchetto(int indice)
    {
        Transform lucchetto = lucchetti[indice];
        Vector3 scalaFinale = scaleOriginaliLucchetti[indice];

        // Nuvoletta di fumo
        if (prefabPuf != null)
        {
            ParticleSystem puf = Instantiate(prefabPuf, lucchetto.position, Quaternion.identity);
            puf.Play();
            float durataPuf = puf.main.duration + puf.main.startLifetime.constantMax;
            Destroy(puf.gameObject, durataPuf + 0.5f);
        }

        Suona(suonoLucchetto);

        // Il lucchetto cresce da zero superando un po' la dimensione finale, poi si assesta
        lucchetto.localScale = Vector3.zero;
        lucchetto.gameObject.SetActive(true);

        float t = 0f;
        while (t < durataComparsa)
        {
            t += Time.deltaTime;
            float k = EaseOutBack(Mathf.Clamp01(t / durataComparsa));
            lucchetto.localScale = scalaFinale * k;
            yield return null;
        }
        lucchetto.localScale = scalaFinale;
    }

    /// <summary>Curva che supera leggermente 1 prima di tornarci (effetto rimbalzo).</summary>
    private static float EaseOutBack(float x)
    {
        const float c1 = 1.70158f;
        const float c3 = c1 + 1f;
        return 1f + c3 * Mathf.Pow(x - 1f, 3f) + c1 * Mathf.Pow(x - 1f, 2f);
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