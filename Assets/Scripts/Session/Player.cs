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

        // Persistent per-building upgrade level. Missing key = level 0 (unbuilt).
        // BuildingSystem mutates this on a successful upgrade; reads default to 0
        // via GetBuildingLevel so we never have to seed the dictionary upfront.
        public Dictionary<BuildingKind, int> BuildingLevels { get; private set; }

        public Player()
        {
            ProfileState = new PlayerProfileState();
            Inventory = new InventoryState();
            Stash = new List<ItemState>();
            QuestProgress = new QuestProgressState();
            BuildingLevels = new Dictionary<BuildingKind, int>();
        }

        public int GetBuildingLevel(BuildingKind kind) =>
            BuildingLevels.TryGetValue(kind, out var lv) ? lv : 0;

        public void SetBuildingLevel(BuildingKind kind, int level)
        {
            BuildingLevels[kind] = level;
        }

        public SaveData ToSaveData()
        {
            var questList = new List<QuestProgressSaveData>();
            foreach (var p in QuestProgress.All.Values)
                questList.Add(QuestProgressSaveData.FromState(p));

            var stashData = new List<ItemSaveData>(Stash.Count);
            foreach (var item in Stash)
                stashData.Add(ItemSaveData.FromState(item));

            var buildingList = new List<BuildingLevelSaveData>(BuildingLevels.Count);
            foreach (var kvp in BuildingLevels)
                buildingList.Add(new BuildingLevelSaveData { Kind = kvp.Key.ToString(), Level = kvp.Value });

            return new SaveData
            {
                PlayerName = ProfileState.PlayerName,
                Inventory = InventorySaveData.FromState(Inventory),
                Stash = stashData,
                Quests = questList,
                BuildingLevels = buildingList,
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

            BuildingLevels.Clear();
            if (data.BuildingLevels != null)
                foreach (var b in data.BuildingLevels)
                    if (System.Enum.TryParse<BuildingKind>(b.Kind, out var kind))
                        BuildingLevels[kind] = b.Level;
        }
    }
}
