using NUnit.Framework;
using State;
using Systems;
using UnityEngine;

namespace Tests.EditMode
{
    public class WorkbenchInteractionTests
    {
        [Test]
        public void ZoneState_TracksPlayerEnteringLeavingAndRemoval()
        {
            var state = RaidState.Create(EditModeTestsUtils.NewAllocator());
            var workbench = WorkbenchState.Create(state.AllocateEId(), Vector3.zero);
            state.Workbenches.Add(workbench);
            state.PlayerEntity = new PlayerEntityState { Position = Vector3.forward };

            LootSystem.UpdateWorkbenchInteractionZones(state);
            Assert.IsTrue(workbench.IsPlayerInInteractionZone);

            state.PlayerEntity.Position = Vector3.back;
            LootSystem.UpdateWorkbenchInteractionZones(state);
            Assert.IsFalse(workbench.IsPlayerInInteractionZone);

            state.PlayerEntity.Position = Vector3.forward;
            LootSystem.UpdateWorkbenchInteractionZones(state);
            Assert.IsTrue(workbench.IsPlayerInInteractionZone);
            state.PlayerEntity = null;
            LootSystem.UpdateWorkbenchInteractionZones(state);
            Assert.IsFalse(workbench.IsPlayerInInteractionZone);
        }

        [TestCase(0f, 0f, 1f, true)]
        [TestCase(1f, 1.5f, 2f, true)]
        [TestCase(1.001f, 0f, 1f, false)]
        [TestCase(0f, 1.501f, 1f, false)]
        [TestCase(0f, 0f, 2.001f, false)]
        [TestCase(0f, 0f, -0.01f, false)]
        public void Selection_RespectsBoxIncludingBoundaries(float x, float y, float z, bool available)
        {
            var state = RaidState.Create(EditModeTestsUtils.NewAllocator());
            var workbench = WorkbenchState.Create(state.AllocateEId(), Vector3.zero);
            state.Workbenches.Add(workbench);

            var result = LootSystem.FindNearestInteractable(state, new Vector3(x, y, z), Vector3.forward);

            Assert.AreEqual(available ? InteractableType.Workbench : InteractableType.None, result.Type);
            if (available) Assert.AreEqual(workbench.Id, result.Id);
        }

        [Test]
        public void Selection_UsesOffsetAndRotationInsteadOfOldRadius()
        {
            var state = RaidState.Create(EditModeTestsUtils.NewAllocator());
            var workbench = WorkbenchState.Create(state.AllocateEId(), new Vector3(10f, 0f, 10f));
            workbench.InteractionRotation = Quaternion.Euler(0f, 90f, 0f);
            workbench.InteractionCenter = workbench.Position + Vector3.right * 5f;
            workbench.InteractionSize = new Vector3(1f, 3f, 4f);
            state.Workbenches.Add(workbench);

            Assert.AreEqual(InteractableType.Workbench, LootSystem.FindNearestInteractable(
                state, workbench.InteractionCenter + Vector3.right, Vector3.forward).Type);
            Assert.AreEqual(InteractableType.None, LootSystem.FindNearestInteractable(
                state, workbench.InteractionCenter + Vector3.forward, Vector3.forward).Type);
            Assert.AreEqual(InteractableType.None, LootSystem.FindNearestInteractable(
                state, workbench.Position, Vector3.forward).Type);
        }

        [TestCase(0f)]
        [TestCase(-1f)]
        public void InvalidSize_DisablesInteraction(float width)
        {
            var workbench = WorkbenchState.Create(EId.None, Vector3.zero);
            workbench.InteractionSize.x = width;
            Assert.IsFalse(LootSystem.IsInsideWorkbenchZone(workbench, workbench.InteractionCenter));
        }
    }
}
