using UnityEngine;

public class FPSController : MonoBehaviour
{
    [Header("Componenti")]
    public CharacterController controller;
    public Transform playerCamera;

    [Header("Movimento")]
    public float walkSpeed = 6f;
    public float runSpeed = 12f;
    public KeyCode runKey = KeyCode.LeftShift;

    [Header("Salto & Gravità")]
    public float jumpHeight = 1.5f;             // Altezza del salto in metri
    public KeyCode jumpKey = KeyCode.Space;     // Tasto per saltare
    public float gravity = -9.81f;
    private Vector3 velocity;

    [Header("Mouse & Visuale")]
    public float mouseSensitivity = 200f;
    public float topClamp = 85f;
    public float bottomClamp = -85f;
    private float xRotation = 0f;

    void Start()
    {
        LockCursor();
    }

    void Update()
    {
        if (Input.GetMouseButtonDown(0))
        {
            LockCursor();
        }

        LookAround();
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

    void MovePlayer()
    {
        // Controlla se il personaggio tocca il suolo
        bool isGrounded = controller.isGrounded;

        // Reset della velocità verticale se è a terra
        if (isGrounded && velocity.y < 0)
        {
            velocity.y = -2f;
        }

        // Input da tastiera (WASD)
        float horizontal = Input.GetAxisRaw("Horizontal");
        float vertical = Input.GetAxisRaw("Vertical");

        // Gestione corsa
        bool isRunning = Input.GetKey(runKey);
        float currentSpeed = isRunning ? runSpeed : walkSpeed;

        // Calcolo direzione movimento
        Vector3 move = transform.right * horizontal + transform.forward * vertical;
        controller.Move(move.normalized * currentSpeed * Time.deltaTime);

        // --- GESTIONE SALTO ---
        if (Input.GetKeyDown(jumpKey) && isGrounded)
        {
            // Formula della fisica per raggiungere esattamente l'altezza desiderata
            velocity.y = Mathf.Sqrt(jumpHeight * -2f * gravity);
        }

        // Applica la gravità nel tempo
        velocity.y += gravity * Time.deltaTime;
        controller.Move(velocity * Time.deltaTime);
    }

    void LockCursor()
    {
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
    }
}