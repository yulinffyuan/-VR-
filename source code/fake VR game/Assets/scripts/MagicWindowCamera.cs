using UnityEngine;
using UnityEngine.InputSystem;

public class MagicWindowCamera : MonoBehaviour
{
    private GameObject gyroContainer;

    [Header("輸入綁定")]
    public InputActionReference recenterAction;
    public InputActionReference fineTuneAction;

    [Header("橫向視角修正")]
    public float landscapeZOffset = -90f;

    [Header("搖桿微調設定")]
    public float baseFineTuneSpeed = 30f;
    public bool invertY = false;

    private float currentFineTuneSpeed;
    private float gyroSensitivity = 1.0f;

    void Start()
    {
        float joySens = PlayerPrefs.GetFloat("JoystickSensitivity", 1.0f);
        currentFineTuneSpeed = baseFineTuneSpeed * joySens;
        gyroSensitivity = PlayerPrefs.GetFloat("GyroSensitivity", 1.0f);

        // 【關鍵修復】：把建立外盒的程式碼移到外面！
        // 這樣就算在沒有陀螺儀的電腦編輯器中，依然會建立外盒供鍵盤轉動
        gyroContainer = new GameObject("GyroContainer");
        gyroContainer.transform.position = transform.position;
        gyroContainer.transform.parent = transform.parent; 
        transform.SetParent(gyroContainer.transform);

        // 接著再檢查是否有陀螺儀
        if (AttitudeSensor.current != null)
        {
            InputSystem.EnableDevice(AttitudeSensor.current);
            Invoke(nameof(Recenter), 0.5f);
        }
        else
        {
            // 在電腦上測試時會印出這行提示
            Debug.LogWarning("未偵測到手機陀螺儀。已啟用純搖桿/鍵盤視角模式供編輯器測試。");
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
        // 1. 處理陀螺儀 (實機上才會有反應)
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

        // 2. 處理右搖桿/鍵盤方向鍵微調
        Vector2 fineTuneInput = Vector2.zero;
        if (fineTuneAction != null) 
        {
            fineTuneInput = fineTuneAction.action.ReadValue<Vector2>();
        }

        if (fineTuneInput.sqrMagnitude > 0.01f && gyroContainer != null)
        {
            // 左右旋轉
            gyroContainer.transform.Rotate(0f, fineTuneInput.x * currentFineTuneSpeed * Time.deltaTime, 0f, Space.World);
            // 上下旋轉
            float pitchDirection = invertY ? 1f : -1f;
            gyroContainer.transform.Rotate(transform.right, fineTuneInput.y * currentFineTuneSpeed * pitchDirection * Time.deltaTime, Space.World);
        }
    }

    public void Recenter()
    {
        if (AttitudeSensor.current == null || gyroContainer == null) return;
        gyroContainer.transform.rotation = Quaternion.Euler(90f, 0f, 0f);
        gyroContainer.transform.Rotate(0f, 0f, landscapeZOffset, Space.Self);
        float currentYaw = transform.eulerAngles.y;
        gyroContainer.transform.Rotate(0f, -currentYaw, 0f, Space.World);
    }
}