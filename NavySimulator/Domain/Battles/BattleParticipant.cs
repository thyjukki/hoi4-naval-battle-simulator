namespace NavySimulator.Domain;

public class BattleParticipant
{
    public Fleet Fleet;
    public string Commander;
    public string Doctrine;
    public string TechnologyLevel;
    public string NationModifier;

    public BattleParticipant(
        Fleet fleet,
        string commander,
        string doctrine,
        string technologyLevel,
        string nationModifier)
    {
        Fleet = fleet;
        Commander = commander;
        Doctrine = doctrine;
        TechnologyLevel = technologyLevel;
        NationModifier = nationModifier;
    }
}


