using System.Linq;
using Adapters;
using ApplicationCore;
using NUnit.Framework;
using Session;
using State;
using Systems;
using Tests.EditMode.Fakes;
using UnityEngine;

namespace Tests.EditMode
{
    [TestFixture]
    public class WeaponStateMachineSystemTests
    {
        // ── Setup DSL ─────────────────────────────────────────
        //
        // Most tests drive the same shape:
        //   1. Player state with default weapon (CreateStateWithPlayer).
        //   2. Put weapon into a specific Phase + timings.
        //   3. Call WeaponStateMachineSystem.Tick with a default/empty context.
        //
        // `Setup(...)` wraps steps 1-2. Callers can still mutate state.PlayerEntity
        // (hotbar slots, SelectedHotbarSlot, etc.) after the call for per-test nuance.

        static (RaidState state, WeaponEntityState weapon) Setup(
            WeaponPhase phase = WeaponPhase.Ready,
            float phaseStart = 0f,
            float elapsedTime = 0f,
            float? fireInterval = null,
            float? equipTime = null,
            float? unequipTime = null,
            float? reloadTime = null,
            int? ammoInMag = null,
            int? pendingSlot = null)
        {
            var state = EditModeTestsUtils.CreateStateWithPlayer(Vector3.zero);
            var weapon = state.PlayerEntity.EquippedWeapon;
            weapon.Phase = phase;
            weapon.PhaseStartTime = phaseStart;
            state.ElapsedTime = elapsedTime;

            if (fireInterval.HasValue) weapon.Stats.FireInterval = fireInterval.Value;
            if (equipTime.HasValue)    weapon.Stats.EquipTime    = equipTime.Value;
            if (unequipTime.HasValue)  weapon.Stats.UnequipTime  = unequipTime.Value;
            if (reloadTime.HasValue)   weapon.Stats.ReloadTime   = reloadTime.Value;
            if (ammoInMag.HasValue)    weapon.AmmoInMagazine     = ammoInMag.Value;
            if (pendingSlot.HasValue)  state.PlayerEntity.PendingHotbarSlot = pendingSlot.Value;

            return (state, weapon);
        }

        // ── Firing → Cooldown ─────────────────────────────────────

        [Test]
        public void Tick_FiringPhase_TransitionsToCooldown()
        {
            var (state, weapon) = Setup(phase: WeaponPhase.Firing, phaseStart: 1f, elapsedTime: 1f);
            var context = TestContextFactory.Create();

            WeaponStateMachineSystem.Tick(state, in context);

            Assert.AreEqual(WeaponPhase.Cooldown, weapon.Phase);
            Assert.AreEqual(state.ElapsedTime, weapon.PhaseStartTime, 0.001f);
        }

        // ── Cooldown → Ready ──────────────────────────────────────

        [Test]
        public void Tick_CooldownExpired_TransitionsToReady()
        {
            var (state, weapon) = Setup(phase: WeaponPhase.Cooldown, fireInterval: 0.2f, elapsedTime: 0.3f);
            var context = TestContextFactory.Create();

            WeaponStateMachineSystem.Tick(state, in context);

            Assert.AreEqual(WeaponPhase.Ready, weapon.Phase);
        }

        [Test]
        public void Tick_CooldownNotExpired_StaysInCooldown()
        {
            var (state, weapon) = Setup(phase: WeaponPhase.Cooldown, fireInterval: 0.2f, elapsedTime: 0.1f);
            var context = TestContextFactory.Create();

            WeaponStateMachineSystem.Tick(state, in context);

            Assert.AreEqual(WeaponPhase.Cooldown, weapon.Phase);
        }

        // ── Equipping → Ready ─────────────────────────────────────

        [Test]
        public void Tick_EquippingDone_TransitionsToReady()
        {
            var (state, weapon) = Setup(phase: WeaponPhase.Equipping, equipTime: 0.3f, elapsedTime: 0.4f);
            var context = TestContextFactory.Create();

            WeaponStateMachineSystem.Tick(state, in context);

            Assert.AreEqual(WeaponPhase.Ready, weapon.Phase);
        }

        [Test]
        public void Tick_EquippingDone_EmitsWeaponEquipFinished()
        {
            var (state, weapon) = Setup(phase: WeaponPhase.Equipping, equipTime: 0.3f, elapsedTime: 0.4f);
            var eventBuffer = new RaidEventBuffer();
            var context = TestContextFactory.Create(events: eventBuffer);

            WeaponStateMachineSystem.Tick(state, in context);

            var finished = eventBuffer.All
                .Where(e => e.Type == RaidEventType.WeaponEquipFinished).ToList();
            Assert.AreEqual(1, finished.Count);
            Assert.AreEqual(weapon.PrefabId, finished[0].StringPayload);
        }

        [Test]
        public void Tick_EquippingNotDone_StaysEquipping()
        {
            var (state, weapon) = Setup(phase: WeaponPhase.Equipping, equipTime: 0.3f, elapsedTime: 0.1f);
            var context = TestContextFactory.Create();

            WeaponStateMachineSystem.Tick(state, in context);

            Assert.AreEqual(WeaponPhase.Equipping, weapon.Phase);
        }

        // ── Unequipping → toggle off / switch ─────────────────────

        [Test]
        public void Tick_UnequippingDone_ToggleOff_GoesUnarmed()
        {
            // PendingSlot == SelectedSlot (both 0) → toggle off.
            var (state, _) = Setup(phase: WeaponPhase.Unequipping, unequipTime: 0.2f, elapsedTime: 0.3f,
                pendingSlot: 0);
            var context = TestContextFactory.Create();

            WeaponStateMachineSystem.Tick(state, in context);

            Assert.IsNull(state.PlayerEntity.EquippedWeapon);
            Assert.AreEqual(-1, state.PlayerEntity.SelectedHotbarSlot);
            Assert.AreEqual(-1, state.PlayerEntity.PendingHotbarSlot);
        }

        [Test]
        public void Tick_UnequippingDone_SwitchToNewWeapon_StartsEquipping()
        {
            // Switch from slot 0 to slot 1.
            var (state, _) = Setup(phase: WeaponPhase.Unequipping, unequipTime: 0.2f, elapsedTime: 0.3f,
                pendingSlot: 1);
            var eventBuffer = new RaidEventBuffer();
            var context = TestContextFactory.Create(events: eventBuffer);

            WeaponStateMachineSystem.Tick(state, in context);

            Assert.IsNotNull(state.PlayerEntity.EquippedWeapon);
            Assert.AreEqual("Weapon_Pistol", state.PlayerEntity.EquippedWeapon.PrefabId);
            Assert.AreEqual(WeaponPhase.Equipping, state.PlayerEntity.EquippedWeapon.Phase);
            Assert.AreEqual(1, state.PlayerEntity.SelectedHotbarSlot);
            Assert.AreEqual(-1, state.PlayerEntity.PendingHotbarSlot);

            var equipStarted = eventBuffer.All
                .Where(e => e.Type == RaidEventType.WeaponEquipStarted).ToList();
            Assert.AreEqual(1, equipStarted.Count);
        }

        [Test]
        public void Tick_UnequippingDone_SwitchToEmptySlot_GoesUnarmed()
        {
            var (state, _) = Setup(phase: WeaponPhase.Unequipping, unequipTime: 0.2f, elapsedTime: 0.3f,
                pendingSlot: 1);
            state.PlayerEntity.Hotbar[1] = null; // target slot is empty
            var context = TestContextFactory.Create();

            WeaponStateMachineSystem.Tick(state, in context);

            Assert.IsNull(state.PlayerEntity.EquippedWeapon);
            Assert.AreEqual(1, state.PlayerEntity.SelectedHotbarSlot);
        }

        // ── Swap intent triggers ──────────────────────────────────

        [Test]
        public void Tick_ReadyWithPendingSwap_StartsUnequipping()
        {
            var (state, weapon) = Setup(phase: WeaponPhase.Ready, pendingSlot: 1);
            var eventBuffer = new RaidEventBuffer();
            var context = TestContextFactory.Create(events: eventBuffer);

            WeaponStateMachineSystem.Tick(state, in context);

            Assert.AreEqual(WeaponPhase.Unequipping, weapon.Phase);
            var unequipEvents = eventBuffer.All
                .Where(e => e.Type == RaidEventType.WeaponUnequipStarted).ToList();
            Assert.AreEqual(1, unequipEvents.Count);
        }

        [Test]
        public void Tick_CooldownWithPendingSwap_StartsUnequipping()
        {
            var (state, weapon) = Setup(phase: WeaponPhase.Cooldown, fireInterval: 0.2f,
                elapsedTime: 0.05f /* still in cooldown */, pendingSlot: 1);
            var context = TestContextFactory.Create();

            WeaponStateMachineSystem.Tick(state, in context);

            Assert.AreEqual(WeaponPhase.Unequipping, weapon.Phase,
                "Swap should interrupt cooldown");
        }

        [Test]
        public void Tick_EquippingWithPendingSwap_StartsUnequipping()
        {
            var (state, weapon) = Setup(phase: WeaponPhase.Equipping, equipTime: 0.3f,
                elapsedTime: 0.1f /* still equipping */, pendingSlot: 1);
            var context = TestContextFactory.Create();

            WeaponStateMachineSystem.Tick(state, in context);

            Assert.AreEqual(WeaponPhase.Unequipping, weapon.Phase,
                "New swap intent should interrupt equipping");
        }

        // ── Unarmed + pending ─────────────────────────────────────

        [Test]
        public void Tick_UnarmedWithPending_StartsEquipping()
        {
            var state = EditModeTestsUtils.CreateStateWithPlayer(Vector3.zero);
            state.PlayerEntity.EquippedWeapon = null;
            state.PlayerEntity.SelectedHotbarSlot = -1;
            state.PlayerEntity.PendingHotbarSlot = 0;
            var eventBuffer = new RaidEventBuffer();
            var context = TestContextFactory.Create(events: eventBuffer);

            WeaponStateMachineSystem.Tick(state, in context);

            Assert.IsNotNull(state.PlayerEntity.EquippedWeapon);
            Assert.AreEqual(WeaponPhase.Equipping, state.PlayerEntity.EquippedWeapon.Phase);
            Assert.AreEqual(0, state.PlayerEntity.SelectedHotbarSlot);
            Assert.AreEqual(-1, state.PlayerEntity.PendingHotbarSlot);

            var equipStarted = eventBuffer.All
                .Where(e => e.Type == RaidEventType.WeaponEquipStarted).ToList();
            Assert.AreEqual(1, equipStarted.Count);
        }

        [Test]
        public void Tick_UnarmedNoPending_DoesNothing()
        {
            var state = EditModeTestsUtils.CreateStateWithPlayer(Vector3.zero);
            state.PlayerEntity.EquippedWeapon = null;
            state.PlayerEntity.SelectedHotbarSlot = -1;
            state.PlayerEntity.PendingHotbarSlot = -1;
            var context = TestContextFactory.Create();

            WeaponStateMachineSystem.Tick(state, in context);

            Assert.IsNull(state.PlayerEntity.EquippedWeapon);
            Assert.AreEqual(-1, state.PlayerEntity.SelectedHotbarSlot);
        }

        // ── Guard checks ──────────────────────────────────────────

        [Test]
        public void Tick_NullPlayer_DoesNotThrow()
        {
            var state = RaidState.Create(EditModeTestsUtils.NewAllocator());
            var context = TestContextFactory.Create();

            Assert.DoesNotThrow(() => WeaponStateMachineSystem.Tick(state, in context));
        }

        [Test]
        public void Tick_ReadyNoPending_StaysReady()
        {
            var (state, weapon) = Setup(phase: WeaponPhase.Ready, pendingSlot: -1);
            var context = TestContextFactory.Create();

            WeaponStateMachineSystem.Tick(state, in context);

            Assert.AreEqual(WeaponPhase.Ready, weapon.Phase);
        }

        [Test]
        public void Tick_UnequippingNotDone_StaysUnequipping()
        {
            var (state, weapon) = Setup(phase: WeaponPhase.Unequipping, unequipTime: 0.2f,
                elapsedTime: 0.1f, pendingSlot: 1);
            var context = TestContextFactory.Create();

            WeaponStateMachineSystem.Tick(state, in context);

            Assert.AreEqual(WeaponPhase.Unequipping, weapon.Phase);
        }

        // ── Reloading tests ─────────────────────────────────────

        [Test]
        public void Tick_ReloadingDone_TransitionsToReady()
        {
            var (state, weapon) = Setup(phase: WeaponPhase.Reloading, reloadTime: 2.0f,
                elapsedTime: 2.5f, ammoInMag: 0);
            var context = TestContextFactory.Create();

            WeaponStateMachineSystem.Tick(state, in context);

            Assert.AreEqual(WeaponPhase.Ready, weapon.Phase);
        }

        [Test]
        public void Tick_ReloadingDone_FillsMagazine()
        {
            // Reserve ammo already in backpack from CreateStateWithPlayer (60 Ammo_Rifle).
            var (state, weapon) = Setup(phase: WeaponPhase.Reloading, reloadTime: 2.0f,
                elapsedTime: 2.5f, ammoInMag: 0);
            var context = TestContextFactory.Create();

            WeaponStateMachineSystem.Tick(state, in context);

            Assert.AreEqual(30, weapon.AmmoInMagazine);
        }

        [Test]
        public void Tick_ReloadingDone_EmitsReloadFinished()
        {
            var (state, weapon) = Setup(phase: WeaponPhase.Reloading, reloadTime: 2.0f,
                elapsedTime: 2.5f, ammoInMag: 0);
            var eventBuffer = new RaidEventBuffer();
            var context = TestContextFactory.Create(events: eventBuffer);

            WeaponStateMachineSystem.Tick(state, in context);

            var finished = eventBuffer.All
                .Where(e => e.Type == RaidEventType.WeaponReloadFinished).ToList();
            Assert.AreEqual(1, finished.Count);
            Assert.AreEqual(weapon.PrefabId, finished[0].StringPayload);
        }

        [Test]
        public void Tick_ReloadingNotDone_StaysReloading()
        {
            var (state, weapon) = Setup(phase: WeaponPhase.Reloading, reloadTime: 2.0f,
                elapsedTime: 1.0f /* halfway */, ammoInMag: 0);
            var context = TestContextFactory.Create();

            WeaponStateMachineSystem.Tick(state, in context);

            Assert.AreEqual(WeaponPhase.Reloading, weapon.Phase);
            Assert.AreEqual(0, weapon.AmmoInMagazine, "Magazine should not fill until reload complete");
        }

        [Test]
        public void Tick_ReloadingWithSwapIntent_InterruptsToUnequipping()
        {
            var (state, weapon) = Setup(phase: WeaponPhase.Reloading, reloadTime: 2.0f,
                elapsedTime: 1.0f, ammoInMag: 0, pendingSlot: 1);
            var context = TestContextFactory.Create();

            WeaponStateMachineSystem.Tick(state, in context);

            Assert.AreEqual(WeaponPhase.Unequipping, weapon.Phase, "Swap should interrupt reload");
        }

        [Test]
        public void Tick_ReadyWithReloadPressed_StartsReloading()
        {
            var (state, weapon) = Setup(phase: WeaponPhase.Ready, ammoInMag: 10); // not full
            var input = new FakeInputAdapter { ReloadPressed = true };
            var eventBuffer = new RaidEventBuffer();
            var context = TestContextFactory.Create(input, events: eventBuffer);

            WeaponStateMachineSystem.Tick(state, in context);

            Assert.AreEqual(WeaponPhase.Reloading, weapon.Phase);
            var started = eventBuffer.All
                .Where(e => e.Type == RaidEventType.WeaponReloadStarted).ToList();
            Assert.AreEqual(1, started.Count);
        }

        [Test]
        public void Tick_ReadyWithReloadPressed_FullMag_StaysReady()
        {
            var (state, weapon) = Setup(phase: WeaponPhase.Ready, ammoInMag: 30); // full
            var input = new FakeInputAdapter { ReloadPressed = true };
            var context = TestContextFactory.Create(input);

            WeaponStateMachineSystem.Tick(state, in context);

            Assert.AreEqual(WeaponPhase.Ready, weapon.Phase);
        }

        [Test]
        public void Tick_ReadyWithReloadPressed_NoReserve_StaysReady()
        {
            var (state, weapon) = Setup(phase: WeaponPhase.Ready, ammoInMag: 10);
            // Clear all reserve ammo.
            for (int i = 0; i < InventoryState.BackpackSize; i++)
                App.Instance.Player.Inventory.Backpack[i] = null;
            var input = new FakeInputAdapter { ReloadPressed = true };
            var context = TestContextFactory.Create(input);

            WeaponStateMachineSystem.Tick(state, in context);

            Assert.AreEqual(WeaponPhase.Ready, weapon.Phase);
        }

        // ── Charging phase (Tier 2) ───────────────────────────

        [Test]
        public void Tick_ChargingWithAttackJustReleased_StaysChargingForShootingSystemFire()
        {
            // Tau-cannon mechanic (2026-05-06): release no longer cancels — it triggers
            // a charged-shot fire path handled by ShootingSystem. WeaponStateMachineSystem
            // leaves phase at Charging; ShootingSystem reads release input, fires, and
            // transitions to Firing/Cooldown.
            var (state, weapon) = Setup(phase: WeaponPhase.Charging, phaseStart: 0.5f,
                elapsedTime: 0.7f /* half-way through charge */);
            var laserSO = WeaponBuilderTestFactory.MakeLaser(chargeTime: 1.0f);
            try
            {
                weapon.PayloadDefinition = laserSO;
                weapon.ChargeStartTime = 0.5f;
                var events = new RaidEventBuffer();
                var context = TestContextFactory.Create(
                    new FakeInputAdapter { AttackJustReleased = true }, events);

                WeaponStateMachineSystem.Tick(state, in context);

                Assert.AreEqual(WeaponPhase.Charging, weapon.Phase,
                    "WSMS leaves charging intact — ShootingSystem handles fire-on-release");
                Assert.IsFalse(events.All.Any(e => e.Type == RaidEventType.WeaponChargeCancelled),
                    "No cancel event — release fires partial-charge shot instead");
            }
            finally { Object.DestroyImmediate(laserSO); }
        }

        [Test]
        public void Tick_ChargingWithAttackHeld_StaysInCharging()
        {
            var (state, weapon) = Setup(phase: WeaponPhase.Charging, elapsedTime: 0.3f);
            var laserSO = WeaponBuilderTestFactory.MakeLaser(chargeTime: 1.0f);
            try
            {
                weapon.PayloadDefinition = laserSO;
                weapon.ChargeStartTime = 0f;
                var input = new FakeInputAdapter { AttackPressed = true, AttackJustReleased = false };
                var context = TestContextFactory.Create(input);

                WeaponStateMachineSystem.Tick(state, in context);

                // State machine does NOT complete charge — ShootingSystem handles completion.
                Assert.AreEqual(WeaponPhase.Charging, weapon.Phase);
            }
            finally { Object.DestroyImmediate(laserSO); }
        }

        [Test]
        public void Tick_ChargingWithPendingSwap_CancelsChargeAndTransitionsToUnequipping()
        {
            var (state, weapon) = Setup(phase: WeaponPhase.Charging, unequipTime: 0.2f,
                elapsedTime: 0.3f, pendingSlot: 1);
            var laserSO = WeaponBuilderTestFactory.MakeLaser(chargeTime: 1.0f);
            try
            {
                weapon.PayloadDefinition = laserSO;
                var events = new RaidEventBuffer();
                var context = TestContextFactory.Create(new FakeInputAdapter(), events);

                WeaponStateMachineSystem.Tick(state, in context);

                Assert.AreEqual(WeaponPhase.Unequipping, weapon.Phase);
                Assert.IsTrue(events.All.Any(e => e.Type == RaidEventType.WeaponChargeCancelled));
                Assert.IsTrue(events.All.Any(e => e.Type == RaidEventType.WeaponUnequipStarted));
            }
            finally { Object.DestroyImmediate(laserSO); }
        }

        [Test]
        public void Tick_PlayerProgression_ReducesEquipTime()
        {
            var (state, weapon) = Setup(
                phase: WeaponPhase.Equipping, equipTime: 1f, elapsedTime: 0.81f);
            var progression = PlayerProgressionConfig.Default;
            progression.EquipTimeMultiplier = 0.8f;
            var context = TestContextFactory.Create(playerProgressionConfig: progression);

            WeaponStateMachineSystem.Tick(state, in context);

            Assert.AreEqual(WeaponPhase.Ready, weapon.Phase);
        }

        [Test]
        public void Tick_PlayerProgression_ReducesReloadTime()
        {
            var (state, weapon) = Setup(
                phase: WeaponPhase.Reloading, reloadTime: 1f, elapsedTime: 0.86f);
            var progression = PlayerProgressionConfig.Default;
            progression.ReloadTimeMultiplier = 0.85f;
            var context = TestContextFactory.Create(playerProgressionConfig: progression);

            WeaponStateMachineSystem.Tick(state, in context);

            Assert.AreEqual(WeaponPhase.Ready, weapon.Phase);
        }
    }
}
