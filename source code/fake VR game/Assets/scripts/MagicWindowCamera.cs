using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.EventSystems; // 必須引入以偵測 UI 點擊

public class MagicWindowCamera : MonoBehaviour
{
    private GameObject gyroContainer;

    [Header("輸入綁定")]
    public InputActionReference recenterAction;
    public InputActionReference fineTuneAction;

    [Header("橫向視角修正")]
    public float landscapeZOffset = -90f;

    [Header("微調速度與觸控設定")]
    public float baseFineTuneSpeed = 30f;
    public float touchSensitivity = 0.15f; 
    public bool invertY = false;

    private float currentFineTuneSpeed;
    private float currentTouchSensitivity;
    private float gyroSensitivity = 1.0f;
    private bool isTouchEnabled = false;

    void Start()
    {
        float joySens = PlayerPrefs.GetFloat("JoystickSensitivity", 1.0f);
        currentFineTuneSpeed = baseFineTuneSpeed * joySens;
        currentTouchSensitivity = touchSensitivity * joySens;
        gyroSensitivity = PlayerPrefs.GetFloat("GyroSensitivity", 1.0f);
        isTouchEnabled = PlayerPrefs.GetInt("TouchEnabled", 0) == 1;

        gyroContainer = new GameObject("GyroContainer");
        gyroContainer.transform.position = transform.position;
        gyroContainer.transform.parent = transform.parent; 
        transform.SetParent(gyroContainer.transform);

        if (AttitudeSensor.current != null)
        {
            InputSystem.EnableDevice(AttitudeSensor.current);
            Invoke(nameof(Recenter), 0.5f);
        }
    }

    void OnEnable() 
    {
        if (recenterAction != null) { recenterAction.action.Enable(); recenterAction.action.performed += ctx => Recenter(); }
        if (fineTuneAction != null) fineTuneAction.action.Enable();
    }

    void OnDisable() 
    {
        if (recenterAction != null) { recenterAction.action.performed -= ctx => Recenter(); recenterAction.action.Disable(); }
        if (fineTuneAction != null) fineTuneAction.action.Disable();
    }

    void Update()
    {
        // 1. 處理陀螺儀硬體數據 (即使暫停也保持運作，避免解除暫停時畫面瞬間跳躍)
        if (AttitudeSensor.current != null)
        {
            Quaternion deviceAttitude = AttitudeSensor.current.attitude.ReadValue();
            Quaternion unityRotation = new Quaternion(deviceAttitude.x, deviceAttitude.y, -deviceAttitude.z, -deviceAttitude.w);
            
            if (Mathf.Abs(gyroSensitivity - 1.0f) > 0.01f)
            {
                unityRotation = Quaternion.SlerpUnclamped(Quaternion.identity, unityRotation, gyroSensitivity);
            }
            transform.localRotation = unityRotation;
        }

        // 【防線一】：如果遊戲已經暫停，停止處理所有後續的視角微調與觸控輸入
        if (Time.timeScale == 0f) return;

        // 2. 處理右搖桿與鍵盤
        Vector2 finalInput = Vector2.zero;
        if (fineTuneAction != null) 
        {
            finalInput = fineTuneAction.action.ReadValue<Vector2>();
        }

        if (finalInput.sqrMagnitude > 0.01f && gyroContainer != null)
        {
            gyroContainer.transform.Rotate(0f, finalInput.x * currentFineTuneSpeed * Time.deltaTime, 0f, Space.World);
            float pitchDirection = invertY ? 1f : -1f;
            gyroContainer.transform.Rotate(transform.right, finalInput.y * currentFineTuneSpeed * pitchDirection * Time.deltaTime, Space.World);
        }

        // 3. 處理「直接觸碰畫面滑動」
        if (isTouchEnabled && gyroContainer != null)
        {
            HandleDirectTouch();
        }
    }

    private void HandleDirectTouch()
    {
        // A. 手機實機觸控
        if (Touchscreen.current != null && Touchscreen.current.touches.Count > 0)
        {
            var touch = Touchscreen.current.touches[0];

            // 【防線二】：檢查手指是否點擊在任何 UI 元素上 (例如虛擬搖桿或按鈕)
            if (EventSystem.current != null && EventSystem.current.IsPointerOverGameObject(touch.touchId.ReadValue())) 
                return;

            if (touch.phase.ReadValue() == UnityEngine.InputSystem.TouchPhase.Moved)
            {
                ApplyRotation(touch.delta.ReadValue());
            }
        }
        // B. 電腦編輯器滑鼠拖曳
        else if (Mouse.current != null && Mouse.current.leftButton.isPressed)
        {
            // 【防線二】：檢查滑鼠是否點擊在任何 UI 元素上
            if (EventSystem.current != null && EventSystem.current.IsPointerOverGameObject()) 
                return;

            ApplyRotation(Mouse.current.delta.ReadValue());
        }
    }

    private void ApplyRotation(Vector2 delta)
    {
        float yaw = delta.x * currentTouchSensitivity;
        float pitch = delta.y * currentTouchSensitivity * (invertY ? -1f : 1f);

        gyroContainer.transform.Rotate(0f, yaw, 0f, Space.World);
        gyroContainer.transform.Rotate(transform.right, -pitch, Space.World);
    }

    public void Recenter()
    {
        if (AttitudeSensor.current == null || gyroContainer == null) return;
        gyroContainer.transform.rotation = Quaternion.Euler(90f, 0f, 0f);
        gyroContainer.transform.Rotate(0f, 0f, landscapeZOffset, Space.Self);
        float currentYaw = transform.eulerAngles.y;
        gyroContainer.transform.Rotate(0f, -currentYaw, 0f, Space.World);
    }

    public void RefreshSettings()
    {
        float joySens = PlayerPrefs.GetFloat("JoystickSensitivity", 1.0f);
        currentFineTuneSpeed = baseFineTuneSpeed * joySens;
        currentTouchSensitivity = touchSensitivity * joySens;
        gyroSensitivity = PlayerPrefs.GetFloat("GyroSensitivity", 1.0f);
        isTouchEnabled = PlayerPrefs.GetInt("TouchEnabled", 0) == 1;
    }
}