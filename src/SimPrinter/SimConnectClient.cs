using System.Runtime.InteropServices;
using Microsoft.FlightSimulator.SimConnect;

namespace SimPrinter
{
    /// <summary>
    /// Thin SimConnect wrapper that just polls the sim's current Zulu clock (seconds since
    /// midnight UTC), once per sim-second, for the "time to off-block" countdown. The sim may
    /// not be running yet (or may close/reopen while SimPrinter stays open) so connecting is
    /// lazy and retried on a timer rather than attempted once at startup.
    /// </summary>
    public sealed class SimConnectClient : IDisposable
    {
        private const string AppName = "SimPrinter";
        private const int WM_USER_SIMCONNECT = 0x0402;
        private const int ReconnectIntervalMs = 5000;

        public event Action<double>? ZuluSecondsUpdated;
        public event Action? Connected;
        public event Action? Disconnected;

        private enum Definitions { ZuluTime }
        private enum Requests { ZuluTime }

        [StructLayout(LayoutKind.Sequential)]
        private struct ZuluTimeData
        {
            public double Seconds;
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

                sc.AddToDataDefinition(Definitions.ZuluTime, "ZULU TIME", "seconds",
                    SIMCONNECT_DATATYPE.FLOAT64, 0f, SimConnect.SIMCONNECT_UNUSED);
                sc.RegisterDataDefineStruct<ZuluTimeData>(Definitions.ZuluTime);

                sc.RequestDataOnSimObject(Requests.ZuluTime, Definitions.ZuluTime,
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
            if ((Requests)data.dwRequestID != Requests.ZuluTime) return;
            var value = (ZuluTimeData)data.dwData[0];
            ZuluSecondsUpdated?.Invoke(value.Seconds);
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
}
