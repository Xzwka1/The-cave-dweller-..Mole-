namespace CaveDweller.Combat
{
    public interface IDamageable
    {
        int CurrentHealth { get; }
        int MaxHealth { get; }
        void TakeDamage(int amount);
        bool IsDead { get; }
    }
}
