using Microsoft.Data.Sqlite;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TestSystem.Core.Interfaces;

namespace TestSystem.Infrastructure.Config
{
    public class SqliteConfigService : IConfigService
    {
        private readonly string _dbPath;

        public SqliteConfigService(string dbPath)
        {
            _dbPath = dbPath;

            // Tự tạo file .db và bảng nếu chưa có
            InitDatabase();
        }

        // ── Đọc chuỗi ────────────────────────────────────────────────────
        public string GetString(string section, string key, string defaultValue = "")
        {
            using var conn = OpenConnection();

            var cmd = conn.CreateCommand();
            cmd.CommandText = @"
            SELECT Value FROM Config
            WHERE Section = $section AND Key = $key
            LIMIT 1";
            cmd.Parameters.AddWithValue("$section", section);
            cmd.Parameters.AddWithValue("$key", key);

            var result = cmd.ExecuteScalar();
            return result?.ToString() ?? defaultValue;
        }

        // ── Đọc số nguyên ────────────────────────────────────────────────
        public int GetInt(string section, string key, int defaultValue = 0)
        {
            var raw = GetString(section, key);
            return int.TryParse(raw, out int val) ? val : defaultValue;
        }

        // ── Ghi giá trị ──────────────────────────────────────────────────
        public void SetValue(string section, string key, string value)
        {
            using var conn = OpenConnection();

            var cmd = conn.CreateCommand();

            // INSERT nếu chưa có, UPDATE nếu đã có
            cmd.CommandText = @"
            INSERT INTO Config (Section, Key, Value)
            VALUES ($section, $key, $value)
            ON CONFLICT(Section, Key)
            DO UPDATE SET Value = $value";
            cmd.Parameters.AddWithValue("$section", section);
            cmd.Parameters.AddWithValue("$key", key);
            cmd.Parameters.AddWithValue("$value", value);

            cmd.ExecuteNonQuery();
        }

        // ── Private helpers ───────────────────────────────────────────────
        private SqliteConnection OpenConnection()
        {
            var conn = new SqliteConnection($"Data Source={_dbPath}");
            conn.Open();
            return conn;
        }

        private void InitDatabase()
        {
            using var conn = OpenConnection();

            var cmd = conn.CreateCommand();
            cmd.CommandText = @"
            CREATE TABLE IF NOT EXISTS Config (
                Section TEXT NOT NULL,
                Key     TEXT NOT NULL,
                Value   TEXT NOT NULL DEFAULT '',
                PRIMARY KEY (Section, Key)
            )";
            cmd.ExecuteNonQuery();
        }
    }
}
