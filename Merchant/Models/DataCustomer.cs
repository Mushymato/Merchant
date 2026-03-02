using System.Diagnostics.CodeAnalysis;
using Microsoft.Xna.Framework;
using StardewValley.Extensions;
using StardewValley.GameData.Characters;

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

    internal List<string>[]? MergedDialogues
    {
        get
        {
            if (field != null)
                return field;
            // merge
            field =
            [
                [],
                [],
                [],
                [],
                [],
            ];
            foreach (CustomerDialogue dialogue in Dialogue.Values)
            {
                if (dialogue.Haggle_Ask != null)
                    field[(int)CustomerDialogueKind.Haggle_Ask].Add(dialogue.Haggle_Ask);
                if (dialogue.Haggle_Compromise != null)
                    field[(int)CustomerDialogueKind.Haggle_Compromise].Add(dialogue.Haggle_Compromise);
                if (dialogue.Haggle_Overpriced != null)
                    field[(int)CustomerDialogueKind.Haggle_Overpriced].Add(dialogue.Haggle_Overpriced);
                if (dialogue.Haggle_Success != null)
                    field[(int)CustomerDialogueKind.Haggle_Success].Add(dialogue.Haggle_Success);
                if (dialogue.Haggle_Fail != null)
                    field[(int)CustomerDialogueKind.Haggle_Fail].Add(dialogue.Haggle_Fail);
            }
            return field;
        }
    }

    internal bool TryGetDialogueText(CustomerDialogueKind kind, [NotNullWhen(true)] out string? dialogueText)
    {
        dialogueText = null;
        if (MergedDialogues == null || (int)kind >= MergedDialogues.Length)
            return false;
        dialogueText = Random.Shared.ChooseFrom(MergedDialogues[(int)kind]);
        return dialogueText != null;
    }
}

public sealed class CustomerDialogue
{
    public string? Haggle_Ask { get; set; } = null;
    public string? Haggle_Compromise { get; set; } = null;
    public string? Haggle_Overpriced { get; set; } = null;
    public string? Haggle_Success { get; set; } = null;
    public string? Haggle_Fail { get; set; } = null;
}

public sealed class CustomerData : BaseCustomerData
{
    public override bool IsTourist() => false;
}

public sealed class TouristData : BaseCustomerData
{
    public override bool IsTourist() => true;

    public List<string>? RequiredContextTags { get; set; } = null;

    public string? DisplayName { get; set; } = null;
    public string? Portrait { get; set; } = null;
    public string? Sprite { get; set; } = null;
    public Point Size { get; set; } = new Point(16, 32);
    public Rectangle? MugShotSourceRect { get; set; } = null;
}
