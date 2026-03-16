using System.Collections.Generic;
using Adapters;
using Systems;
using Systems.Bot;
using State;

namespace Session
{
    public class RaidSession
    {
        public RaidState RaidState { get; private set; }
        public LevelState LevelState { get; private set; }
        public bool IsActive => RaidState.IsRunning;

        readonly RaidEventBuffer _eventBuffer;
        readonly ITimeAdapter _timeAdapter;
        readonly IInputAdapter _inputAdapter;
        readonly INavMeshAdapter _navMeshAdapter;
        readonly IPhysicsAdapter _physicsAdapter;
        readonly IGrenadePositionAdapter _grenadePositionAdapter;
        readonly List<HitSignal> _hitInbox = new();
        readonly List<CollisionSignal> _collisionInbox = new();

        public RaidSession(string levelId, ITimeAdapter timeAdapter, IInputAdapter inputAdapter,
            INavMeshAdapter navMeshAdapter, IPhysicsAdapter physicsAdapter = null,
            IGrenadePositionAdapter grenadePositionAdapter = null)
        {
            _timeAdapter = timeAdapter;
            _inputAdapter = inputAdapter;
            _navMeshAdapter = navMeshAdapter;
            _physicsAdapter = physicsAdapter;
            _grenadePositionAdapter = grenadePositionAdapter;
            _eventBuffer = new RaidEventBuffer();
            RaidState = RaidState.Create();
            LevelState = LevelState.Create(levelId);
        }

        public void Start()
        {
            PlayerSpawnSystem.SpawnPlayer(RaidState, _eventBuffer);
            SpawnTestGroundItems();
            //SpawnTestBots();

            if (LevelState.LevelId == "shooting_range")
                SpawnShootingRangeTargets();

            _eventBuffer.RaidStarted();
        }

        void SpawnTestGroundItems()
        {
            var testItems = new (string defId, UnityEngine.Vector3 pos, int count)[]
            {
                ("Medkit", new UnityEngine.Vector3(3f, 0f, 2f), (int)Constants.MedConstants.TotalHealAmount),
                ("Helmet_Basic", new UnityEngine.Vector3(-2f, 0f, 4f), 1),
                ("Armor_Basic", new UnityEngine.Vector3(5f, 0f, -1f), 1),
                ("Ammo_Rifle", new UnityEngine.Vector3(-3f, 0f, -3f), 30),
                ("Ammo_Shotgun", new UnityEngine.Vector3(-1f, 0f, -4f), 10),
                ("Rifle", new UnityEngine.Vector3(4f, 0f, 4f), 1),
                ("Shotgun", new UnityEngine.Vector3(-4f, 0f, 1f), 1),
            };

            foreach (var (defId, pos, count) in testItems)
            {
                var id = RaidState.AllocateEId();
                var groundItem = GroundItemState.Create(id, defId, pos, count);
                RaidState.GroundItems.Add(groundItem);
                _eventBuffer.GroundItemSpawned(id, pos, defId);
            }
        }

        void SpawnTestBots()
        {
            // BotSpawnSystem.SpawnBot(RaidState, "Scav",
            //     new UnityEngine.Vector3(10f, 0f, 10f),
            //     new[]
            //     {
            //         new UnityEngine.Vector3(10f, 0f, 10f),
            //         new UnityEngine.Vector3(15f, 0f, 5f),
            //         new UnityEngine.Vector3(20f, 0f, 10f),
            //     },
            //     _eventBuffer);

            // BotSpawnSystem.SpawnBot(RaidState, "PMC",
            //     new UnityEngine.Vector3(-10f, 0f, 15f),
            //     new[]
            //     {
            //         new UnityEngine.Vector3(-10f, 0f, 15f),
            //         new UnityEngine.Vector3(-5f, 0f, 20f),
            //         new UnityEngine.Vector3(-15f, 0f, 20f),
            //     },
            //     _eventBuffer);
            //
            // BotSpawnSystem.SpawnBot(RaidState, "Boss",
            //     new UnityEngine.Vector3(0f, 0f, 25f),
            //     new[]
            //     {
            //         new UnityEngine.Vector3(0f, 0f, 25f),
            //         new UnityEngine.Vector3(5f, 0f, 30f),
            //     },
            //     _eventBuffer);
        }

        void SpawnShootingRangeTargets()
        {
            // Left group: 5 immortal targets (10000 HP)
            for (int i = 0; i < 5; i++)
            {
                float x = -8f + i * 4f;
                var pos = new UnityEngine.Vector3(x, 0f, 15f);
                BotSpawnSystem.SpawnBot(RaidState, "Target", pos, new[] { pos }, _eventBuffer);
            }

            // Right group: 5 weak targets (50 HP) for kill testing
            for (int i = 0; i < 5; i++)
            {
                float x = 16f + i * 4f;
                var pos = new UnityEngine.Vector3(x, 0f, 15f);
                BotSpawnSystem.SpawnBot(RaidState, "TargetWeak", pos, new[] { pos }, _eventBuffer);
            }
        }

        public void Tick()
        {
            if (!RaidState.IsRunning) return;

            var context = new RaidContext(
                deltaTime: _timeAdapter.DeltaTime,
                events: _eventBuffer,
                time: _timeAdapter,
                input: _inputAdapter,
                navMesh: _navMeshAdapter,
                physics: _physicsAdapter,
                grenadePositions: _grenadePositionAdapter
            );

            RollSystem.Tick(RaidState, in context);
            MovementSystem.Tick(RaidState, in context);
            WeaponEquipSystem.Tick(RaidState, in context);
            WeaponStateMachineSystem.Tick(RaidState, in context);
            AimingSystem.Tick(RaidState, in context);
            GrenadeSystem.Tick(RaidState, in context);
            MedkitSystem.Tick(RaidState, in context);
            ShootingSystem.Tick(RaidState, in context);

            PlayerFOVSystem.Tick(RaidState);
            BotPerceptionSystem.Tick(RaidState, in context);
            BotBrainSystem.Tick(RaidState, in context);
            BotMovementSystem.Tick(RaidState, in context);
            BotCombatSystem.Tick(RaidState, in context);

            ProjectileSystem.Tick(RaidState, in context);
            GrenadeSystem.TickExplosions(RaidState, in context);
            DamageSystem.Tick(RaidState, _hitInbox, in context);
            _hitInbox.Clear();
            ProcessCollisions(in context);
            _collisionInbox.Clear();
            ProcessDamageAlerts();
            ProcessDeathEvents();

            if (context.Input.PickUpPressed && RaidState.PlayerEntity != null)
            {
                var nearest = InventorySystem.FindNearestGroundItem(RaidState, RaidState.PlayerEntity.Position);
                if (nearest.IsValid)
                    InventorySystem.TryPickUp(RaidState, nearest, _eventBuffer);
            }

            RaidState.ElapsedTime += context.DeltaTime;
        }

        void ProcessCollisions(in RaidContext context)
        {
            foreach (var col in _collisionInbox)
            {
                for (int i = RaidState.Projectiles.Count - 1; i >= 0; i--)
                {
                    if (RaidState.Projectiles[i].Id == col.ProjectileId)
                    {
                        context.Events.ProjectileHit(col.ProjectileId, col.Position);
                        context.Events.ProjectileDespawned(col.ProjectileId);
                        RaidState.Projectiles.RemoveAt(i);
                        break;
                    }
                }
            }
        }

        void ProcessDamageAlerts()
        {
            foreach (var e in _eventBuffer.All)
            {
                if (e.Type != RaidEventType.EntityDamaged) continue;

                for (int i = 0; i < RaidState.Bots.Count; i++)
                {
                    if (RaidState.Bots[i].Id == e.Id)
                    {
                        RaidState.Bots[i].Blackboard.WasDamaged = true;
                        break;
                    }
                }
            }
        }

        void ProcessDeathEvents()
        {
            int count = _eventBuffer.All.Count;
            for (int idx = 0; idx < count; idx++)
            {
                var e = _eventBuffer.All[idx];
                if (e.Type != RaidEventType.EntityDied) continue;

                for (int i = RaidState.Bots.Count - 1; i >= 0; i--)
                {
                    if (RaidState.Bots[i].Id == e.Id)
                    {
                        RaidState.Bots.RemoveAt(i);
                        _eventBuffer.BotDespawned(e.Id);
                        break;
                    }
                }

                if (RaidState.PlayerEntity != null && RaidState.PlayerEntity.Id == e.Id)
                {
                    End();
                }

                RaidState.HealthMap.Remove(e.Id);
            }
        }

        public RaidEventBuffer ConsumeEvents() => _eventBuffer;

        public void ClearEvents() => _eventBuffer.Clear();

        public void ReportHit(HitSignal signal)
        {
            _hitInbox.Add(signal);
        }

        public void ReportCollision(CollisionSignal signal)
        {
            _collisionInbox.Add(signal);
        }

        public bool RequestDrop(InventorySlotRef slot, UnityEngine.Vector3 dropPosition)
        {
            return InventorySystem.TryDrop(RaidState, slot, dropPosition, _eventBuffer);
        }

        public void End()
        {
            RaidState.IsRunning = false;
            _eventBuffer.RaidEnded();
        }
    }
}
