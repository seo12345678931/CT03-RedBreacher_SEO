using UnityEngine;

/// <summary>
/// 대상 Transform을 로컬 축 기준으로 계속 회전시킨다(헬기 프로펠러/로터 등).
/// 스프라이트 로터는 로컬 Z(법선) 기준 회전이 자기 평면 안에서 도는 모양이 되어 자연스럽다.
/// 프리팹의 로터 오브젝트에 직접 붙여도 되고, 코드에서 AddComponent + Configure로 붙여도 된다.
/// </summary>
[DisallowMultipleComponent]
public class RotorSpin : MonoBehaviour
{
    [SerializeField] private Vector3 axis = Vector3.forward;
    [SerializeField] private float degreesPerSecond = 900f;

    public void Configure(float speed)
    {
        degreesPerSecond = speed;
    }

    public void Configure(Vector3 spinAxis, float speed)
    {
        axis = spinAxis.sqrMagnitude > 0f ? spinAxis : Vector3.forward;
        degreesPerSecond = speed;
    }

    private void Update()
    {
        transform.Rotate(axis * (degreesPerSecond * Time.deltaTime), Space.Self);
    }
}
