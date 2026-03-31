using TestSystem.Infrastructure.Config;

namespace TestSystem.UI
{
    internal static class Program
    {
        public static SqliteConfigService Config { get; private set; } = null!;
        /// <summary>
        ///  The main entry point for the application.
        /// </summary>
        [STAThread]
        static void Main()
        {
            string dbPath = Path.Combine(
    AppDomain.CurrentDomain.BaseDirectory,
    "config.db"
);
            Config = new SqliteConfigService(dbPath);
            SeedDefaultConfig(Config);
            // To customize application configuration such as set high DPI settings or default font,
            // see https://aka.ms/applicationconfiguration.
            ApplicationConfiguration.Initialize();
            Application.Run(new Form1());
        }
        private static void SeedDefaultConfig(SqliteConfigService cfg)
        {
            // Chỉ seed nếu key chưa tồn tại
            void SeedIfEmpty(string section, string key, string value)
            {
                if (string.IsNullOrEmpty(cfg.GetString(section, key)))
                    cfg.SetValue(section, key, value);
            }

            // GlobalVariable — giống section trong Access cũ
            SeedIfEmpty("GlobalVariable", "DUT_IP", "192.168.1.1");
            SeedIfEmpty("GlobalVariable", "DUT_POWERON_TIME", "60");
            SeedIfEmpty("GlobalVariable", "Station", "L1-PT1-01");
            SeedIfEmpty("GlobalVariable", "Model", "TEST-MODEL");
            SeedIfEmpty("GlobalVariable", "LogFolder", @"D:\TestLogs");

            // COM port config
            SeedIfEmpty("COM", "DUT_COM", "COM3");
            SeedIfEmpty("COM", "SFIS_COM", "COM4");
            SeedIfEmpty("COM", "BaudRate", "115200");

            // SFIS config
            SeedIfEmpty("SFIS", "Enabled", "1");
            SeedIfEmpty("SFIS", "Station", "L1-PT1-01");
        }
    }
}