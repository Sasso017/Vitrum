using UnityEngine;

public class FPSController : MonoBehaviour
{
    [Header("Componenti")]
    public CharacterController controller;
    public Transform playerCamera;

    [Header("Movimento & Velocità")]
    public float walkSpeed = 6f;
    public float runSpeed = 12f;
    public float crouchSpeed = 3f;           // Velocità da accovacciato
    public KeyCode runKey = KeyCode.LeftShift;
    public KeyCode crouchKey = KeyCode.LeftControl; // Tasto accovacciati (CTRL)

    [Header("Altezze Crouch")]
    public float standingHeight = 2f;        // Altezza normale del CharacterController
    public float crouchingHeight = 1f;       // Altezza accovacciato del CharacterController
    public float standingCameraY = 0.8f;     // Altezza locale normale della camera
    public float crouchingCameraY = 0.2f;    // Altezza locale accovacciato della camera
    public float crouchTransitionSpeed = 10f;// Fluidità della transizione

    [Header("Salto & Gravità")]
    public float jumpHeight = 1.5f;
    public KeyCode jumpKey = KeyCode.Space;
    public float gravity = -9.81f;
    private Vector3 velocity;

    [Header("Mouse & Visuale")]
    public float mouseSensitivity = 200f;
    public float topClamp = 85f;
    public float bottomClamp = -85f;
    private float xRotation = 0f;

    // Stato corrente
    private bool isCrouching = false;

    void Start()
    {
        LockCursor();

        // Se non specificate, imposta le altezze di default basate sul controller
        if (controller != null)
        {
            standingHeight = controller.height;
        }
    }

    void Update()
    {
        if (Input.GetMouseButtonDown(0))
        {
            LockCursor();
        }

        LookAround();
        HandleCrouch();
        MovePlayer();
    }

    void LookAround()
    {
        float mouseX = Input.GetAxis("Mouse X") * mouseSensitivity * Time.deltaTime;
        float mouseY = Input.GetAxis("Mouse Y") * mouseSensitivity * Time.deltaTime;

        xRotation -= mouseY;
        xRotation = Mathf.Clamp(xRotation, bottomClamp, topClamp);
        playerCamera.localRotation = Quaternion.Euler(xRotation, 0f, 0f);

        transform.Rotate(Vector3.up * mouseX);
    }

    void HandleCrouch()
    {
        // Controlla se il giocatore tiene premuto il tasto Crouch (CTRL)
        isCrouching = Input.GetKey(crouchKey);

        // Calcola l'altezza bersaglio per il controller e per la camera
        float targetHeight = isCrouching ? crouchingHeight : standingHeight;
        float targetCameraY = isCrouching ? crouchingCameraY : standingCameraY;

        // Modifica l'altezza del CharacterController in modo fluido
        controller.height = Mathf.Lerp(controller.height, targetHeight, Time.deltaTime * crouchTransitionSpeed);

        // Modifica la posizione Y della telecamera in modo fluido
        Vector3 camPos = playerCamera.localPosition;
        camPos.y = Mathf.Lerp(camPos.y, targetCameraY, Time.deltaTime * crouchTransitionSpeed);
        playerCamera.localPosition = camPos;
    }

    void MovePlayer()
    {
        bool isGrounded = controller.isGrounded;

        if (isGrounded && velocity.y < 0)
        {
            velocity.y = -2f;
        }

        float horizontal = Input.GetAxisRaw("Horizontal");
        float vertical = Input.GetAxisRaw("Vertical");

        // Calcola la velocità: se è accovacciato usa crouchSpeed, altrimenti controlla la corsa
        float currentSpeed = walkSpeed;
        if (isCrouching)
        {
            currentSpeed = crouchSpeed;
        }
        else if (Input.GetKey(runKey))
        {
            currentSpeed = runSpeed;
        }

        Vector3 move = transform.right * horizontal + transform.forward * vertical;
        controller.Move(move.normalized * currentSpeed * Time.deltaTime);

        // Permetti il salto solo se NON stai accovacciato ed è a terra
        if (Input.GetKeyDown(jumpKey) && isGrounded && !isCrouching)
        {
            velocity.y = Mathf.Sqrt(jumpHeight * -2f * gravity);
        }

        velocity.y += gravity * Time.deltaTime;
        controller.Move(velocity * Time.deltaTime);
    }

    void LockCursor()
    {
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
    }
}