using UnityEngine;

public class shopPopupManager : MonoBehaviour
{
    // 작업도중 감춰야 할 패널인 스킬 패널을 감추지 못하고 다른 패널이 나오는 꼬이는 문제를 방지하기 위해 스크립트로 강제제어
    [Header("Panels")] 
    [SerializeField] private GameObject WeaponGachaPanel;
    [SerializeField] private GameObject SkillGachaPanel;
    [SerializeField] private GameObject WeaponGachaOddDetailPanel;
    [SerializeField] private GameObject SkillGachaOddDetailPanel;
    void Start()
    {
        WeaponGachaPanel.SetActive(true);
        SkillGachaPanel.SetActive(false);
        WeaponGachaOddDetailPanel.SetActive(false);
        SkillGachaOddDetailPanel.SetActive(false);
    }
}
