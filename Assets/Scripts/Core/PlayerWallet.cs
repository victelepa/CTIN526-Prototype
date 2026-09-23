using UnityEngine;

namespace RaceSabotage
{
    public class PlayerWallet : MonoBehaviour
    {
        public int Coins { get; private set; }

        public void Add(int amount) => Coins = Mathf.Max(0, Coins + amount);

        public bool TrySpend(int amount)
        {
            if (amount < 0 || Coins < amount) return false;
            Coins -= amount;
            return true;
        }
    }
}
