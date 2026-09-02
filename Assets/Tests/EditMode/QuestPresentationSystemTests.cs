using System.Collections.Generic;
using NUnit.Framework;
using Quests;
using Session;
using State;
using Systems;
using UnityEngine;

namespace Tests.EditMode
{
    [TestFixture]
    public class QuestPresentationSystemTests
    {
        readonly List<QuestDefinition> _quests = new();
        QuestDatabase _database;
        Player _player;

        [SetUp]
        public void SetUp()
        {
            _database = ScriptableObject.CreateInstance<QuestDatabase>();
            _player = new Player();
        }

        [TearDown]
        public void TearDown()
        {
            for (int i = 0; i < _quests.Count; i++)
                Object.DestroyImmediate(_quests[i]);
            Object.DestroyImmediate(_database);
        }

        [Test]
        public void NpcAttention_RequiresFullHandoverAmount()
        {
            AddHandoverQuest("key", required: 2);
            _player.Inventory.Backpack[0] = ItemState.Create(new EId(1), "TestKey", 1);

            Assert.AreEqual(QuestSystem.NpcQuestAttention.None, ResolveAttention());

            _player.Stash.Add(ItemState.Create(new EId(2), "TestKey", 1));

            Assert.AreEqual(QuestSystem.NpcQuestAttention.Ready, ResolveAttention());
        }

        [Test]
        public void NpcAttention_IsReadyWhenAllTasksAreComplete()
        {
            var quest = AddHandoverQuest("complete", required: 1);
            _player.QuestProgress.GetProgress(quest.Id).Tasks[0].CurrentCount = 1;

            Assert.AreEqual(QuestSystem.NpcQuestAttention.Ready, ResolveAttention());
        }

        [Test]
        public void NpcAttention_AvailableQuestKeepsOfferPriority()
        {
            AddHandoverQuest("active", required: 1);
            AddQuest("offer", start: false);

            Assert.AreEqual(QuestSystem.NpcQuestAttention.Available, ResolveAttention());
        }

        [Test]
        public void UpgradeRequirements_CombineBackpackAndStashCounts()
        {
            _player.Inventory.Backpack[0] =
                ItemState.Create(new EId(1), "Mechanical_Parts", 2);
            _player.Stash.Add(ItemState.Create(new EId(2), "Mechanical_Parts", 3));
            _player.Stash.Add(ItemState.Create(new EId(3), "Gunpowder", 8));

            var requirements = BuildingSystem.GetNextUpgradeRequirements(
                _player, BuildingKind.WeaponBuilder);

            Assert.AreEqual(3, requirements.Count);
            Assert.AreEqual("Mechanical_Parts", requirements[0].ItemId);
            Assert.AreEqual(5, requirements[0].Available);
            Assert.AreEqual(10, requirements[0].Required);
            Assert.IsFalse(requirements[0].IsMet);
            Assert.IsTrue(requirements[1].IsMet);
        }

        QuestDefinition AddHandoverQuest(string id, int required)
        {
            var quest = AddQuest(id, start: false);
            quest.Tasks.Add(new FindItemTask
            {
                ItemId = "TestKey",
                RequiredCount = required,
            });
            _player.QuestProgress.StartQuest(quest.Id, quest.Tasks.Count);
            RebuildDatabase();
            return quest;
        }

        QuestDefinition AddQuest(string id, bool start)
        {
            var quest = ScriptableObject.CreateInstance<QuestDefinition>();
            quest.Id = id;
            quest.NpcId = "AL";
            _quests.Add(quest);
            if (start)
                _player.QuestProgress.StartQuest(quest.Id, quest.Tasks.Count);
            RebuildDatabase();
            return quest;
        }

        void RebuildDatabase()
        {
            var entries = new List<QuestDatabaseEntry>(_quests.Count);
            for (int i = 0; i < _quests.Count; i++)
                entries.Add(new QuestDatabaseEntry { Quest = _quests[i] });
            _database.SetEntries(entries);
        }

        QuestSystem.NpcQuestAttention ResolveAttention() =>
            QuestSystem.GetNpcQuestAttention(
                _player.QuestProgress, _database, 1, "AL",
                _player.Inventory, _player.Stash);
    }
}
