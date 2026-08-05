namespace SimPrinter
{
    internal static class Program
    {
        [STAThread]
        static void Main()
        {
            ApplicationConfiguration.Initialize();
            UiStyle.IsDarkMode = Preferences.Load().DarkMode;
            Application.Run(new MainForm());
        }
    }
}
