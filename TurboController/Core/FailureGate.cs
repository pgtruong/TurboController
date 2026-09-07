namespace TurboController;

/// <summary>
/// Counts consecutive failures so a hook detour that throws every frame cannot
/// flood the log. Once tripped it stays tripped: the caller is expected to stop
/// doing the failing work entirely rather than keep retrying at frame rate.
/// </summary>
public sealed class FailureGate
{
    public const int DefaultLimit = 10;

    private readonly int limit;
    private int consecutive;
    private bool tripped;

    public FailureGate(int limit = DefaultLimit) => this.limit = limit;

    /// <summary>
    /// True once <see cref="RecordFailure"/> has been called <c>limit</c> times
    /// in a row without an intervening success.
    /// </summary>
    public bool IsTripped => tripped;

    /// <summary>
    /// Records one failure.
    /// </summary>
    public bool RecordFailure()
    {
        if (tripped)
            return false;

        consecutive++;
        if (consecutive >= limit)
            tripped = true;

        return true;
    }

    /// <summary>
    /// Records one clean pass, clearing the streak. Has no effect once tripped —
    /// a tripped gate is terminal for the lifetime of the plugin instance.
    /// </summary>
    public void RecordSuccess()
    {
        if (!tripped)
            consecutive = 0;
    }
}
