using System;
using System.Diagnostics;
using Dalamud.Game.ClientState.Conditions;
using Dalamud.Hooking;
using FFXIVClientStructs.FFXIV.Client.System.Input;
using FFXIVClientStructs.FFXIV.Client.UI;
using FFXIVClientStructs.FFXIV.Client.UI.Misc;

namespace TurboController;

/// <summary>
/// Actually injects the code that would modify the bits in ButtonsPressed struct in order
/// to repeat actions again based on TurboDecision and timing.
/// </summary>
internal sealed unsafe class CrossbarInjector : IDisposable
{
    /// <summary>
    /// The crossbar's per-frame binding check.
    /// </summary>
    private delegate byte CheckCrossbarBindingsDelegate(nint a1, nint a2, nint a3, nint a4);

    private delegate byte ExecuteSlotDelegate(RaptureHotbarModule* module, RaptureHotbarModule.HotbarSlot* slot);

    private const string CheckCrossbarBindingsSig = "E8 ?? ?? ?? ?? EB 20 E8 ?? ?? ?? ?? 84 C0";

    private readonly Configuration configuration;
    private readonly TurboDecision decision = new();
    private readonly ActionClassifier classifier = new();
    private readonly Stopwatch clock = Stopwatch.StartNew();

    private readonly Hook<CheckCrossbarBindingsDelegate>? checkCrossbarBindingsHook;
    private readonly Hook<ExecuteSlotDelegate>? executeSlotHook;

    private ushort currentButton;
    private bool insideCrossbarCheck;

    internal CrossbarInjector(Configuration configuration)
    {
        this.configuration = configuration;

        try
        {
            checkCrossbarBindingsHook = TurboControllerPlugin.GameInterop
                .HookFromSignature<CheckCrossbarBindingsDelegate>(CheckCrossbarBindingsSig, CheckCrossbarBindingsDetour);

            executeSlotHook = TurboControllerPlugin.GameInterop.HookFromAddress<ExecuteSlotDelegate>(
                (nint)RaptureHotbarModule.MemberFunctionPointers.ExecuteSlot,
                ExecuteSlotDetour);

            checkCrossbarBindingsHook.Enable();
            executeSlotHook.Enable();

            TurboControllerPlugin.HooksReady = true;
            TurboControllerPlugin.Log.Information("[TurboController] hooks initialised");
        }
        catch (Exception ex)
        {
            // Never throw out of here: a failed signature must not stop the
            // plugin loading, or the user cannot even uninstall it cleanly.
            TurboControllerPlugin.HooksReady = false;
            TurboControllerPlugin.Log.Error(ex, "[TurboController] hook setup failed; turbo is inactive");
        }
    }

    public void Dispose()
    {
        checkCrossbarBindingsHook?.Disable();
        checkCrossbarBindingsHook?.Dispose();
        executeSlotHook?.Disable();
        executeSlotHook?.Dispose();
        TurboControllerPlugin.HooksReady = false;
    }

    private byte CheckCrossbarBindingsDetour(nint a1, nint a2, nint a3, nint a4)
    {
        var original = GamepadButtonsFlags.None;
        InputData* input = null;

        try
        {
            var ui = UIInputData.Instance();
            if (ui != null)
            {
                // UIInputData : AtkInputData : InputData, so InputData is at offset 0.
                input = (InputData*)ui;
                original = input->GamepadInputs.ButtonsPressed;
                Inject(input);
            }
        }
        catch (Exception ex)
        {
            TurboControllerPlugin.Log.Error(ex, "[TurboController] injection failed");
        }

        insideCrossbarCheck = true;
        try
        {
            return checkCrossbarBindingsHook!.Original(a1, a2, a3, a4);
        }
        finally
        {
            // Load-bearing: a fake bit left set here would be visible to the
            // rest of the frame — menus, targeting, everything.
            if (input != null)
                input->GamepadInputs.ButtonsPressed = original;

            insideCrossbarCheck = false;
            currentButton = 0;
        }
    }

    private void Inject(InputData* input)
    {
        var settings = configuration.Settings;
        var now = clock.ElapsedMilliseconds;

        var held = (ushort)input->GamepadInputs.Buttons & TurboDecision.CrossbarButtonMask;
        var pressed = (ushort)input->GamepadInputs.ButtonsPressed & TurboDecision.CrossbarButtonMask;
        var inCombat = TurboControllerPlugin.Condition[ConditionFlag.InCombat];

        for (var i = 0; i < 8; i++)
        {
            var bit = (ushort)(1 << i);
            var isHeld = (held & bit) != 0;
            var isPressed = (pressed & bit) != 0;

            if (isPressed)
            {
                decision.OnGenuinePress(bit, now, settings);
                currentButton = bit;
            }
            else if (isHeld)
            {
                if (decision.ShouldRepeat(bit, now, settings, inCombat))
                {
                    input->GamepadInputs.ButtonsPressed |= (GamepadButtonsFlags)bit;
                    decision.OnRepeatFired(bit, now, settings);
                    currentButton = bit;
                }
            }
            else
            {
                decision.OnRelease(bit);
            }
        }
    }

    private byte ExecuteSlotDetour(RaptureHotbarModule* module, RaptureHotbarModule.HotbarSlot* slot)
    {
        try
        {
            if (insideCrossbarCheck && currentButton != 0 && slot != null)
            {
                var isAction = slot->ApparentSlotType == RaptureHotbarModule.HotbarSlotType.Action;
                var kind = classifier.Classify(slot->ApparentActionId, isAction);
                decision.OnLearn(currentButton, slot->ApparentActionId, kind);
            }
        }
        catch (Exception ex)
        {
            TurboControllerPlugin.Log.Error(ex, "[TurboController] learn failed");
        }

        return executeSlotHook!.Original(module, slot);
    }
}
