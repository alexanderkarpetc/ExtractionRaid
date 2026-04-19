using System;
using Constants;
using UnityEngine;

namespace Quests
{
    public enum QuestTaskType
    {
        FindAndTransfer,
        KillEnemy,
        FindPlace,
        ProvideSupply,
        Extract,
        Craft,
        FindItem
    }

    [Serializable]
    public abstract class QuestTask
    {
        public abstract QuestTaskType TaskType { get; }
        public string Description;
        public int RequiredCount = 1;
        public bool InOneRaid;
    }

    [Serializable]
    public class FindAndTransferTask : QuestTask
    {
        public override QuestTaskType TaskType => QuestTaskType.FindAndTransfer;
        public string QuestItemId;
    }

    [Serializable]
    public class KillEnemyTask : QuestTask
    {
        public override QuestTaskType TaskType => QuestTaskType.KillEnemy;
        public string EnemyTypeId;
        public bool HeadshotsOnly;
    }

    [Serializable]
    public class FindPlaceTask : QuestTask
    {
        public override QuestTaskType TaskType => QuestTaskType.FindPlace;
        public string PlaceId;
    }

    [Serializable]
    public class ProvideSupplyTask : QuestTask
    {
        public override QuestTaskType TaskType => QuestTaskType.ProvideSupply;
        public string ItemId;
    }

    [Serializable]
    public class ExtractTask : QuestTask
    {
        public override QuestTaskType TaskType => QuestTaskType.Extract;
        public string LevelId;
    }

    [Serializable]
    public class CraftTask : QuestTask
    {
        public override QuestTaskType TaskType => QuestTaskType.Craft;
        public string ItemId;
    }

    [Serializable]
    public class FindItemTask : QuestTask
    {
        public override QuestTaskType TaskType => QuestTaskType.FindItem;
        public string ItemId;
        public Vector3 Coordinates;
        public MapId Map;
    }
}
