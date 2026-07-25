using Domain.Common;

namespace Domain.Entities;

public class CargoPiece : BaseEntity
{
    public Guid CargoItemId { get; set; }
    public string PieceNumber { get; set; } = string.Empty;
    public decimal ActualWeightKg { get; set; }
    public decimal LengthCm { get; set; }
    public decimal WidthCm { get; set; }
    public decimal HeightCm { get; set; }
    public string WarehouseLocation { get; set; } = string.Empty;
    public string Condition { get; set; } = "Received in good condition";
    public DateTime ReceivedAt { get; set; }
}
