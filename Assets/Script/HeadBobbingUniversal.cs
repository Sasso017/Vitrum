using UnityEngine;

public class HeadBobbingUniversal : MonoBehaviour
{
    [Header("Impostazioni Camminata")]
    public float walkBobbingSpeed = 12f;
    public float walkBobbingAmount = 0.05f;

    [Header("Impostazioni Corsa")]
    public float runBobbingSpeed = 18f;
    public float runBobbingAmount = 0.1f;

    [Header("Impostazioni Crouch")]
    public float crouchBobbingSpeed = 8f;
    public float crouchBobbingAmount = 0.02f;

    [Header("Tasti")]
    public KeyCode runKey = KeyCode.LeftShift;
    public KeyCode crouchKey = KeyCode.LeftControl;

    [Header("Riferimenti")]
    public CharacterController controller;

    private float timer = 0;

    void Start()
    {
        if (controller == null)
            controller = GetComponentInParent<CharacterController>();
    }

    void Update()
    {
        bool isGrounded = (controller != null) ? controller.isGrounded : true;

        float horizontal = Input.GetAxis("Horizontal");
        float vertical = Input.GetAxis("Vertical");
        bool isMoving = Mathf.Abs(horizontal) > 0.1f || Mathf.Abs(vertical) > 0.1f;

        if (isMoving && isGrounded)
        {
            bool isCrouching = Input.GetKey(crouchKey);
            bool isRunning = Input.GetKey(runKey) && !isCrouching;

            float currentSpeed = isCrouching ? crouchBobbingSpeed : (isRunning ? runBobbingSpeed : walkBobbingSpeed);
            float currentAmount = isCrouching ? crouchBobbingAmount : (isRunning ? runBobbingAmount : walkBobbingAmount);

            // COMPENSAZIONE SCALA: divide l'intensità per la scala Y del padre (0.65)
            float parentScaleY = transform.lossyScale.y;
            if (parentScaleY != 0) currentAmount /= parentScaleY;

            timer += Time.deltaTime * currentSpeed;
            float newY = transform.localPosition.y + Mathf.Sin(timer) * currentAmount;

            transform.localPosition = new Vector3(
                transform.localPosition.x,
                newY,
                transform.localPosition.z
            );
        }
        else
        {
            timer = 0;
        }
    }
}