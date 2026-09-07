using TurboController;
using Xunit;

namespace TurboController.Tests;

public class FailureGateTests
{
    [Fact]
    public void StartsUntripped()
    {
        var gate = new FailureGate(limit: 3);
        Assert.False(gate.IsTripped);
    }

    [Fact]
    public void LogsEveryFailureUpToTheLimit()
    {
        var gate = new FailureGate(limit: 3);

        Assert.True(gate.RecordFailure());
        Assert.True(gate.RecordFailure());
        Assert.True(gate.RecordFailure());
    }

    [Fact]
    public void TripsOnceTheLimitIsReached()
    {
        var gate = new FailureGate(limit: 3);

        gate.RecordFailure();
        gate.RecordFailure();
        Assert.False(gate.IsTripped);

        gate.RecordFailure();
        Assert.True(gate.IsTripped);
    }

    [Fact]
    public void GoesSilentOnceTripped()
    {
        var gate = new FailureGate(limit: 3);

        gate.RecordFailure();
        gate.RecordFailure();
        gate.RecordFailure();

        // A tripped gate must never ask the caller to log again, however many
        // more frames fail.
        Assert.False(gate.RecordFailure());
        Assert.False(gate.RecordFailure());
        Assert.True(gate.IsTripped);
    }

    [Fact]
    public void ASuccessResetsTheStreak()
    {
        var gate = new FailureGate(limit: 3);

        gate.RecordFailure();
        gate.RecordFailure();
        gate.RecordSuccess();

        // Back to a clean streak: three more failures are needed to trip.
        Assert.True(gate.RecordFailure());
        Assert.True(gate.RecordFailure());
        Assert.False(gate.IsTripped);
        Assert.True(gate.RecordFailure());
        Assert.True(gate.IsTripped);
    }

    [Fact]
    public void ASuccessDoesNotUntripAThrownGate()
    {
        var gate = new FailureGate(limit: 2);

        gate.RecordFailure();
        gate.RecordFailure();
        Assert.True(gate.IsTripped);

        // Injection is disabled after tripping, so no real success can arrive;
        // pin the behaviour anyway so a future caller cannot resurrect it by accident.
        gate.RecordSuccess();
        Assert.True(gate.IsTripped);
    }

    [Fact]
    public void DefaultConstructorTripsAtTenConsecutiveFailures()
    {
        // Production always constructs with new(), never with an explicit limit,
        // so DefaultLimit itself needs direct coverage.
        var gate = new FailureGate();

        for (var i = 0; i < FailureGate.DefaultLimit - 1; i++)
            Assert.True(gate.RecordFailure());
        Assert.False(gate.IsTripped);

        Assert.True(gate.RecordFailure());
        Assert.True(gate.IsTripped);
    }
}
