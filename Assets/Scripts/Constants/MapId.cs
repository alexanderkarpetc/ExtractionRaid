namespace Constants
{
    public enum MapId
    {
        None = 0,
        Hideout = 1,
        TestMap = 2,
    }

    public static class MapIds
    {
        public const string HideoutLevelId = "hideout";
        public const string TestMapLevelId = "main_map";

        public static string ToLevelId(MapId map)
        {
            switch (map)
            {
                case MapId.Hideout: return HideoutLevelId;
                case MapId.TestMap: return TestMapLevelId;
                default: return null;
            }
        }

        public static MapId FromLevelId(string levelId)
        {
            if (string.IsNullOrEmpty(levelId)) return MapId.None;
            if (levelId == HideoutLevelId) return MapId.Hideout;
            if (levelId == TestMapLevelId) return MapId.TestMap;
            return MapId.None;
        }
    }
}
