using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Controls;

public class baseUIZoomController : MonoBehaviour
{
    [SerializeField] private RectTransform targetUI;
    [SerializeField] private Camera uiCamera;
    
    [Header("Zoom")]
    [SerializeField] private float mouseWheelZoomSpeed = 4f;
    [SerializeField] private float pinchZoomSpeed = 0.005f;
    
    [Header("Drag")]
    [SerializeField] private float dragSpeed = 10f;
    
    [Header("Zoom Min/Max Scale")]
    [SerializeField] private float minScale = 0.5f;
    [SerializeField] private float maxScale = 2.0f;
    
    [Header("예외처리 관련코드")]
    [Tooltip("타겟으로 지정된 UI가 화면 바깥으로 나가 더 이상 조작이 안될 경우를 방지하기 위해 만든 코드.")]
    [SerializeField] private bool clampToParentBounds = true;
    [SerializeField] private Vector2 boundsPadding;

    private bool isMouseDragging;
    private bool isTouchDragging;
    private Vector2 previousMouseLocalPosition;
    private Vector2 previousTouchLocalPosition;

    private void Awake()
    {
        if (targetUI == null)
        {
            targetUI = transform as RectTransform;
        }
    }

    private void Start()
    {
        ClampTargetToParentBounds();
    }

    // 조작 갱신
    private void Update()
    {
        HandleMouseWheelZoom();
        HandleTouchPinchZoom();
        HandleMouseDrag();
        HandleTouchDrag();
    }

    // 마우스 줌
    private void HandleMouseWheelZoom()
    {
        if (targetUI == null || Mouse.current == null)
        {
            return;
        }

        Vector2 scrollValue = Mouse.current.scroll.ReadValue();
        float scrollInput = scrollValue.y * 0.01f;
        if (Mathf.Approximately(scrollInput, 0f))
        {
            return;
        }

        Vector2 mousePosition = Mouse.current.position.ReadValue();
        if (!IsScreenPointInsideGameView(mousePosition))
        {
            return;
        }

        if (!RectTransformUtility.RectangleContainsScreenPoint(targetUI, mousePosition, uiCamera))
        {
            return;
        }

        ApplyZoom(scrollInput * mouseWheelZoomSpeed);
    }

    // 터치 줌
    private void HandleTouchPinchZoom()
    {
        if (targetUI == null || Touchscreen.current == null)
        {
            return;
        }

        int activeTouchCount = GetActiveTouches(out TouchControl firstTouch, out TouchControl secondTouch);
        if (activeTouchCount < 2)
        {
            return;
        }

        Vector2 firstPosition = firstTouch.position.ReadValue();
        Vector2 secondPosition = secondTouch.position.ReadValue();

        Vector2 pinchCenter = (firstPosition + secondPosition) * 0.5f;
        if (!RectTransformUtility.RectangleContainsScreenPoint(targetUI, pinchCenter, uiCamera))
        {
            return;
        }

        Vector2 firstPreviousPosition = firstPosition - firstTouch.delta.ReadValue();
        Vector2 secondPreviousPosition = secondPosition - secondTouch.delta.ReadValue();

        float previousDistance = Vector2.Distance(firstPreviousPosition, secondPreviousPosition);
        float currentDistance = Vector2.Distance(firstPosition, secondPosition);
        float pinchDelta = currentDistance - previousDistance;

        if (Mathf.Approximately(pinchDelta, 0f))
        {
            return;
        }

        ApplyZoom(pinchDelta * pinchZoomSpeed);
    }

    // 마우스 드래그
    private void HandleMouseDrag()
    {
        if (targetUI == null || Mouse.current == null)
        {
            return;
        }

        Vector2 mousePosition = Mouse.current.position.ReadValue();

        if (!IsScreenPointInsideGameView(mousePosition))
        {
            isMouseDragging = false;
            ClampTargetToParentBounds();
            return;
        }

        if (Mouse.current.leftButton.wasPressedThisFrame)
        {
            isMouseDragging = TryStartDrag(mousePosition, out previousMouseLocalPosition);
        }

        if (Mouse.current.leftButton.wasReleasedThisFrame || !Mouse.current.leftButton.isPressed)
        {
            isMouseDragging = false;
        }

        if (!isMouseDragging || !Mouse.current.leftButton.isPressed)
        {
            return;
        }

        MoveByPointer(mousePosition, ref previousMouseLocalPosition);
    }

    // 터치 드래그
    private void HandleTouchDrag()
    {
        if (targetUI == null || Touchscreen.current == null)
        {
            return;
        }

        int activeTouchCount = GetActiveTouches(out TouchControl firstTouch, out _);
        if (activeTouchCount != 1)
        {
            isTouchDragging = false;
            return;
        }

        Vector2 touchPosition = firstTouch.position.ReadValue();

        if (!isTouchDragging)
        {
            if (firstTouch.press.wasPressedThisFrame)
            {
                isTouchDragging = TryStartDrag(touchPosition, out previousTouchLocalPosition);
            }

            return;
        }

        MoveByPointer(touchPosition, ref previousTouchLocalPosition);
    }

    // 터치 활성화
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

    private void ApplyZoom(float zoomDelta)
    {
        Vector3 currentScale = targetUI.localScale;
        float nextScale = Mathf.Clamp(currentScale.x + zoomDelta, minScale, maxScale);
        targetUI.localScale = new Vector3(nextScale, nextScale, 1f);
        ClampTargetToParentBounds();
    }

    private bool TryStartDrag(Vector2 screenPosition, out Vector2 localPosition)
    {
        localPosition = default;

        if (!RectTransformUtility.RectangleContainsScreenPoint(targetUI, screenPosition, uiCamera))
        {
            return false;
        }

        return TryGetLocalPointerPosition(screenPosition, out localPosition);
    }

    private void MoveByPointer(Vector2 screenPosition, ref Vector2 previousLocalPosition)
    {
        if (!TryGetLocalPointerPosition(screenPosition, out Vector2 currentLocalPosition))
        {
            return;
        }

        Vector2 delta = (currentLocalPosition - previousLocalPosition) * dragSpeed;
        targetUI.anchoredPosition += delta;
        ClampTargetToParentBounds();
        previousLocalPosition = currentLocalPosition;
    }

    private bool TryGetLocalPointerPosition(Vector2 screenPosition, out Vector2 localPosition)
    {
        localPosition = default;

        RectTransform parentRect = targetUI.parent as RectTransform;
        if (parentRect == null)
        {
            return false;
        }

        return RectTransformUtility.ScreenPointToLocalPointInRectangle(parentRect, screenPosition, uiCamera, out localPosition);
    }

    private bool IsScreenPointInsideGameView(Vector2 screenPosition)
    {
        return screenPosition.x >= 0f
            && screenPosition.x <= Screen.width
            && screenPosition.y >= 0f
            && screenPosition.y <= Screen.height;
    }

    // UI 영역 밖으로 나가면 다시 위치를 경계 안으로 보정해서 예외처리.
    private void ClampTargetToParentBounds()
    {
        RectTransform parentRect = targetUI != null ? targetUI.parent as RectTransform : null;
        if (!clampToParentBounds || targetUI == null || parentRect == null)
        {
            return;
        }

        Rect parentLocalRect = parentRect.rect;
        Bounds targetBounds = RectTransformUtility.CalculateRelativeRectTransformBounds(parentRect, targetUI);

        float minX = parentLocalRect.xMin + boundsPadding.x;
        float maxX = parentLocalRect.xMax - boundsPadding.x;
        float minY = parentLocalRect.yMin + boundsPadding.y;
        float maxY = parentLocalRect.yMax - boundsPadding.y;

        Vector2 correction = Vector2.zero;

        if (targetBounds.size.x > maxX - minX)
        {
            if (targetBounds.min.x > minX)
            {
                correction.x = minX - targetBounds.min.x;
            }
            else if (targetBounds.max.x < maxX)
            {
                correction.x = maxX - targetBounds.max.x;
            }
        }
        else if (targetBounds.min.x < minX)
        {
            correction.x = minX - targetBounds.min.x;
        }
        else if (targetBounds.max.x > maxX)
        {
            correction.x = maxX - targetBounds.max.x;
        }

        if (targetBounds.size.y > maxY - minY)
        {
            if (targetBounds.min.y > minY)
            {
                correction.y = minY - targetBounds.min.y;
            }
            else if (targetBounds.max.y < maxY)
            {
                correction.y = maxY - targetBounds.max.y;
            }
        }
        else if (targetBounds.min.y < minY)
        {
            correction.y = minY - targetBounds.min.y;
        }
        else if (targetBounds.max.y > maxY)
        {
            correction.y = maxY - targetBounds.max.y;
        }

        targetUI.anchoredPosition += correction;
    }
}
