namespace NavySimulator.Web.Services;

/// <summary>A HOI4 install or mod folder that can serve as a defines source.</summary>
public sealed record DefinesCandidate(string Name, string Path, DefinesCandidateKind Kind)
{
    public string KindLabel => Kind switch
    {
        DefinesCandidateKind.Game => "game",
        DefinesCandidateKind.LocalMod => "local mod",
        _ => "workshop"
    };
}
