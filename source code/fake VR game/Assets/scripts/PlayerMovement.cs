using UnityEngine;
using UnityEngine.InputSystem;

[RequireComponent(typeof(CharacterController))]
public class PlayerMovement : MonoBehaviour
{
    [Header("移動設定")]
    public float baseMoveSpeed = 3.0f; // 基礎移動速度
    private float currentMoveSpeed;

    [Header("輸入綁定")]
    [Tooltip("請將 PlayerControls 裡的 Move Action 拖曳到這裡")]
    public InputActionReference moveAction;

    [Header("視角參考")]
    [Tooltip("請拖曳 MainCamera 進來，讓移動方向能根據玩家面對的方向")]
    public Transform cameraTransform; 

    // 內部元件參考
    private CharacterController controller;
    private bool isDebugMode;
    
    // 簡單的重力處理
    private float gravity = -9.81f;
    private float velocityY = 0f;

    void Start()
    {
        // 自動抓取身上的 CharacterController
        controller = GetComponent<CharacterController>();

        // 【防呆機制】：如果沒拖曳相機，自動尋找標籤為 MainCamera 的物件
        if (cameraTransform == null && Camera.main != null)
        {
            cameraTransform = Camera.main.transform;
        }

        // 初始化設定
        RefreshSettings();
    }

    void OnEnable()
    {
        if (moveAction != null && moveAction.action != null) 
        {
            moveAction.action.Enable();
        }
    }

    void OnDisable()
    {
        if (moveAction != null && moveAction.action != null) 
        {
            moveAction.action.Disable();
        }
    }

    /// <summary>
    /// 供外部 (如 PauseMenuManager) 呼叫，以即時更新設定
    /// </summary>
    public void RefreshSettings()
    {
        // 【修正】：移動速度保持獨立，不再乘上視角靈敏度 (JoystickSensitivity)
        currentMoveSpeed = baseMoveSpeed; 
        
        isDebugMode = PlayerPrefs.GetInt("DebugMode", 0) == 1;
    }

    void Update()
    {
        // 1. 如果遊戲暫停 (Time.timeScale == 0)，就不處理移動
        if (Time.timeScale == 0f) return;

        // 2. 取得搖桿/鍵盤的移動輸入向量
        Vector2 input = Vector2.zero;

        // 【防呆機制】：嚴格檢查 moveAction 是否存在，避免 NullReferenceException
        if (moveAction != null && moveAction.action != null)
        {
            input = moveAction.action.ReadValue<Vector2>();
        }

        // 3. 計算移動方向 (根據攝影機朝向)
        Vector3 moveDirection = Vector3.zero;

        if (cameraTransform != null)
        {
            // 取得相機的前方與右方
            Vector3 forward = cameraTransform.forward;
            Vector3 right = cameraTransform.right;

            // 【重要】：將 Y 軸強制歸零並重新標準化，防止玩家往上看時「飛天」或往下看時「遁地」
            forward.y = 0f;
            right.y = 0f;
            forward.Normalize();
            right.Normalize();

            // 根據玩家輸入與相機方向，組合成最終的 3D 移動方向
            moveDirection = (forward * input.y) + (right * input.x);
        }

        // 4. 套用移動與重力 (確保緊貼地面)
        if (controller != null)
        {
            if (controller.isGrounded)
            {
                velocityY = -2f; // 給予微小向下的力量確保與地面貼合
            }
            else
            {
                velocityY += gravity * Time.deltaTime; // 自由落體
            }

            // 合併水平移動與垂直重力
            Vector3 finalVelocity = (moveDirection * currentMoveSpeed) + (Vector3.up * velocityY);
            
            // 執行移動
            controller.Move(finalVelocity * Time.deltaTime);
        }
    }
}