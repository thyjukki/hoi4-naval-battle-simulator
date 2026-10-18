namespace NavySimulator.Web.Services;

public enum DefinesCandidateKind
{
    /// <summary>The HOI4 install itself.</summary>
    Game,

    /// <summary>A mod in the launcher's mod folder (Documents/Paradox Interactive/Hearts of Iron IV/mod), e.g. a local checkout.</summary>
    LocalMod,

    /// <summary>A Steam workshop mod.</summary>
    WorkshopMod
}
