using State;

namespace Session
{
    public class Player
    {
        public PlayerProfileState ProfileState { get; private set; }

        public Player()
        {
            ProfileState = new PlayerProfileState();
        }
    }
}
