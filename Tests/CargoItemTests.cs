using Domain.Entities;
using Domain.Enums;
using Xunit;

namespace Tests;

public class CargoItemTests
{
    [Fact]
    public void UsesTheGreaterOfActualAndVolumetricWeight()
    {
        var cargo = new CargoItem(Guid.NewGuid(), Guid.NewGuid(), "TRK-1", "Test cargo",
            5, 100, 50, 40, 100, "GBP", "General", "NG");

        Assert.Equal(40, cargo.VolumetricWeightKg);
        Assert.Equal(40, cargo.BillableWeightKg);
    }

    [Fact]
    public void CannotAssignCargoThatHasAlreadyLeftTheWarehouse()
    {
        var cargo = new CargoItem(Guid.NewGuid(), Guid.NewGuid(), "TRK-2", "Test cargo",
            5, 10, 10, 10, 100, "GBP", "General", "NG");
        cargo.UpdateStatus(CargoStatus.InTransit);

        Assert.Throws<InvalidOperationException>(() => cargo.AssignToBatch(Guid.NewGuid()));
    }
}
