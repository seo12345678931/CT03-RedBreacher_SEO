using UnityEngine;

/// <summary>
/// 이 오브젝트가 활성인 동안 지정한 BGM 컨텍스트를 켠다(비활성 시 해제).
/// 예: 기지 패널 루트에 붙이고 context=Base로 두면, 기지 열림 → 기지 BGM / 닫힘 → 전투(또는 보스) BGM으로 복귀.
/// 전투는 기본값이라 별도 바인더가 필요 없다(보스도 BGMManager가 자동 감지).
/// </summary>
[DisallowMultipleComponent]
public class BgmContextBinder : MonoBehaviour
{
    [SerializeField] private BgmContext context = BgmContext.Base;

    private void OnEnable() => Apply(true);

    private void OnDisable() => Apply(false);

    private void Apply(bool active)
    {
        BGMManager manager = BGMManager.Instance;
        if (manager == null)
        {
            return;
        }

        switch (context)
        {
            case BgmContext.Base:
                manager.SetBaseActive(active);
                break;
            case BgmContext.Boss:
                manager.SetBossActive(active);
                break;
            // Combat은 기본 상태라 별도 처리하지 않는다.
        }
    }
}
