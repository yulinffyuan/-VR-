using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.EventSystems;

public class MagicWindowCamera : MonoBehaviour
{
    // 【三層架構】
    private GameObject yawContainer;  // 最外層：負責搖桿/觸控的左右轉向
    private GameObject gyroContainer; // 中間層：負責陀螺儀真實硬體方向

    [Header("輸入綁定")]
    public InputActionReference recenterAction;
    public InputActionReference fineTuneAction;

    [Header("橫向視角基準")]
    public float landscapeZOffset = -90f;

    [Header("微調速度與觸控設定")]
    public float baseFineTuneSpeed = 150f; 
    public float touchSensitivity = 0.15f; 
    public bool invertY = false;
    
    [Header("視角限制")]
    public float maxPitch = 80f; // 限制上下看最大的角度

    private float currentFineTuneSpeed;
    private float currentTouchSensitivity;
    private float gyroSensitivity = 1.0f;
    private bool isTouchEnabled = false;

    private float joystickYaw = 0f;
    private float joystickPitch = 0f;
    
    // 用來儲存按下重置時的「反向抵銷值」，確保精準面朝正前方
    private Quaternion gyroCalibrationOffset = Quaternion.identity;

    void Start()
    {
        RefreshSettings();

        // 1. 建立最外層 (身體)
        yawContainer = new GameObject("YawContainer");
        yawContainer.transform.position = transform.position;
        yawContainer.transform.parent = transform.parent; 

        // 2. 建立中間層 (頭部)
        gyroContainer = new GameObject("GyroContainer");
        gyroContainer.transform.position = transform.position;
        gyroContainer.transform.parent = yawContainer.transform;

        // 3. 攝影機本體 (眼睛)
        transform.SetParent(gyroContainer.transform);
        transform.localPosition = Vector3.zero;

        if (AttitudeSensor.current != null)
        {
            InputSystem.EnableDevice(AttitudeSensor.current);
            Invoke(nameof(Recenter), 0.5f); // 延遲校正確保感測器已啟動
        }
    }

    public void RefreshSettings()
    {
        float joySens = PlayerPrefs.GetFloat("JoystickSensitivity", 1.0f);
        currentFineTuneSpeed = baseFineTuneSpeed * joySens;
        currentTouchSensitivity = touchSensitivity * joySens;
        gyroSensitivity = PlayerPrefs.GetFloat("GyroSensitivity", 1.0f);
        isTouchEnabled = PlayerPrefs.GetInt("TouchEnabled", 0) == 1;
    }

// 新增這個專屬的接收方法
    private void OnRecenterPerformed(InputAction.CallbackContext ctx)
    {
        Recenter();
    }

    void OnEnable() 
    {
        if (recenterAction != null && recenterAction.action != null) 
        { 
            recenterAction.action.Enable(); 
            // 改用具名函式綁定
            recenterAction.action.performed += OnRecenterPerformed; 
        }
        if (fineTuneAction != null && fineTuneAction.action != null) fineTuneAction.action.Enable();
    }

    void OnDisable() 
    {
        if (recenterAction != null && recenterAction.action != null) 
        { 
            // 精準解除綁定
            recenterAction.action.performed -= OnRecenterPerformed; 
        }
        if (fineTuneAction != null && fineTuneAction.action != null) fineTuneAction.action.Disable();
    }

    void Update()
    {
        // 1. 處理陀螺儀 (保留真實 Roll 軸自然傾斜)
        if (AttitudeSensor.current != null)
        {
            Quaternion deviceAttitude = AttitudeSensor.current.attitude.ReadValue();
            Quaternion rawGyro = new Quaternion(deviceAttitude.x, deviceAttitude.y, -deviceAttitude.z, -deviceAttitude.w);
            
            Quaternion landscapeRot = Quaternion.Euler(90f, 0f, landscapeZOffset);
            Quaternion orientedGyro = landscapeRot * rawGyro;

            // 套用重置抵銷值 (完美面朝 Z 軸前方)
            Quaternion calibratedGyro = gyroCalibrationOffset * orientedGyro;

            if (Mathf.Abs(gyroSensitivity - 1.0f) > 0.01f)
            {
                calibratedGyro = Quaternion.SlerpUnclamped(Quaternion.identity, calibratedGyro, gyroSensitivity);
            }

            // 【保留自然傾斜】直接套用完整旋轉，不鎖死 Z 軸
            gyroContainer.transform.localRotation = calibratedGyro;
        }

        if (Time.timeScale == 0f) return;

        // 2. 處理搖桿微調
        Vector2 finalInput = Vector2.zero;
        if (fineTuneAction != null) 
        {
            finalInput = fineTuneAction.action.ReadValue<Vector2>();
        }

        if (finalInput.sqrMagnitude > 0.01f)
        {
            ApplyInputDelta(finalInput.x * currentFineTuneSpeed * Time.deltaTime, finalInput.y * currentFineTuneSpeed * Time.deltaTime);
        }

        // 3. 處理觸控滑動
        if (isTouchEnabled)
        {
            HandleDirectTouch();
        }

        // 輸出：搖桿與觸控拆分在內外兩層，確保與陀螺儀互不干涉
        yawContainer.transform.localRotation = Quaternion.Euler(0f, joystickYaw, 0f);
        transform.localRotation = Quaternion.Euler(joystickPitch, 0f, 0f); 
    }

    private void HandleDirectTouch()
    {
        if (Touchscreen.current != null && Touchscreen.current.touches.Count > 0)
        {
            var touch = Touchscreen.current.touches[0];
            if (EventSystem.current != null && EventSystem.current.IsPointerOverGameObject(touch.touchId.ReadValue())) 
                return;

            if (touch.phase.ReadValue() == UnityEngine.InputSystem.TouchPhase.Moved)
            {
                ApplyInputDelta(touch.delta.ReadValue().x * currentTouchSensitivity, touch.delta.ReadValue().y * currentTouchSensitivity);
            }
        }
        else if (Mouse.current != null && Mouse.current.leftButton.isPressed)
        {
            if (EventSystem.current != null && EventSystem.current.IsPointerOverGameObject()) 
                return;

            ApplyInputDelta(Mouse.current.delta.ReadValue().x * currentTouchSensitivity, Mouse.current.delta.ReadValue().y * currentTouchSensitivity);
        }
    }

    private void ApplyInputDelta(float deltaX, float deltaY)
    {
        joystickYaw += deltaX;
        
        float pitchDir = invertY ? 1f : -1f;
        joystickPitch += deltaY * pitchDir;
        joystickPitch = Mathf.Clamp(joystickPitch, -maxPitch, maxPitch); 
    }

    public void Recenter()
    {
        // 1. 歸零搖桿與觸控
        joystickYaw = 0f;
        joystickPitch = 0f;

        // 2. 產生「反向抵銷值」並死綁正前方
        if (AttitudeSensor.current != null)
        {
            Quaternion deviceAttitude = AttitudeSensor.current.attitude.ReadValue();
            Quaternion rawGyro = new Quaternion(deviceAttitude.x, deviceAttitude.y, -deviceAttitude.z, -deviceAttitude.w);
            Quaternion landscapeRot = Quaternion.Euler(90f, 0f, landscapeZOffset);
            Quaternion orientedGyro = landscapeRot * rawGyro;

            // 記錄當下的絕對旋轉，並產生 Inverse(反轉值) 以備 Update 抵銷用
            gyroCalibrationOffset = Quaternion.Inverse(orientedGyro);
        }
    }
}