using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 버튼에 얹기만 하면 클릭 시 펀치 피드백을 주는 드롭온 컴포넌트.
/// 코드 없이 인스펙터만으로 전 버튼의 클릭 손맛을 통일할 수 있다.
/// 실제 효과는 <see cref="UITween"/>를 단일 출처로 사용한다.
/// </summary>
[RequireComponent(typeof(Button))]
[DisallowMultipleComponent]
public class UIButtonFeedback : MonoBehaviour
{
    [Tooltip("펀치 강도. 0 이하면 UITween 기본값을 사용한다.")]
    [SerializeField] private float punchStrength = -1f;

    [Tooltip("펀치 길이(초). 0 이하면 UITween 기본값을 사용한다.")]
    [SerializeField] private float punchDuration = -1f;

    [Tooltip("펀치를 적용할 대상. 비워두면 이 게임오브젝트의 Transform을 사용한다.")]
    [SerializeField] private Transform punchTarget;

    private Button button;

    private void Awake()
    {
        button = GetComponent<Button>();
        if (punchTarget == null)
        {
            punchTarget = transform;
        }
    }

    private void OnEnable()
    {
        button?.onClick.AddListener(PlayFeedback);
    }

    private void OnDisable()
    {
        button?.onClick.RemoveListener(PlayFeedback);
    }

    private void PlayFeedback()
    {
        punchTarget.Punch(punchStrength, punchDuration);
    }
}
