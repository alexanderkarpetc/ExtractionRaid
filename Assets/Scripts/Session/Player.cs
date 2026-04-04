using System.Collections.Generic;
using Save;
using State;

namespace Session
{
    public class Player
    {
        public PlayerProfileState ProfileState { get; private set; }
        public InventoryState Inventory { get; private set; }
        public List<ItemState> Stash { get; private set; }
        public QuestProgressState QuestProgress { get; private set; }

        public Player()
        {
            ProfileState = new PlayerProfileState();
            Inventory = new InventoryState();
            Stash = new List<ItemState>();
            QuestProgress = new QuestProgressState();
        }

        public SaveData ToSaveData()
        {
            var questList = new List<QuestProgressSaveData>();
            foreach (var p in QuestProgress.All.Values)
                questList.Add(QuestProgressSaveData.FromState(p));

            var stashData = new List<ItemSaveData>(Stash.Count);
            foreach (var item in Stash)
                stashData.Add(ItemSaveData.FromState(item));

            return new SaveData
            {
                PlayerName = ProfileState.PlayerName,
                Inventory = InventorySaveData.FromState(Inventory),
                Stash = stashData,
                Quests = questList
            };
        }

        public void LoadFrom(SaveData data)
        {
            if (data == null) return;

            ProfileState.PlayerName = data.PlayerName;
            data.Inventory?.ApplyTo(Inventory);

            Stash.Clear();
            if (data.Stash != null)
                foreach (var s in data.Stash)
                {
                    var item = s?.ToState();
                    if (item != null) Stash.Add(item);
                }

            var questStates = new List<QuestProgress>();
            if (data.Quests != null)
                foreach (var q in data.Quests)
                    questStates.Add(q.ToState());
            QuestProgress.RestoreFrom(questStates);
        }
    }
}
