namespace Domain.ValueObjects;

public record VolumetricWeight
{
    public decimal LengthCm { get; init; }
    public decimal WidthCm { get; init; }
    public decimal HeightCm { get; init; }
    public decimal DimFactor { get; init; }

    public VolumetricWeight(decimal lengthCm, decimal widthCm, decimal heightCm, decimal dimFactor = 5000)
    {
        if (lengthCm <= 0 || widthCm <= 0 || heightCm <= 0)
        {
            throw new ArgumentException("Dimensions must be greater than zero.");
        }
        if (dimFactor <= 0)
        {
            throw new ArgumentException("Volumetric factor must be positive.");
        }
        LengthCm = lengthCm;
        WidthCm = widthCm;
        HeightCm = heightCm;
        DimFactor = dimFactor;
    }

    public decimal WeightKg => Math.Round((LengthCm * WidthCm * HeightCm) / DimFactor, 2);

    public decimal VolumeCubicMeters => Math.Round((LengthCm * WidthCm * HeightCm) / 1000000m, 3);
}
