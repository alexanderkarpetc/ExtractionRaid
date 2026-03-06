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
        public void Tick_PressSelectedSlotWithWeapon_DeselectsAndUnequips()
        {
            var state = EditModeTestsUtils.CreateStateWithPlayer(Vector3.zero);
            var input = new FakeInputAdapter { HotbarSlotPressed = 0 };
            var context = CreateContext(input);

            WeaponEquipSystem.Tick(state, in context);

            Assert.AreEqual(-1, state.PlayerEntity.SelectedHotbarSlot);
            Assert.IsNull(state.PlayerEntity.EquippedWeapon);
            Assert.IsNotNull(state.PlayerEntity.Hotbar[0]);
        }

        [Test]
        public void Tick_PressSelectedSlotTwice_ReequipsWeapon()
        {
            var state = EditModeTestsUtils.CreateStateWithPlayer(Vector3.zero);
            var weaponId = state.PlayerEntity.EquippedWeapon.Id;
            var input = new FakeInputAdapter { HotbarSlotPressed = 0 };
            var context = CreateContext(input);

            WeaponEquipSystem.Tick(state, in context);
            WeaponEquipSystem.Tick(state, in context);

            Assert.AreEqual(0, state.PlayerEntity.SelectedHotbarSlot);
            Assert.IsNotNull(state.PlayerEntity.EquippedWeapon);
            Assert.AreEqual(weaponId, state.PlayerEntity.EquippedWeapon.Id);
        }

        [Test]
        public void Tick_NoSlotPressed_NoChange()
        {
            var state = EditModeTestsUtils.CreateStateWithPlayer(Vector3.zero);
            var input = new FakeInputAdapter { HotbarSlotPressed = -1 };
            var context = CreateContext(input);

            WeaponEquipSystem.Tick(state, in context);

            Assert.AreEqual(0, state.PlayerEntity.SelectedHotbarSlot);
            Assert.IsNotNull(state.PlayerEntity.EquippedWeapon);
        }

        [Test]
        public void Tick_PressEmptySlot_SelectsSlotAndUnequips()
        {
            var state = EditModeTestsUtils.CreateStateWithPlayer(Vector3.zero);
            var input = new FakeInputAdapter { HotbarSlotPressed = 4 };
            var context = CreateContext(input);

            WeaponEquipSystem.Tick(state, in context);

            Assert.AreEqual(4, state.PlayerEntity.SelectedHotbarSlot);
            Assert.IsNull(state.PlayerEntity.EquippedWeapon);
        }

        [Test]
        public void Tick_FromEmptySlot_PressWeaponSlot_Equips()
        {
            var state = EditModeTestsUtils.CreateStateWithPlayer(Vector3.zero);
            var weaponId = state.PlayerEntity.Hotbar[0].Id;

            state.PlayerEntity.SelectedHotbarSlot = 4;
            state.PlayerEntity.EquippedWeapon = null;

            var input = new FakeInputAdapter { HotbarSlotPressed = 0 };
            var context = CreateContext(input);

            WeaponEquipSystem.Tick(state, in context);

            Assert.AreEqual(0, state.PlayerEntity.SelectedHotbarSlot);
            Assert.IsNotNull(state.PlayerEntity.EquippedWeapon);
            Assert.AreEqual(weaponId, state.PlayerEntity.EquippedWeapon.Id);
        }

        [Test]
        public void Tick_PressSelectedEmptySlot_Deselects()
        {
            var state = EditModeTestsUtils.CreateStateWithPlayer(Vector3.zero);
            state.PlayerEntity.SelectedHotbarSlot = 3;
            state.PlayerEntity.EquippedWeapon = null;

            var input = new FakeInputAdapter { HotbarSlotPressed = 3 };
            var context = CreateContext(input);

            WeaponEquipSystem.Tick(state, in context);

            Assert.AreEqual(-1, state.PlayerEntity.SelectedHotbarSlot);
            Assert.IsNull(state.PlayerEntity.EquippedWeapon);
        }

        [Test]
        public void Tick_NoPlayer_DoesNotThrow()
        {
            var state = RaidState.Create();
            var input = new FakeInputAdapter { HotbarSlotPressed = 0 };
            var context = CreateContext(input);

            Assert.DoesNotThrow(() => WeaponEquipSystem.Tick(state, in context));
        }

        [Test]
        public void Tick_Deselect_WeaponRemainsInHotbarSlot()
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
