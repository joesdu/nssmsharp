using NssmSharp.Core;

namespace NssmSharp.Gui;

public sealed class MainForm : Form
{
    private readonly ListBox lstServices = new();
    private readonly Button btnAdd = new();
    private readonly Button btnEdit = new();
    private readonly Button btnRemove = new();
    private readonly Button btnRefresh = new();
    private readonly ConfigManager configManager = new();

    public MainForm()
    {
        Text = "NssmSharp 服务管理器";
        Width = 600;
        Height = 400;
        StartPosition = FormStartPosition.CenterScreen;
        InitUI();
        LoadServices();
    }

    private void InitUI()
    {
        lstServices.Left = 20; lstServices.Top = 20; lstServices.Width = 400; lstServices.Height = 300;
        btnAdd.Text = "注册服务"; btnAdd.Left = 440; btnAdd.Top = 40; btnAdd.Width = 120;
        btnEdit.Text = "编辑服务"; btnEdit.Left = 440; btnEdit.Top = 90; btnEdit.Width = 120;
        btnRemove.Text = "卸载服务"; btnRemove.Left = 440; btnRemove.Top = 140; btnRemove.Width = 120;
        btnRefresh.Text = "刷新"; btnRefresh.Left = 440; btnRefresh.Top = 190; btnRefresh.Width = 120;
        btnAdd.Click += (_, _) => AddService();
        btnEdit.Click += (_, _) => EditService();
        btnRemove.Click += (_, _) => RemoveService();
        btnRefresh.Click += (_, _) => LoadServices();
        Controls.AddRange(lstServices, btnAdd, btnEdit, btnRemove, btnRefresh);
    }

    private void LoadServices()
    {
        lstServices.Items.Clear();
        var dir = new DirectoryInfo("configs");
        if (!dir.Exists) dir.Create();
        foreach (var file in dir.GetFiles("*.json"))
        {
            lstServices.Items.Add(Path.GetFileNameWithoutExtension(file.Name));
        }
    }

    private void AddService()
    {
        using var form = new ServiceConfigForm();
        if (form.ShowDialog() != DialogResult.OK || form.ServiceConfig == null) return;
        configManager.SaveServiceConfig(form.ServiceConfig);
        ServiceManager.InstallService(form.ServiceConfig);
        LoadServices();
    }

    private void EditService()
    {
        if (lstServices.SelectedItem == null) return;
        var name = lstServices.SelectedItem.ToString()!;
        var config = ConfigManager.LoadServiceConfig(name);
        if (config == null) return;
        using var form = new ServiceConfigForm(config);
        if (form.ShowDialog() != DialogResult.OK || form.ServiceConfig == null) return;
        configManager.SaveServiceConfig(form.ServiceConfig);
        ServiceManager.UninstallService(name);
        ServiceManager.InstallService(form.ServiceConfig);
        LoadServices();
    }

    private void RemoveService()
    {
        if (lstServices.SelectedItem == null) return;
        var name = lstServices.SelectedItem.ToString()!;
        if (MessageBox.Show($"确定要卸载服务 {name} 吗？", "确认", MessageBoxButtons.YesNo) != DialogResult.Yes) return;
        ServiceManager.UninstallService(name);
        ConfigManager.DeleteServiceConfig(name);
        LoadServices();
    }
}