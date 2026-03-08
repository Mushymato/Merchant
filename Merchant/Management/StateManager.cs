using Microsoft.Xna.Framework;

namespace Merchant.Management;

public sealed class StateManager<T>(T defaultValue, string? loggingName)
    where T : Enum
{
    private readonly string? loggingName = loggingName;
    public delegate void stateChanged();
    public T Current
    {
        get => field;
        set
        {
#if DEBUG
            if (loggingName != null)
                ModEntry.Log($"STATE({loggingName}): {field} -> {value}");
#endif
            if (Lock)
                return;

            field = value;
            Next = value;
            Timer = TimeSpan.Zero;
            changeCallback = null;
        }
    } = defaultValue;
    public T Next { get; private set; } = defaultValue;
    public bool Lock { get; private set; } = false;
    public TimeSpan Timer { get; private set; } = TimeSpan.Zero;
    private double timerTotalMS = -1;
    public float TimerProgress => (float)(Timer.TotalMilliseconds / timerTotalMS);

    public float TimerProgressInRange(float range)
    {
        double msLeft = timerTotalMS - Timer.TotalMilliseconds;
        if (msLeft < range)
            return (float)(msLeft / range);
        return 1f;
    }

    private stateChanged? changeCallback = null;

    public void SetNext(T next, double transition, stateChanged? onChange = null)
    {
        if (Timer == TimeSpan.Zero)
        {
            Next = next;
            changeCallback = onChange;
            Timer = TimeSpan.FromMilliseconds(transition);
            timerTotalMS = transition;
        }
    }

    public void Unlock()
    {
        Lock = false;
    }

    public void SetAndLock(T current)
    {
        Current = current;
        Lock = true;
    }

    public void Update(GameTime time)
    {
        // state transition
        if (Timer > TimeSpan.Zero)
        {
            Timer -= time.ElapsedGameTime;
            if (Timer <= TimeSpan.Zero)
            {
                T old = Current;
                stateChanged? cb = changeCallback;
                Current = Next;
                cb?.Invoke();
            }
        }
    }

    public override string ToString()
    {
        return $"State.{typeof(T).Name}: {Current}";
    }
}
