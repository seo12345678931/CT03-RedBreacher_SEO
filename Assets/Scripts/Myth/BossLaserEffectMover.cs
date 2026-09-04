using UnityEngine;

public class BossLaserEffectMover : MonoBehaviour
{
    private Vector3 direction;
    private float speed;
    private float remainingDistance;
    private bool moving;

    public void Launch(Vector3 launchDirection, float launchSpeed, float travelDistance)
    {
        direction = CombatPlane.ProjectDirection(launchDirection);
        speed = Mathf.Max(0.1f, launchSpeed);
        remainingDistance = Mathf.Max(0.01f, travelDistance);
        moving = direction.sqrMagnitude > 0f;
    }

    private void Update()
    {
        if (!moving)
        {
            return;
        }

        float moveDistance = Mathf.Min(speed * Time.deltaTime, remainingDistance);
        transform.position = CombatPlane.WithFixedY(transform.position + direction * moveDistance);
        remainingDistance -= moveDistance;
        if (remainingDistance > 0f)
        {
            return;
        }

        // 데미지는 히트스캔에서 처리하므로 VFX는 지정 거리 이동 후 풀로 반환한다.
        moving = false;
        CombatObjectPool.ReleaseEffect(gameObject);
    }

    private void OnDisable()
    {
        moving = false;
        remainingDistance = 0f;
    }
}
