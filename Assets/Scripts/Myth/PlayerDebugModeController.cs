using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;

[DisallowMultipleComponent]
public sealed class PlayerDebugModeController : MonoBehaviour
{
    [Header("디버그 모드")]
    [SerializeField, UnityEngine.Serialization.FormerlySerializedAs("enableF1Toggle")]
    private bool _enableF1Toggle = true;

    [SerializeField, UnityEngine.Serialization.FormerlySerializedAs("invulnerableInDebugMode")]
    private bool _invulnerableInDebugMode = true;

    [Header("선택 UI")]
    [SerializeField, UnityEngine.Serialization.FormerlySerializedAs("debugStatusRoot")]
    private GameObject _debugStatusRoot;

    [SerializeField, UnityEngine.Serialization.FormerlySerializedAs("debugStatusText")]
    private TMP_Text _debugStatusText;

    private CombatHealth _health;

    public bool IsDebugModeEnabled { get; private set; }

    public static PlayerDebugModeController Ensure(PlayerController owner)
    {
        PlayerDebugModeController controller = owner.GetComponent<PlayerDebugModeController>();
        if (controller == null)
        {
            controller = owner.gameObject.AddComponent<PlayerDebugModeController>();
        }

        controller._health = owner.Health;
        controller.ApplyDebugMode();
        return controller;
    }

    private void Awake()
    {
        _health = GetComponent<CombatHealth>();
        ApplyDebugMode();
    }

    private void OnEnable()
    {
        // 플레이어 오브젝트가 다시 활성화되면 현재 디버그 설정을 복원한다.
        ApplyDebugMode();
    }

    private void Update()
    {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        if (_enableF1Toggle && Keyboard.current != null)
        {
            if (Keyboard.current.f1Key.wasPressedThisFrame)
            {
                SetDebugMode(!IsDebugModeEnabled);
            }

            if (IsDebugModeEnabled)
            {
                // 1키: 무적 토글
                if (Keyboard.current.digit1Key.wasPressedThisFrame)
                {
                    _invulnerableInDebugMode = !_invulnerableInDebugMode;
                    ApplyDebugMode();
                    Debug.Log($"[디버그 모드] 무적 상태 토글: {(_invulnerableInDebugMode ? "ON" : "OFF")}", this);
                }

                // 2키: 희귀 재화 (코어 크리스탈) 지급
                if (Keyboard.current.digit2Key.wasPressedThisFrame)
                {
                    AddDebugCrystals(100);
                }

                // 3키: 보스 티켓 지급
                if (Keyboard.current.digit3Key.wasPressedThisFrame)
                {
                    AddDebugBossTickets(5);
                }
            }
        }
#endif
    }

    private void OnDisable()
    {
        if (_health != null)
        {
            _health.SetDebugInvulnerable(false);
        }
    }

    public void SetDebugMode(bool enabled)
    {
        IsDebugModeEnabled = enabled;
        ApplyDebugMode();
        Debug.Log($"[디버그 모드] {(enabled ? "활성화" : "비활성화")}", this);
    }

    private void ApplyDebugMode()
    {
        _health ??= GetComponent<CombatHealth>();
        _health?.SetDebugInvulnerable(IsDebugModeEnabled && _invulnerableInDebugMode);

        if (_debugStatusRoot != null)
        {
            _debugStatusRoot.SetActive(IsDebugModeEnabled);
        }

        if (_debugStatusText != null)
        {
            if (IsDebugModeEnabled)
            {
                _debugStatusText.text = $"[디버그 모드]\n1: 무적 상태: {(_invulnerableInDebugMode ? "ON" : "OFF")}\n2: 코어 크리스탈 +100\n3: 보스 티켓 +5";
            }
            else
            {
                _debugStatusText.text = string.Empty;
            }
        }
    }

    private void AddDebugBossTickets(int amount)
    {
        CommandCenter commandCenter = BaseCampManager.Instance != null
            ? BaseCampManager.Instance.CommandCenter
            : FindFirstObjectByType<CommandCenter>(FindObjectsInactive.Include);
        if (commandCenter != null)
        {
            commandCenter.AddBossTickets(amount);
            Debug.Log($"[디버그 모드] 보스 티켓 +{amount} 지급 완료 (현재: {commandCenter.BossTickets})", this);
        }
        else
        {
            Debug.LogWarning("[디버그 모드] CommandCenter를 찾지 못해 보스 티켓을 지급하지 못했습니다.", this);
        }
    }

    private void AddDebugCrystals(int amount)
    {
        PlayerCurrencyWallet wallet = FindFirstObjectByType<PlayerCurrencyWallet>(FindObjectsInactive.Include);
        if (wallet != null)
        {
            wallet.AddCoreCrystals(amount);
            Debug.Log($"[디버그 모드] 코어 크리스탈 +{amount} 지급 완료 (현재: {wallet.CoreCrystals})", this);
        }
        else
        {
            Debug.LogWarning("[디버그 모드] PlayerCurrencyWallet을 찾을 수 없어 코어 크리스탈을 지급하지 못했습니다.", this);
        }
    }
}
