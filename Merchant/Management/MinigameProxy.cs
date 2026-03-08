using System.Reflection;
using StardewModdingAPI.Utilities;
using StardewValley.Minigames;

namespace Merchant.Management;

public class MinigameProxy : DispatchProxy
{
    internal ShopkeepGame Target = null!;

    private static readonly Dictionary<MethodInfo, MethodInfo?> cachedMethodInfo = [];
    private static readonly PerScreen<IMinigame> proxyInstance = new(Create<IMinigame, MinigameProxy>);

    protected override object? Invoke(MethodInfo? targetMethod, object?[]? args)
    {
        if (targetMethod == null)
            return null;

        if (!cachedMethodInfo.TryGetValue(targetMethod, out MethodInfo? method))
        {
            Type[] paramTypes = targetMethod.GetParameters().Select(p => p.ParameterType).ToArray();
            method = typeof(ShopkeepGame).GetMethod(targetMethod.Name, paramTypes);
            cachedMethodInfo[targetMethod] = method;
        }

        if (method == null)
        {
            ModEntry.LogOnce($"Unknown method {targetMethod}");
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

    internal static IMinigame GetProxy(ShopkeepGame game)
    {
        IMinigame proxy = proxyInstance.Value;
        (proxy as MinigameProxy)!.Target = game;
        return proxy;
    }
}
