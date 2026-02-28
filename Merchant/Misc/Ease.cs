namespace Merchant.Misc;

internal static class Ease
{
    internal static float InQuad(float x) => MathF.Pow(x, 2);

    internal static float InOutQuad(float x) => x < 0.5 ? 2 * MathF.Pow(x, 2) : 1 - MathF.Pow(-2 * x + 2, 2) / 2;
}
