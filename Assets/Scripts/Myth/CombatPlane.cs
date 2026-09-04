using UnityEngine;

public static class CombatPlane
{
    public const float FixedY = 0.1f;

    // 전투 아레나(벽 안쪽) XZ 경계. CombatArena가 등록한다. 미설정 시 Y 평면 고정만 수행.
    private static bool hasArenaBounds;
    private static float arenaMinX, arenaMaxX, arenaMinZ, arenaMaxZ;

    public static void SetArenaBounds(Vector3 center, Vector2 size, float padding)
    {
        Vector2 half = Vector2.Max(Vector2.zero, size * 0.5f - Vector2.one * Mathf.Max(0f, padding));
        arenaMinX = center.x - half.x;
        arenaMaxX = center.x + half.x;
        arenaMinZ = center.z - half.y;
        arenaMaxZ = center.z + half.y;
        hasArenaBounds = true;
    }

    public static void ClearArenaBounds()
    {
        hasArenaBounds = false;
    }

    public static Vector3 WithFixedY(Vector3 position)
    {
        position.y = FixedY;
        return position;
    }

    /// <summary>Y 평면 고정 + (등록된 경우) 아레나 XZ 경계로 제한한다.</summary>
    public static Vector3 ClampPosition(Vector3 position)
    {
        position.y = FixedY;
        if (hasArenaBounds)
        {
            position.x = Mathf.Clamp(position.x, arenaMinX, arenaMaxX);
            position.z = Mathf.Clamp(position.z, arenaMinZ, arenaMaxZ);
        }

        return position;
    }

    public static void ClampTransform(Transform target)
    {
        target.position = ClampPosition(target.position);
    }

    public static Vector3 ProjectDirection(Vector3 direction)
    {
        direction.y = 0f;
        return direction.sqrMagnitude > 0f ? direction.normalized : Vector3.zero;
    }

    public static float DirectionToZAngle(Vector3 direction)
    {
        Vector3 projectedDirection = ProjectDirection(direction);
        if (projectedDirection.sqrMagnitude <= 0f)
        {
            return 0f;
        }

        return Vector2.SignedAngle(Vector2.up, new Vector2(projectedDirection.x, projectedDirection.z));
    }

    public static float DirectionToYAngle(Vector3 direction)
    {
        return -DirectionToZAngle(direction);
    }

    public static void RotateYOnlyToward(Transform target, Vector3 direction, float maxDegreesDelta)
    {
        if (target == null || ProjectDirection(direction).sqrMagnitude <= 0f)
        {
            return;
        }

        // 바닥에 눕힌 본체 스프라이트는 로컬 X/Z를 유지하고 로컬 Y축만 회전한다.
        Vector3 eulerAngles = target.localEulerAngles;
        eulerAngles.y = Mathf.MoveTowardsAngle(eulerAngles.y, DirectionToLocalYAngle(target, direction), maxDegreesDelta);
        target.localEulerAngles = eulerAngles;
    }

    public static void SetYOnlyRotation(Transform target, Vector3 direction)
    {
        if (target == null || ProjectDirection(direction).sqrMagnitude <= 0f)
        {
            return;
        }

        // 눕힌 본체 계열 오브젝트는 로컬 X/Z를 유지하고 로컬 Y축만 방향에 맞춘다.
        Vector3 eulerAngles = target.localEulerAngles;
        eulerAngles.y = DirectionToLocalYAngle(target, direction);
        target.localEulerAngles = eulerAngles;
    }

    public static void RotateZOnlyToward(Transform target, Vector3 direction, float maxDegreesDelta)
    {
        if (target == null || ProjectDirection(direction).sqrMagnitude <= 0f)
        {
            return;
        }

        // 탑뷰 스프라이트 회전은 로컬 X/Y를 유지하고 로컬 Z축만 목표 방향으로 보간한다.
        Vector3 eulerAngles = target.localEulerAngles;
        eulerAngles.z = Mathf.MoveTowardsAngle(eulerAngles.z, DirectionToLocalZAngle(target, direction), maxDegreesDelta);
        target.localEulerAngles = eulerAngles;
    }

    public static void SetZOnlyRotation(Transform target, Vector3 direction)
    {
        if (target == null || ProjectDirection(direction).sqrMagnitude <= 0f)
        {
            return;
        }

        // 발사체/이펙트도 기존 로컬 X/Y 기울기는 보존하고 로컬 Z축만 발사 방향에 맞춘다.
        Vector3 eulerAngles = target.localEulerAngles;
        eulerAngles.z = DirectionToLocalZAngle(target, direction);
        target.localEulerAngles = eulerAngles;
    }

    public static Vector3 DirectionFromZRotation(Transform target)
    {
        if (target == null)
        {
            return Vector3.zero;
        }

        // 로컬 Z로 도는 터렛은 부모 Y 회전까지 반영해 X/Z 평면 방향으로 변환한다.
        float zRadians = GetWorldZAngle(target) * Mathf.Deg2Rad;
        Vector3 flatDirection = new Vector3(-Mathf.Sin(zRadians), 0f, Mathf.Cos(zRadians));
        return ProjectDirection(Quaternion.Euler(0f, GetWorldYAngle(target), 0f) * flatDirection);
    }

    public static Vector3 PositionFromZPlaneChild(Transform planeRoot, Transform child, Vector3 forwardDirection)
    {
        if (planeRoot == null || child == null)
        {
            return Vector3.zero;
        }

        // 2D 터렛 자식의 로컬 X/Y를 전투용 X/Z 평면 좌표로 변환한다.
        Vector3 localOffset = planeRoot.InverseTransformPoint(child.position);
        Vector3 forward = ProjectDirection(forwardDirection);
        if (forward.sqrMagnitude <= 0f)
        {
            forward = DirectionFromZRotation(planeRoot);
        }

        Vector3 right = ProjectDirection(new Vector3(forward.z, 0f, -forward.x));
        Vector3 scale = planeRoot.lossyScale;
        Vector3 flatOffset = right * (localOffset.x * Mathf.Abs(scale.x)) + forward * (localOffset.y * Mathf.Abs(scale.y));
        return WithFixedY(planeRoot.position + flatOffset);
    }

    public static Vector3 DirectionFromYRotation(Transform target)
    {
        if (target == null)
        {
            return Vector3.zero;
        }

        // 로컬 Y로 도는 본체는 실제 월드 forward 방향을 이동/대체 발사 방향으로 사용한다.
        return ProjectDirection(target.forward);
    }

    private static float DirectionToLocalZAngle(Transform target, Vector3 direction)
    {
        Vector3 localDirection = Quaternion.Euler(0f, -GetParentWorldYAngle(target), 0f) * ProjectDirection(direction);
        return DirectionToZAngle(localDirection) - GetParentWorldZAngle(target);
    }

    private static float DirectionToLocalYAngle(Transform target, Vector3 direction)
    {
        return DirectionToYAngle(direction) - GetParentWorldYAngle(target);
    }

    private static float GetWorldZAngle(Transform target)
    {
        return GetParentWorldZAngle(target) + target.localEulerAngles.z;
    }

    private static float GetParentWorldZAngle(Transform target)
    {
        return target.parent != null ? GetWorldZAngle(target.parent) : 0f;
    }

    private static float GetWorldYAngle(Transform target)
    {
        return GetParentWorldYAngle(target) + target.localEulerAngles.y;
    }

    private static float GetParentWorldYAngle(Transform target)
    {
        return target.parent != null ? GetWorldYAngle(target.parent) : 0f;
    }

    public static Vector3 Direction(Vector3 from, Vector3 to)
    {
        return ProjectDirection(WithFixedY(to) - WithFixedY(from));
    }

    public static float DistanceSqr(Vector3 from, Vector3 to)
    {
        Vector3 offset = WithFixedY(to) - WithFixedY(from);
        offset.y = 0f;
        return offset.sqrMagnitude;
    }

    public static void ClampVelocity(Rigidbody body)
    {
        if (body == null)
        {
            return;
        }

        // Rigidbody도 Y 속도를 제거해 X/Z 평면 이동만 허용한다.
        Vector3 velocity = body.linearVelocity;
        velocity.y = 0f;
        body.linearVelocity = velocity;
    }
}
