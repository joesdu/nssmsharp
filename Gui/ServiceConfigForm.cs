using System.ComponentModel;
using NssmSharp.Interop;

namespace NssmSharp.Gui;

public sealed class ServiceConfigForm : Form
{
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public NssmService? ServiceConfig { get; private set; }

    private readonly TextBox txtName = new();
    private readonly TextBox txtDisplayName = new();
    private readonly TextBox txtDescription = new();
    private readonly TextBox txtExePath = new();
    private readonly TextBox txtArguments = new();
    private readonly TextBox txtWorkDir = new();
    private readonly Button btnBrowse = new();
    private readonly Button btnOK = new();
    private readonly Button btnCancel = new();

    public ServiceConfigForm(NssmService? config = null)
    {
        Text = config == null ? "注册新服务" : "编辑服务";
        Width = 500;
        Height = 400;
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        MinimizeBox = false;
        StartPosition = FormStartPosition.CenterScreen;
        InitUI();
        if (config != null) LoadConfig(config);
    }

    private void InitUI()
    {
        var lblName = new Label { Text = "服务名:", Left = 20, Top = 20, Width = 80 };
        txtName.Left = 110; txtName.Top = 20; txtName.Width = 350;
        var lblDisplayName = new Label { Text = "显示名:", Left = 20, Top = 60, Width = 80 };
        txtDisplayName.Left = 110; txtDisplayName.Top = 60; txtDisplayName.Width = 350;
        var lblDescription = new Label { Text = "描述:", Left = 20, Top = 100, Width = 80 };
        txtDescription.Left = 110; txtDescription.Top = 100; txtDescription.Width = 350;
        var lblExePath = new Label { Text = "可执行文件:", Left = 20, Top = 140, Width = 80 };
        txtExePath.Left = 110; txtExePath.Top = 140; txtExePath.Width = 260;
        btnBrowse.Text = "浏览..."; btnBrowse.Left = 380; btnBrowse.Top = 140; btnBrowse.Width = 80;
        btnBrowse.Click += (_, _) => {
            using var ofd = new OpenFileDialog();
            ofd.Filter = "可执行文件 (*.exe)|*.exe|所有文件 (*.*)|*.*";
            if (ofd.ShowDialog() == DialogResult.OK) txtExePath.Text = ofd.FileName;
        };
        var lblArguments = new Label { Text = "参数:", Left = 20, Top = 180, Width = 80 };
        txtArguments.Left = 110; txtArguments.Top = 180; txtArguments.Width = 350;
        var lblWorkDir = new Label { Text = "工作目录:", Left = 20, Top = 220, Width = 80 };
        txtWorkDir.Left = 110; txtWorkDir.Top = 220; txtWorkDir.Width = 350;
        btnOK.Text = "确定"; btnOK.Left = 280; btnOK.Top = 280; btnOK.Width = 80;
        btnOK.Click += (_, _) =>
        {
            if (!ValidateInput()) return;
            ServiceConfig = GetConfig(); DialogResult = DialogResult.OK; Close();
        };
        btnCancel.Text = "取消"; btnCancel.Left = 380; btnCancel.Top = 280; btnCancel.Width = 80;
        btnCancel.Click += (_, _) => { DialogResult = DialogResult.Cancel; Close(); };
        Controls.AddRange(lblName, txtName, lblDisplayName, txtDisplayName, lblDescription, txtDescription, lblExePath, txtExePath, btnBrowse, lblArguments, txtArguments, lblWorkDir, txtWorkDir, btnOK, btnCancel);
    }

    private void LoadConfig(NssmService config)
    {
        txtName.Text = config.Name;
        txtDisplayName.Text = config.DisplayName;
        txtDescription.Text = config.Description;
        txtExePath.Text = config.ExecutablePath;
        txtArguments.Text = config.Arguments;
        txtWorkDir.Text = config.WorkingDirectory;
    }

    private bool ValidateInput()
    {
        if (string.IsNullOrWhiteSpace(txtName.Text)) { MessageBox.Show("服务名不能为空"); return false; }
        if (!string.IsNullOrWhiteSpace(txtExePath.Text)) return true;
        MessageBox.Show("可执行文件路径不能为空"); return false;
    }

    private NssmService GetConfig()
    {
        return new()
        {
            Name = txtName.Text.Trim(),
            DisplayName = txtDisplayName.Text.Trim(),
            Description = txtDescription.Text.Trim(),
            ExecutablePath = txtExePath.Text.Trim(),
            Arguments = txtArguments.Text.Trim(),
            WorkingDirectory = txtWorkDir.Text.Trim()
        };
    }
}