using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TestSystem.Core.Interfaces
{
    public interface IConfigService
    {
        // Đọc chuỗi — tương đương getGlobalVeriableString() trong C++ cũ
        string GetString(string section, string key, string defaultValue = "");

        // Đọc số nguyên — tương đương getGlobalVeriableInt() trong C++ cũ
        int GetInt(string section, string key, int defaultValue = 0);

        // Ghi giá trị (dùng khi save config từ UI)
        void SetValue(string section, string key, string value);
    }
}
