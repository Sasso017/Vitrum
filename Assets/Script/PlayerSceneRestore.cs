using UnityEngine;

/// <summary>
/// Quando il giocatore torna nella Basilica da un minigioco, lo riporta
/// nella posizione in cui si trovava prima di entrarci.
/// </summary>
public class PlayerSceneRestore : MonoBehaviour
{
    private void Start()
    {
        // Blocca il mouse per la visuale FPS quando ci troviamo nella Basilica
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;

        GameManager gm = GameManager.instance;
        if (gm == null || !gm.PosizioneDaRipristinare()) return;

        // Disabilita temporaneamente il CharacterController per consentire il teletrasporto
        CharacterController cc = GetComponent<CharacterController>();
        if (cc != null) cc.enabled = false;

        transform.SetPositionAndRotation(gm.ultimaPosizioneGiocatore, gm.ultimaRotazioneGiocatore);

        if (cc != null) cc.enabled = true;
    }
}