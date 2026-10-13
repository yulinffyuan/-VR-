using UnityEngine;
using UnityEngine.InputSystem;

[RequireComponent(typeof(CharacterController))]
public class PlayerMovement : MonoBehaviour
{
    [Header("綁定設定")]
    [Tooltip("請將 Main Camera (掛載 MagicWindowCamera 的物件) 拖曳到這裡")]
    public Transform cameraTransform; 
    [Tooltip("請將 Input Action Asset 的 Move Action 綁定過來 (對應左搖桿)")]
    public InputActionReference moveAction; 

    [Header("移動參數")]
    public float moveSpeed = 3.0f;
    public float gravity = -9.81f;

    private CharacterController controller;
    private Vector2 inputVector;
    private Vector3 velocity;

    void Start()
    {
        controller = GetComponent<CharacterController>();
    }

    // 啟動與關閉 Input System 的監聽
    void OnEnable() 
    { 
        if (moveAction != null) moveAction.action.Enable(); 
    }
    
    void OnDisable() 
    { 
        if (moveAction != null) moveAction.action.Disable(); 
    }

    void Update()
    {
        if (moveAction == null || cameraTransform == null) return;

        // 1. 讀取實體搖桿的 2D 輸入值 (X為左右，Y為前後)
        inputVector = moveAction.action.ReadValue<Vector2>();

        if (inputVector.sqrMagnitude > 0.01f)
        {
            // 2. 取得相機當下的「正前方」與「正右方」
            Vector3 camForward = cameraTransform.forward;
            Vector3 camRight = cameraTransform.right;

            // 3. 將 Y 軸歸零並重新標準化，確保玩家只在水平面上移動 (不會飛起來)
            camForward.y = 0;
            camRight.y = 0;
            camForward.Normalize();
            camRight.Normalize();

            // 4. 將搖桿的輸入映射到相機的方向上
            Vector3 moveDirection = (camForward * inputVector.y + camRight * inputVector.x).normalized;

            // 5. 透過 CharacterController 移動玩家
            controller.Move(moveDirection * moveSpeed * Time.deltaTime);
        }

        // 6. 處理簡單的重力，確保玩家會貼在地上
        if (controller.isGrounded && velocity.y < 0)
        {
            velocity.y = -2f; 
        }
        velocity.y += gravity * Time.deltaTime;
        controller.Move(velocity * Time.deltaTime);
    }
}