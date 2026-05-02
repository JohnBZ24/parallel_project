namespace parallel_project
{
    internal static class Program
    {
        //
        // The main entry point for the application.
        //
        // @notes
        // - Sets up WinForms defaults (DPI/font/etc) and then opens Form1.
        //
        [STAThread]
        static void Main()
        {
            // To customize application configuration such as set high DPI settings or default font,
            // see https://aka.ms/applicationconfiguration.
            ApplicationConfiguration.Initialize();
            Application.Run(new Form1());
        }
    }
}
