using NavySimulator.GameData.Contracts;

namespace NavySimulator.GameData;

/// <summary>S-MIO-COUNTRY: which MIOs a country can have, from the country tags of their <c>allowed</c> triggers.</summary>
public static class MioCountries
{
    /// <summary>
    /// S-MIO-COUNTRY [PDX]: an MIO is available to a country when its <c>allowed</c> names the country's tag, or names no
    /// tag outside a <c>NOT</c> and does not exclude it (the generic MIOs of countries without their own). Archetypes and
    /// debug-mode MIOs are available to none. Other conditions (<c>has_dlc</c>, focuses, scripted triggers) are not
    /// checked. With no country every MIO is.
    /// </summary>
    public static bool IsAvailable(MioDto mio, string? country)
    {
        if (string.IsNullOrEmpty(country))
        {
            return true;
        }

        if (mio.ArchetypeOnly || mio.DebugOnly)
        {
            return false;
        }

        return (mio.Countries ?? []).Count > 0
            ? mio.Countries!.Contains(country, StringComparer.Ordinal)
            : !(mio.ExcludedCountries ?? []).Contains(country, StringComparer.Ordinal);
    }
}
