using UnityEngine;
using UnityEngine.InputSystem;

public class MagicWindowCamera : MonoBehaviour
{
    // 動態生成的隱形父物件，用來隔離座標系
    private GameObject gyroContainer;

    [Header("輸入綁定")]
    public InputActionReference recenterAction;

    [Header("橫向視角修正")]
    [Tooltip("如果是橫向向左 (Landscape Left) 設為 -90；橫向向右 (Landscape Right) 設為 90")]
    public float landscapeZOffset = -90f;

    void Start()
    {
        if (AttitudeSensor.current != null)
        {
            InputSystem.EnableDevice(AttitudeSensor.current);
            
            // 1. 在遊戲開始時，動態建立一個隱形的父物件外盒
            gyroContainer = new GameObject("GyroContainer");
            
            // 2. 將外盒對齊相機當前的位置，並繼承原本相機的父層 (PlayerBase -> CameraPivot)
            gyroContainer.transform.position = transform.position;
            gyroContainer.transform.parent = transform.parent; 
            
            // 3. 把相機塞進這個外盒裡面
            transform.SetParent(gyroContainer.transform);

            // 延遲0.5秒進行第一次自動校正
            Invoke(nameof(Recenter), 0.5f);
        }
        else
        {
            Debug.LogError("抓不到姿態感測器。");
        }
    }

    void OnEnable() 
    {
        if (recenterAction != null)
        {
            recenterAction.action.Enable();
            recenterAction.action.performed += ctx => Recenter();
        }
    }

    void OnDisable() 
    {
        if (recenterAction != null)
        {
            recenterAction.action.performed -= ctx => Recenter();
            recenterAction.action.Disable();
        }
    }

    void Update()
    {
        if (AttitudeSensor.current != null)
        {
            // 【關鍵】：相機本身不再做任何數學偏移，單純將右手的陀螺儀轉換為左手座標後，直接套用
            Quaternion deviceAttitude = AttitudeSensor.current.attitude.ReadValue();
            transform.localRotation = new Quaternion(deviceAttitude.x, deviceAttitude.y, -deviceAttitude.z, -deviceAttitude.w);
        }
    }

    public void Recenter()
    {
        if (AttitudeSensor.current == null || gyroContainer == null) return;
        
        // 1. 將外盒的基礎角度重置：X軸轉90度(將看天空改為看前方)
        gyroContainer.transform.rotation = Quaternion.Euler(90f, 0f, 0f);
        
        // 2. 依照橫向模式，以外盒的自身 Z 軸進行旋轉，把歪斜的畫面轉正
        gyroContainer.transform.Rotate(0f, 0f, landscapeZOffset, Space.Self);
        
        // 3. 讀取當前相機在世界座標中的水平偏航角 (Yaw)
        float currentYaw = transform.eulerAngles.y;
        
        // 4. 將外盒在世界座標中「反向轉回去」，完成視角正前方的校正
        gyroContainer.transform.Rotate(0f, -currentYaw, 0f, Space.World);
        
        Debug.Log("已執行視角校正 (Recenter)");
    }
}