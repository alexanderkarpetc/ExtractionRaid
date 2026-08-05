using System.Collections.Generic;

namespace State
{
    public class RaidState
    {
        public float ElapsedTime;

        // Raid clock (M1.2). Seconds the player has before RaidTimerSystem KIAs them.
        // 0 = no limit — that's how the hideout and the shooting ranges opt out. The
        // remaining time is derived (RaidDurationSeconds - ElapsedTime), never stored,
        // so there is no second clock to keep in sync.
        public float RaidDurationSeconds;

        public bool IsRunning;
        public PlayerEntityState PlayerEntity;
        public List<ProjectileEntityState> Projectiles;
        public List<GrenadeEntityState> Grenades;
        public Dictionary<EId, HealthState> HealthMap;
        public List<GroundItemState> GroundItems;
        public List<BotEntityState> Bots;
        public List<LootableContainerState> Lootables;
        public List<WorkbenchState> Workbenches;
        public List<DeployPointState> DeployPoints;
        public List<NpcState> Npcs;
        public List<ExtractionPointState> ExtractionPoints;
        public Dictionary<EId, List<StatusEffectInstance>> StatusEffects;
        public Dictionary<EId, ArmorSlotState> ArmorMap;

        // Horde test scene scheduling — populated only when LevelId == "horde_range".
        // 0 = uninitialised; HordeSpawnSystem seeds it on the first post-grace tick.
        public float HordeNextSpawnTime;

        System.Func<EId> _allocateEId;

        public EId AllocateEId() => _allocateEId();

        public static RaidState Create(System.Func<EId> allocateEId)
        {
            return new RaidState
            {
                ElapsedTime = 0f,
                IsRunning = true,
                _allocateEId = allocateEId,
                Projectiles = new List<ProjectileEntityState>(),
                Grenades = new List<GrenadeEntityState>(),
                HealthMap = new Dictionary<EId, HealthState>(),
                GroundItems = new List<GroundItemState>(),
                Bots = new List<BotEntityState>(),
                Lootables = new List<LootableContainerState>(),
                Workbenches = new List<WorkbenchState>(),
                DeployPoints = new List<DeployPointState>(),
                Npcs = new List<NpcState>(),
                ExtractionPoints = new List<ExtractionPointState>(),
                StatusEffects = new Dictionary<EId, List<StatusEffectInstance>>(),
                ArmorMap = new Dictionary<EId, ArmorSlotState>(),
            };
        }
    }
}
