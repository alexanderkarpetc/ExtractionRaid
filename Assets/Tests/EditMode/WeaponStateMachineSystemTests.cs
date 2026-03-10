using System.Linq;
using Adapters;
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
        static RaidContext CreateContext(IRaidEvents events = null, float deltaTime = 1f / 60f)
        {
            return new RaidContext(
                deltaTime: deltaTime,
                events: events ?? new RaidEventBuffer(),
                time: new FakeTimeAdapter { DeltaTime = deltaTime },
                input: new FakeInputAdapter(),
                navMesh: new FakeNavMeshAdapter()
            );
        }

        // ── Firing → Cooldown ─────────────────────────────────────

        [Test]
        public void Tick_FiringPhase_TransitionsToCooldown()
        {
            var state = EditModeTestsUtils.CreateStateWithPlayer(Vector3.zero);
            var weapon = state.PlayerEntity.EquippedWeapon;
            weapon.Phase = WeaponPhase.Firing;
            weapon.PhaseStartTime = 1f;
            state.ElapsedTime = 1f;
            var context = CreateContext();

            WeaponStateMachineSystem.Tick(state, in context);

            Assert.AreEqual(WeaponPhase.Cooldown, weapon.Phase);
            Assert.AreEqual(state.ElapsedTime, weapon.PhaseStartTime, 0.001f);
        }

        // ── Cooldown → Ready ──────────────────────────────────────

        [Test]
        public void Tick_CooldownExpired_TransitionsToReady()
        {
            var state = EditModeTestsUtils.CreateStateWithPlayer(Vector3.zero);
            var weapon = state.PlayerEntity.EquippedWeapon;
            weapon.Phase = WeaponPhase.Cooldown;
            weapon.PhaseStartTime = 0f;
            weapon.FireInterval = 0.2f;
            state.ElapsedTime = 0.3f;
            var context = CreateContext();

            WeaponStateMachineSystem.Tick(state, in context);

            Assert.AreEqual(WeaponPhase.Ready, weapon.Phase);
        }

        [Test]
        public void Tick_CooldownNotExpired_StaysInCooldown()
        {
            var state = EditModeTestsUtils.CreateStateWithPlayer(Vector3.zero);
            var weapon = state.PlayerEntity.EquippedWeapon;
            weapon.Phase = WeaponPhase.Cooldown;
            weapon.PhaseStartTime = 0f;
            weapon.FireInterval = 0.2f;
            state.ElapsedTime = 0.1f;
            var context = CreateContext();

            WeaponStateMachineSystem.Tick(state, in context);

            Assert.AreEqual(WeaponPhase.Cooldown, weapon.Phase);
        }

        // ── Equipping → Ready ─────────────────────────────────────

        [Test]
        public void Tick_EquippingDone_TransitionsToReady()
        {
            var state = EditModeTestsUtils.CreateStateWithPlayer(Vector3.zero);
            var weapon = state.PlayerEntity.EquippedWeapon;
            weapon.Phase = WeaponPhase.Equipping;
            weapon.PhaseStartTime = 0f;
            weapon.EquipTime = 0.3f;
            state.ElapsedTime = 0.4f;
            var context = CreateContext();

            WeaponStateMachineSystem.Tick(state, in context);

            Assert.AreEqual(WeaponPhase.Ready, weapon.Phase);
        }

        [Test]
        public void Tick_EquippingDone_EmitsWeaponEquipFinished()
        {
            var state = EditModeTestsUtils.CreateStateWithPlayer(Vector3.zero);
            var weapon = state.PlayerEntity.EquippedWeapon;
            weapon.Phase = WeaponPhase.Equipping;
            weapon.PhaseStartTime = 0f;
            weapon.EquipTime = 0.3f;
            state.ElapsedTime = 0.4f;
            var eventBuffer = new RaidEventBuffer();
            var context = CreateContext(events: eventBuffer);

            WeaponStateMachineSystem.Tick(state, in context);

            var finished = eventBuffer.All
                .Where(e => e.Type == RaidEventType.WeaponEquipFinished).ToList();
            Assert.AreEqual(1, finished.Count);
            Assert.AreEqual(weapon.PrefabId, finished[0].StringPayload);
        }

        [Test]
        public void Tick_EquippingNotDone_StaysEquipping()
        {
            var state = EditModeTestsUtils.CreateStateWithPlayer(Vector3.zero);
            var weapon = state.PlayerEntity.EquippedWeapon;
            weapon.Phase = WeaponPhase.Equipping;
            weapon.PhaseStartTime = 0f;
            weapon.EquipTime = 0.3f;
            state.ElapsedTime = 0.1f;
            var context = CreateContext();

            WeaponStateMachineSystem.Tick(state, in context);

            Assert.AreEqual(WeaponPhase.Equipping, weapon.Phase);
        }

        // ── Unequipping → toggle off / switch ─────────────────────

        [Test]
        public void Tick_UnequippingDone_ToggleOff_GoesUnarmed()
        {
            var state = EditModeTestsUtils.CreateStateWithPlayer(Vector3.zero);
            var weapon = state.PlayerEntity.EquippedWeapon;
            weapon.Phase = WeaponPhase.Unequipping;
            weapon.PhaseStartTime = 0f;
            weapon.UnequipTime = 0.2f;
            state.ElapsedTime = 0.3f;
            // PendingSlot == SelectedSlot → toggle off
            state.PlayerEntity.PendingHotbarSlot = 0;
            state.PlayerEntity.SelectedHotbarSlot = 0;
            var context = CreateContext();

            WeaponStateMachineSystem.Tick(state, in context);

            Assert.IsNull(state.PlayerEntity.EquippedWeapon);
            Assert.AreEqual(-1, state.PlayerEntity.SelectedHotbarSlot);
            Assert.AreEqual(-1, state.PlayerEntity.PendingHotbarSlot);
        }

        [Test]
        public void Tick_UnequippingDone_SwitchToNewWeapon_StartsEquipping()
        {
            var state = EditModeTestsUtils.CreateStateWithPlayer(Vector3.zero);
            var weapon = state.PlayerEntity.EquippedWeapon;
            weapon.Phase = WeaponPhase.Unequipping;
            weapon.PhaseStartTime = 0f;
            weapon.UnequipTime = 0.2f;
            state.ElapsedTime = 0.3f;
            // Switch from slot 0 to slot 1
            state.PlayerEntity.SelectedHotbarSlot = 0;
            state.PlayerEntity.PendingHotbarSlot = 1;
            var eventBuffer = new RaidEventBuffer();
            var context = CreateContext(events: eventBuffer);

            WeaponStateMachineSystem.Tick(state, in context);

            Assert.IsNotNull(state.PlayerEntity.EquippedWeapon);
            Assert.AreEqual("Weapon_Shotgun", state.PlayerEntity.EquippedWeapon.PrefabId);
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
            var state = EditModeTestsUtils.CreateStateWithPlayer(Vector3.zero);
            var weapon = state.PlayerEntity.EquippedWeapon;
            weapon.Phase = WeaponPhase.Unequipping;
            weapon.PhaseStartTime = 0f;
            weapon.UnequipTime = 0.2f;
            state.ElapsedTime = 0.3f;
            // Switch to empty slot 5
            state.PlayerEntity.SelectedHotbarSlot = 0;
            state.PlayerEntity.PendingHotbarSlot = 5;
            var context = CreateContext();

            WeaponStateMachineSystem.Tick(state, in context);

            Assert.IsNull(state.PlayerEntity.EquippedWeapon);
            Assert.AreEqual(5, state.PlayerEntity.SelectedHotbarSlot);
        }

        // ── Swap intent triggers ──────────────────────────────────

        [Test]
        public void Tick_ReadyWithPendingSwap_StartsUnequipping()
        {
            var state = EditModeTestsUtils.CreateStateWithPlayer(Vector3.zero);
            var weapon = state.PlayerEntity.EquippedWeapon;
            weapon.Phase = WeaponPhase.Ready;
            state.PlayerEntity.PendingHotbarSlot = 1;
            var eventBuffer = new RaidEventBuffer();
            var context = CreateContext(events: eventBuffer);

            WeaponStateMachineSystem.Tick(state, in context);

            Assert.AreEqual(WeaponPhase.Unequipping, weapon.Phase);
            var unequipEvents = eventBuffer.All
                .Where(e => e.Type == RaidEventType.WeaponUnequipStarted).ToList();
            Assert.AreEqual(1, unequipEvents.Count);
        }

        [Test]
        public void Tick_CooldownWithPendingSwap_StartsUnequipping()
        {
            var state = EditModeTestsUtils.CreateStateWithPlayer(Vector3.zero);
            var weapon = state.PlayerEntity.EquippedWeapon;
            weapon.Phase = WeaponPhase.Cooldown;
            weapon.PhaseStartTime = 0f;
            weapon.FireInterval = 0.2f;
            state.ElapsedTime = 0.05f; // still in cooldown
            state.PlayerEntity.PendingHotbarSlot = 1;
            var context = CreateContext();

            WeaponStateMachineSystem.Tick(state, in context);

            Assert.AreEqual(WeaponPhase.Unequipping, weapon.Phase,
                "Swap should interrupt cooldown");
        }

        [Test]
        public void Tick_EquippingWithPendingSwap_StartsUnequipping()
        {
            var state = EditModeTestsUtils.CreateStateWithPlayer(Vector3.zero);
            var weapon = state.PlayerEntity.EquippedWeapon;
            weapon.Phase = WeaponPhase.Equipping;
            weapon.PhaseStartTime = 0f;
            weapon.EquipTime = 0.3f;
            state.ElapsedTime = 0.1f; // still equipping
            state.PlayerEntity.PendingHotbarSlot = 1;
            var context = CreateContext();

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
            var context = CreateContext(events: eventBuffer);

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
            var context = CreateContext();

            WeaponStateMachineSystem.Tick(state, in context);

            Assert.IsNull(state.PlayerEntity.EquippedWeapon);
            Assert.AreEqual(-1, state.PlayerEntity.SelectedHotbarSlot);
        }

        // ── Guard checks ──────────────────────────────────────────

        [Test]
        public void Tick_NullPlayer_DoesNotThrow()
        {
            var state = RaidState.Create();
            var context = CreateContext();

            Assert.DoesNotThrow(() => WeaponStateMachineSystem.Tick(state, in context));
        }

        [Test]
        public void Tick_ReadyNoPending_StaysReady()
        {
            var state = EditModeTestsUtils.CreateStateWithPlayer(Vector3.zero);
            var weapon = state.PlayerEntity.EquippedWeapon;
            weapon.Phase = WeaponPhase.Ready;
            state.PlayerEntity.PendingHotbarSlot = -1;
            var context = CreateContext();

            WeaponStateMachineSystem.Tick(state, in context);

            Assert.AreEqual(WeaponPhase.Ready, weapon.Phase);
        }

        [Test]
        public void Tick_UnequippingNotDone_StaysUnequipping()
        {
            var state = EditModeTestsUtils.CreateStateWithPlayer(Vector3.zero);
            var weapon = state.PlayerEntity.EquippedWeapon;
            weapon.Phase = WeaponPhase.Unequipping;
            weapon.PhaseStartTime = 0f;
            weapon.UnequipTime = 0.2f;
            state.ElapsedTime = 0.1f;
            state.PlayerEntity.PendingHotbarSlot = 1;
            var context = CreateContext();

            WeaponStateMachineSystem.Tick(state, in context);

            Assert.AreEqual(WeaponPhase.Unequipping, weapon.Phase);
        }
    }
}
