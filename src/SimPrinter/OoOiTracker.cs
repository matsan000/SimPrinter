namespace SimPrinter
{
    /// <summary>
    /// Watches SimConnect's per-second flight state and detects the four OOOI events for one
    /// flight: OUT (leaves the gate), OFF (wheels up), ON (wheels down), IN (both engines shut
    /// down after landing). <see cref="Current"/> reflects whatever's been reached so far (null
    /// for events that haven't happened yet), for a live display; <see cref="Completed"/> fires
    /// once IN is reached, then the tracker goes idle until <see cref="Reset"/> is called for
    /// the next flight.
    /// </summary>
    public sealed class OoOiTracker
    {
        private enum State { PreFlight, TaxiOut, Airborne, ConfirmingLanding, TaxiIn, Done }

        // Real-world OUT is "brake released", which can happen before engine start during a
        // push - a small ground-speed threshold catches that without needing engine state.
        private const double OutGroundSpeedThresholdKts = 0.5;

        // A touch-and-go or bounced landing also registers "on ground" briefly. Requiring the
        // aircraft to stay down for a few seconds before ON is confirmed filters those out,
        // while still recording the timestamp of the original touchdown, not the confirmation.
        private const double LandingConfirmSeconds = 5.0;

        private State _state = State.PreFlight;
        private double? _onCandidateSeconds;
        private double? _confirmingSinceSeconds;

        public OoOiProgress Current { get; private set; }

        public event Action<OoOiProgress>? Completed;

        public void Reset()
        {
            _state = State.PreFlight;
            _onCandidateSeconds = null;
            _confirmingSinceSeconds = null;
            Current = default;
        }

        public void Update(SimFlightState s)
        {
            Current = Current with { Date = s.ZuluDate };

            switch (_state)
            {
                case State.PreFlight:
                    if (s.OnGround && s.GroundVelocityKts > OutGroundSpeedThresholdKts)
                    {
                        Current = Current with { OutSeconds = s.ZuluSeconds };
                        _state = State.TaxiOut;
                    }
                    break;

                case State.TaxiOut:
                    if (!s.OnGround)
                    {
                        Current = Current with { OffSeconds = s.ZuluSeconds };
                        _state = State.Airborne;
                    }
                    break;

                case State.Airborne:
                    if (s.OnGround)
                    {
                        _onCandidateSeconds = s.ZuluSeconds;
                        _confirmingSinceSeconds = s.ZuluSeconds;
                        _state = State.ConfirmingLanding;
                    }
                    break;

                case State.ConfirmingLanding:
                    if (!s.OnGround)
                    {
                        // Bounced back up before staying down - not a real landing yet, keep
                        // waiting for the next touchdown attempt.
                        _onCandidateSeconds = null;
                        _confirmingSinceSeconds = null;
                        _state = State.Airborne;
                    }
                    else if (SecondsSince(_confirmingSinceSeconds!.Value, s.ZuluSeconds) >= LandingConfirmSeconds)
                    {
                        Current = Current with { OnSeconds = _onCandidateSeconds };
                        _state = State.TaxiIn;
                    }
                    break;

                case State.TaxiIn:
                    if (AllEnginesOff(s))
                    {
                        Current = Current with { InSeconds = s.ZuluSeconds };
                        _state = State.Done;
                        Completed?.Invoke(Current);
                    }
                    break;

                case State.Done:
                    break;
            }
        }

        private static bool AllEnginesOff(SimFlightState s)
        {
            foreach (bool running in s.EngineCombustion)
                if (running) return false;
            return true;
        }

        private static double SecondsSince(double fromZuluSeconds, double toZuluSeconds)
        {
            double diff = toZuluSeconds - fromZuluSeconds;
            if (diff < 0) diff += 86400; // midnight rollover
            return diff;
        }
    }

    /// <summary>
    /// The OOOI timestamps known so far for the current flight, each as seconds-since-midnight
    /// Zulu (matching the sim's own "ZULU TIME" simvar) - null for events that haven't happened
    /// yet. Date is the sim's current Zulu calendar date, kept fresh independent of OOOI
    /// progress so it's available as soon as SimConnect data starts flowing.
    /// </summary>
    public readonly record struct OoOiProgress(
        double? OutSeconds = null,
        double? OffSeconds = null,
        double? OnSeconds = null,
        double? InSeconds = null,
        DateOnly? Date = null);
}
