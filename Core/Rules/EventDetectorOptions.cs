namespace Core.Rules;

public sealed class EventDetectorOptions
{
    public TimeSpan DeathCooldown { get; init; } = TimeSpan.FromSeconds(1);
    public TimeSpan CombatCooldown { get; init; } = TimeSpan.FromSeconds(5);
    public TimeSpan CombatDebounce { get; init; } = TimeSpan.FromMilliseconds(250);
    public TimeSpan IdleCooldown { get; init; } = TimeSpan.FromSeconds(10);
    public TimeSpan IdleDebounce { get; init; } = TimeSpan.FromSeconds(5);
    public float MovementThreshold { get; init; } = 0.01f;
}
