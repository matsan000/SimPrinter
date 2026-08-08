using System.Runtime.InteropServices;
using Microsoft.FlightSimulator.SimConnect;

namespace SimPrinter
{
    /// <summary>
    /// Thin SimConnect wrapper that polls one shared struct of flight data once per sim-second:
    /// the Zulu clock/date, ground contact, ground speed, and per-engine combustion state - all
    /// consumed by OoOiTracker for Out/Off/On/In detection. The sim may not be running yet (or
    /// may close/reopen while SimPrinter stays open) so connecting is lazy and retried on a
    /// timer rather than attempted once at startup.
    /// </summary>
    public sealed class SimConnectClient : IDisposable
    {
        private const string AppName = "SimPrinter";
        private const int WM_USER_SIMCONNECT = 0x0402;
        private const int ReconnectIntervalMs = 5000;

        public event Action<SimFlightState>? FlightStateUpdated;
        public event Action? Connected;
        public event Action? Disconnected;

        private enum Definitions { FlightData }
        private enum Requests { FlightData }

        /// <summary>
        /// Every field is requested as FLOAT64 regardless of its "natural" type - SimConnect
        /// converts booleans (0/1) and small integers (day, month, year, engine count) to it
        /// without issue, which keeps this one struct simple instead of juggling mixed types
        /// and their StructLayout offsets.
        /// </summary>
        [StructLayout(LayoutKind.Sequential)]
        private struct FlightData
        {
            public double ZuluSeconds;
            public double ZuluDay;
            public double ZuluMonth;
            public double ZuluYear;
            public double OnGround;
            public double GroundVelocity;
            public double Eng1Combustion;
            public double Eng2Combustion;
            public double Eng3Combustion;
            public double Eng4Combustion;
        }

        private readonly MessageWindow _window;
        private readonly System.Windows.Forms.Timer _reconnectTimer;
        private SimConnect? _simConnect;

        public bool IsConnected => _simConnect != null;

        public SimConnectClient()
        {
            _window = new MessageWindow(this);
            _reconnectTimer = new System.Windows.Forms.Timer { Interval = ReconnectIntervalMs };
            _reconnectTimer.Tick += (_, _) => TryConnect();
            _reconnectTimer.Start();
            TryConnect();
        }

        private void TryConnect()
        {
            if (_simConnect != null) return;

            try
            {
                var sc = new SimConnect(AppName, _window.Handle, WM_USER_SIMCONNECT, null, 0);
                sc.OnRecvOpen += (_, _) => Connected?.Invoke();
                sc.OnRecvQuit += (_, _) => HandleDisconnect();
                sc.OnRecvException += (_, _) => { };
                sc.OnRecvSimobjectData += OnRecvSimobjectData;

                sc.AddToDataDefinition(Definitions.FlightData, "ZULU TIME", "seconds",
                    SIMCONNECT_DATATYPE.FLOAT64, 0f, SimConnect.SIMCONNECT_UNUSED);
                sc.AddToDataDefinition(Definitions.FlightData, "ZULU DAY OF MONTH", "number",
                    SIMCONNECT_DATATYPE.FLOAT64, 0f, SimConnect.SIMCONNECT_UNUSED);
                sc.AddToDataDefinition(Definitions.FlightData, "ZULU MONTH OF YEAR", "number",
                    SIMCONNECT_DATATYPE.FLOAT64, 0f, SimConnect.SIMCONNECT_UNUSED);
                sc.AddToDataDefinition(Definitions.FlightData, "ZULU YEAR", "number",
                    SIMCONNECT_DATATYPE.FLOAT64, 0f, SimConnect.SIMCONNECT_UNUSED);
                sc.AddToDataDefinition(Definitions.FlightData, "SIM ON GROUND", "bool",
                    SIMCONNECT_DATATYPE.FLOAT64, 0f, SimConnect.SIMCONNECT_UNUSED);
                sc.AddToDataDefinition(Definitions.FlightData, "GROUND VELOCITY", "knots",
                    SIMCONNECT_DATATYPE.FLOAT64, 0f, SimConnect.SIMCONNECT_UNUSED);
                sc.AddToDataDefinition(Definitions.FlightData, "GENERAL ENG COMBUSTION:1", "bool",
                    SIMCONNECT_DATATYPE.FLOAT64, 0f, SimConnect.SIMCONNECT_UNUSED);
                sc.AddToDataDefinition(Definitions.FlightData, "GENERAL ENG COMBUSTION:2", "bool",
                    SIMCONNECT_DATATYPE.FLOAT64, 0f, SimConnect.SIMCONNECT_UNUSED);
                sc.AddToDataDefinition(Definitions.FlightData, "GENERAL ENG COMBUSTION:3", "bool",
                    SIMCONNECT_DATATYPE.FLOAT64, 0f, SimConnect.SIMCONNECT_UNUSED);
                sc.AddToDataDefinition(Definitions.FlightData, "GENERAL ENG COMBUSTION:4", "bool",
                    SIMCONNECT_DATATYPE.FLOAT64, 0f, SimConnect.SIMCONNECT_UNUSED);
                sc.RegisterDataDefineStruct<FlightData>(Definitions.FlightData);

                sc.RequestDataOnSimObject(Requests.FlightData, Definitions.FlightData,
                    SimConnect.SIMCONNECT_OBJECT_ID_USER, SIMCONNECT_PERIOD.SECOND,
                    SIMCONNECT_DATA_REQUEST_FLAG.DEFAULT, 0, 0, 0);

                _simConnect = sc;
            }
            catch (COMException)
            {
                // Sim isn't running (or isn't ready yet) - retry on the next timer tick.
                _simConnect = null;
            }
        }

        private void OnRecvSimobjectData(SimConnect sender, SIMCONNECT_RECV_SIMOBJECT_DATA data)
        {
            if ((Requests)data.dwRequestID != Requests.FlightData) return;
            var value = (FlightData)data.dwData[0];

            FlightStateUpdated?.Invoke(new SimFlightState(
                ZuluSeconds: value.ZuluSeconds,
                ZuluDate: new DateOnly((int)value.ZuluYear, (int)value.ZuluMonth, (int)value.ZuluDay),
                OnGround: value.OnGround != 0,
                GroundVelocityKts: value.GroundVelocity,
                EngineCombustion: new[]
                {
                    value.Eng1Combustion != 0,
                    value.Eng2Combustion != 0,
                    value.Eng3Combustion != 0,
                    value.Eng4Combustion != 0,
                }));
        }

        internal void ReceiveMessage()
        {
            try
            {
                _simConnect?.ReceiveMessage();
            }
            catch (COMException)
            {
                HandleDisconnect();
            }
        }

        private void HandleDisconnect()
        {
            if (_simConnect == null) return;
            _simConnect.Dispose();
            _simConnect = null;
            Disconnected?.Invoke();
        }

        public void Dispose()
        {
            _reconnectTimer.Stop();
            _reconnectTimer.Dispose();
            _simConnect?.Dispose();
            _simConnect = null;
            _window.DestroyHandle();
        }

        /// <summary>Message-only native window that receives the WM_USER message SimConnect
        /// posts when new data is ready to be pulled via ReceiveMessage().</summary>
        private sealed class MessageWindow : System.Windows.Forms.NativeWindow
        {
            private readonly SimConnectClient _owner;

            public MessageWindow(SimConnectClient owner)
            {
                _owner = owner;
                CreateHandle(new System.Windows.Forms.CreateParams());
            }

            protected override void WndProc(ref System.Windows.Forms.Message m)
            {
                if (m.Msg == WM_USER_SIMCONNECT)
                {
                    _owner.ReceiveMessage();
                    return;
                }
                base.WndProc(ref m);
            }
        }
    }

    /// <summary>
    /// One sim-second snapshot of the flight data OoOiTracker needs. EngineCombustion always has
    /// 4 entries (engines 1-4, MSFS's own indexing) - an aircraft with fewer engines just reports
    /// false for the indices it doesn't have, so callers can check "all engines off" by checking
    /// all 4 without needing to know the aircraft's actual engine count.
    /// </summary>
    public readonly record struct SimFlightState(
        double ZuluSeconds,
        DateOnly ZuluDate,
        bool OnGround,
        double GroundVelocityKts,
        bool[] EngineCombustion);
}
