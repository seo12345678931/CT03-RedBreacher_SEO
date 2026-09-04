using System.Collections.Generic;
using Unity.Cinemachine;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Controls;
using UnityEngine.UI;

[DisallowMultipleComponent]
[RequireComponent(typeof(Camera))]
[DefaultExecutionOrder(10000)]
public class MainGameCameraZoom : MonoBehaviour
{
    [Header("Zoom Range")]
    [SerializeField] private float minFieldOfView = 45f;
    [SerializeField] private float maxFieldOfView = 80f;

    [Header("Input")]
    [SerializeField] private float mouseWheelSensitivity = 0.02f;
    [SerializeField] private float pinchSensitivity = 0.08f;
    [SerializeField] private bool ignoreInputOverUi = true;

    [Header("Smoothing")]
    [SerializeField] private float zoomSmoothTime = 0.12f;

    private readonly List<RaycastResult> uiRaycastResults = new List<RaycastResult>();
    private Camera targetCamera;
    private CinemachineCamera targetCinemachineCamera;
    private float targetFieldOfView;
    private float currentFieldOfView;
    private float zoomVelocity;
    private float previousPinchDistance;
    private bool isPinching;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void EnsureMainCameraZoom()
    {
        Camera mainCamera = Camera.main;
        if (mainCamera != null && mainCamera.GetComponent<MainGameCameraZoom>() == null)
        {
            // 씬 연결 없이도 메인 전투 카메라에 줌 기능을 자동 보강한다.
            mainCamera.gameObject.AddComponent<MainGameCameraZoom>();
        }
    }

    private void Awake()
    {
        targetCamera = GetComponent<Camera>();
        targetCinemachineCamera = FindFirstObjectByType<CinemachineCamera>();
        currentFieldOfView = targetCinemachineCamera != null
            ? targetCinemachineCamera.Lens.FieldOfView
            : targetCamera.fieldOfView;
        targetFieldOfView = currentFieldOfView;
    }

    private void OnValidate()
    {
        minFieldOfView = Mathf.Clamp(minFieldOfView, 1f, 179f);
        maxFieldOfView = Mathf.Clamp(maxFieldOfView, minFieldOfView, 179f);
        zoomSmoothTime = Mathf.Max(0.01f, zoomSmoothTime);
    }

    private void Update()
    {
        HandleMouseWheel();
        HandleTouchPinch();
        ApplyZoom();
    }

    private void HandleMouseWheel()
    {
        if (Mouse.current == null)
        {
            return;
        }

        Vector2 pointerPosition = Mouse.current.position.ReadValue();
        if (ignoreInputOverUi && IsPointerOverUi(pointerPosition))
        {
            return;
        }

        float scrollDelta = Mouse.current.scroll.ReadValue().y;
        if (Mathf.Approximately(scrollDelta, 0f))
        {
            return;
        }

        // 휠을 아래로 내리면 FOV가 커지며 줌아웃된다.
        AddZoomInput(-scrollDelta * mouseWheelSensitivity);
    }

    private void HandleTouchPinch()
    {
        if (Touchscreen.current == null
            || GetActiveTouches(out TouchControl firstTouch, out TouchControl secondTouch) < 2)
        {
            isPinching = false;
            return;
        }

        Vector2 firstPosition = firstTouch.position.ReadValue();
        Vector2 secondPosition = secondTouch.position.ReadValue();
        Vector2 pinchCenter = (firstPosition + secondPosition) * 0.5f;
        if (ignoreInputOverUi && IsPointerOverUi(pinchCenter))
        {
            isPinching = false;
            return;
        }

        float currentDistance = Vector2.Distance(firstPosition, secondPosition);
        if (!isPinching)
        {
            // 핀치 시작 프레임은 기준 거리만 저장해 입력 튐을 막는다.
            previousPinchDistance = currentDistance;
            isPinching = true;
            return;
        }

        float pinchDelta = currentDistance - previousPinchDistance;
        previousPinchDistance = currentDistance;
        if (Mathf.Approximately(pinchDelta, 0f))
        {
            return;
        }

        // 손가락을 오므리면 FOV를 키워 줌아웃한다.
        AddZoomInput(-pinchDelta * pinchSensitivity);
    }

    private void AddZoomInput(float fieldOfViewDelta)
    {
        targetFieldOfView = Mathf.Clamp(
            targetFieldOfView + fieldOfViewDelta,
            minFieldOfView,
            maxFieldOfView);
    }

    private void ApplyZoom()
    {
        if (targetCamera == null)
        {
            return;
        }

        // Cinemachine 렌즈를 기준으로 보간해 실제 카메라와 값이 충돌하지 않게 한다.
        currentFieldOfView = Mathf.SmoothDamp(
            currentFieldOfView,
            targetFieldOfView,
            ref zoomVelocity,
            zoomSmoothTime);

        if (targetCinemachineCamera != null)
        {
            LensSettings lens = targetCinemachineCamera.Lens;
            lens.FieldOfView = currentFieldOfView;
            targetCinemachineCamera.Lens = lens;
            return;
        }

        targetCamera.fieldOfView = currentFieldOfView;
    }

    private int GetActiveTouches(out TouchControl firstTouch, out TouchControl secondTouch)
    {
        firstTouch = null;
        secondTouch = null;
        int activeTouchCount = 0;

        foreach (TouchControl touch in Touchscreen.current.touches)
        {
            if (!touch.press.isPressed)
            {
                continue;
            }

            if (activeTouchCount == 0)
            {
                firstTouch = touch;
            }
            else if (activeTouchCount == 1)
            {
                secondTouch = touch;
            }

            activeTouchCount++;
            if (activeTouchCount >= 2)
            {
                break;
            }
        }

        return activeTouchCount;
    }

    private bool IsPointerOverUi(Vector2 screenPosition)
    {
        if (EventSystem.current == null)
        {
            return false;
        }

        PointerEventData pointerData = new PointerEventData(EventSystem.current)
        {
            position = screenPosition
        };
        uiRaycastResults.Clear();
        EventSystem.current.RaycastAll(pointerData, uiRaycastResults);

        for (int i = 0; i < uiRaycastResults.Count; i++)
        {
            GameObject hitObject = uiRaycastResults[i].gameObject;
            if (hitObject.GetComponentInParent<Selectable>() != null
                || hitObject.GetComponentInParent<ScrollRect>() != null)
            {
                return true;
            }
        }

        return false;
    }
}
