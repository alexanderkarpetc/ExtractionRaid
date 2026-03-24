using State;

namespace Session
{
    public class Player
    {
        public PlayerProfileState ProfileState { get; private set; }
        public InventoryState Inventory { get; private set; }
        public InventoryState Stash { get; private set; }

        public Player()
        {
            ProfileState = new PlayerProfileState();
            Inventory = new InventoryState();
            Stash = new InventoryState();
        }
    }
}
