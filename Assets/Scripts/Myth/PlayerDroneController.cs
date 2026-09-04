using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
public class PlayerDroneController : MonoBehaviour
{
    [Header("Source")]
    [SerializeField] private PlayerController player;
    [SerializeField] private DroneConfig droneConfig;
    [SerializeField] private Transform droneSpawnParent;
    [SerializeField] private bool spawnOnStart = true;
    [SerializeField] private AudioSource droneFireSource;

    private readonly List<PlayerDroneUnit> drones = new List<PlayerDroneUnit>();
    private DroneConfig appliedConfig;

    public DroneConfig DroneConfig => droneConfig;

    private void Awake()
    {
        if (player == null)
        {
            player = GetComponent<PlayerController>();
        }
    }

    private void Start()
    {
        if (spawnOnStart)
        {
            RefreshDrones();
        }
    }

    private void Update()
    {
        if (appliedConfig != droneConfig)
        {
            RefreshDrones();
        }
    }

    public void SetDroneConfig(DroneConfig config)
    {
        // 같은 드론이면 재생성하지 않는다. 무의미한 ClearDrones+재스폰은 드론 팝(대형 재진입)만 유발한다.
        if (appliedConfig == config && drones.Count > 0)
        {
            droneConfig = config;
            return;
        }

        droneConfig = config;
        RefreshDrones();
    }

    [ContextMenu("Refresh Drones")]
    public void RefreshDrones()
    {
        ClearDrones();
        appliedConfig = droneConfig;

        if (player == null)
        {
            player = GetComponent<PlayerController>();
        }

        if (player == null || droneConfig == null)
        {
            return;
        }

        int count = droneConfig.DroneCount;
        for (int i = 0; i < count; i++)
        {
            PlayerDroneUnit drone = CreateDrone(i, count);
            drones.Add(drone);
        }
    }

    private PlayerDroneUnit CreateDrone(int index, int count)
    {
        GameObject droneObject = droneConfig.DronePrefab != null
            ? Instantiate(droneConfig.DronePrefab, GetDroneParent())
            : CreateFallbackDrone();

        droneObject.name = $"{droneConfig.DisplayName} {index + 1}";
        PlayerDroneUnit drone = droneObject.GetComponent<PlayerDroneUnit>();
        if (drone == null)
        {
            drone = droneObject.AddComponent<PlayerDroneUnit>();
        }

        drone.Initialize(player, droneConfig, index, count, droneFireSource);
        return drone;
    }

    private GameObject CreateFallbackDrone()
    {
        GameObject droneObject = new GameObject("Drone");
        droneObject.transform.SetParent(GetDroneParent());

        // 드론 프리팹이 없어도 전투 테스트가 가능하도록 임시 표시를 만든다.
        SpriteRenderer renderer = droneObject.AddComponent<SpriteRenderer>();
        renderer.sprite = CombatVisualFactory.CreateCircleSprite(new Color(0.2f, 0.8f, 1f, 1f));
        renderer.sortingOrder = 2;
        droneObject.transform.localScale = Vector3.one * 0.35f;
        return droneObject;
    }

    private Transform GetDroneParent()
    {
        // 별도 슬롯 루트가 지정되면 그 아래에 두고, 없으면 플레이어 회전을 상속하지 않도록 씬 루트(독립)에 둔다.
        return droneSpawnParent;
    }

    private void ClearDrones()
    {
        for (int i = drones.Count - 1; i >= 0; i--)
        {
            if (drones[i] != null)
            {
                Destroy(drones[i].gameObject);
            }
        }

        drones.Clear();
    }
}
