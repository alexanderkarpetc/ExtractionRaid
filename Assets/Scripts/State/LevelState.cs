namespace State
{
    public class LevelState
    {
        public string LevelId;

        public static LevelState Create(string levelId)
        {
            return new LevelState { LevelId = levelId };
        }
    }
}
