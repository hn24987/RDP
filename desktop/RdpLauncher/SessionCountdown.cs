namespace RdpLauncher;

public enum SessionWarning { TenMinutes, FiveMinutes, Expired }

/// <summary>Tracks threshold notifications per run. UI owns the clock and notification display.</summary>
public sealed class SessionCountdown
{
    sealed class State
    {
        public DateTimeOffset ExpiresAt;
        public bool TenShown;
        public bool FiveShown;
        public bool ExpiredShown;
    }
    readonly Dictionary<long, State> states = new();
    long? activeRun;

    public long? RunId => activeRun;
    public DateTimeOffset? ExpiresAt => activeRun is long id ? states[id].ExpiresAt : null;

    public void Configure(long runId, DateTimeOffset expiresAt, DateTimeOffset now)
    {
        if (runId <= 0) throw new ArgumentOutOfRangeException(nameof(runId));
        if (!states.TryGetValue(runId, out var state))
        {
            state = new State { TenShown = expiresAt - now <= TimeSpan.FromMinutes(5) };
            states.Add(runId, state);
        }
        state.ExpiresAt = expiresAt;
        activeRun = runId;
    }

    public TimeSpan? Remaining(DateTimeOffset now)
    {
        if (ExpiresAt is not DateTimeOffset end) return null;
        return end > now ? end - now : TimeSpan.Zero;
    }

    public IReadOnlyList<SessionWarning> Tick(DateTimeOffset now)
    {
        if (activeRun is not long id) return Array.Empty<SessionWarning>();
        var state = states[id];
        var remaining = state.ExpiresAt - now;
        if (remaining <= TimeSpan.Zero)
        {
            if (state.ExpiredShown) return Array.Empty<SessionWarning>();
            state.ExpiredShown = state.TenShown = state.FiveShown = true;
            return new[] { SessionWarning.Expired };
        }
        if (state.ExpiredShown) return Array.Empty<SessionWarning>();
        if (remaining <= TimeSpan.FromMinutes(5))
        {
            state.TenShown = true;
            if (state.FiveShown) return Array.Empty<SessionWarning>();
            state.FiveShown = true;
            return new[] { SessionWarning.FiveMinutes };
        }
        if (remaining <= TimeSpan.FromMinutes(10) && !state.TenShown)
        {
            state.TenShown = true;
            return new[] { SessionWarning.TenMinutes };
        }
        return Array.Empty<SessionWarning>();
    }

    /// <summary>Stops active notifications without forgetting warnings for previously monitored runs.</summary>
    public void Clear() => activeRun = null;
}
