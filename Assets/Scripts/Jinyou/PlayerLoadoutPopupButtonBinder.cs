using System;
using UnityEngine;
using UnityEngine.UI;

[DisallowMultipleComponent]
public class PlayerLoadoutPopupButtonBinder : MonoBehaviour
{
    [Header("Buttons")]
    [SerializeField] private Button weaponButton;
    [SerializeField] private Button droneButton;
    [SerializeField] private Button closeButton;
    
    [Header("Panels")]
    [SerializeField] private GameObject droneInfoPanel;
    [SerializeField] private GameObject weaponInfoPanel;
    
    [Header("Target")]
    [SerializeField] private PlayerLoadoutSelectionPanel selectionPanel;

    private PanelTweenTransition panelTransition;

    private void Start()
    {
        droneInfoPanel.SetActive(false);
        weaponInfoPanel.SetActive(true);
    }

    private void OnEnable()
    {
        EnsurePanelTransition();
        SetCloseButtonVisible(true);
        weaponButton?.onClick.AddListener(OpenWeapons);
        droneButton?.onClick.AddListener(OpenDrones);
    }

    private void OnDisable()
    {
        SetCloseButtonVisible(true);
        weaponButton?.onClick.RemoveListener(OpenWeapons);
        droneButton?.onClick.RemoveListener(OpenDrones);
    }

    private void OpenWeapons()
    {
        if (selectionPanel == null)
        {
            return;
        }

        SetCloseButtonVisible(false);
        selectionPanel.OpenWeapons(() => SetCloseButtonVisible(true));
    }

    private void OpenDrones()
    {
        if (selectionPanel == null)
        {
            return;
        }

        SetCloseButtonVisible(false);
        selectionPanel.OpenDrones(() => SetCloseButtonVisible(true));
    }

    private void EnsurePanelTransition()
    {
        // 플레이어 팝업도 prefab 수정 없이 공통 패널 연출을 적용한다.
        panelTransition ??= GetComponent<PanelTweenTransition>();
        if (panelTransition == null)
        {
            panelTransition = gameObject.AddComponent<PanelTweenTransition>();
        }
    }

    private void SetCloseButtonVisible(bool visible)
    {
        if (closeButton != null && closeButton.gameObject.activeSelf != visible)
        {
            closeButton.gameObject.SetActive(visible);
        }
    }
}
