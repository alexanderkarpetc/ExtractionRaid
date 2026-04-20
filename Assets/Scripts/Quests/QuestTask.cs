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

    /// <summary>
    /// Enemy categories selectable in quest tasks. Values map to
    /// <see cref="Constants.BotConstants"/> TypeIds via <see cref="EnemyTypeExtensions"/>.
    /// </summary>
    public enum EnemyType
    {
        Any = 0,
        Scav = 1,   // low-tier "Scav" — civilian/local scavenger
        PMC = 2,     // mid-tier human operator
        BossA = 3,
    }

    public static class EnemyTypeExtensions
    {
        /// <summary>
        /// Returns the BotTypeConfig TypeId string matching this enemy type,
        /// or null for <see cref="EnemyType.Any"/>.
        /// </summary>
        public static string ToBotTypeId(this EnemyType type)
        {
            switch (type)
            {
                case EnemyType.Scav: return "Scav";
                case EnemyType.PMC:   return "PMC";
                case EnemyType.BossA:  return "Boss";
                default:              return null;
            }
        }

        /// <summary>
        /// True if the given bot TypeId satisfies a kill task targeting this enemy type.
        /// </summary>
        public static bool Matches(this EnemyType type, string botTypeId)
        {
            if (type == EnemyType.Any) return true;
            return string.Equals(type.ToBotTypeId(), botTypeId);
        }
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
        public EnemyType EnemyType = EnemyType.Any;
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
