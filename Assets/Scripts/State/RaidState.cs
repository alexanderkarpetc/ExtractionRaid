using System.Collections.Generic;

namespace State
{
    public class RaidState
    {
        public float ElapsedTime;
        public bool IsRunning;
        public PlayerEntityState PlayerEntity;
        public List<ProjectileEntityState> Projectiles;
        public List<GrenadeEntityState> Grenades;
        public Dictionary<EId, HealthState> HealthMap;
        public List<GroundItemState> GroundItems;
        public List<BotEntityState> Bots;
        public List<LootableContainerState> Lootables;
        public InventoryState Inventory;
        public Dictionary<EId, List<StatusEffectInstance>> StatusEffects;

        int _nextEIdValue;

        public EId AllocateEId()
        {
            _nextEIdValue++;
            return new EId(_nextEIdValue);
        }

        public static RaidState Create()
        {
            return new RaidState
            {
                ElapsedTime = 0f,
                IsRunning = true,
                _nextEIdValue = 0,
                Projectiles = new List<ProjectileEntityState>(),
                Grenades = new List<GrenadeEntityState>(),
                HealthMap = new Dictionary<EId, HealthState>(),
                GroundItems = new List<GroundItemState>(),
                Bots = new List<BotEntityState>(),
                Lootables = new List<LootableContainerState>(),
                Inventory = new InventoryState(),
                StatusEffects = new Dictionary<EId, List<StatusEffectInstance>>(),
            };
        }
    }
}
