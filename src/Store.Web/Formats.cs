using System.Globalization;

namespace Store.Web;

public static class Formats
{
    /// <summary>
    /// Prices in pounds, e.g. £4.99. Built from the invariant culture so it works even when the
    /// container runs .NET in globalization-invariant mode (no en-GB culture data).
    /// </summary>
    public static readonly NumberFormatInfo Gbp = CreateGbp();

    private static NumberFormatInfo CreateGbp()
    {
        var format = (NumberFormatInfo)CultureInfo.InvariantCulture.NumberFormat.Clone();
        format.CurrencySymbol = "£";
        format.CurrencyPositivePattern = 0; // £n
        format.CurrencyNegativePattern = 1; // -£n
        return NumberFormatInfo.ReadOnly(format);
    }
}
