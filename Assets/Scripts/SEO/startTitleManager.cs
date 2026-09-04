using System;
using UnityEngine;

public class startTitleManager : MonoBehaviour
{
    [SerializeField] private GameObject TitlePanel;

    private void Start()
    {
        gameObject.SetActive(true);
    }

    public void CloseTitlePanel()
    {
        gameObject.SetActive(false);
    }
}
