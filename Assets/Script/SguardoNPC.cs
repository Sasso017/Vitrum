using UnityEngine;

/// <summary>
/// L'NPC segue il giocatore con lo sguardo: ruota su se stesso attorno all'asse verticale,
/// senza mai spostarsi né inclinarsi. Quando il giocatore si allontana torna nella posa iniziale.
/// Mettilo sull'NPC (o sulla parte da ruotare, es. solo la testa, tramite "Parte Da Ruotare").
/// </summary>
public class SguardoNPC : MonoBehaviour
{
    [Tooltip("Cosa ruotare. Se vuoto, l'NPC intero (l'oggetto con questo script)")]
    [SerializeField] private Transform parteDaRuotare;

    [Header("Attivazione")]
    [Tooltip("Entro questa distanza l'NPC inizia a seguire il giocatore con lo sguardo")]
    [SerializeField] private float distanzaAttivazione = 8f;
    [Tooltip("Se attivo, oltre la distanza di attivazione l'NPC torna lentamente nella posa iniziale")]
    [SerializeField] private bool tornaAllaPosaIniziale = true;

    [Header("Rotazione")]
    [Tooltip("Velocità della rotazione (più alto = più reattivo)")]
    [SerializeField] private float velocitaRotazione = 3f;
    [Tooltip("Correzione se il modello non guarda nella direzione giusta (es. 90, 180, -90)")]
    [SerializeField] private float correzioneAngolo = 0f;
    [Tooltip("Angolo massimo di rotazione rispetto alla posa iniziale (360 = può girarsi completamente)")]
    [SerializeField, Range(0f, 360f)] private float angoloMassimo = 360f;

    private Transform giocatore;
    private Quaternion rotazioneIniziale;

    private void Start()
    {
        if (parteDaRuotare == null) parteDaRuotare = transform;
        rotazioneIniziale = parteDaRuotare.rotation;

        GameObject playerObj = GameObject.FindGameObjectWithTag("Player");
        if (playerObj != null) giocatore = playerObj.transform;
        else Debug.LogWarning("[SguardoNPC] Nessun oggetto con tag 'Player' trovato.", this);
    }

    private void LateUpdate()
    {
        if (giocatore == null) return;

        Quaternion obiettivo = rotazioneIniziale;

        Vector3 direzione = giocatore.position - parteDaRuotare.position;
        direzione.y = 0f; // Solo rotazione orizzontale: l'NPC non si inclina mai

        bool vicino = direzione.magnitude <= distanzaAttivazione;

        if (vicino && direzione.sqrMagnitude > 0.0001f)
        {
            obiettivo = Quaternion.LookRotation(direzione) * Quaternion.Euler(0f, correzioneAngolo, 0f);

            // Limite di rotazione rispetto alla posa iniziale (es. un NPC che gira solo un po' la testa)
            if (angoloMassimo < 360f)
            {
                float angolo = Quaternion.Angle(rotazioneIniziale, obiettivo);
                if (angolo > angoloMassimo * 0.5f)
                    obiettivo = Quaternion.RotateTowards(rotazioneIniziale, obiettivo, angoloMassimo * 0.5f);
            }
        }
        else if (!tornaAllaPosaIniziale)
        {
            return; // Resta com'è
        }

        // Rotazione morbida, indipendente dal frame rate
        float k = 1f - Mathf.Exp(-velocitaRotazione * Time.deltaTime);
        parteDaRuotare.rotation = Quaternion.Slerp(parteDaRuotare.rotation, obiettivo, k);
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = new Color(0.4f, 0.8f, 1f);
        Gizmos.DrawWireSphere(transform.position, distanzaAttivazione);
    }
}
