using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;

[DisallowMultipleComponent]
public sealed class PlayerBossDodgeController : MonoBehaviour
{
    [Header("보스전 드래그 회피")]
    [SerializeField] private float minimumDragDistance = 40f;
    [SerializeField] private float dodgeDistance = 2.5f;
    [SerializeField] private float dodgeDuration = 0.2f;
    [SerializeField] private float invulnerabilityDuration = 0.25f;
    [SerializeField] private float dodgeCooldown = 1.5f;
    [SerializeField] private float collisionRadius = 0.45f;
    [SerializeField] private float wallClearance = 0.05f;
    [SerializeField] private LayerMask obstacleMask;
    [SerializeField] private AudioClip[] dodgeClips;
    [SerializeField] private AudioSource dodgeSource;

    [Header("Boss Overlap Guard")]
    [SerializeField] private bool preventBossOverlap = true;
    [SerializeField] private float bossDodgeClearance = 0.35f;

    [Header("회피 속도감 연출")]
    [Tooltip("값이 클수록 회피 초반이 더 폭발적으로 가속됩니다. (이전 기본값 3)")]
    [SerializeField, Range(1f, 6f)] private float dodgeEaseExponent = 4.5f;

    [Header("퍼펙트 회피 보상")]
    [SerializeField, Range(0f, 2f)] private float perfectDodgeAttackBonus = 0.2f;
    [SerializeField] private float perfectDodgeBuffDuration = 3f;

    [Header("퍼펙트 회피 피드백")]
    [SerializeField] private TMP_FontAsset perfectDodgeFont;
    [SerializeField] private string perfectDodgeMessage = "PERFECT DODGE";
    [SerializeField] private Color perfectDodgeTextColor = new Color(0.35f, 1f, 1f, 1f);
    [SerializeField] private float perfectDodgeTextSize = 3.5f;
    [SerializeField] private float perfectDodgeFeedbackDuration = 0.6f;
    [SerializeField] private Vector3 perfectDodgeTextOffset = new Vector3(0f, 1.2f, 0f);
    [SerializeField] private Color perfectDodgeFlashColor = new Color(0.45f, 1f, 1f, 1f);
    [SerializeField] private AudioSource perfectDodgeSound;

    [Header("회피 쿨타임 UI")]
    [SerializeField] private GameObject dodgeUiRoot;
    [SerializeField] private Image cooldownOverlay;
    [SerializeField] private TMP_Text cooldownText;

    private readonly List<RaycastResult> uiRaycastResults = new List<RaycastResult>();
    private PlayerController player;
    private BossEncounterManager bossEncounterManager;
    private const float BossEncounterSearchInterval = 1f;
    private float nextBossEncounterSearchTime;
    private CombatHealth health;
    private Camera mainCamera;
    private Vector2 dragStartPosition;
    private Vector3 dodgeStartPosition;
    private Vector3 dodgeDestination;
    private float dodgeElapsed;
    private float nextDodgeTime;
    private float perfectDodgeWindowUntil;
    private float attackBuffUntil;
    private int activeTouchId = -1;
    private bool isDragging;
    private bool perfectDodgeTriggered;
    private GameObject activeFeedbackText;
    private SpriteRenderer[] flashingRenderers;
    private Color[] originalRendererColors;
    private AfterimageEmitter afterimageEmitter;

    public bool IsDodging { get; private set; }
    public float CooldownRemaining => Mathf.Max(0f, nextDodgeTime - Time.time);
    public bool IsAttackBuffActive => Time.time < attackBuffUntil;
    public float AttackDamageMultiplier => IsAttackBuffActive
        ? 1f + Mathf.Max(0f, perfectDodgeAttackBonus)
        : 1f;
    private float DodgeCooldownValue => player != null && player.UnitConfig != null
        ? player.UnitConfig.BossDodgeCooldown
        : Mathf.Max(0f, dodgeCooldown);

    public static PlayerBossDodgeController Ensure(PlayerController owner)
    {
        PlayerBossDodgeController controller = owner.GetComponent<PlayerBossDodgeController>();
        if (controller == null)
        {
            controller = owner.gameObject.AddComponent<PlayerBossDodgeController>();
        }

        controller.player = owner;
        controller.health = owner.Health;
        return controller;
    }

    private void Awake()
    {
        player = GetComponent<PlayerController>();
        health = GetComponent<CombatHealth>();
        afterimageEmitter = GetComponent<AfterimageEmitter>();
        if (afterimageEmitter == null)
        {
            afterimageEmitter = gameObject.AddComponent<AfterimageEmitter>();
        }

        int wallLayer = LayerMask.NameToLayer("Wall");
        if (obstacleMask.value == 0 && wallLayer >= 0)
        {
            obstacleMask = 1 << wallLayer;
        }
    }

    private void OnEnable()
    {
        health ??= GetComponent<CombatHealth>();
        if (health != null)
        {
            health.OnDamageBlockedByInvulnerability += HandleDamageBlocked;
        }
    }

    private void OnDisable()
    {
        if (health != null)
        {
            health.OnDamageBlockedByInvulnerability -= HandleDamageBlocked;
        }

        CleanupPerfectDodgeFeedback();
    }

    private void Update()
    {
        ResolveReferences();
        if (!CanReceiveDodgeInput())
        {
            CancelDrag();
            IsDodging = false;
            attackBuffUntil = 0f;
            RefreshCooldownUi(false);
            return;
        }

        RefreshCooldownUi(true);
        if (IsDodging)
        {
            UpdateDodge();
            return;
        }

        HandleTouchInput();
        HandleMouseInput();
    }

    private bool CanReceiveDodgeInput()
    {
        return bossEncounterManager != null
            && bossEncounterManager.IsEncounterActive
            && health != null
            && !health.IsDead;
    }

    private void HandleTouchInput()
    {
        if (Touchscreen.current == null)
        {
            return;
        }

        if (!isDragging)
        {
            foreach (UnityEngine.InputSystem.Controls.TouchControl touch in Touchscreen.current.touches)
            {
                if (!touch.press.wasPressedThisFrame)
                {
                    continue;
                }

                Vector2 position = touch.position.ReadValue();
                if (!IsPointerOverUi(position))
                {
                    BeginDrag(position, touch.touchId.ReadValue());
                }
                return;
            }
        }

        foreach (UnityEngine.InputSystem.Controls.TouchControl touch in Touchscreen.current.touches)
        {
            if (touch.touchId.ReadValue() != activeTouchId)
            {
                continue;
            }

            if (touch.press.wasReleasedThisFrame)
            {
                EndDrag(touch.position.ReadValue());
            }
            return;
        }
    }

    private void HandleMouseInput()
    {
        if (Touchscreen.current != null && Touchscreen.current.primaryTouch.press.isPressed)
        {
            return;
        }

        if (Mouse.current == null)
        {
            return;
        }

        Vector2 position = Mouse.current.position.ReadValue();
        if (!isDragging && Mouse.current.leftButton.wasPressedThisFrame && !IsPointerOverUi(position))
        {
            BeginDrag(position, -1);
        }
        else if (isDragging && activeTouchId < 0 && Mouse.current.leftButton.wasReleasedThisFrame)
        {
            EndDrag(position);
        }
    }

    private void BeginDrag(Vector2 screenPosition, int touchId)
    {
        dragStartPosition = screenPosition;
        activeTouchId = touchId;
        isDragging = true;
    }

    private void EndDrag(Vector2 screenPosition)
    {
        Vector2 dragDelta = screenPosition - dragStartPosition;
        CancelDrag();

        if (Time.time < nextDodgeTime || dragDelta.magnitude < Mathf.Max(1f, minimumDragDistance))
        {
            return;
        }

        Vector3 direction = GetWorldDirection(dragDelta.normalized);
        if (direction.sqrMagnitude > 0f)
        {
            StartDodge(direction);
        }
    }

    private void StartDodge(Vector3 direction)
    {
        dodgeStartPosition = CombatPlane.WithFixedY(transform.position);
        float distance = Mathf.Max(0f, dodgeDistance);

        // 벽이 있으면 충돌 지점보다 플레이어 반경만큼 앞에서 회피를 끝낸다.
        if (obstacleMask.value != 0
            && Physics.SphereCast(
                dodgeStartPosition,
                Mathf.Max(0.01f, collisionRadius),
                direction,
                out RaycastHit hit,
                distance,
                obstacleMask,
                QueryTriggerInteraction.Ignore))
        {
            distance = Mathf.Max(0f, hit.distance - Mathf.Max(0f, wallClearance));
        }

        distance = ClampDodgeDistanceAgainstBoss(direction, distance);
        if (distance <= 0.01f)
        {
            return;
        }

        dodgeDestination = CombatPlane.WithFixedY(dodgeStartPosition + direction * distance);
        dodgeElapsed = 0f;
        IsDodging = true;
        nextDodgeTime = Time.time + DodgeCooldownValue;
        float invulnerability = Mathf.Max(0f, invulnerabilityDuration);
        perfectDodgeWindowUntil = Time.time + invulnerability;
        perfectDodgeTriggered = false;
        health?.SetTemporaryInvulnerability(invulnerability);

        afterimageEmitter?.Emit(true);
        PlayDodgeSfx();
        TutorialManager.Report(TutorialEventType.BossDodgeUsed);
    }

    // 플레이어가 대쉬로 접근하여 보스 콜라이더와 겹치는 꼼수 플레이가 되지않게 봉쇄한다.
    private float ClampDodgeDistanceAgainstBoss(Vector3 direction, float distance)
    {
        if (!preventBossOverlap || distance <= 0f || !BossEnemyController.IsBossBattleActive)
        {
            return distance;
        }

        BossEnemyController boss = FindFirstObjectByType<BossEnemyController>();
        if (boss == null || boss.Health == null || boss.Health.IsDead)
        {
            return distance;
        }

        Vector3 bossPosition = CombatPlane.WithFixedY(boss.transform.position);
        Vector3 fromBossToStart = CombatPlane.WithFixedY(dodgeStartPosition) - bossPosition;
        Vector3 flatDirection = CombatPlane.ProjectDirection(direction);
        if (flatDirection.sqrMagnitude <= 0f)
        {
            return distance;
        }

        float safetyRadius = GetBossSafetyRadius(boss);
        float safetyRadiusSqr = safetyRadius * safetyRadius;
        float startDistanceSqr = fromBossToStart.sqrMagnitude;
        if (startDistanceSqr <= safetyRadiusSqr)
        {
            Vector3 awayFromBoss = CombatPlane.ProjectDirection(fromBossToStart);
            return Vector3.Dot(flatDirection, awayFromBoss) > 0f ? distance : 0f;
        }

        float projected = Vector3.Dot(fromBossToStart, flatDirection);
        if (projected >= 0f)
        {
            return distance;
        }

        float discriminant = projected * projected - (startDistanceSqr - safetyRadiusSqr);
        if (discriminant < 0f)
        {
            return distance;
        }

        float hitDistance = -projected - Mathf.Sqrt(discriminant);
        if (hitDistance < 0f || hitDistance > distance)
        {
            return distance;
        }

        return Mathf.Max(0f, hitDistance - Mathf.Max(0f, wallClearance));
    }

    private float GetBossSafetyRadius(BossEnemyController boss)
    {
        float bossRadius = GetHorizontalColliderRadius(boss.gameObject, 1.2f);
        return bossRadius + Mathf.Max(0.01f, collisionRadius) + Mathf.Max(0f, bossDodgeClearance);
    }

    private static float GetHorizontalColliderRadius(GameObject owner, float fallback)
    {
        if (owner == null)
        {
            return Mathf.Max(0.01f, fallback);
        }

        Collider[] colliders = owner.GetComponentsInChildren<Collider>(false);
        float radius = 0f;
        for (int i = 0; i < colliders.Length; i++)
        {
            Collider collider = colliders[i];
            if (collider == null || !collider.enabled)
            {
                continue;
            }

            Vector3 extents = collider.bounds.extents;
            radius = Mathf.Max(radius, extents.x, extents.z);
        }

        return radius > 0f ? radius : Mathf.Max(0.01f, fallback);
    }

    private void PlayDodgeSfx()
    {
        if (dodgeSource == null || dodgeClips == null || dodgeClips.Length == 0)
        {
            return;
        }

        AudioClip clip = GetRandomClip(dodgeClips);
        if (clip == null)
        {
            return;
        }

        dodgeSource.PlayOneShot(clip);
    }

    private static AudioClip GetRandomClip(AudioClip[] clips)
    {
        if (clips == null || clips.Length == 0)
        {
            return null;
        }

        int validCount = 0;
        for (int i = 0; i < clips.Length; i++)
        {
            if (clips[i] != null)
            {
                validCount++;
            }
        }

        if (validCount <= 0)
        {
            return null;
        }

        int selectedIndex = Random.Range(0, validCount);
        for (int i = 0; i < clips.Length; i++)
        {
            if (clips[i] == null)
            {
                continue;
            }

            if (selectedIndex == 0)
            {
                return clips[i];
            }

            selectedIndex--;
        }

        return null;
    }

    private void UpdateDodge()
    {
        float duration = Mathf.Max(0.01f, dodgeDuration);
        dodgeElapsed += Time.deltaTime;
        float progress = Mathf.Clamp01(dodgeElapsed / duration);
        // 지수가 클수록 초반에 거리를 더 많이 당겨와 폭발적인 가속감을 준다.
        float easedProgress = 1f - Mathf.Pow(1f - progress, Mathf.Max(1f, dodgeEaseExponent));
        transform.position = CombatPlane.WithFixedY(
            Vector3.Lerp(dodgeStartPosition, dodgeDestination, easedProgress));

        afterimageEmitter?.Emit();

        if (progress >= 1f)
        {
            IsDodging = false;
        }
    }

    private SpriteRenderer[] GatherBodyRenderers()
    {
        SpriteRenderer[] renderers = GetComponentsInChildren<SpriteRenderer>(true);
        List<SpriteRenderer> valid = new List<SpriteRenderer>(renderers.Length);
        for (int i = 0; i < renderers.Length; i++)
        {
            if (renderers[i] != null && renderers[i].gameObject.name != "__SpriteShapeShadow")
            {
                valid.Add(renderers[i]);
            }
        }

        return valid.ToArray();
    }

    private Vector3 GetWorldDirection(Vector2 screenDirection)
    {
        mainCamera ??= Camera.main;
        if (mainCamera == null)
        {
            return CombatPlane.ProjectDirection(new Vector3(screenDirection.x, 0f, screenDirection.y));
        }

        Vector3 cameraUp = CombatPlane.ProjectDirection(mainCamera.transform.up);
        if (cameraUp.sqrMagnitude <= 0f)
        {
            cameraUp = CombatPlane.ProjectDirection(mainCamera.transform.forward);
        }

        Vector3 cameraRight = CombatPlane.ProjectDirection(mainCamera.transform.right);
        return CombatPlane.ProjectDirection(cameraRight * screenDirection.x + cameraUp * screenDirection.y);
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

    private void ResolveReferences()
    {
        // 매 프레임 호출된다. 보스 매니저가 없는 씬에서는 ??=로 매 프레임 전체 씬 스캔이 되므로,
        // 미발견 시 1초 간격으로만 재탐색한다(보스가 늦게 스폰돼도 곧 잡힘).
        if (bossEncounterManager == null && Time.unscaledTime >= nextBossEncounterSearchTime)
        {
            nextBossEncounterSearchTime = Time.unscaledTime + BossEncounterSearchInterval;
            bossEncounterManager = FindFirstObjectByType<BossEncounterManager>();
        }

        mainCamera ??= Camera.main;
    }

    private void CancelDrag()
    {
        isDragging = false;
        activeTouchId = -1;
    }

    private void HandleDamageBlocked(float blockedDamage)
    {
        if (blockedDamage <= 0f
            || perfectDodgeTriggered
            || Time.time > perfectDodgeWindowUntil
            || bossEncounterManager == null
            || !bossEncounterManager.IsEncounterActive)
        {
            return;
        }

        // 실제 공격을 회피했을 때만 공격력 버프를 부여하고 중첩 없이 시간을 갱신한다.
        perfectDodgeTriggered = true;
        attackBuffUntil = Time.time + Mathf.Max(0f, perfectDodgeBuffDuration);
        StartCoroutine(PlayPerfectDodgeFeedback());
    }

    private IEnumerator PlayPerfectDodgeFeedback()
    {
        if (perfectDodgeSound != null)
        {
            perfectDodgeSound.Play();
        }
        CleanupPerfectDodgeFeedback();
        CreatePerfectDodgeText();
        CacheFlashRenderers();

        float duration = Mathf.Max(0.1f, perfectDodgeFeedbackDuration);
        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.unscaledDeltaTime;
            float progress = Mathf.Clamp01(elapsed / duration);
            UpdatePerfectDodgeText(progress);
            UpdateFlash(progress);
            yield return null;
        }

        CleanupPerfectDodgeFeedback();
    }

    private void CreatePerfectDodgeText()
    {
        activeFeedbackText = new GameObject("Perfect Dodge Feedback");
        TextMeshPro feedbackText = activeFeedbackText.AddComponent<TextMeshPro>();
        feedbackText.font = perfectDodgeFont != null ? perfectDodgeFont : TMP_Settings.defaultFontAsset;
        feedbackText.text = perfectDodgeMessage;
        feedbackText.color = perfectDodgeTextColor;
        feedbackText.fontSize = Mathf.Max(0.1f, perfectDodgeTextSize);
        feedbackText.alignment = TextAlignmentOptions.Center;
        feedbackText.renderer.sortingOrder = 100;
    }

    private void CacheFlashRenderers()
    {
        flashingRenderers = GatherBodyRenderers();
        originalRendererColors = new Color[flashingRenderers.Length];
        for (int i = 0; i < flashingRenderers.Length; i++)
        {
            originalRendererColors[i] = flashingRenderers[i].color;
        }
    }

    private void UpdatePerfectDodgeText(float progress)
    {
        if (activeFeedbackText == null)
        {
            return;
        }

        activeFeedbackText.transform.position = transform.position
            + perfectDodgeTextOffset
            + Vector3.up * (progress * 0.35f);
        mainCamera ??= Camera.main;
        if (mainCamera != null)
        {
            activeFeedbackText.transform.rotation = mainCamera.transform.rotation;
        }

        TextMeshPro feedbackText = activeFeedbackText.GetComponent<TextMeshPro>();
        if (feedbackText != null)
        {
            float alpha = 1f - Mathf.Clamp01((progress - 0.55f) / 0.45f);
            feedbackText.color = new Color(
                perfectDodgeTextColor.r,
                perfectDodgeTextColor.g,
                perfectDodgeTextColor.b,
                alpha);
        }
    }

    private void UpdateFlash(float progress)
    {
        if (flashingRenderers == null || originalRendererColors == null)
        {
            return;
        }

        float flashAmount = Mathf.Sin(progress * Mathf.PI * 4f);
        flashAmount = Mathf.Max(0f, flashAmount);
        for (int i = 0; i < flashingRenderers.Length; i++)
        {
            if (flashingRenderers[i] == null)
            {
                continue;
            }

            Color original = originalRendererColors[i];
            Color flash = new Color(
                perfectDodgeFlashColor.r,
                perfectDodgeFlashColor.g,
                perfectDodgeFlashColor.b,
                original.a);
            flashingRenderers[i].color = Color.Lerp(original, flash, flashAmount);
        }
    }

    private void CleanupPerfectDodgeFeedback()
    {
        if (flashingRenderers != null && originalRendererColors != null)
        {
            for (int i = 0; i < flashingRenderers.Length; i++)
            {
                if (flashingRenderers[i] != null && i < originalRendererColors.Length)
                {
                    flashingRenderers[i].color = originalRendererColors[i];
                }
            }
        }

        flashingRenderers = null;
        originalRendererColors = null;
        if (activeFeedbackText != null)
        {
            Destroy(activeFeedbackText);
            activeFeedbackText = null;
        }
    }

    private void RefreshCooldownUi(bool encounterActive)
    {
        if (dodgeUiRoot != null && dodgeUiRoot.activeSelf != encounterActive)
        {
            dodgeUiRoot.SetActive(encounterActive);
        }

        float cooldown = Mathf.Max(0.01f, DodgeCooldownValue);
        float remaining = CooldownRemaining;
        if (cooldownOverlay != null)
        {
            // 스킬 아이콘처럼 회피 준비 진행도를 이미지 Fill로 표시한다.
            cooldownOverlay.fillAmount = 1f - Mathf.Clamp01(remaining / cooldown);
        }

        if (cooldownText != null)
        {
            cooldownText.gameObject.SetActive(remaining > 0f);
            if (remaining > 0f)
            {
                cooldownText.text = remaining >= 1f
                    ? Mathf.CeilToInt(remaining).ToString()
                    : remaining.ToString("0.0");
            }
        }
    }
}
