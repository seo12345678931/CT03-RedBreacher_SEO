using UnityEngine;
using UnityEngine.UI;

public class CoreChargerOptionButton : MonoBehaviour
{
    [SerializeField] private Button optionButton;

    private void OnEnable()
    {
        optionButton ??= GetComponent<Button>() ?? GetComponentInChildren<Button>(true);
        if (optionButton != null)
        {
            optionButton.interactable = false;
        }

        gameObject.SetActive(false);
    }
}
