using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class PlayerStats : EntityStats
{
    CharacterData characterData;
    public CharacterData.Stats baseStats;
    [SerializeField] CharacterData.Stats actualStats;

    public CharacterData.Stats Stats
    {
        get { return actualStats; }
        set
        {
            actualStats = value;
        }
    }

    public CharacterData.Stats Actual
    {
        get { return actualStats; }
    }

    #region Current Stats Properties
    public float CurrentHealth
    {
        get { return health; }

        set
        {
            if (health != value)
            {
                health = value;
                UpdateHealthBar();
            }
        }
    }
    #endregion

    [Header("Visuals")]
    public ParticleSystem damageEffect;
    public ParticleSystem blockedEffect;

    [Header("Experience/Level")]
    public int experience = 0;
    public int level = 1;
    public int experienceCap;

    [System.Serializable]
    public class LevelRange
    {
        public int startLevel;
        public int endLevel;
        public int experienceCapIncrease;
    }

    [Header("I-Frames")]
    public float invincibilityDuration;
    float invincibilityTimer;
    bool isInvincible;

    public List<LevelRange> levelRanges;

    PlayerInventory inventory;
    PlayerCollector collector;

    // -------------------------
    // CHECKPOINT + REVIVE
    // -------------------------

    [Header("Revive")]
    public bool reviveUsed = false;
    public Vector3 checkpointPosition;
    public bool hasCheckpoint = false;

    [Header("Checkpoint Timer")]
    public float checkpointInterval = 60f;
    private float checkpointTimer;

    [Header("UI")]
    public Image healthBar;
    public Image expBar;
    public TMP_Text levelText;

    void Awake()
    {
        characterData = UICharacterSelector.GetData();

        inventory = GetComponent<PlayerInventory>();
        collector = GetComponentInChildren<PlayerCollector>();

        baseStats = actualStats = characterData.stats;
        collector.SetRadius(actualStats.magnet);
        health = actualStats.maxHealth;
    }

    protected override void Start()
    {
        base.Start();

        if (UILevelSelector.globalBuff && !UILevelSelector.globalBuffAffectsPlayer)
            ApplyBuff(UILevelSelector.globalBuff);

        inventory.Add(characterData.StartingWeapon);

        experienceCap = levelRanges[0].experienceCapIncrease;

        GameManager.instance.AssignChosenCharacterUI(characterData);

        // Premier checkpoint = spawn du joueur
        checkpointPosition = transform.position;
        hasCheckpoint = true;
        checkpointTimer = checkpointInterval;
        reviveUsed = false;

        UpdateHealthBar();
        UpdateExpBar();
        UpdateLevelText();
    }

    protected override void Update()
    {
        base.Update();

        if (invincibilityTimer > 0)
        {
            invincibilityTimer -= Time.deltaTime;
        }
        else if (isInvincible)
        {
            isInvincible = false;
        }

        // Timer checkpoint automatique
        checkpointTimer -= Time.deltaTime;

        if (checkpointTimer <= 0f)
        {
            checkpointPosition = transform.position;
            hasCheckpoint = true;
            checkpointTimer = checkpointInterval;

            Debug.Log("Checkpoint sauvegardé : " + checkpointPosition);
        }

        Recover();
    }

    public override void RecalculateStats()
    {
        actualStats = baseStats;

        foreach (PlayerInventory.Slot s in inventory.passiveSlots)
        {
            Passive p = s.item as Passive;
            if (p)
            {
                actualStats += p.GetBoosts();
            }
        }

        CharacterData.Stats multiplier = new CharacterData.Stats
        {
            maxHealth = 1f,
            recovery = 1f,
            armor = 1f,
            moveSpeed = 1f,
            might = 1f,
            area = 1f,
            speed = 1f,
            duration = 1f,
            amount = 1,
            cooldown = 1f,
            luck = 1f,
            growth = 1f,
            greed = 1f,
            curse = 1f,
            magnet = 1f,
            revival = 1
        };

        foreach (Buff b in activeBuffs)
        {
            BuffData.Stats bd = b.GetData();
            switch (bd.modifierType)
            {
                case BuffData.ModifierType.additive:
                    actualStats += bd.playerModifier;
                    break;

                case BuffData.ModifierType.multiplicative:
                    multiplier *= bd.playerModifier;
                    break;
            }
        }

        actualStats *= multiplier;

        collector.SetRadius(actualStats.magnet);
    }

    public void IncreaseExperience(int amount)
    {
        experience += amount;

        LevelUpChecker();
        UpdateExpBar();
    }

    void LevelUpChecker()
    {
        if (experience >= experienceCap)
        {
            level++;
            experience -= experienceCap;

            int experienceCapIncrease = 0;
            foreach (LevelRange range in levelRanges)
            {
                if (level >= range.startLevel && level <= range.endLevel)
                {
                    experienceCapIncrease = range.experienceCapIncrease;
                    break;
                }
            }

            experienceCap += experienceCapIncrease;

            UpdateLevelText();

            GameManager.instance.StartLevelUp();

            if (experience >= experienceCap) LevelUpChecker();
        }
    }

    void UpdateExpBar()
    {
        expBar.fillAmount = (float)experience / experienceCap;
    }

    void UpdateLevelText()
    {
        levelText.text = "LV " + level.ToString();
    }

    public override void TakeDamage(float dmg)
    {
        if (!isInvincible)
        {
            dmg -= actualStats.armor;

            if (dmg > 0)
            {
                CurrentHealth -= dmg;

                if (damageEffect)
                    Destroy(Instantiate(damageEffect, transform.position, Quaternion.identity), 5f);

                if (CurrentHealth <= 0)
                {
                    Kill();
                }
            }
            else
            {
                if (blockedEffect)
                    Destroy(Instantiate(blockedEffect, transform.position, Quaternion.identity), 5f);
            }

            invincibilityTimer = invincibilityDuration;
            isInvincible = true;
        }
    }

    void UpdateHealthBar()
    {
        healthBar.fillAmount = CurrentHealth / actualStats.maxHealth;
    }

    public override void Kill()
    {
        if (!GameManager.instance.isGameOver)
        {
            // Revive si disponible
            if (!reviveUsed && hasCheckpoint)
            {
                reviveUsed = true;

                transform.position = checkpointPosition;

                CurrentHealth = actualStats.maxHealth * 0.5f;

                invincibilityTimer = invincibilityDuration;
                isInvincible = true;

                Debug.Log("Revive utilisé !");
            }
            else
            {
                GameManager.instance.AssignLevelReachedUI(level);
                GameManager.instance.GameOver();
            }
        }
    }

    public override void RestoreHealth(float amount)
    {
        if (CurrentHealth < actualStats.maxHealth)
        {
            CurrentHealth += amount;

            if (CurrentHealth > actualStats.maxHealth)
            {
                CurrentHealth = actualStats.maxHealth;
            }
        }
    }

    void Recover()
    {
        if (CurrentHealth < actualStats.maxHealth)
        {
            CurrentHealth += Stats.recovery * Time.deltaTime;

            if (CurrentHealth > actualStats.maxHealth)
            {
                CurrentHealth = actualStats.maxHealth;
            }
        }
    }
}