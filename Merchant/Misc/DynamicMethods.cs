using System.Reflection;
using System.Reflection.Emit;
using StardewValley;
using StardewValley.Minigames;

internal static class DynamicMethods
{
    internal const string _currentMinigame = "_currentMinigame";
    internal static Action<IMinigame?> Set_Game1_currentMinigame = null!;

    internal static void Make()
    {
        if (
            typeof(Game1).GetField(_currentMinigame, BindingFlags.Static | BindingFlags.NonPublic)
            is not FieldInfo minigameField
        )
        {
            throw new NullReferenceException($"Failed to get '{_currentMinigame}' field info");
        }
        DynamicMethod dm = new(nameof(Set_Game1_currentMinigame), null, [typeof(IMinigame)]);
        ILGenerator gen = dm.GetILGenerator();
        gen.Emit(OpCodes.Ldarg_0);
        gen.Emit(OpCodes.Stsfld, minigameField);
        gen.Emit(OpCodes.Ret);
        Set_Game1_currentMinigame = dm.CreateDelegate<Action<IMinigame?>>();
    }
}
