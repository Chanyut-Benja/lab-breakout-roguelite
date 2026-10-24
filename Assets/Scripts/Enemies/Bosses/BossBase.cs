using System;
using System.Collections.Generic;
using Enemies.Bosses.States;
using Enemies.Core;
using Enemies.States;
using UnityEngine;

namespace Enemies.Bosses
{
    /// <summary>
    /// Base class for all Boss entities.
    /// Manages the Boss FSM lifecycle:
    /// Intro -> Reposition <-> PickAttack (Non-repeating modular attacks)
    /// -> PhaseTransition on HP threshold -> Dead (OnBossDied event + RoomManager defeat notification).
    /// </summary>
    public abstract class BossBase : EnemyBase
    {
        [Header("Boss Specific Data")]
        [SerializeField] protected BossData bossConfig;

        // Static Events per specification contract
        public static event Action<BossData> OnBossDied;
        public static event Action<float, float> OnBossHealthChanged; // (current, max)
        public static event Action<string, float, float> OnBossSpawned; // (name, current, max)
        public static event Action<int> OnBossPhaseChanged; // (phaseNumber)

        // Runtime Phase & State (Balance 8.3: All 6 bosses use 3 Phases: P2 at 66% HP, P3 Enrage at 33% HP or 180s)
        public BossData BossConfig => bossConfig;
        public int CurrentPhase { get; protected set; } = 1;
        public int MaxPhases { get; protected set; } = 3;

        private float combatElapsedTime = 0f;
        private bool isTimeEnraged = false;
        private int remainingEnrageChainAttacks = 0;

        public bool IsTimeEnraged => isTimeEnraged;
        public float CombatElapsedTime => combatElapsedTime;
        public virtual float DefaultBaseMaxHP => bossConfig != null && bossConfig.MaxHP >= 10000f ? bossConfig.MaxHP : 12000f;

        // Minions summoned by this boss
        private readonly List<EnemyBase> activeMinions = new List<EnemyBase>();
        public int ActiveMinionCount
        {
            get
            {
                activeMinions.RemoveAll(m => m == null || !m.gameObject.activeInHierarchy);
                return activeMinions.Count;
            }
        }

        // Shared Boss States
        protected BossIntroState introState;
        protected BossRepositionState repositionState;
        protected BossPickAttackState pickAttackState;
        protected BossPhaseTransitionState phaseTransitionState;
        protected BossDeadState bossDeadState;

        protected override void Awake()
        {
            if (bossConfig != null && data == null)
            {
                data = bossConfig;
            }

            base.Awake();
        }

        protected override void OnEnable()
        {
            base.OnEnable();
            CurrentPhase = 1;
            combatElapsedTime = 0f;
            isTimeEnraged = false;
            remainingEnrageChainAttacks = 0;

            if (Health != null)
            {
                Health.OnHealthChanged += HandleHealthChanged;
            }
        }

        protected override void Update()
        {
            if (Health != null && !Health.IsDead)
            {
                combatElapsedTime += Time.deltaTime;
                float enrageTimeLimit = bossConfig != null && bossConfig.EnrageTimeLimitSeconds > 0f
                    ? bossConfig.EnrageTimeLimitSeconds
                    : 180f;

                // บาลานซ์ 8.3: บอสทุกตัว Enrage อัตโนมัติหากสู้เกิน 3 นาที (180s)
                if (!IsEnraged && combatElapsedTime >= enrageTimeLimit)
                {
                    TriggerTimeLimitEnrage();
                }
            }

            base.Update();
        }

        protected virtual void OnDisable()
        {
            if (Health != null)
            {
                Health.OnHealthChanged -= HandleHealthChanged;
            }

            ClearMinions();
        }

        public override void Init(Transform target, float difficultyMultiplier = 1f)
        {
            Init(target, 1f, difficultyMultiplier, 1f);
        }

        public override void Init(Transform target, float hpMultiplier, float damageMultiplier, float speedMultiplier)
        {
            base.Init(target, hpMultiplier, damageMultiplier, speedMultiplier);

            CurrentPhase = 1;
            MaxPhases = 3;
            combatElapsedTime = 0f;
            isTimeEnraged = false;
            remainingEnrageChainAttacks = 0;

            if (Health != null)
            {
                // บังคับใช้ Max HP พื้นฐานตามตารางบาลานซ์ 8.3 ของบอสแต่ละตัว
                Health.SetExactMaxHealth(DefaultBaseMaxHP);
                OnBossHealthChanged?.Invoke(Health.CurrentHealth, Health.MaxHealth);
            }
        }

        protected override void StartInitialState()
        {
            if (stateMachine != null && introState != null)
            {
                stateMachine.Initialize(introState);
            }
        }

        private void HandleHealthChanged(float current, float max)
        {
            OnBossHealthChanged?.Invoke(current, max);

            if (max <= 0f) return;

            float hpPercent = current / max;
            // บาลานซ์ 8.3: เฟส 2 เริ่มที่ HP 66%, เฟส 3 (Enrage) เริ่มที่ HP 33% สำหรับบอสทุกตัว
            float p2Threshold = bossConfig != null && bossConfig.Phase2HPThreshold > 0.55f ? bossConfig.Phase2HPThreshold : 0.66f;
            float p3Threshold = bossConfig != null && bossConfig.Phase3HPThreshold > 0f ? bossConfig.Phase3HPThreshold : 0.33f;

            // Phase 1 -> 2 Check (HP <= 66%)
            if (CurrentPhase == 1 && hpPercent <= p2Threshold && hpPercent > 0f)
            {
                TriggerPhaseTransition();
            }
            // Phase 2 -> 3 Check (HP <= 33%)
            else if (CurrentPhase == 2 && MaxPhases >= 3 && hpPercent <= p3Threshold && hpPercent > 0f)
            {
                TriggerPhaseTransition();
            }
        }

        public void TriggerTimeLimitEnrage()
        {
            if (isTimeEnraged || IsEnraged) return;
            isTimeEnraged = true;
            Debug.LogWarning($"[{bossConfig?.BossTitle ?? name}] Fight exceeded 180s (3 minutes)! Auto-Enrage (Phase 3) triggered!");

            if (CurrentPhase < 2)
            {
                CurrentPhase = 2; // จะถูก AdvancePhase() เข้าสู่ Phase 3 ใน BossPhaseTransitionState
            }
            TriggerPhaseTransition();
        }

        public void TriggerPhaseTransition()
        {
            if (stateMachine != null && phaseTransitionState != null)
            {
                if (!(stateMachine.CurrentState is BossPhaseTransitionState) && !(stateMachine.CurrentState is BossDeadState))
                {
                    stateMachine.ChangeState(phaseTransitionState);
                }
            }
        }

        public void ForceSetPhase(int targetPhase)
        {
            if (targetPhase > CurrentPhase && targetPhase <= MaxPhases)
            {
                TriggerPhaseTransition();
            }
        }

        public virtual bool IsFullRadial360 => (bossConfig != null && bossConfig.FullRadial360) || IsEnraged;
        public virtual bool IsEnraged => CurrentPhase >= 3 || isTimeEnraged;
        public virtual float RecoverDurationMultiplier => IsEnraged ? 0.55f : (CurrentPhase == 2 ? 0.80f : 1.0f);

        /// <summary>
        /// ตัวคูณความเร็วเคลื่อนที่ตามเฟส (บาลานซ์ 8.3: เฟส 2 สปีด +20%, เฟส 3 Enrage สปีด +35%)
        /// </summary>
        public virtual float GetPhaseSpeedMultiplier()
        {
            if (IsEnraged)
            {
                return bossConfig != null && bossConfig.Phase3SpeedMultiplier > 1f ? bossConfig.Phase3SpeedMultiplier : 1.35f;
            }
            if (CurrentPhase == 2)
            {
                return bossConfig != null && bossConfig.Phase2SpeedMultiplier > 1f ? bossConfig.Phase2SpeedMultiplier : 1.20f;
            }
            return 1.0f;
        }

        /// <summary>
        /// ในเฟส 3 (Enrage) บอสจะปล่อยท่าใหญ่ต่อเนื่อง (Chain Attack 2 ท่าติดกันก่อนพัก Reposition สั้นๆ)
        /// </summary>
        public bool TryConsumeEnrageChainAttack()
        {
            if (!IsEnraged) return false;

            if (remainingEnrageChainAttacks > 0)
            {
                remainingEnrageChainAttacks--;
                return true;
            }

            // รีเซ็ตให้รอบถัดไปปล่อยท่าต่อเนื่องได้อีก
            remainingEnrageChainAttacks = 1;
            return false;
        }

        public virtual void AdvancePhase()
        {
            CurrentPhase = Mathf.Min(MaxPhases, CurrentPhase + 1);
            if (IsEnraged)
            {
                remainingEnrageChainAttacks = 1;
            }
            OnBossPhaseChanged?.Invoke(CurrentPhase);
            Debug.Log($"[{bossConfig?.BossTitle ?? name}] Advanced to Phase {CurrentPhase} (Speed x{GetPhaseSpeedMultiplier():F2})!");
        }

        private float lastContactDamageTime;

        public override void HandlePlayerContact(GameObject playerObj)
        {
            if (playerObj == null) return;

            // If currently in Charge attack state, delegate to charge state for heavy charge damage
            if (stateMachine != null && stateMachine.CurrentState is BossChargeAttackState chargeState)
            {
                chargeState.OnPlayerContact(playerObj);
                return;
            }

            // Normal contact damage when bumping into player
            float contactCooldown = data != null ? data.ContactDamageCooldown : 0.6f;
            if (Time.time >= lastContactDamageTime + contactCooldown)
            {
                lastContactDamageTime = Time.time;
                IDamageable dmg = playerObj.GetComponentInChildren<IDamageable>() ?? playerObj.GetComponentInParent<IDamageable>();
                if (dmg != null)
                {
                    float contactDmg = (data != null ? data.ContactDamage : 15f) * DifficultyMultiplier;
                    Vector2 kb = ((Vector2)playerObj.transform.position - (Vector2)transform.position).normalized * 6f;
                    dmg.TakeDamage(contactDmg, kb);
                    Debug.Log($"[{name}] Contact hit Player for {contactDmg} damage!");
                }
            }
        }

        public void RegisterMinion(EnemyBase minion)
        {
            if (minion != null && !activeMinions.Contains(minion))
            {
                activeMinions.Add(minion);
            }
        }

        private void ClearMinions()
        {
            for (int i = 0; i < activeMinions.Count; i++)
            {
                if (activeMinions[i] != null && activeMinions[i].gameObject.activeInHierarchy)
                {
                    activeMinions[i].Despawn();
                }
            }
            activeMinions.Clear();
        }

        public virtual void NotifyBossSpawned()
        {
            string title = bossConfig != null ? bossConfig.BossTitle : gameObject.name;
            float currentHP = Health != null ? Health.CurrentHealth : 100f;
            float maxHP = Health != null ? Health.MaxHealth : 100f;
            OnBossSpawned?.Invoke(title, currentHP, maxHP);
        }

        public virtual void NotifyBossDefeated()
        {
            ClearMinions();

            OnBossDied?.Invoke(bossConfig);

            if (RoomManager.Instance != null)
            {
                RoomManager.Instance.OnBossDefeated();
            }

            Debug.Log($"[{bossConfig?.BossTitle ?? name}] Defeated! Fired OnBossDied and RoomManager.OnBossDefeated.");
        }

        public override void OnDeath()
        {
            if (bossDeadState != null && stateMachine != null)
            {
                stateMachine.ChangeState(bossDeadState);
            }
            else
            {
                NotifyBossDefeated();
                base.OnDeath();
            }
        }

        /// <summary>
        /// Derived boss implementations provide their attack pool per phase.
        /// </summary>
        public abstract List<IState> GetCurrentPhaseAttacks();

        public void SetBossConfig(BossData newConfig)
        {
            this.bossConfig = newConfig;
            this.data = newConfig;
        }

        public static int GetBalancedExpByRoom(int roomNumber)
        {
            return roomNumber switch
            {
                3 => 40,
                6 => 50,
                9 => 60,
                12 => 70,
                15 => 80,
                18 => 100,
                _ => roomNumber >= 18 ? 100 : (30 + roomNumber * 3)
            };
        }

        public static void ClearAllBossHazards()
        {
            AcidPool[] pools = UnityEngine.Object.FindObjectsByType<AcidPool>(FindObjectsSortMode.None);
            for (int i = 0; i < pools.Length; i++)
            {
                if (pools[i] != null) UnityEngine.Object.Destroy(pools[i].gameObject);
            }

            BossTurret[] turrets = UnityEngine.Object.FindObjectsByType<BossTurret>(FindObjectsSortMode.None);
            for (int i = 0; i < turrets.Length; i++)
            {
                if (turrets[i] != null) UnityEngine.Object.Destroy(turrets[i].gameObject);
            }
        }
    }
}
