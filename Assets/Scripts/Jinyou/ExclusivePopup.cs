using UnityEngine;

/// <summary>
/// 같은 부모(그룹) 아래의 다른 <see cref="ExclusivePopup"/> 들과 상호 배타적으로 열리는 팝업.
/// 이 팝업이 활성화되는 순간 같은 그룹에서 현재 열려 있는 다른 팝업을 모두 비활성화한다.
/// MenuButtons_Panel 버튼이 기존처럼 SetActive(true)로 팝업을 열기만 해도
/// 별도의 onClick 재배선 없이 "이전 팝업 닫고 새 팝업 열기"가 동작한다.
/// </summary>
[DisallowMultipleComponent]
public class ExclusivePopup : MonoBehaviour
{
    private void OnEnable()
    {
        EnsureTransitionForSlidePopup(gameObject);

        Transform parent = transform.parent;
        if (parent == null)
        {
            return;
        }

        for (int i = 0; i < parent.childCount; i++)
        {
            Transform sibling = parent.GetChild(i);
            if (sibling == transform || !sibling.gameObject.activeSelf)
            {
                continue;
            }

            // 같은 그룹의 다른 배타 팝업만 닫는다(HUD/일반 패널은 영향 없음).
            if (sibling.GetComponent<ExclusivePopup>() != null)
            {
                PanelTweenTransition transition = sibling.GetComponent<PanelTweenTransition>();
                if (transition != null)
                {
                    transition.Close();
                    continue;
                }

                sibling.gameObject.SetActive(false);
            }
        }
    }

    private static void EnsureTransitionForSlidePopup(GameObject popup)
    {
        if (popup == null || (popup.name != "Shop_Popup" && popup.name != "Base_Popup"))
        {
            return;
        }

        // 상점/기지는 전용 패널 스크립트가 없어 여기서 공통 슬라이드 연출을 보장한다.
        if (popup.GetComponent<PanelTweenTransition>() == null)
        {
            popup.AddComponent<PanelTweenTransition>();
        }
    }
}
