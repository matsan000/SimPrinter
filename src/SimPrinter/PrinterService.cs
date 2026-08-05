using System.IO.Ports;
using System.Runtime.InteropServices;

namespace SimPrinter
{
    public static class PrinterService
    {
        /// <summary>Sends raw ESC/POS bytes over a COM port (works for USB and paired Bluetooth SPP printers).</summary>
        public static void PrintViaSerial(string portName, int baudRate, byte[] data)
        {
            using var port = new SerialPort(portName, baudRate)
            {
                DataBits = 8,
                Parity = Parity.None,
                StopBits = StopBits.One,
                WriteTimeout = 5000
            };

            port.Open();
            port.Write(data, 0, data.Length);

            // Give the buffer time to actually flush to the device before closing
            int waited = 0;
            while (port.BytesToWrite > 0 && waited < 3000)
            {
                Thread.Sleep(50);
                waited += 50;
            }

            port.Close();
        }

        /// <summary>Sends raw ESC/POS bytes to a printer installed in Windows (spooler-based, e.g. via a driver).</summary>
        public static void PrintViaWindowsPrinter(string printerName, byte[] data)
        {
            bool ok = RawPrinterHelper.SendBytesToPrinter(printerName, data);
            if (!ok)
                throw new Exception("Windows was unable to send raw data to the printer. " +
                    "Make sure the printer's driver supports raw/RAW datatype passthrough.");
        }
    }

    /// <summary>
    /// Standard raw-printing helper via winspool.drv, based on the well-known
    /// Microsoft sample pattern for sending raw bytes directly to a print queue.
    /// </summary>
    internal static class RawPrinterHelper
    {
        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Ansi)]
        internal class DOCINFOA
        {
            [MarshalAs(UnmanagedType.LPStr)] public string? pDocName;
            [MarshalAs(UnmanagedType.LPStr)] public string? pOutputFile;
            [MarshalAs(UnmanagedType.LPStr)] public string? pDataType;
        }

        [DllImport("winspool.drv", EntryPoint = "OpenPrinterA", SetLastError = true, CharSet = CharSet.Ansi, ExactSpelling = true)]
        private static extern bool OpenPrinter(string szPrinter, out IntPtr hPrinter, IntPtr pd);

        [DllImport("winspool.drv", EntryPoint = "ClosePrinter", SetLastError = true, ExactSpelling = true)]
        private static extern bool ClosePrinter(IntPtr hPrinter);

        [DllImport("winspool.drv", EntryPoint = "StartDocPrinterA", SetLastError = true, CharSet = CharSet.Ansi, ExactSpelling = true)]
        private static extern bool StartDocPrinter(IntPtr hPrinter, int level, DOCINFOA di);

        [DllImport("winspool.drv", EntryPoint = "EndDocPrinter", SetLastError = true, ExactSpelling = true)]
        private static extern bool EndDocPrinter(IntPtr hPrinter);

        [DllImport("winspool.drv", EntryPoint = "StartPagePrinter", SetLastError = true, ExactSpelling = true)]
        private static extern bool StartPagePrinter(IntPtr hPrinter);

        [DllImport("winspool.drv", EntryPoint = "EndPagePrinter", SetLastError = true, ExactSpelling = true)]
        private static extern bool EndPagePrinter(IntPtr hPrinter);

        [DllImport("winspool.drv", EntryPoint = "WritePrinter", SetLastError = true, ExactSpelling = true)]
        private static extern bool WritePrinter(IntPtr hPrinter, IntPtr pBytes, int dwCount, out int dwWritten);

        public static bool SendBytesToPrinter(string printerName, byte[] bytes)
        {
            IntPtr pUnmanagedBytes = Marshal.AllocCoTaskMem(bytes.Length);
            Marshal.Copy(bytes, 0, pUnmanagedBytes, bytes.Length);
            bool success = SendBytesToPrinter(printerName, pUnmanagedBytes, bytes.Length);
            Marshal.FreeCoTaskMem(pUnmanagedBytes);
            return success;
        }

        private static bool SendBytesToPrinter(string printerName, IntPtr pBytes, int count)
        {
            if (!OpenPrinter(printerName, out IntPtr hPrinter, IntPtr.Zero))
                return false;

            var di = new DOCINFOA
            {
                pDocName = "SimPrinter Ticket",
                pDataType = "RAW"
            };

            bool success = false;
            try
            {
                if (StartDocPrinter(hPrinter, 1, di))
                {
                    if (StartPagePrinter(hPrinter))
                    {
                        success = WritePrinter(hPrinter, pBytes, count, out _);
                        EndPagePrinter(hPrinter);
                    }
                    EndDocPrinter(hPrinter);
                }
            }
            finally
            {
                ClosePrinter(hPrinter);
            }

            return success;
        }
    }
}
