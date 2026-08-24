using UnityEngine;

public class HeadBobbingUniversal : MonoBehaviour
{
    [Header("Impostazioni Camminata")]
    public float walkBobbingSpeed = 12f;
    public float walkBobbingAmount = 0.05f;

    [Header("Impostazioni Corsa")]
    public float runBobbingSpeed = 18f;
    public float runBobbingAmount = 0.1f;

    [Header("Tasti")]
    public KeyCode runKey = KeyCode.LeftShift;

    [Header("Riferimenti")]
    public CharacterController controller; // Trascina qui Carlo dal pannello Hierarchy

    private float defaultPosY = 0;
    private float timer = 0;

    void Start()
    {
        defaultPosY = transform.localPosition.y;

        // Tenta di trovare da solo il CharacterController nel padre se non assegnato
        if (controller == null)
            controller = GetComponentInParent<CharacterController>();
    }

    void Update()
    {
        // 1. Verifichiamo se il giocatore è a terra
        bool isGrounded = (controller != null) ? controller.isGrounded : true;

        // 2. Leggiamo l'input del movimento
        float horizontal = Input.GetAxis("Horizontal");
        float vertical = Input.GetAxis("Vertical");
        bool isMoving = Mathf.Abs(horizontal) > 0.1f || Mathf.Abs(vertical) > 0.1f;

        // 3. L'oscillazione si attiva SOLO SE il giocatore si muove ED È A TERRA
        if (isMoving && isGrounded)
        {
            bool isRunning = Input.GetKey(runKey);
            float currentSpeed = isRunning ? runBobbingSpeed : walkBobbingAmount; // Velocità
            float currentAmount = isRunning ? runBobbingAmount : walkBobbingAmount;

            timer += Time.deltaTime * (isRunning ? runBobbingSpeed : walkBobbingSpeed);
            float newY = defaultPosY + Mathf.Sin(timer) * currentAmount;

            transform.localPosition = new Vector3(
                transform.localPosition.x,
                newY,
                transform.localPosition.z
            );
        }
        else
        {
            // Quando saltiamo o ci fermiamo, l'oscillazione si azzera e la camera torna fluida in posizione centrale
            timer = 0;
            Vector3 targetPosition = new Vector3(
                transform.localPosition.x,
                defaultPosY,
                transform.localPosition.z
            );

            transform.localPosition = Vector3.Lerp(
                transform.localPosition,
                targetPosition,
                Time.deltaTime * 8f
            );
        }
    }
}