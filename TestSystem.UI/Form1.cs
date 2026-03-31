using TestSystem.Core.Engine;
using TestSystem.Core.Models;
using TestSystem.Steps;

namespace TestSystem.UI
{
    public partial class Form1 : Form
    {
        private TestEngine _engine;
        public Form1()
        {
            InitializeComponent();
            SetupEngine();
        }

        private void SetupEngine()
        {
            _engine = new TestEngine(new List<Core.Interfaces.ITestStep>
        {
            new PingStep(timeoutSec: 30)
            // Sau này thêm step khác vào đây:
            // new ConnectDutStep(),
            // new SfisPreDataStep(),
            // new SfisSendResultStep(),
        });

            // Engine báo status → hiển thị lên UI
            _engine.OnStatusChanged += msg =>
            {
                // Phải chạy trên UI thread
                if (InvokeRequired)
                    Invoke(() => UpdateStatus(msg));
                else
                    UpdateStatus(msg);
            };
        }

        private void UpdateStatus(string msg)
        {
            lblStatus.Text = msg;
            lstLog.Items.Add(msg);
            lstLog.TopIndex = lstLog.Items.Count - 1; // auto scroll
        }

        private async void btnStart_Click(object sender, EventArgs e)
        {
            if (string.IsNullOrWhiteSpace(txtIpAddress.Text))
            {
                MessageBox.Show("Nhập IP DUT trước!", "Thiếu thông tin",
                    MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            // Chuẩn bị
            btnStart.Enabled = false;
            lstLog.Items.Clear();
            lblStatus.Text = "Đang chạy...";
            lblStatus.ForeColor = Color.Blue;

            // Tạo context cho lần test này
            var context = new TestContext
            {
                IpAddress = txtIpAddress.Text.Trim(),
                Station = "TEST-PC-01",
                Model = "TEST-MODEL",
                Mac = "AABBCCDDEEFF"   // sau này lấy từ barcode scanner
            };

            // Chạy engine
            var result = await _engine.RunAsync(context);

            // Hiển thị kết quả
            if (result.IsPassed)
            {
                lblStatus.Text = "PASS";
                lblStatus.ForeColor = Color.Green;
            }
            else
            {
                lblStatus.Text = $"FAIL [{result.ErrorCode}]";
                lblStatus.ForeColor = Color.Red;
            }

            // Đổ log ra listbox
            foreach (var log in result.StepLogs)
                lstLog.Items.Add(log);

            btnStart.Enabled = true;

        }
    }
}
