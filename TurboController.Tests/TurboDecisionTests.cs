using TurboController;
using Xunit;

namespace TurboController.Tests;

public class TurboDecisionTests
{
    private const ushort Cross = 1 << 5;

    private static TurboSettings Settings() => new()
    {
        Enabled = true,
        IntervalMs = 250,
        JitterMs = 0,
        InitialDelayMs = 0,
        TurboGcds = true,
        TurboOgcds = true,
        TurboOutOfCombat = false,
    };

    // No jitter, so timing assertions are exact.
    private static TurboDecision Decision() => new((lo, hi) => 0);

    [Fact]
    public void DoesNotRepeatWithoutAGenuinePress()
    {
        var d = Decision();
        Assert.False(d.ShouldRepeat(Cross, 100_000, Settings(), inCombat: true));
    }

    [Fact]
    public void DoesNotRepeatBeforeTheIntervalElapses()
    {
        var d = Decision();
        d.OnGenuinePress(Cross, 1000, Settings());
        Assert.False(d.ShouldRepeat(Cross, 1249, Settings(), inCombat: true));
    }

    [Fact]
    public void RepeatsOnceTheIntervalElapses()
    {
        var d = Decision();
        d.OnGenuinePress(Cross, 1000, Settings());
        Assert.True(d.ShouldRepeat(Cross, 1250, Settings(), inCombat: true));
    }

    [Fact]
    public void StopsRepeatingAfterRelease()
    {
        var d = Decision();
        d.OnGenuinePress(Cross, 1000, Settings());
        d.OnRelease(Cross);
        Assert.False(d.ShouldRepeat(Cross, 5000, Settings(), inCombat: true));
    }

    [Fact]
    public void HonoursInitialDelayBeforeTheFirstRepeat()
    {
        var s = Settings();
        s.InitialDelayMs = 600;

        var d = Decision();
        d.OnGenuinePress(Cross, 1000, s);

        Assert.False(d.ShouldRepeat(Cross, 1599, s, inCombat: true));
        Assert.True(d.ShouldRepeat(Cross, 1600, s, inCombat: true));
    }

    [Fact]
    public void UsesIntervalNotInitialDelayForSubsequentRepeats()
    {
        var s = Settings();
        s.InitialDelayMs = 600;

        var d = Decision();
        d.OnGenuinePress(Cross, 1000, s);
        d.OnRepeatFired(Cross, 1600, s);

        Assert.False(d.ShouldRepeat(Cross, 1849, s, inCombat: true));
        Assert.True(d.ShouldRepeat(Cross, 1850, s, inCombat: true));
    }

    [Fact]
    public void BlocksGcdsWhenGcdTurboIsOff()
    {
        var s = Settings();
        s.TurboGcds = false;

        var d = Decision();
        d.OnGenuinePress(Cross, 1000, s);
        d.OnLearn(Cross, actionId: 3617, ActionKind.Gcd);

        Assert.False(d.ShouldRepeat(Cross, 2000, s, inCombat: true));
    }

    [Fact]
    public void AllowsOgcdsWhenOnlyGcdTurboIsOff()
    {
        var s = Settings();
        s.TurboGcds = false;

        var d = Decision();
        d.OnGenuinePress(Cross, 1000, s);
        d.OnLearn(Cross, actionId: 7541, ActionKind.OGcd);

        Assert.True(d.ShouldRepeat(Cross, 2000, s, inCombat: true));
    }

    [Fact]
    public void AllowsNonActionSlotsRegardlessOfActionFilters()
    {
        var s = Settings();
        s.TurboGcds = false;
        s.TurboOgcds = false;

        var d = Decision();
        d.OnGenuinePress(Cross, 1000, s);
        d.OnLearn(Cross, actionId: 0, ActionKind.NonAction);

        Assert.True(d.ShouldRepeat(Cross, 2000, s, inCombat: true));
    }

    [Fact]
    public void AllowsUnlearnedButtonsSoTheFirstRepeatIsNeverBlocked()
    {
        var d = Decision();
        d.OnGenuinePress(Cross, 1000, Settings());
        Assert.Equal(ActionKind.Unknown, d.KindOf(Cross));
        Assert.True(d.ShouldRepeat(Cross, 2000, Settings(), inCombat: true));
    }

    [Fact]
    public void BlocksOutOfCombatUnlessAllowed()
    {
        var s = Settings();
        var d = Decision();
        d.OnGenuinePress(Cross, 1000, s);

        Assert.False(d.ShouldRepeat(Cross, 2000, s, inCombat: false));

        s.TurboOutOfCombat = true;
        Assert.True(d.ShouldRepeat(Cross, 2000, s, inCombat: false));
    }

    [Fact]
    public void BlocksEverythingWhenDisabled()
    {
        var s = Settings();
        var d = Decision();
        d.OnGenuinePress(Cross, 1000, s);

        s.Enabled = false;
        Assert.False(d.ShouldRepeat(Cross, 2000, s, inCombat: true));
    }

    [Fact]
    public void ForgetsTheLearnedActionOnRelease()
    {
        var d = Decision();
        d.OnGenuinePress(Cross, 1000, Settings());
        d.OnLearn(Cross, actionId: 3617, ActionKind.Gcd);
        d.OnRelease(Cross);

        Assert.Equal(ActionKind.Unknown, d.KindOf(Cross));
    }

    [Fact]
    public void ClampsJitteredIntervalToTheMinimumFloor()
    {
        var s = Settings();
        s.IntervalMs = 60;
        s.JitterMs = 200;

        // Worst-case negative jitter would give 60 - 200 = -140ms.
        var d = new TurboDecision((lo, hi) => lo);
        d.OnGenuinePress(Cross, 1000, s);
        d.OnRepeatFired(Cross, 1000, s);

        Assert.False(d.ShouldRepeat(Cross, 1000 + TurboDecision.MinIntervalMs - 1, s, inCombat: true));
        Assert.True(d.ShouldRepeat(Cross, 1000 + TurboDecision.MinIntervalMs, s, inCombat: true));
    }

    [Fact]
    public void TracksButtonsIndependently()
    {
        const ushort circle = 1 << 7;

        var d = Decision();
        d.OnGenuinePress(Cross, 1000, Settings());

        Assert.True(d.ShouldRepeat(Cross, 1250, Settings(), inCombat: true));
        Assert.False(d.ShouldRepeat(circle, 1250, Settings(), inCombat: true));
    }

    [Fact]
    public void ClampsASubFloorInitialDelayToTheMinimum()
    {
        var s = Settings();
        s.InitialDelayMs = 10;

        var d = Decision();
        d.OnGenuinePress(Cross, 1000, s);

        // 10ms is below the 50ms floor, so the first repeat lands at +50ms, not +10ms.
        Assert.False(d.ShouldRepeat(Cross, 1000 + TurboDecision.MinIntervalMs - 1, s, inCombat: true));
        Assert.True(d.ShouldRepeat(Cross, 1000 + TurboDecision.MinIntervalMs, s, inCombat: true));
    }

    [Fact]
    public void AppliesTheFullPositiveJitterRange()
    {
        var s = Settings();
        s.IntervalMs = 250;
        s.JitterMs = 200;

        // Random.Next(lo, hi) has an exclusive upper bound, so the largest value the
        // source can return is hi - 1. OnRepeatFired must pass hi = JitterMs + 1 for
        // maximum positive jitter to be reachable at all.
        var d = new TurboDecision((lo, hi) => hi - 1);
        d.OnGenuinePress(Cross, 1000, s);
        d.OnRepeatFired(Cross, 1000, s);

        Assert.False(d.ShouldRepeat(Cross, 1449, s, inCombat: true));
        Assert.True(d.ShouldRepeat(Cross, 1450, s, inCombat: true));
    }

    [Fact]
    public void QueryingAnUntouchedButtonReportsNothingAndCreatesNoState()
    {
        const ushort square = 1 << 6;

        var d = Decision();

        // Pure queries against a button that was never pressed.
        Assert.Equal(ActionKind.Unknown, d.KindOf(square));
        Assert.False(d.ShouldRepeat(square, 9999, Settings(), inCombat: true));
        d.OnRelease(square);

        // None of the above may have started a turbo clock for it.
        Assert.False(d.ShouldRepeat(square, 999_999, Settings(), inCombat: true));
    }
}
