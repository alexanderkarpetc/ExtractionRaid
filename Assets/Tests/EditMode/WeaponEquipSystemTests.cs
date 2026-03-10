using Systems;
using NUnit.Framework;
using Session;
using State;
using Tests.EditMode.Fakes;
using UnityEngine;

namespace Tests.EditMode
{
    [TestFixture]
    public class WeaponEquipSystemTests
    {
        static RaidContext CreateContext(FakeInputAdapter input, float deltaTime = 1f / 60f)
        {
            return new RaidContext(
                deltaTime: deltaTime,
                events: new FakeRaidEvents(),
                time: new FakeTimeAdapter { DeltaTime = deltaTime },
                input: input,
                navMesh: new FakeNavMeshAdapter()
            );
        }

        [Test]
        public void Tick_PressSlot_SetsPendingHotbarSlot()
        {
            var state = EditModeTestsUtils.CreateStateWithPlayer(Vector3.zero);
            var input = new FakeInputAdapter { HotbarSlotPressed = 1 };
            var context = CreateContext(input);

            WeaponEquipSystem.Tick(state, in context);

            Assert.AreEqual(1, state.PlayerEntity.PendingHotbarSlot);
        }

        [Test]
        public void Tick_PressCurrentSlot_SetsPendingToSameSlot()
        {
            var state = EditModeTestsUtils.CreateStateWithPlayer(Vector3.zero);
            var input = new FakeInputAdapter { HotbarSlotPressed = 0 };
            var context = CreateContext(input);

            WeaponEquipSystem.Tick(state, in context);

            Assert.AreEqual(0, state.PlayerEntity.PendingHotbarSlot);
        }

        [Test]
        public void Tick_NoSlotPressed_PendingUnchanged()
        {
            var state = EditModeTestsUtils.CreateStateWithPlayer(Vector3.zero);
            state.PlayerEntity.PendingHotbarSlot = -1;
            var input = new FakeInputAdapter { HotbarSlotPressed = -1 };
            var context = CreateContext(input);

            WeaponEquipSystem.Tick(state, in context);

            Assert.AreEqual(-1, state.PlayerEntity.PendingHotbarSlot);
        }

        [Test]
        public void Tick_NullPlayer_DoesNotThrow()
        {
            var state = RaidState.Create();
            var input = new FakeInputAdapter { HotbarSlotPressed = 0 };
            var context = CreateContext(input);

            Assert.DoesNotThrow(() => WeaponEquipSystem.Tick(state, in context));
        }

        [Test]
        public void Tick_PressEmptySlot_SetsPending()
        {
            var state = EditModeTestsUtils.CreateStateWithPlayer(Vector3.zero);
            var input = new FakeInputAdapter { HotbarSlotPressed = 5 };
            var context = CreateContext(input);

            WeaponEquipSystem.Tick(state, in context);

            Assert.AreEqual(5, state.PlayerEntity.PendingHotbarSlot);
        }

        [Test]
        public void Tick_AlreadyHasPending_Overwrites()
        {
            var state = EditModeTestsUtils.CreateStateWithPlayer(Vector3.zero);
            state.PlayerEntity.PendingHotbarSlot = 1;
            var input = new FakeInputAdapter { HotbarSlotPressed = 3 };
            var context = CreateContext(input);

            WeaponEquipSystem.Tick(state, in context);

            Assert.AreEqual(3, state.PlayerEntity.PendingHotbarSlot);
        }

        [Test]
        public void Tick_DoesNotChangeEquippedWeapon()
        {
            var state = EditModeTestsUtils.CreateStateWithPlayer(Vector3.zero);
            var originalWeapon = state.PlayerEntity.EquippedWeapon;
            var input = new FakeInputAdapter { HotbarSlotPressed = 1 };
            var context = CreateContext(input);

            WeaponEquipSystem.Tick(state, in context);

            Assert.AreSame(originalWeapon, state.PlayerEntity.EquippedWeapon,
                "EquipSystem should only set PendingHotbarSlot, not change EquippedWeapon");
            Assert.AreEqual(0, state.PlayerEntity.SelectedHotbarSlot,
                "EquipSystem should not change SelectedHotbarSlot");
        }

        [Test]
        public void Tick_WeaponRemainsInHotbarSlot()
        {
            var state = EditModeTestsUtils.CreateStateWithPlayer(Vector3.zero);
            var weaponId = state.PlayerEntity.Hotbar[0].Id;
            var input = new FakeInputAdapter { HotbarSlotPressed = 0 };
            var context = CreateContext(input);

            WeaponEquipSystem.Tick(state, in context);

            Assert.IsNotNull(state.PlayerEntity.Hotbar[0]);
            Assert.AreEqual(weaponId, state.PlayerEntity.Hotbar[0].Id);
        }
    }
}
