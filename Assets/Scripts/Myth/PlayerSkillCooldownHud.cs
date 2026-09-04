using System.Collections.Generic;
using UnityEngine;

public class PlayerSkillCooldownHud : MonoBehaviour
{
    [SerializeField] private PlayerSkillCooldownSlot _slotPrefab;
    [SerializeField] private Transform _slotContainer;

    private PlayerController _player;
    private PlayerAutoSkillController _skillController;
    private readonly List<PlayerSkillCooldownSlot> _activeSlots = new List<PlayerSkillCooldownSlot>();
    private int _observedLoadoutVersion = -1;

    private void Awake()
    {
        if (_player == null)
        {
            _player = FindFirstObjectByType<PlayerController>();
        }

        ResolveSkillController();
    }

    private void Update()
    {
        if (_player == null)
        {
            _player = FindFirstObjectByType<PlayerController>();
            if (_player != null)
            {
                ResolveSkillController();
            }
            return;
        }

        if (_skillController == null)
        {
            ResolveSkillController();
            return;
        }

        // 같은 슬롯 수로 스킬을 교체한 경우에도 HUD를 다시 구성한다.
        if (_observedLoadoutVersion != _skillController.LoadoutVersion)
        {
            RebuildSlots();
        }
    }

    private void ResolveSkillController()
    {
        if (_player != null)
        {
            _skillController = _player.GetComponent<PlayerAutoSkillController>();
            if (_skillController != null)
            {
                RebuildSlots();
            }
        }
    }

    private void RebuildSlots()
    {
        // 기존 활성화된 슬롯 정리
        for (int i = 0; i < _activeSlots.Count; i++)
        {
            if (_activeSlots[i] != null)
            {
                Destroy(_activeSlots[i].gameObject);
            }
        }
        _activeSlots.Clear();

        if (_skillController == null || _slotPrefab == null || _slotContainer == null)
        {
            return;
        }

        _observedLoadoutVersion = _skillController.LoadoutVersion;
        IReadOnlyList<PlayerSkillConfig> skills = _skillController.EquippedSkills;
        for (int i = 0; i < skills.Count; i++)
        {
            PlayerSkillConfig skill = skills[i];
            if (skill == null)
            {
                continue;
            }

            PlayerSkillCooldownSlot newSlot = Instantiate(_slotPrefab, _slotContainer);
            newSlot.Setup(_skillController, skill);
            _activeSlots.Add(newSlot);
        }
    }
}
