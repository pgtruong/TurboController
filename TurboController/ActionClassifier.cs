using System.Collections.Generic;
using Lumina.Excel;

namespace TurboController;

/// <summary>
/// Maps an action id to a <see cref="ActionKind"/>. Actions sharing the global
/// cooldown are in cooldown group 58; everything else will be considered an off-GCD ability. (except non actions)
/// </summary>
internal sealed class ActionClassifier
{
    private const byte GlobalCooldownGroup = 58;

    private readonly Dictionary<uint, ActionKind> cache = [];

    /// <summary>
    /// Resolved once at load. Classification runs inside a hook detour on the game
    /// thread, so the sheet must not be looked up there.
    /// </summary>
    private readonly ExcelSheet<Lumina.Excel.Sheets.Action>? sheet =
        TurboControllerPlugin.DataManager.GetExcelSheet<Lumina.Excel.Sheets.Action>();

    /// <summary>
    /// This will classify the action type and also cache it since we'll most likely be
    /// identifying the action multiple times as we repeat.
    /// </summary>
    public ActionKind Classify(uint actionId, bool isActionSlot)
    {
        // Base cases: a slot holding something that is not an action (item, macro, mount)
        // is never a GCD or an oGCD, and an empty slot has nothing to classify.
        if (!isActionSlot) return ActionKind.NonAction;
        if (actionId == 0) return ActionKind.Unknown;

        // We have this cache so we don't have to do more look ups in the data sheet each time.
        if (cache.TryGetValue(actionId, out var cached))
            return cached;

        var kind = ActionKind.Unknown;

        if (sheet != null && sheet.TryGetRow(actionId, out var row))
            kind = row.CooldownGroup == GlobalCooldownGroup ? ActionKind.Gcd : ActionKind.OGcd;

        cache[actionId] = kind;
        return kind;
    }
}
