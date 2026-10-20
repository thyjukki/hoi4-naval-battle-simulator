namespace NavySimulator.Engine.Battles.Replay;

/// <summary>A-ACE: a wing's ace in a logged game hour: alive or not, and its template's <c>chance</c> (which tells the template apart).</summary>
public sealed record ReplayAce(bool Alive, double? TemplateChance);
