namespace NavySimulator.Engine.Doctrines;

/// <summary>A subdoctrine reward; <see cref="Mastery"/> null takes <c>NDoctrines.DEFAULT_REWARD_MASTERY</c> (S-DOCTRINE-MASTERY).</summary>
public sealed class DoctrineReward(string id, double? mastery, CountryEffects effects)
{
    public string ID { get; } = id;
    public double? Mastery { get; } = mastery;
    public CountryEffects Effects { get; } = effects;
}
