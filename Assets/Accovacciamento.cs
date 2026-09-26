using System.Collections.Generic;
using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

/// <summary>
/// Permette al giocatore di accovacciarsi: abbassa il CharacterController, gli altri collider
/// di Carlo e la visuale in modo fluido, così può passare sotto gli ostacoli.
/// Tornando in piedi controlla che sopra la testa ci sia spazio.
/// Mettilo su Carlo, accanto al CharacterController e a FPSController.
/// Altri script possono leggere Accovacciamento.IsAccovacciato (es. per rallentare il movimento).
/// </summary>
[DefaultExecutionOrder(-50)] // Prima di FPSController: il movimento usa già l'altezza aggiornata
[RequireComponent(typeof(CharacterController))]
public class Accovacciamento : MonoBehaviour
{
    public enum Modalita
    {
        TieniPremuto, // Accovacciato finché il tasto è premuto
        Alterna       // Un tocco per abbassarsi, un altro per rialzarsi
    }

    public enum TipoAltezza
    {
        Percentuale, // Frazione dell'altezza in piedi (es. 0.7 = 70%)
        Metri        // Altezza esatta in metri (es. 0.7 = 70 cm)
    }

    [Header("Comando")]
    [SerializeField] private Modalita modalita = Modalita.TieniPremuto;
#if !ENABLE_INPUT_SYSTEM
    [SerializeField] private KeyCode tasto = KeyCode.LeftControl;
#endif

    [Header("Altezza da accovacciato")]
    [SerializeField] private TipoAltezza tipoAltezza = TipoAltezza.Metri;
    [Tooltip("Se Percentuale: frazione dell'altezza in piedi. Se Metri: altezza reale del personaggio accovacciato")]
    [SerializeField] private float altezzaAccovacciato = 0.7f;
    [Tooltip("Velocità della transizione in piedi / accovacciato")]
    [SerializeField] private float velocitaTransizione = 8f;

    [Header("Visuale")]
    [Tooltip("Il punto da abbassare insieme al personaggio: l'oggetto CameraPivot (o la Main Camera)")]
    [SerializeField] private Transform puntoCamera;

    [Header("Altri collider")]
    [Tooltip("Riduce anche i Box/Capsule Collider presenti su Carlo e sui suoi figli (esclusi i trigger)")]
    [SerializeField] private bool riduciAltriCollider = true;

    [Header("Controllo del soffitto")]
    [Tooltip("Livelli considerati ostacoli sopra la testa")]
    [SerializeField] private LayerMask ostacoli = ~0;

    /// <summary>True mentre il giocatore è accovacciato (o si sta abbassando).</summary>
    public bool IsAccovacciato { get; private set; }

    private CharacterController cc;

    // Valori in coordinate locali del CharacterController (come nell'Inspector)
    private float altezzaInPiedi;
    private Vector3 centroInPiedi;
    private float altezzaBassa;
    private Vector3 centroBasso;

    private float cameraYInPiedi;
    private float cameraYBassa;

    // Stato attuale della transizione: 0 = in piedi, 1 = accovacciato
    private float progresso = 0f;

    private class ColliderExtra
    {
        public BoxCollider box;
        public CapsuleCollider capsula;
        public float altezza;
        public Vector3 centro;
    }
    private readonly List<ColliderExtra> altriCollider = new List<ColliderExtra>();

    private readonly Collider[] risultati = new Collider[8];
    private bool bloccatoDalSoffitto = false;

    private void Awake()
    {
        cc = GetComponent<CharacterController>();

        altezzaInPiedi = cc.height;
        centroInPiedi = cc.center;

        // Calcola l'altezza da accovacciato in unità locali del CharacterController
        float scalaY = Mathf.Max(transform.lossyScale.y, 0.0001f);
        if (tipoAltezza == TipoAltezza.Percentuale)
            altezzaBassa = altezzaInPiedi * Mathf.Clamp(altezzaAccovacciato, 0.1f, 1f);
        else
            altezzaBassa = Mathf.Min(altezzaAccovacciato / scalaY, altezzaInPiedi);

        // Il collider non può essere più basso del doppio del raggio (diventerebbe una sfera)
        altezzaBassa = Mathf.Max(altezzaBassa, cc.radius * 2f + 0.01f);

        // I piedi restano a terra: il centro scende di metà della riduzione
        float riduzione = altezzaInPiedi - altezzaBassa;
        centroBasso = centroInPiedi - new Vector3(0f, riduzione / 2f, 0f);

        if (puntoCamera != null)
        {
            // La camera scende della stessa quantità del personaggio (convertita nello spazio del suo genitore)
            float riduzioneMondo = riduzione * scalaY;
            float scalaGenitore = puntoCamera.parent != null ? Mathf.Max(puntoCamera.parent.lossyScale.y, 0.0001f) : 1f;
            cameraYInPiedi = puntoCamera.localPosition.y;
            cameraYBassa = cameraYInPiedi - riduzioneMondo / scalaGenitore;
        }
        else
        {
            Debug.LogWarning("[Accovacciamento] Nessun Punto Camera assegnato: si abbasserà solo il collider.", this);
        }

        if (cc.stepOffset > altezzaBassa)
            cc.stepOffset = altezzaBassa * 0.5f;

        if (riduciAltriCollider) RaccogliAltriCollider();

        Debug.Log($"[Accovacciamento] In piedi: {altezzaInPiedi * scalaY:0.00} m, accovacciato: {altezzaBassa * scalaY:0.00} m", this);
    }

    private void RaccogliAltriCollider()
    {
        foreach (BoxCollider b in GetComponentsInChildren<BoxCollider>())
        {
            if (b.isTrigger) continue;
            altriCollider.Add(new ColliderExtra { box = b, altezza = b.size.y, centro = b.center });
        }
        foreach (CapsuleCollider c in GetComponentsInChildren<CapsuleCollider>())
        {
            if (c.isTrigger || c.direction != 1) continue; // Solo capsule verticali
            altriCollider.Add(new ColliderExtra { capsula = c, altezza = c.height, centro = c.center });
        }
    }

    private void Update()
    {
        LeggiComando();
        AggiornaAltezza();
    }

    private void LeggiComando()
    {
        // Niente comandi in pausa, durante la cutscene o un minigioco
        if (PauseMenu.IsPaused || IntroBasilica.InCorso || StazioneMinigioco.InUso) return;

        bloccatoDalSoffitto = false;

        if (modalita == Modalita.TieniPremuto)
        {
            if (TastoTenuto())
                IsAccovacciato = true;
            else if (IsAccovacciato)
            {
                if (PuoAlzarsi()) IsAccovacciato = false;
                else bloccatoDalSoffitto = true;
            }
        }
        else if (TastoPremuto())
        {
            if (!IsAccovacciato) IsAccovacciato = true;
            else if (PuoAlzarsi()) IsAccovacciato = false;
            else bloccatoDalSoffitto = true;
        }
    }

    private void AggiornaAltezza()
    {
        float obiettivo = IsAccovacciato ? 1f : 0f;
        float k = 1f - Mathf.Exp(-velocitaTransizione * Time.deltaTime);
        progresso = Mathf.Lerp(progresso, obiettivo, k);
        if (Mathf.Abs(progresso - obiettivo) < 0.001f) progresso = obiettivo;

        // Character Controller
        cc.height = Mathf.Lerp(altezzaInPiedi, altezzaBassa, progresso);
        cc.center = Vector3.Lerp(centroInPiedi, centroBasso, progresso);

        // Altri collider: stessa proporzione, piedi fermi
        float fattore = Mathf.Lerp(1f, altezzaBassa / altezzaInPiedi, progresso);
        foreach (ColliderExtra c in altriCollider)
        {
            float nuovaAltezza = c.altezza * fattore;
            Vector3 nuovoCentro = c.centro - new Vector3(0f, (c.altezza - nuovaAltezza) / 2f, 0f);

            if (c.box != null)
            {
                Vector3 s = c.box.size; s.y = nuovaAltezza;
                c.box.size = s;
                c.box.center = nuovoCentro;
            }
            else if (c.capsula != null)
            {
                c.capsula.height = nuovaAltezza;
                c.capsula.center = nuovoCentro;
            }
        }

        // Visuale
        if (puntoCamera != null)
        {
            Vector3 pos = puntoCamera.localPosition;
            pos.y = Mathf.Lerp(cameraYInPiedi, cameraYBassa, progresso);
            puntoCamera.localPosition = pos;
        }
    }

    /// <summary>Controlla se c'è spazio sopra la testa per rimettersi in piedi.</summary>
    private bool PuoAlzarsi()
    {
        GetCapsulaMondo(altezzaInPiedi, centroInPiedi, out Vector3 basso, out Vector3 alto, out float raggio);

        int n = Physics.OverlapCapsuleNonAlloc(basso, alto, raggio * 0.95f, risultati, ostacoli, QueryTriggerInteraction.Ignore);
        for (int i = 0; i < n; i++)
        {
            // Ignora i collider di Carlo stesso
            if (risultati[i].transform == transform || risultati[i].transform.IsChildOf(transform)) continue;
            return false;
        }
        return true;
    }

    private void GetCapsulaMondo(float altezza, Vector3 centroLocale, out Vector3 basso, out Vector3 alto, out float raggio)
    {
        Vector3 scala = transform.lossyScale;
        raggio = cc.radius * Mathf.Max(Mathf.Abs(scala.x), Mathf.Abs(scala.z));
        float altezzaMondo = altezza * Mathf.Abs(scala.y);
        Vector3 centro = transform.TransformPoint(centroLocale);
        float meta = Mathf.Max(altezzaMondo / 2f - raggio, 0f);
        basso = centro - Vector3.up * meta;
        alto = centro + Vector3.up * meta;
    }

    private void OnDrawGizmosSelected()
    {
        CharacterController c = cc != null ? cc : GetComponent<CharacterController>();
        if (c == null) return;
        if (cc == null) cc = c;

        // Disegna la capsula attuale: verde, rossa se il soffitto impedisce di alzarsi
        GetCapsulaMondo(c.height, c.center, out Vector3 basso, out Vector3 alto, out float raggio);
        Gizmos.color = bloccatoDalSoffitto ? Color.red : Color.green;
        Gizmos.DrawWireSphere(basso, raggio);
        Gizmos.DrawWireSphere(alto, raggio);
        Gizmos.DrawLine(basso + Vector3.left * raggio, alto + Vector3.left * raggio);
        Gizmos.DrawLine(basso + Vector3.right * raggio, alto + Vector3.right * raggio);
        Gizmos.DrawLine(basso + Vector3.forward * raggio, alto + Vector3.forward * raggio);
        Gizmos.DrawLine(basso + Vector3.back * raggio, alto + Vector3.back * raggio);
    }

    // ---------- Input ----------

    private bool TastoTenuto()
    {
#if ENABLE_INPUT_SYSTEM
        return Keyboard.current != null && Keyboard.current.leftCtrlKey.isPressed;
#else
        return Input.GetKey(tasto);
#endif
    }

    private bool TastoPremuto()
    {
#if ENABLE_INPUT_SYSTEM
        return Keyboard.current != null && Keyboard.current.leftCtrlKey.wasPressedThisFrame;
#else
        return Input.GetKeyDown(tasto);
#endif
    }
}