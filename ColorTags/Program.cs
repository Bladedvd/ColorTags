using Microsoft.Win32;
using System.Drawing;
using System.Runtime.InteropServices;
using System.Text;

namespace ColorTags;

internal static class Program
{
    [STAThread]
    static void Main()
    {
        ApplicationConfiguration.Initialize();
        Application.Run(new MainForm());
    }
}

public sealed class MainForm : Form
{
    private readonly TextBox folderBox = new() { ReadOnly = true, Width = 390 };
    private readonly NotifyIcon tray = new();
    private readonly CheckBox startup = new() { Text = "Iniciar com o Windows", AutoSize = true };
    private readonly Label status = new() { AutoSize = true, ForeColor = Color.DimGray };
    private string? selectedFolder;

    private readonly (string Name, Color Color)[] colors =
    {
        ("Vermelho", Color.FromArgb(220, 65, 65)), ("Laranja", Color.FromArgb(240, 137, 48)),
        ("Amarelo", Color.FromArgb(242, 197, 65)), ("Verde", Color.FromArgb(69, 166, 105)),
        ("Turquesa", Color.FromArgb(54, 174, 169)), ("Azul", Color.FromArgb(65, 130, 220)),
        ("Azul-marinho", Color.FromArgb(55, 83, 145)), ("Roxo", Color.FromArgb(145, 94, 190)),
        ("Rosa", Color.FromArgb(224, 115, 164)), ("Marrom", Color.FromArgb(151, 105, 75)),
        ("Cinza", Color.FromArgb(135, 142, 151)), ("Preto", Color.FromArgb(65, 68, 75))
    };

    public MainForm()
    {
        Text = "ColorTags — Pastas coloridas";
        Width = 570; Height = 480;
        MinimumSize = new Size(570, 480);
        StartPosition = FormStartPosition.CenterScreen;
        BackColor = Color.FromArgb(247, 248, 250);
        Font = new Font("Segoe UI", 10);

        var title = new Label { Text = "ColorTags", Font = new Font("Segoe UI Semibold", 22), AutoSize = true, Location = new Point(24, 18) };
        var subtitle = new Label { Text = "Organize suas pastas com etiquetas coloridas", AutoSize = true, ForeColor = Color.DimGray, Location = new Point(27, 58) };
        Controls.Add(title); Controls.Add(subtitle);

        var folderLabel = new Label { Text = "PASTA SELECIONADA", AutoSize = true, Font = new Font("Segoe UI Semibold", 9), Location = new Point(27, 105) };
        folderBox.Location = new Point(27, 132);
        var browse = new Button { Text = "Escolher pasta...", Location = new Point(427, 130), Width = 112, Height = 30 };
        browse.Click += (_, _) => ChooseFolder();
        Controls.Add(folderLabel); Controls.Add(folderBox); Controls.Add(browse);

        var colorLabel = new Label { Text = "ESCOLHA UMA COR", AutoSize = true, Font = new Font("Segoe UI Semibold", 9), Location = new Point(27, 184) };
        Controls.Add(colorLabel);

        var panel = new FlowLayoutPanel { Location = new Point(24, 210), Size = new Size(510, 125), WrapContents = true };
        foreach (var item in colors)
        {
            var b = new Button { Text = item.Name, BackColor = item.Color, ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat, Width = 120, Height = 42, Margin = new Padding(3), Tag = item.Color };
            b.FlatAppearance.BorderSize = 0;
            b.Click += (_, _) => ApplyColor((Color)b.Tag!, item.Name);
            panel.Controls.Add(b);
        }
        Controls.Add(panel);

        var clear = new Button { Text = "Remover cor da pasta", Location = new Point(27, 352), Width = 180, Height = 36 };
        clear.Click += (_, _) => RemoveColor();
        startup.Location = new Point(27, 407);
        startup.Checked = IsStartupEnabled();
        startup.CheckedChanged += (_, _) => SetStartup(startup.Checked);
        status.Location = new Point(27, 442);
        Controls.Add(clear); Controls.Add(startup); Controls.Add(status);

        tray.Text = "ColorTags";
        tray.Icon = SystemIcons.Application;
        tray.Visible = true;
        var menu = new ContextMenuStrip();
        menu.Items.Add("Abrir ColorTags", null, (_, _) => ShowWindow());
        menu.Items.Add("Sair", null, (_, _) => { tray.Visible = false; Application.Exit(); });
        tray.ContextMenuStrip = menu;
        tray.DoubleClick += (_, _) => ShowWindow();
        Resize += (_, _) => { if (WindowState == FormWindowState.Minimized) Hide(); };
        FormClosing += (_, e) => { if (e.CloseReason == CloseReason.UserClosing) { e.Cancel = true; Hide(); } };
    }

    private void ShowWindow() { Show(); WindowState = FormWindowState.Normal; Activate(); }

    private void ChooseFolder()
    {
        using var dialog = new FolderBrowserDialog { Description = "Selecione a pasta que deseja colorir", UseDescriptionForTitle = true };
        if (dialog.ShowDialog() == DialogResult.OK)
        {
            selectedFolder = dialog.SelectedPath;
            folderBox.Text = selectedFolder;
            status.Text = "Pasta selecionada.";
        }
    }

    private void ApplyColor(Color color, string name)
    {
        if (!EnsureFolder()) return;
        try
        {
            string iconDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "ColorTags", "Icons");
            Directory.CreateDirectory(iconDir);
            string iconPath = Path.Combine(iconDir, $"{color.R}_{color.G}_{color.B}.ico");
            if (!File.Exists(iconPath)) CreateFolderIcon(iconPath, color);

            string ini = Path.Combine(selectedFolder!, "desktop.ini");
            File.WriteAllText(ini, "[.ShellClassInfo]\r\nIconResource=" + iconPath + ",0\r\n", Encoding.Unicode);
            File.SetAttributes(ini, FileAttributes.Hidden | FileAttributes.System);
            var attrs = File.GetAttributes(selectedFolder!);
            File.SetAttributes(selectedFolder!, attrs | FileAttributes.ReadOnly);
            RefreshFolder(selectedFolder!);
            status.Text = $"Cor {name} aplicada com sucesso.";
        }
        catch (Exception ex) { MessageBox.Show("Não foi possível aplicar a cor:\n" + ex.Message, "ColorTags", MessageBoxButtons.OK, MessageBoxIcon.Error); }
    }

    private void RemoveColor()
    {
        if (!EnsureFolder()) return;
        try
        {
            string ini = Path.Combine(selectedFolder!, "desktop.ini");
            if (File.Exists(ini)) { File.SetAttributes(ini, FileAttributes.Normal); File.Delete(ini); }
            var attrs = File.GetAttributes(selectedFolder!);
            File.SetAttributes(selectedFolder!, attrs & ~FileAttributes.ReadOnly);
            RefreshFolder(selectedFolder!);
            status.Text = "Personalização removida.";
        }
        catch (Exception ex) { MessageBox.Show("Não foi possível remover a cor:\n" + ex.Message, "ColorTags", MessageBoxButtons.OK, MessageBoxIcon.Error); }
    }

    private bool EnsureFolder()
    {
        if (!string.IsNullOrWhiteSpace(selectedFolder) && Directory.Exists(selectedFolder)) return true;
        MessageBox.Show("Escolha uma pasta primeiro.", "ColorTags", MessageBoxButtons.OK, MessageBoxIcon.Information);
        return false;
    }

    private static void CreateFolderIcon(string path, Color color)
    {
        using var bmp = new Bitmap(256, 256);
        using (var g = Graphics.FromImage(bmp))
        {
            g.Clear(Color.Transparent);
            g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
            using var brush = new SolidBrush(color);
            using var shade = new SolidBrush(ControlPaint.Dark(color, .18f));
            PointF[] tab = { new(28, 62), new(105, 62), new(127, 82), new(222, 82), new(222, 192), new(28, 192) };
            g.FillPolygon(shade, tab);
            g.FillRectangle(brush, 24, 87, 208, 112);
            using var shine = new SolidBrush(Color.FromArgb(45, Color.White));
            g.FillRectangle(shine, 34, 97, 188, 24);
        }
        using var ms = new MemoryStream();
        bmp.Save(ms, System.Drawing.Imaging.ImageFormat.Png);
        byte[] png = ms.ToArray();
        using var fs = new FileStream(path, FileMode.Create);
        using var bw = new BinaryWriter(fs);
        bw.Write((ushort)0); bw.Write((ushort)1); bw.Write((ushort)1);
        bw.Write((byte)0); bw.Write((byte)0); bw.Write((byte)0); bw.Write((byte)0);
        bw.Write((ushort)1); bw.Write((ushort)32); bw.Write((uint)png.Length); bw.Write((uint)22);
        bw.Write(png);
    }

    private static void RefreshFolder(string path)
    {
        SHChangeNotify(0x00001000, 0x0005, path, IntPtr.Zero);
    }
    [DllImport("shell32.dll", CharSet = CharSet.Unicode)]
    private static extern void SHChangeNotify(int wEventId, int uFlags, string dwItem1, IntPtr dwItem2);

    private const string RunKey = @"Software\Microsoft\Windows\CurrentVersion\Run";
    private static bool IsStartupEnabled()
    {
        using var key = Registry.CurrentUser.OpenSubKey(RunKey, false);
        return key?.GetValue("ColorTags") is string;
    }
    private static void SetStartup(bool enabled)
    {
        using var key = Registry.CurrentUser.OpenSubKey(RunKey, true) ?? Registry.CurrentUser.CreateSubKey(RunKey);
        if (enabled) key.SetValue("ColorTags", "\"" + Application.ExecutablePath + "\"");
        else key.DeleteValue("ColorTags", false);
    }
}
