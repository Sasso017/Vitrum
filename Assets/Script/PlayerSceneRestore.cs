using UnityEngine;

public class PlayerSceneRestore : MonoBehaviour
{
    private void Start()
    {
        // Blocca il mouse per la visuale FPS quando ci troviamo nella Basilica
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;

        // Se è presente una posizione salvata precedentemente, sposta qui il giocatore
        if (GameManager.instance != null && GameManager.instance.ultimaPosizioneGiocatore != Vector3.zero)
        {
            // Disabilita temporaneamente il CharacterController per consentire il teletrasporto
            CharacterController cc = GetComponent<CharacterController>();
            if (cc != null) cc.enabled = false;

            transform.position = GameManager.instance.ultimaPosizioneGiocatore;
            transform.rotation = GameManager.instance.ultimaRotazioneGiocatore;

            if (cc != null) cc.enabled = true;
        }
    }
}