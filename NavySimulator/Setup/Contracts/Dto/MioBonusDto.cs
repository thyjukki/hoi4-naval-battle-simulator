namespace NavySimulator.Setup.Contracts;

public class MioBonusDto
{
    public string ID { get; set; } = string.Empty;
    public List<MioModifierDto> Modifiers { get; set; } = [];
}


