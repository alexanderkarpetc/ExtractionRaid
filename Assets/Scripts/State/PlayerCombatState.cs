namespace State
{
    public class PlayerCombatState
    {
        public float LastFireTime;

        public static PlayerCombatState Create()
        {
            return new PlayerCombatState
            {
                LastFireTime = -999f,
            };
        }
    }
}
