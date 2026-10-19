using NavySimulator.Engine;
using NavySimulator.Engine.Defines;
using NavySimulator.GameData.Contracts;
using NavySimulator.Web.Services;

namespace NavySimulator.Web.Tests;

public class CarrierLoadoutsTests
{
    [Fact]
    public void AddWing_PadsTheCarriersBefore_AndRemovingTheLastWingDropsTheEmptyLoadouts()
    {
        var fleet = Fleet(("cv", 3));

        CarrierLoadouts.AddWing(fleet, "cv", 2, "bomber");

        Assert.Equal(3, fleet.CarrierAirwings["cv"].Count);
        Assert.Null(CarrierLoadouts.Find(fleet, "cv", 5));
        Assert.Equal(10, CarrierLoadouts.Planes(CarrierLoadouts.Find(fleet, "cv", 2)));

        CarrierLoadouts.RemoveWing(fleet, "cv", 2, fleet.CarrierAirwings["cv"][2].Wings[0]);

        Assert.False(fleet.CarrierAirwings.ContainsKey("cv"));
    }

    [Fact]
    public void CopyToAll_GivesEveryCarrierOfTheFleetItsOwnCopy()
    {
        var fleet = Fleet(("cv", 2), ("cvl", 1));
        CarrierLoadouts.AddWing(fleet, "cv", 0, "fighter");
        CarrierLoadouts.AddWing(fleet, "cv", 0, "bomber");
        fleet.CarrierAirwings["cv"][0].Wings[1].Airwings = 3;

        CarrierLoadouts.CopyToAll(fleet, "cv", 0, ["cv", "cvl"]);

        var loadouts = fleet.CarrierAirwings["cv"].Concat(fleet.CarrierAirwings["cvl"]).ToList();
        Assert.Equal(3, loadouts.Count);
        Assert.All(loadouts, loadout => Assert.Equal(["fighter", "bomber"], loadout.Wings.Select(wing => wing.PlaneID)));
        Assert.All(loadouts, loadout => Assert.Equal(40, CarrierLoadouts.Planes(loadout)));

        loadouts[1].Wings[0].PlaneCount = 5;
        Assert.Equal(10, CarrierLoadouts.PlanesPerWing(loadouts[0].Wings[0]));
    }

    [Fact]
    public void Trim_DropsLoadoutsBeyondTheShipCount()
    {
        var fleet = Fleet(("cv", 3));
        CarrierLoadouts.CopyToAll(fleet, "cv", 0, ["cv"]);
        CarrierLoadouts.AddWing(fleet, "cv", 0, "bomber");
        CarrierLoadouts.CopyToAll(fleet, "cv", 0, ["cv"]);

        fleet.ShipDesigns["cv"] = 1;
        CarrierLoadouts.Trim(fleet, "cv");

        Assert.Single(fleet.CarrierAirwings["cv"]);
    }

    [Fact]
    public void Role_FollowsThePlanesTypesAndMissions()
    {
        var values = new HardcodedValues();

        Assert.Equal("strike", CarrierLoadouts.Role(new PlaneEquipment("nb", new PlaneStats(), ["naval_bomber"], ["naval_bomber"]), values));
        Assert.Equal("fighter", CarrierLoadouts.Role(new PlaneEquipment("f", new PlaneStats(), ["fighter"], ["interception"]), values));
        // TFB CAS: a fighter by its equipment type, but the fighter test reads the sub-unit, { cas strategic_bomber }.
        Assert.Equal("strike", CarrierLoadouts.Role(new PlaneEquipment("cas", new PlaneStats(), ["cas", "fighter"], ["naval_bomber"], true, "cv_cas", ["cas", "strategic_bomber"]), values));
        Assert.Equal("strike + fighter", CarrierLoadouts.Role(new PlaneEquipment("hf", new PlaneStats(), ["fighter"], ["naval_bomber"], false, "hf", ["heavy_fighter"]), values));
    }

    private static FleetDto Fleet(params (string Design, int Count)[] designs)
    {
        return new FleetDto { ID = "f", ShipDesigns = designs.ToDictionary(entry => entry.Design, entry => entry.Count) };
    }
}
