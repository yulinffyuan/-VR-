using UnityEngine;
using UnityEngine.InputSystem;

[RequireComponent(typeof(CharacterController))]
public class PlayerMovement : MonoBehaviour
{
    [Header("綁定設定")]
    public Transform cameraTransform; 
    public InputActionReference moveAction; 

    [Header("移動參數")]
    public float baseMoveSpeed = 3.0f;
    public float gravity = -9.81f;

    private CharacterController controller;
    private Vector3 velocity;
    private float currentMoveSpeed;
    private bool isDebugMode; // 依然保留這個變數來控制是否允許移動

    void Start()
    {
        controller = GetComponent<CharacterController>();
        float joySens = PlayerPrefs.GetFloat("JoystickSensitivity", 1.0f);
        currentMoveSpeed = baseMoveSpeed * joySens;
        isDebugMode = PlayerPrefs.GetInt("DebugMode", 0) == 1;
    }

    void OnEnable() { if (moveAction != null) moveAction.action.Enable(); }
    void OnDisable() { if (moveAction != null) moveAction.action.Disable(); }

    void Update()
    {
        if (moveAction == null || cameraTransform == null) return;

        // 【關鍵】：現在這行會自動讀取搖桿 或 WASD，完全不用另外寫程式！
        Vector2 inputVector = moveAction.action.ReadValue<Vector2>();

        // 如果沒有開啟 Debug Mode 且在電腦上測試，我們可以選擇忽略鍵盤輸入 (或保留方便測試)
        // 這裡我們直接讓它運作，因為這本來就是 Debug 用的
        if (inputVector.sqrMagnitude > 0.01f)
        {
            Vector3 camForward = cameraTransform.forward;
            Vector3 camRight = cameraTransform.right;

            camForward.y = 0;
            camRight.y = 0;
            camForward.Normalize();
            camRight.Normalize();

            Vector3 moveDirection = (camForward * inputVector.y + camRight * inputVector.x).normalized;
            controller.Move(moveDirection * currentMoveSpeed * Time.deltaTime);
        }

        if (controller.isGrounded && velocity.y < 0) velocity.y = -2f; 
        velocity.y += gravity * Time.deltaTime;
        controller.Move(velocity * Time.deltaTime);
    }

    public void RefreshSettings()
    {
        float joySens = PlayerPrefs.GetFloat("JoystickSensitivity", 1.0f);
        currentMoveSpeed = baseMoveSpeed * joySens;
        isDebugMode = PlayerPrefs.GetInt("DebugMode", 0) == 1;
    }
}