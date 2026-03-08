using System.Reflection;
using StardewValley.Minigames;

namespace Merchant.Management;

public class MinigameProxy : DispatchProxy
{
    internal ShopkeepGame Target = null!;

    protected override object? Invoke(MethodInfo? targetMethod, object?[]? args)
    {
        if (targetMethod == null)
            return null;

        var paramTypes = targetMethod.GetParameters().Select(p => p.ParameterType).ToArray();
        var method = typeof(ShopkeepGame).GetMethod(targetMethod.Name, paramTypes);

        if (method == null)
        {
            // Return default for unknown methods (e.g. GetForcedScaleFactor on mobile)
            if (targetMethod.ReturnType == typeof(float))
                return 1f;
            if (targetMethod.ReturnType == typeof(bool))
                return false;
            if (targetMethod.ReturnType == typeof(string))
                return "";
            return null;
        }

        return method.Invoke(Target, args);
    }

    internal static IMinigame Create(ShopkeepGame game)
    {
        var proxy = DispatchProxy.Create<IMinigame, MinigameProxy>();
        ((MinigameProxy)(object)proxy).Target = game;
        return proxy;
    }
}
