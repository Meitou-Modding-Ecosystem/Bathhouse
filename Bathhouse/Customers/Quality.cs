namespace Bathhouse.Customers;

/// <summary>
/// A visit's quality and its parts (docs/bathhouse.md "Quality and payment"): 1 less what lukewarm water, waiting, a soak cut short by cold, a
/// dirty tub and bathing without a towel or without soap took off, never below 0. Decor, robes and staff skill join in later milestones.
/// </summary>
public readonly record struct Quality(float Heat, float Wait, float Cold, float Dirt = 0, float NoTowel = 0, float NoSoap = 0)
{
    /// <summary>
    /// What bathing without a towel takes off.
    /// </summary>
    public const float TowelWeight = 0.1f;

    /// <summary>
    /// What bathing without soap takes off.
    /// </summary>
    public const float SoapWeight = 0.1f;

    /// <summary>
    /// The most lukewarm water takes off (a soak all lukewarm).
    /// </summary>
    public const float HeatWeight = 0.5f;

    /// <summary>
    /// What each game hour of waiting at the counter takes off.
    /// </summary>
    public const float WaitPerHour = 0.15f;

    /// <summary>
    /// The most waiting takes off.
    /// </summary>
    public const float MostWait = 0.3f;

    /// <summary>
    /// What a soak cut short by cold water takes off.
    /// </summary>
    public const float ColdWeight = 0.3f;

    /// <summary>
    /// What a tub nobody scrubbed since its last bather takes off.
    /// </summary>
    public const float DirtWeight = 0.2f;

    /// <summary>
    /// The quality at or above which a customer pays the full price; under it, the price scaled by quality over this.
    /// </summary>
    public const float FullPay = 0.8f;

    /// <summary>
    /// The score: 1 less each part, from 0 to 1.
    /// </summary>
    public float Total => Math.Clamp(1 - Heat - Wait - Cold - Dirt - NoTowel - NoSoap, 0, 1);

    /// <summary>
    /// The parts of a visit that soaked <paramref name="soaked"/> hours, <paramref name="lukewarm"/> of them lukewarm, after waiting
    /// <paramref name="waited"/> hours, whether cold water ended it, whether its tub was dirty and whether it got a towel and soap.
    /// </summary>
    public static Quality Of(double soaked, double lukewarm, double waited, bool cold, bool dirty = false, bool towel = true, bool soap = true)
    {
        float heat = soaked > 0 ? HeatWeight * (float)Math.Clamp(lukewarm / soaked, 0, 1) : 0;
        float wait = Math.Min(MostWait, WaitPerHour * (float)Math.Max(0, waited));
        return new Quality(heat, wait, cold ? ColdWeight : 0, dirty ? DirtWeight : 0, towel ? 0 : TowelWeight, soap ? 0 : SoapWeight);
    }

    /// <summary>
    /// What the customer pays of <paramref name="price"/>: all of it at <see cref="FullPay"/> or better, less below.
    /// </summary>
    public long Pay(long price) => (long)Math.Round(price * Math.Min(1, Total / FullPay));

    /// <summary>
    /// The parts as the debug row and the payment message show them.
    /// </summary>
    public override string ToString() =>
        $"{Total:P0} (heat -{Heat:P0}, wait -{Wait:P0}, cold -{Cold:P0}, dirty tub -{Dirt:P0}, no towel -{NoTowel:P0}, no soap -{NoSoap:P0})";
}
