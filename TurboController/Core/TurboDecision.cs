using System;
using System.Collections.Generic;

namespace TurboController;

/// <summary>
/// What kind of action a crossbar button currently resolves to.
/// </summary>
public enum ActionKind
{
    Unknown,
    Gcd,
    OGcd,
    // An item, macro, emote, mount, etc. GCD/oGCD filters do not apply. (Maybe we should add this as an option as well for turbo mode?)
    NonAction,
}

/// <summary>
/// Separate settings to modify how the turbo functionality works.
/// </summary>
public sealed class TurboSettings
{
    public bool Enabled = true;
    // How often it should repeat presses.
    public int IntervalMs = 250;
    // The amount of fluctuation, plus or minus the interval ms.
    public int JitterMs;
    // Initial delay before turbo starts.
    public int InitialDelayMs;
    public bool TurboGcds = true;
    public bool TurboOgcds = true;
    public bool TurboOutOfCombat;
    public bool TurboWeaponDrawn;
}

/// <summary>
/// Checks the state and based on settings, decides whether we run turbo or not on the hotkey.
/// Settings are global, but timing and learned-action <em>state</em> is tracked per button, so
/// holding two buttons at once gives each its own clock and its own learned action.
/// </summary>
public sealed class TurboDecision
{
    // The default minimal interval to use if:
    // Interval - Jitter < MinIntervalMs
    public const int MinIntervalMs = 50;

    /// <summary>
    /// The eight crossbar buttons are exactly bits 0-7 of GamepadButtonsFlags, in this
    /// order: DPadUp, DPadLeft, DPadDown, DPadRight, North, East, South, West. Face-button
    /// labels depend on the controller layout, so the flags are named by direction, not by
    /// Triangle/Circle/Cross/Square.
    /// </summary>
    public const ushort CrossbarButtonMask = 0x00FF;

    /// <summary>
    /// When a button is pressed, the state is saved to determine how we handle turbo.
    /// </summary>
    private sealed class ButtonState
    {
        // When the button is actually pressed and when it's released
        public bool Running;
        // When this was last "pressed" (either by the first press or turbo)
        public long LastFireMs;
        // Delay before running a repeat of the button
        public int RepeatDelay;
        // This gets rewritten but will determine what time of action it is in case we turbo or not
        public ActionKind Kind = ActionKind.Unknown;
    }

    private readonly Dictionary<ushort, ButtonState> states = new();
    private readonly Func<int, int, int> randomRange;

    /// <param name="randomRange">
    /// Inclusive-low, exclusive-high random source, matching Random.Next(int, int).
    /// Injectable so tests can pin jitter.
    /// </param>
    public TurboDecision(Func<int, int, int>? randomRange = null)
    {
        this.randomRange = randomRange ?? new Random().Next;
    }

    private ButtonState StateFor(ushort bit)
    {
        if (!states.TryGetValue(bit, out var state))
            states[bit] = state = new ButtonState();
        return state;
    }

    /// <summary>
    /// Lookup that does not create state. Every read path uses this so that merely
    /// asking about a button cannot start tracking it.
    /// </summary>
    private ButtonState? PeekState(ushort bit) =>
        states.TryGetValue(bit, out var state) ? state : null;

    /// <summary>
    /// The player physically pressed the button. This starts the turbo clock —
    /// without it nothing ever repeats, which guarantees turbo can never fire
    /// for a button the player never pressed.
    /// </summary>
    public void OnGenuinePress(ushort bit, long nowMs, TurboSettings settings)
    {
        var state = StateFor(bit);
        state.Running = true;
        state.LastFireMs = nowMs;
        state.RepeatDelay = Math.Max(
            MinIntervalMs,
            settings.InitialDelayMs > 0 ? settings.InitialDelayMs : settings.IntervalMs);
    }

    /// <summary>
    /// Records an injected repeat and schedules the next one, with jitter.
    /// </summary>
    public void OnRepeatFired(ushort bit, long nowMs, TurboSettings settings)
    {
        var state = StateFor(bit);
        state.LastFireMs = nowMs;

        var jitter = settings.JitterMs > 0
            ? randomRange(-settings.JitterMs, settings.JitterMs + 1)
            : 0;

        state.RepeatDelay = Math.Max(MinIntervalMs, settings.IntervalMs + jitter);
    }

    /// <summary>
    /// Button is released so we reset state and no longer run turbo (whether it is actually running or not).
    /// </summary>
    public void OnRelease(ushort bit)
    {
        var state = PeekState(bit);
        if (state == null) return;

        state.Running = false;
        state.Kind = ActionKind.Unknown;
    }

    /// <summary>
    /// Records what the game actually executed for this button.
    /// </summary>
    public void OnLearn(ushort bit, uint actionId, ActionKind kind)
    {
        var state = StateFor(bit);
        state.Kind = kind;
    }

    /// <summary>
    /// Helper function to check what kind of action our state is at.
    /// </summary>
    public ActionKind KindOf(ushort bit) => PeekState(bit)?.Kind ?? ActionKind.Unknown;

    /// <summary>
    /// Whether an injected repeat is due for this button right now.
    /// </summary>
    public bool ShouldRepeat(ushort bit, long nowMs, TurboSettings settings, bool inCombat, bool weaponDrawn)
    {
        if (!settings.Enabled) return false;

        var state = PeekState(bit);
        if (state == null || !state.Running) return false;
        if (nowMs - state.LastFireMs < state.RepeatDelay) return false;

        var combatGateOpen = settings.TurboOutOfCombat
            || inCombat
            || (settings.TurboWeaponDrawn && weaponDrawn);
        if (!combatGateOpen) return false;

        return state.Kind switch
        {
            ActionKind.Gcd => settings.TurboGcds,
            ActionKind.OGcd => settings.TurboOgcds,
            _ => true, // Unknown and NonAction always pass, (TODO) maybe add a setting for NonActions later...
        };
    }
}
