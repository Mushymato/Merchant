using System.Diagnostics.CodeAnalysis;
using StardewValley;
using StardewValley.Delegates;
using StardewValley.Extensions;

namespace Merchant.Models;

public enum CustomerDialogueKind
{
    Haggle_Ask = 0,
    Haggle_Compromise = 1,
    Haggle_Overpriced = 2,
    Haggle_Success = 3,
    Haggle_Fail = 4,
}

public abstract class BaseCustomerData
{
    public abstract bool IsTourist();

    // Will Shop
    public string? Condition { get; set; } = null;
    public float Chance { get; set; } = 1.0f;
    public string? OverrideAppearanceId { get; set; } = null;

    // Haggle Dialogue
    public Dictionary<string, CustomerDialogue> Dialogue = [];
    internal List<string>[]? MergedDialogues => field ??= CustomerDialogue.GetMergedDialogues(Dialogue);

    public virtual bool WillComeToShop(GameStateQueryContext context)
    {
        if (Condition == null)
            return true;
        return GameStateQuery.CheckConditions(Condition, context);
    }
}

public sealed class CustomerDialogue
{
    public string? Haggle_Ask { get; set; } = null;
    public string? Haggle_Compromise { get; set; } = null;
    public string? Haggle_Overpriced { get; set; } = null;
    public string? Haggle_Success { get; set; } = null;
    public string? Haggle_Fail { get; set; } = null;

    internal static List<string>[] GetMergedDialogues(Dictionary<string, CustomerDialogue> dialogueRaw)
    {
        // merge
        List<string>[] merged =
        [
            [],
            [],
            [],
            [],
            [],
        ];
        foreach (CustomerDialogue dialogue in dialogueRaw.Values)
        {
            if (dialogue.Haggle_Ask != null)
                merged[(int)CustomerDialogueKind.Haggle_Ask].Add(dialogue.Haggle_Ask);
            if (dialogue.Haggle_Compromise != null)
                merged[(int)CustomerDialogueKind.Haggle_Compromise].Add(dialogue.Haggle_Compromise);
            if (dialogue.Haggle_Overpriced != null)
                merged[(int)CustomerDialogueKind.Haggle_Overpriced].Add(dialogue.Haggle_Overpriced);
            if (dialogue.Haggle_Success != null)
                merged[(int)CustomerDialogueKind.Haggle_Success].Add(dialogue.Haggle_Success);
            if (dialogue.Haggle_Fail != null)
                merged[(int)CustomerDialogueKind.Haggle_Fail].Add(dialogue.Haggle_Fail);
        }
        return merged;
    }

    internal static bool TryGetDialogueText(
        List<string>[]? MergedDialogues,
        CustomerDialogueKind kind,
        [NotNullWhen(true)] out string? dialogueText
    )
    {
        dialogueText = null;
        if (MergedDialogues == null || (int)kind >= MergedDialogues.Length)
            return false;
        dialogueText = Random.Shared.ChooseFrom(MergedDialogues[(int)kind]);
        return dialogueText != null;
    }
}

public sealed class CustomerData : BaseCustomerData
{
    public override bool IsTourist() => false;
}
