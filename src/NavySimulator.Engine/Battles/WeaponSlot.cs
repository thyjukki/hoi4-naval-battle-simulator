namespace NavySimulator.Engine.Battles;

/// <summary>
/// Weapon slots in the order a ship processes them each hour (game gun index; also the BASE_GUN_COOLDOWNS index).
/// The light slot also fires depth charges when its target is a submarine.
/// </summary>
public enum WeaponSlot
{
    Heavy = 0,
    Torpedo = 1,
    Light = 2
}
