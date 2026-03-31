namespace TestSystem.UI
{
    partial class Form1
    {
        /// <summary>
        ///  Required designer variable.
        /// </summary>
        private System.ComponentModel.IContainer components = null;

        /// <summary>
        ///  Clean up any resources being used.
        /// </summary>
        /// <param name="disposing">true if managed resources should be disposed; otherwise, false.</param>
        protected override void Dispose(bool disposing)
        {
            if (disposing && (components != null))
            {
                components.Dispose();
            }
            base.Dispose(disposing);
        }

        #region Windows Form Designer generated code

        /// <summary>
        ///  Required method for Designer support - do not modify
        ///  the contents of this method with the code editor.
        /// </summary>
        private void InitializeComponent()
        {
            txtIpAddress = new TextBox();
            btnStart = new Button();
            lblStatus = new Label();
            lstLog = new ListBox();
            SuspendLayout();
            // 
            // txtIpAddress
            // 
            txtIpAddress.Location = new Point(93, 86);
            txtIpAddress.Name = "txtIpAddress";
            txtIpAddress.Size = new Size(317, 27);
            txtIpAddress.TabIndex = 0;
            // 
            // btnStart
            // 
            btnStart.Location = new Point(528, 84);
            btnStart.Name = "btnStart";
            btnStart.Size = new Size(94, 29);
            btnStart.TabIndex = 1;
            btnStart.Text = "button1";
            btnStart.UseVisualStyleBackColor = true;
            btnStart.Click += btnStart_Click;
            // 
            // lblStatus
            // 
            lblStatus.AutoSize = true;
            lblStatus.Location = new Point(197, 43);
            lblStatus.Name = "lblStatus";
            lblStatus.Size = new Size(50, 20);
            lblStatus.TabIndex = 2;
            lblStatus.Text = "label1";
            // 
            // lstLog
            // 
            lstLog.FormattingEnabled = true;
            lstLog.Location = new Point(133, 164);
            lstLog.Name = "lstLog";
            lstLog.Size = new Size(416, 124);
            lstLog.TabIndex = 3;
            // 
            // Form1
            // 
            AutoScaleDimensions = new SizeF(8F, 20F);
            AutoScaleMode = AutoScaleMode.Font;
            ClientSize = new Size(800, 450);
            Controls.Add(lstLog);
            Controls.Add(lblStatus);
            Controls.Add(btnStart);
            Controls.Add(txtIpAddress);
            Name = "Form1";
            Text = "Form1";
            ResumeLayout(false);
            PerformLayout();
        }

        #endregion

        private TextBox txtIpAddress;
        private Button btnStart;
        private Label lblStatus;
        private ListBox lstLog;
    }
}
