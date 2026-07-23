namespace State
{
    public class PlayerProfileState
    {
        public string PlayerName;
        public int Level = 1;
        // Soft currency for shop transactions (ShopSystem.TryBuy/TrySell). Persisted
        // via SaveData.Credits; ShopSystem and the inventory UI mutate this directly.
        // Non-zero starting value is a placeholder until quest rewards / loot drops
        // wire in — keeps shops testable out of the box on a fresh profile.
        public int Credits = 1000;
    }
}
