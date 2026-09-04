using UnityEngine;

[CreateAssetMenu(menuName = "Myth/Combat/Player Unit Config")]
public class PlayerUnitConfig : ScriptableObject
{
    [Header("Visual")]
    [SerializeField] private string id = "unit_default";
    [SerializeField] private string displayName = "유닛이름";
    [SerializeField] private GameObject unitPrefab;

    [Header("Stats")]
    [SerializeField] private float maxHealth = 100f;
    [SerializeField, Range(0f, 1f)] private float critChance = 0.1f;
    [SerializeField] private float critMultiplier = 1.5f;

    [Header("Combat")]
    [SerializeField] private float attackRange = 6f;
    [SerializeField] private float attackDamage = 10f;
    [SerializeField] private float attackInterval = 0.5f;
    [SerializeField] private float moveSpeed = 3f;
    [SerializeField] private float rotationSpeed = 540f;
    [SerializeField] private float fireAngleTolerance = 3f;

    [Header("Auto Reposition")]
    [SerializeField] private float repositionDistance = 1.8f;
    [SerializeField] private float repositionCooldown = 5f;
    
    [Header("boss Reposition")]
    [SerializeField] private float bossDodgeCooldown = 1.5f;

    public string Id => id;
    public string DisplayName => displayName;
    public GameObject UnitPrefab => unitPrefab;
    public float MaxHealth => maxHealth;
    public float CritChance => critChance;
    public float CritMultiplier => critMultiplier;
    public float AttackRange => attackRange;
    public float AttackDamage => attackDamage;
    public float AttackInterval => attackInterval;
    public float MoveSpeed => moveSpeed;
    public float RotationSpeed => rotationSpeed;
    public float FireAngleTolerance => fireAngleTolerance;
    public float RepositionDistance => repositionDistance;
    public float RepositionCooldown => repositionCooldown;
    public float BossDodgeCooldown => Mathf.Max(0f, bossDodgeCooldown);
}
