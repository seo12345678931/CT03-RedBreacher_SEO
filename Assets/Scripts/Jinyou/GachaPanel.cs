using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.Serialization;
using UnityEngine.UI;

public class GachaPanel : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private BaseCampManager baseCampManager;
    [SerializeField] private WeaponGachaFacility weaponGacha;

    [Header("Weapon Draw Commands")]
    [SerializeField] private Button weaponDrawOnceButton;
    [SerializeField] private Button weaponDrawMultiButton;
    [SerializeField] private TMP_Text weaponDrawOnceCostText;
    [SerializeField] private TMP_Text weaponDrawMultiCostText;

    [Header("Skill Draw Commands")]
    [SerializeField] private Button skillDrawOnceButton;
    [SerializeField] private Button skillDrawMultiButton;
    [SerializeField] private TMP_Text skillDrawOnceCostText;
    [SerializeField] private TMP_Text skillDrawMultiCostText;

    [Header("Controls")]
    [SerializeField] private Button exitButton;
    
    // 기존의 Odd슬롯 프리팹 삭제를 까먹고 그대로 코드에 구현하는 바람에 연결 대상이 불명확해져
    // FormerlySerializedAs로 명시적 구현
    [Header("WeaponGachaDetailPopup_Slot")]
    [SerializeField] private Transform weaponOddRoot;
    [FormerlySerializedAs("weaponOddPrefab")]
    [SerializeField] private WeaponItemOdd weaponItemOddSlot;

    [Header("SkillGachaDetailPopup_Slot")]
    [SerializeField] private Transform skillOddRoot;
    [FormerlySerializedAs("skillOddPrefab")]
    [SerializeField] private SkillItemOdd skillItemOddSlot;

    [Header("Result Slots")]
    [SerializeField] private Transform resultSlotRoot;
    [SerializeField] private GachaResultSlot resultSlotPrefab;

    [Header("Result Panels")]
    [SerializeField] private GameObject _resultPanel;

    [Header("Tween")]
    [SerializeField] private GachaTweenTransition gachaTweenTransition;

    [Header("Draw Video")]
    [SerializeField] private GachaVideoPlayer gachaVideoPlayer;

    [Header("Draw Confirm(선택기능)")]
    [Tooltip("뽑기 버튼을 누르면 먼저 뜨는 확인창 루트. 비우면 확인 없이 바로 뽑힌다(기존 동작).")]
    [SerializeField] private GameObject confirmPopup;
    [SerializeField] private TMP_Text confirmMessageText;
    [SerializeField] private Button confirmYesButton;
    [SerializeField] private Button confirmNoButton;

    private readonly List<WeaponItemOdd> _weaponItemOdds = new List<WeaponItemOdd>();
    private readonly List<SkillItemOdd> _skillItemOdds = new List<SkillItemOdd>();

    private readonly List<GachaResultSlot> resultSlots = new List<GachaResultSlot>();
    private GachaCategory selectedCategory = GachaCategory.Weapon;
    private bool isDrawing;
    private GachaCategory pendingCategory = GachaCategory.Weapon;
    private int pendingCount = 1;

    private void OnEnable()
    {
        ResolveReferences();
        EnsureExitButtonVisible();
        weaponDrawOnceButton?.onClick.AddListener(DrawWeaponOnce);
        weaponDrawMultiButton?.onClick.AddListener(DrawWeaponMulti);
        skillDrawOnceButton?.onClick.AddListener(DrawSkillOnce);
        skillDrawMultiButton?.onClick.AddListener(DrawSkillMulti);
        confirmYesButton?.onClick.AddListener(ConfirmDraw);
        confirmNoButton?.onClick.AddListener(CancelDraw);
        if (confirmPopup != null)
        {
            confirmPopup.SetActive(false);
        }

        RefreshNow();
    }

    private void OnDisable()
    {
        weaponDrawOnceButton?.onClick.RemoveListener(DrawWeaponOnce);
        weaponDrawMultiButton?.onClick.RemoveListener(DrawWeaponMulti);
        skillDrawOnceButton?.onClick.RemoveListener(DrawSkillOnce);
        skillDrawMultiButton?.onClick.RemoveListener(DrawSkillMulti);
        confirmYesButton?.onClick.RemoveListener(ConfirmDraw);
        confirmNoButton?.onClick.RemoveListener(CancelDraw);
        gachaTweenTransition?.ResetAll();
    }

    // 매 프레임 문자열 생성/TMP 갱신을 피하기 위한 주기적 갱신 간격.
    private const float RefreshInterval = 0.25f;
    private float nextRefreshTime;

    private void Update()
    {
        if (Time.unscaledTime < nextRefreshTime)
        {
            return;
        }

        RefreshNow();
    }

    private void RefreshNow()
    {
        nextRefreshTime = Time.unscaledTime + RefreshInterval;
        Refresh();
    }

    public void DrawWeaponOnce()
    {
        RequestDraw(GachaCategory.Weapon, 1);
    }

    public void DrawWeaponMulti()
    {
        ResolveReferences();
        RequestDraw(GachaCategory.Weapon, weaponGacha != null ? weaponGacha.MultiDrawCount : 10);
    }

    public void DrawSkillOnce()
    {
        RequestDraw(GachaCategory.Skill, 1);
    }

    public void DrawSkillMulti()
    {
        ResolveReferences();
        RequestDraw(GachaCategory.Skill, weaponGacha != null ? weaponGacha.MultiDrawCount : 10);
    }

    // 뽑기 버튼 → 확인창 표시. 확인창이 없거나 뽑을 수 없으면 기존처럼 바로 처리한다.
    private void RequestDraw(GachaCategory category, int count)
    {
        ResolveReferences();
        if (isDrawing || weaponGacha == null)
        {
            return;
        }

        if (!weaponGacha.CanDraw(category, count))
        {
            // 재화 부족 등으로 못 뽑으면 확인창도 띄우지 않는다.
            return;
        }

        if (confirmPopup == null)
        {
            Draw(category, count);
            return;
        }

        pendingCategory = category;
        pendingCount = count;
        UpdateConfirmMessage(category, count);
        confirmPopup.SetActive(true);
    }

    public void ConfirmDraw()
    {
        if (confirmPopup != null)
        {
            confirmPopup.SetActive(false);
        }

        Draw(pendingCategory, pendingCount);
    }

    public void CancelDraw()
    {
        if (confirmPopup != null)
        {
            confirmPopup.SetActive(false);
        }
    }

    private void UpdateConfirmMessage(GachaCategory category, int count)
    {
        if (confirmMessageText == null)
        {
            return;
        }

        int cost = weaponGacha != null ? weaponGacha.GetDrawCost(category, count) : 0;
        string kind = category == GachaCategory.Weapon ? "무기" : "스킬";
        confirmMessageText.text = $"{kind} {count}회 뽑기\n코어 크리스탈 {cost}개를 사용할까요?";
    }

    private void Draw(GachaCategory category, int count)
    {
        ResolveReferences();
        if (isDrawing || weaponGacha == null)
        {
            return;
        }

        isDrawing = true;
        // 각 버튼이 자신의 뽑기 종류를 직접 지정해 탭 상태와 섞이지 않게 한다.
        selectedCategory = category;
        CloseResultPanel();
        bool succeeded = weaponGacha.TryDraw(category, count);
        if (succeeded)
        {
            ReportDrawMissions(category, count);
            IReadOnlyList<GachaDrawResult> results = weaponGacha.LastResults;
            Rarity highestRarity = GetHighestRarity(results);

            System.Action showResults = () =>
            {
                ShowResults(results);
                isDrawing = false;
                Refresh();
            };

            if (gachaVideoPlayer != null && gachaVideoPlayer.HasAnyClip)
            {
                // 영상이 기존 박스 연출(PlayTitle)을 대체한다. 끝나면 바로 결과로.
                gachaVideoPlayer.Play(highestRarity, showResults);
            }
            else
            {
                // 영상이 없으면 기존 박스 연출로 폴백.
                PlayDrawTween(category, showResults);
            }

            return;
        }

        isDrawing = false;
        Refresh();
    }

    private static void ReportDrawMissions(GachaCategory category, int count)
    {
        if (category == GachaCategory.Skill)
        {
            MainGuideMissionManager.ReportSkillGachaDrawn(count);
            return;
        }

        DailyMissionManager.ReportWeaponGachaDrawn(count);
        MainGuideMissionManager.ReportWeaponGachaDrawn(count);
    }

    private void Refresh()
    {
        ResolveReferences();
        EnsureExitButtonVisible();
        bool connected = weaponGacha != null;
        int crystals = baseCampManager != null ? baseCampManager.CoreCrystals : 0;
        int multiCount = connected ? weaponGacha.MultiDrawCount : 10;
        
        RefreshDrawCostTexts(connected, multiCount);
        RefreshWeaponOddList();
        RefreshSkillOddList();

        bool canDrawWeaponOnce = connected && !isDrawing && weaponGacha.CanDraw(GachaCategory.Weapon, 1);
        bool canDrawWeaponMulti = connected && !isDrawing && weaponGacha.CanDraw(GachaCategory.Weapon, multiCount);
        bool canDrawSkillOnce = connected && !isDrawing && weaponGacha.CanDraw(GachaCategory.Skill, 1);
        bool canDrawSkillMulti = connected && !isDrawing && weaponGacha.CanDraw(GachaCategory.Skill, multiCount);

        SetButton(weaponDrawOnceButton, canDrawWeaponOnce);
        SetButton(weaponDrawMultiButton, canDrawWeaponMulti);
        SetButton(skillDrawOnceButton, canDrawSkillOnce);
        SetButton(skillDrawMultiButton, canDrawSkillMulti);
        SetCostTextColor(weaponDrawOnceCostText, canDrawWeaponOnce);
        SetCostTextColor(weaponDrawMultiCostText, canDrawWeaponMulti);
        SetCostTextColor(skillDrawOnceCostText, canDrawSkillOnce);
        SetCostTextColor(skillDrawMultiCostText, canDrawSkillMulti);
    }

    private void RefreshWeaponOddList()
    {
        if (weaponGacha == null || weaponOddRoot == null || weaponItemOddSlot == null)
        {
            return;
        }

        IReadOnlyList<WeaponGachaFacility.WeaponGachaEntry> entries = weaponGacha.GetWeaponEntries();
        int validCount = 0;
        for (int i = 0; i < entries.Count; i++)
        {
            WeaponGachaFacility.WeaponGachaEntry entry = entries[i];
            if (!IsValidWeaponOddEntry(entry))
            {
                continue;
            }

            validCount++;
        }

        while (_weaponItemOdds.Count < validCount)
        {
            WeaponItemOdd slot = Instantiate(weaponItemOddSlot, weaponOddRoot);
            slot.gameObject.SetActive(true);
            _weaponItemOdds.Add(slot);
        }

        int slotIndex = 0;
        for (int i = 0; i < entries.Count; i++)
        {
            WeaponGachaFacility.WeaponGachaEntry entry = entries[i];
            if (!IsValidWeaponOddEntry(entry))
            {
                continue;
            }

            WeaponItemOdd slot = _weaponItemOdds[slotIndex++];
            slot.gameObject.SetActive(true);
            slot.Setup(entry, weaponGacha.GetWeaponEntryProbabilityPercent(entry));
        }

        for (int i = slotIndex; i < _weaponItemOdds.Count; i++)
        {
            if (_weaponItemOdds[i] != null)
            {
                _weaponItemOdds[i].gameObject.SetActive(false);
            }
        }
    }

    private static bool IsValidWeaponOddEntry(WeaponGachaFacility.WeaponGachaEntry entry)
    {
        return entry != null
            && entry.enabled
            && entry.weaponConfig != null
            && entry.weight > 0f;
    }

    private void RefreshSkillOddList()
    {
        if (weaponGacha == null || skillOddRoot == null || skillItemOddSlot == null)
        {
            return;
        }

        IReadOnlyList<WeaponGachaFacility.SkillGachaEntry> entries = weaponGacha.GetSkillEntries();
        int validCount = 0;
        for (int i = 0; i < entries.Count; i++)
        {
            WeaponGachaFacility.SkillGachaEntry entry = entries[i];
            if (!IsValidSkillOddEntry(entry))
            {
                continue;
            }

            validCount++;
        }

        while (_skillItemOdds.Count < validCount)
        {
            SkillItemOdd slot = Instantiate(skillItemOddSlot, skillOddRoot);
            slot.gameObject.SetActive(true);
            _skillItemOdds.Add(slot);
        }

        int slotIndex = 0;
        for (int i = 0; i < entries.Count; i++)
        {
            WeaponGachaFacility.SkillGachaEntry entry = entries[i];
            if (!IsValidSkillOddEntry(entry))
            {
                continue;
            }

            SkillItemOdd slot = _skillItemOdds[slotIndex++];
            slot.gameObject.SetActive(true);
            slot.Setup(entry, weaponGacha.GetSkillEntryProbabilityPercent(entry));
        }

        for (int i = slotIndex; i < _skillItemOdds.Count; i++)
        {
            if (_skillItemOdds[i] != null)
            {
                _skillItemOdds[i].gameObject.SetActive(false);
            }
        }
    }

    private static bool IsValidSkillOddEntry(WeaponGachaFacility.SkillGachaEntry entry)
    {
        return entry != null
            && entry.enabled
            && entry.skillConfig != null
            && entry.weight > 0f;
    }

    private void RefreshDrawCostTexts(bool connected, int multiCount)
    {
        if (!connected)
        {
            SetText(weaponDrawOnceCostText, "--");
            SetText(weaponDrawMultiCostText, "--");
            SetText(skillDrawOnceCostText, "--");
            SetText(skillDrawMultiCostText, "--");
            return;
        }

        SetText(weaponDrawOnceCostText, FormatCoreCrystalCost(weaponGacha.GetDrawCost(GachaCategory.Weapon, 1)));
        SetText(weaponDrawMultiCostText, FormatCoreCrystalCost(weaponGacha.GetDrawCost(GachaCategory.Weapon, multiCount)));
        SetText(skillDrawOnceCostText, FormatCoreCrystalCost(weaponGacha.GetDrawCost(GachaCategory.Skill, 1)));
        SetText(skillDrawMultiCostText, FormatCoreCrystalCost(weaponGacha.GetDrawCost(GachaCategory.Skill, multiCount)));
    }

    private static string FormatCoreCrystalCost(int cost)
    {
        return $"{cost:N0} 소모";
    }

    private void ShowResults(IReadOnlyList<GachaDrawResult> results)
    {
        if (_resultPanel != null)
        {
            _resultPanel.SetActive(true);
        }

        gachaTweenTransition?.PlayResultPanel();

        int resultCount = Mathf.Min(10, results != null ? results.Count : 0);
        if (resultSlotRoot != null && resultSlotPrefab != null)
        {
            EnsureResultSlotCount(resultCount);
            for (int i = 0; i < resultSlots.Count; i++)
            {
                bool active = i < resultCount;
                resultSlots[i].gameObject.SetActive(active);
                if (active)
                {
                    resultSlots[i].Setup(results[i]);
                }
            }
        }
    }

    private void EnsureResultSlotCount(int count)
    {
        while (resultSlots.Count < count)
        {
            GachaResultSlot slot = Instantiate(resultSlotPrefab, resultSlotRoot);
            resultSlots.Add(slot);
        }
    }

    public void CloseResultPanel()
    {
        if (_resultPanel != null)
        {
            _resultPanel.SetActive(false);
        }

        gachaTweenTransition?.ResetAll();
    }

    private void ResolveReferences()
    {
        baseCampManager ??= BaseCampManager.Instance ?? FindFirstObjectByType<BaseCampManager>();
        weaponGacha ??= FindFirstObjectByType<WeaponGachaFacility>(FindObjectsInactive.Include);
        gachaTweenTransition ??= GetComponentInChildren<GachaTweenTransition>(true);
        gachaTweenTransition ??= FindFirstObjectByType<GachaTweenTransition>(FindObjectsInactive.Include);
        if (gachaTweenTransition == null)
        {
            gachaTweenTransition = gameObject.AddComponent<GachaTweenTransition>();
        }
    }

    private static Rarity GetHighestRarity(IReadOnlyList<GachaDrawResult> results)
    {
        Rarity highest = Rarity.Common;
        if (results == null)
        {
            return highest;
        }

        for (int i = 0; i < results.Count; i++)
        {
            if (results[i] != null && (int)results[i].Rarity > (int)highest)
            {
                highest = results[i].Rarity;
            }
        }

        return highest;
    }

    private void PlayDrawTween(GachaCategory category, System.Action onComplete)
    {
        ResolveReferences();
        if (gachaTweenTransition == null)
        {
            onComplete?.Invoke();
            return;
        }

        gachaTweenTransition.PlayTitle(category, onComplete);
    }

    private static void SetButton(Button button, bool interactable)
    {
        if (button != null)
        {
            button.interactable = interactable;
        }
    }

    private static void SetText(TMP_Text target, string value)
    {
        if (target != null)
        {
            target.text = value;
        }
    }

    private static void SetCostTextColor(TMP_Text target, bool canDraw)
    {
        if (target != null)
        {
            target.color = canDraw ? Color.white : Color.red;
        }
    }
    
    private void EnsureExitButtonVisible()
    {
        if (exitButton != null && !exitButton.gameObject.activeSelf)
        {
            exitButton.gameObject.SetActive(true);
        }
    }
}
