namespace App
{
    public enum LaunchMode
    {
        Menu,
        Raid,
        TestScenario,
        Hideout,
    }

    public struct LaunchOptions
    {
        public LaunchMode Mode;
        public string LevelId;

        public static LaunchOptions DefaultRaid(string levelId = "test_level")
        {
            return new LaunchOptions
            {
                Mode = LaunchMode.Raid,
                LevelId = levelId,
            };
        }

        public static LaunchOptions Menu()
        {
            return new LaunchOptions
            {
                Mode = LaunchMode.Menu,
                LevelId = null,
            };
        }

        public static LaunchOptions Hideout()
        {
            return new LaunchOptions
            {
                Mode = LaunchMode.Hideout,
                LevelId = null,
            };
        }
    }
}
