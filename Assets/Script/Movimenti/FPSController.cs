using UnityEngine;
using UnityEngine.SceneManagement;

public class FPSController : MonoBehaviour
{
    [Header("Impostazioni Movimento")]
    public float walkSpeed = 5f;
    public float runSpeed = 9f;
    public float jumpHeight = 1.2f;
    public float gravity = -19.62f;

    [Header("Impostazioni Visuale")]
    public float mouseSensitivity = 100f;
    public Transform cameraTransform; // Trascina la Main Camera qui nell'Inspector

    private CharacterController controller;
    private float xRotation = 0f;
    private Vector3 velocity;
    private bool isGrounded;

    void Start()
    {
        controller = GetComponent<CharacterController>();

        if (cameraTransform == null && Camera.main != null)
        {
            cameraTransform = Camera.main.transform;
        }

        // Se siamo nel minigioco sblocca il mouse, altrimenti bloccalo per la prima persona
        if (SceneManager.GetActiveScene().name == "ScenaMosaico")
        {
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
        }
        else
        {
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
        }
    }

    void Update()
    {
        // -------------------------------------------------------------
        // CHECKS PER LA SCENA MINIGIOCO (Non tocca il movimento 3D)
        // -------------------------------------------------------------
        if (SceneManager.GetActiveScene().name == "ScenaMosaico")
        {
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
            return; // Se siamo nel mosaico, non muovere il personaggio
        }

        // Cliccando nella Basilica, ri-blocca il cursore se era sbloccato
        if (Input.GetMouseButtonDown(0) && Cursor.lockState != CursorLockMode.Locked)
        {
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
        }

        // -------------------------------------------------------------
        // MOVIMENTO E VISUALE FPS (Con Corsa e Salto)
        // -------------------------------------------------------------

        // --- ROTAZIONE MOUSE ---
        float mouseX = Input.GetAxis("Mouse X") * mouseSensitivity * Time.deltaTime;
        float mouseY = Input.GetAxis("Mouse Y") * mouseSensitivity * Time.deltaTime;

        xRotation -= mouseY;
        xRotation = Mathf.Clamp(xRotation, -90f, 90f);

        if (cameraTransform != null)
        {
            cameraTransform.localRotation = Quaternion.Euler(xRotation, 0f, 0f);
        }
        transform.Rotate(Vector3.up * mouseX);

        // --- CONTROLLO TERRENO E GRAVITÀ ---
        if (controller != null && controller.enabled)
        {
            isGrounded = controller.isGrounded;

            if (isGrounded && velocity.y < 0)
            {
                velocity.y = -2f;
            }

            // --- CORSA (Shift) ---
            bool isRunning = Input.GetKey(KeyCode.LeftShift);
            float currentSpeed = isRunning ? runSpeed : walkSpeed;

            // --- INPUT MOVIMENTO (WASD) ---
            float x = Input.GetAxis("Horizontal");
            float z = Input.GetAxis("Vertical");

            Vector3 move = transform.right * x + transform.forward * z;
            controller.Move(move * currentSpeed * Time.deltaTime);

            // --- SALTO (Spazio) ---
            if (Input.GetButtonDown("Jump") && isGrounded)
            {
                velocity.y = Mathf.Sqrt(jumpHeight * -2f * gravity);
            }

            // APPLICA GRAVITÀ
            velocity.y += gravity * Time.deltaTime;
            controller.Move(velocity * Time.deltaTime);
        }
    }
}