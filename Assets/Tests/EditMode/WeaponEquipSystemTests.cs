using Systems;
using NUnit.Framework;
using State;
using Tests.EditMode.Fakes;
using UnityEngine;

namespace Tests.EditMode
{
    [TestFixture]
    public class WeaponEquipSystemTests
    {

        // System is intent-only — any pressed slot is echoed to PendingHotbarSlot
        // regardless of slot identity (current, other, empty) or prior pending value.
        [TestCase(0, Description = "current slot")]
        [TestCase(1, Description = "other hotbar slot")]
        [TestCase(5, Description = "empty slot beyond hotbar")]
        public void Tick_PressSlot_SetsPendingToInput(int pressed)
        {
            var state = EditModeTestsUtils.CreateStateWithPlayer(Vector3.zero);
            var input = new FakeInputAdapter { HotbarSlotPressed = pressed };
            var context = TestContextFactory.Create(input);

            WeaponEquipSystem.Tick(state, in context);

            Assert.AreEqual(pressed, state.PlayerEntity.PendingHotbarSlot);
        }

        [Test]
        public void Tick_PressSlot_OverwritesPending()
        {
            var state = EditModeTestsUtils.CreateStateWithPlayer(Vector3.zero);
            state.PlayerEntity.PendingHotbarSlot = 1;
            var input = new FakeInputAdapter { HotbarSlotPressed = 3 };
            var context = TestContextFactory.Create(input);

            WeaponEquipSystem.Tick(state, in context);

            Assert.AreEqual(3, state.PlayerEntity.PendingHotbarSlot);
        }

        [Test]
        public void Tick_NoSlotPressed_PendingUnchanged()
        {
            var state = EditModeTestsUtils.CreateStateWithPlayer(Vector3.zero);
            state.PlayerEntity.PendingHotbarSlot = -1;
            var input = new FakeInputAdapter { HotbarSlotPressed = -1 };
            var context = TestContextFactory.Create(input);

            WeaponEquipSystem.Tick(state, in context);

            Assert.AreEqual(-1, state.PlayerEntity.PendingHotbarSlot);
        }

        [Test]
        public void Tick_NullPlayer_DoesNotThrow()
        {
            var state = RaidState.Create(EditModeTestsUtils.NewAllocator());
            var input = new FakeInputAdapter { HotbarSlotPressed = 0 };
            var context = TestContextFactory.Create(input);

            Assert.DoesNotThrow(() => WeaponEquipSystem.Tick(state, in context));
        }

        [Test]
        public void Tick_DoesNotChangeEquippedWeapon()
        {
            var state = EditModeTestsUtils.CreateStateWithPlayer(Vector3.zero);
            var originalWeapon = state.PlayerEntity.EquippedWeapon;
            var input = new FakeInputAdapter { HotbarSlotPressed = 1 };
            var context = TestContextFactory.Create(input);

            WeaponEquipSystem.Tick(state, in context);

            Assert.AreSame(originalWeapon, state.PlayerEntity.EquippedWeapon,
                "EquipSystem should only set PendingHotbarSlot, not change EquippedWeapon");
            Assert.AreEqual(0, state.PlayerEntity.SelectedHotbarSlot,
                "EquipSystem should not change SelectedHotbarSlot");
        }
    }
}
