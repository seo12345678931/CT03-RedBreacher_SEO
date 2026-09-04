using UnityEngine;

/// <summary>
/// 전투 아레나(벽 안쪽) 경계를 정의해 <see cref="CombatPlane"/>에 등록한다.
/// 플레이어/적이 매 틱 호출하는 CombatPlane.ClampTransform/ClampPosition이 이 경계로 XZ를 제한하므로,
/// 키네마틱 이동·추적·넉백으로 벽을 통과해 밖으로 나가는 것을 막는다.
/// </summary>
[DisallowMultipleComponent]
public sealed class CombatArena : MonoBehaviour
{
    [Header("아레나 경계 (XZ, 월드 기준)")]
    [Tooltip("경계 중심(월드 좌표).")]
    [SerializeField] private Vector3 center = Vector3.zero;

    [Tooltip("경계 전체 크기(X, Z). 벽 안쪽 치수에 맞춘다.")]
    [SerializeField] private Vector2 size = new Vector2(48f, 48f);

    [Tooltip("벽에서 안쪽으로 띄울 여유. 유닛 반지름만큼 여유를 둔다.")]
    [SerializeField] private float padding = 1f;

    private void Awake()
    {
        Apply();
    }

    private void OnEnable()
    {
        Apply();
    }

    public void Apply()
    {
        CombatPlane.SetArenaBounds(center, size, padding);
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        if (Application.isPlaying)
        {
            Apply();
        }
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = new Color(1f, 0.4f, 0.1f, 0.6f);
        Gizmos.DrawWireCube(
            new Vector3(center.x, CombatPlane.FixedY, center.z),
            new Vector3(size.x, 0.1f, size.y));
    }
#endif
}
