using System;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Animation;
using Microsoft.Win32;

namespace Zuzify;

public sealed class ZuzifyWindow : Window
{
    private readonly StackPanel page = new();
    private readonly StackPanel nav = new();
    private readonly TextBlock status = new();
    private readonly StringBuilder report = new();
    private readonly SolidColorBrush accent = new(Color.FromRgb(234, 94, 122));
    private readonly SolidColorBrush muted = new(Color.FromRgb(174, 157, 169));
    private readonly SolidColorBrush panel = new(Color.FromRgb(31, 24, 32));
    private string current = "Overview";
    private string selectedPlan = "Monthly";

    [STAThread]
    public static void Main() => new Application().Run(new ZuzifyWindow());

    public ZuzifyWindow()
    {
        Title = "Zuzify | Security Workspace";
        Width = 1180; Height = 800; MinWidth = 850; MinHeight = 600;
        WindowStartupLocation = WindowStartupLocation.CenterScreen;
        FontFamily = new FontFamily("Segoe UI");
        Foreground = Brushes.White;
        Background = new LinearGradientBrush(Color.FromRgb(35, 15, 28), Color.FromRgb(12, 13, 20), 45);
        var layout = new Grid();
        layout.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(235) });
        layout.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        var side = new DockPanel { Background = new SolidColorBrush(Color.FromRgb(17, 15, 22)), Margin = new Thickness(0) };
        var brand = new StackPanel { Margin = new Thickness(20, 28, 20, 20) };
        brand.Children.Add(Text("ZUZIFY", 29, accent, true));
        brand.Children.Add(Text("SECURITY WORKSPACE", 11, muted, true));
        DockPanel.SetDock(brand, Dock.Top); side.Children.Add(brand);
        var footer = Text("v0.1.0  •  Local preview", 12, muted);
        footer.Margin = new Thickness(20); DockPanel.SetDock(footer, Dock.Bottom); side.Children.Add(footer);
        nav.Margin = new Thickness(12, 0, 12, 0); side.Children.Add(nav);
        layout.Children.Add(side);
        var main = new DockPanel { Margin = new Thickness(30, 20, 30, 18) };
        Grid.SetColumn(main, 1); layout.Children.Add(main);
        status.Foreground = muted; status.FontSize = 12; status.Text = "Ready • No data sent to a server";
        status.Margin = new Thickness(0, 12, 0, 0); DockPanel.SetDock(status, Dock.Bottom); main.Children.Add(status);
        main.Children.Add(new ScrollViewer { Content = page, VerticalScrollBarVisibility = ScrollBarVisibility.Auto });
        Content = layout;
        ShowPage("Overview");
    }

    private TextBlock Text(string value, double size = 14, Brush? color = null, bool bold = false) =>
        new() { Text = value, FontSize = size, Foreground = color ?? Brushes.White, TextWrapping = TextWrapping.Wrap,
                FontWeight = bold ? FontWeights.SemiBold : FontWeights.Normal, Margin = new Thickness(0, 3, 0, 7) };

    private Button Button(string label, Action action, bool active = false)
    {
        var b = new Button { Content = label, Foreground = Brushes.White, Background = active ? accent : panel,
            Padding = new Thickness(14, 11, 14, 11), Margin = new Thickness(0, 5, 0, 5), BorderThickness = new Thickness(0),
            HorizontalContentAlignment = HorizontalAlignment.Left, Cursor = System.Windows.Input.Cursors.Hand };
        var template = new ControlTemplate(typeof(Button));
        var border = new FrameworkElementFactory(typeof(Border));
        border.SetValue(Border.CornerRadiusProperty, new CornerRadius(9));
        border.SetBinding(Border.BackgroundProperty, new System.Windows.Data.Binding("Background") { RelativeSource = System.Windows.Data.RelativeSource.TemplatedParent });
        var presenter = new FrameworkElementFactory(typeof(ContentPresenter));
        presenter.SetValue(FrameworkElement.MarginProperty, new Thickness(14, 10, 14, 10));
        border.AppendChild(presenter); template.VisualTree = border; b.Template = template;
        b.MouseEnter += (_, _) => b.Opacity = .8; b.MouseLeave += (_, _) => b.Opacity = 1;
        b.Click += (_, _) => { try { action(); } catch (Exception ex) { status.Text = ex.Message; } };
        return b;
    }

    private StackPanel Card(string title, string description)
    {
        var box = new StackPanel();
        box.Children.Add(Text(title, 20, null, true));
        box.Children.Add(Text(description, 14, muted));
        page.Children.Add(new Border { Child = box, Background = panel, CornerRadius = new CornerRadius(14),
            BorderBrush = new SolidColorBrush(Color.FromRgb(73, 52, 68)), BorderThickness = new Thickness(1),
            Padding = new Thickness(20), Margin = new Thickness(0, 8, 0, 10) });
        return box;
    }

    private TextBox Input(string value = "", bool readOnly = false) => new()
    {
        Text = value, IsReadOnly = readOnly, Background = new SolidColorBrush(Color.FromRgb(15, 14, 19)),
        Foreground = Brushes.White, CaretBrush = Brushes.White, BorderBrush = muted, Padding = new Thickness(10),
        FontSize = 13, TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 6, 0, 6)
    };

    private void Note(string line)
    {
        status.Text = line;
        report.AppendLine(DateTimeOffset.Now.ToString("u") + " | " + line);
    }

    private void ShowPage(string name)
    {
        current = name; nav.Children.Clear(); page.Children.Clear();
        foreach (string n in new[] { "Overview", "File integrity", "Password generator", "Device diagnostics", "Security checklist", "Activity", "Plans", "Settings" })
            nav.Children.Add(Button(n, () => ShowPage(n), n == current));
        page.Children.Add(Text("WORKSPACE / " + name.ToUpperInvariant(), 12, accent, true));
        page.Children.Add(Text(name, 34, null, true));
        switch (name)
        {
            case "Overview":
                Card("Your security workspace", "Native Windows application. Local tools, no browser engine, and no background uploads.");
                foreach (var item in new[] { ("File integrity", "Verify a file against a trusted SHA-256 checksum."),
                    ("Password generator", "Create strong random passwords locally."),
                    ("Device diagnostics", "Review basic information about this computer."),
                    ("Security checklist", "Review practical account and device safety steps.") })
                {
                    var card = Card(item.Item1, item.Item2);
                    card.Children.Add(Button("Open tool", () => ShowPage(item.Item1)));
                }
                break;
            case "File integrity":
                var fileCard = Card("SHA-256 verification", "Choose a local file. Compare the result with a checksum from its trusted publisher.");
                var path = Input("", true); var expected = Input(); var result = Input("", true);
                fileCard.Children.Add(path);
                fileCard.Children.Add(Button("Choose file", () => { var dialog = new OpenFileDialog(); if (dialog.ShowDialog() == true) path.Text = dialog.FileName; }));
                fileCard.Children.Add(Text("Expected SHA-256 (optional)", 13, muted)); fileCard.Children.Add(expected);
                fileCard.Children.Add(Button("Calculate and compare", () => _ = Verify()));
                fileCard.Children.Add(result);
                async Task Verify()
                {
                    var selected = path.Text; var comparison = expected.Text.Trim();
                    if (string.IsNullOrEmpty(selected)) { status.Text = "Select a file first."; return; }
                    if (comparison.Length != 0 && (comparison.Length != 64 || !comparison.All(Uri.IsHexDigit))) { status.Text = "Enter a 64-character hexadecimal SHA-256."; return; }
                    try
                    {
                        status.Text = "Calculating SHA-256...";
                        await using var stream = File.OpenRead(selected);
                        var hash = Convert.ToHexString(await SHA256.HashDataAsync(stream)).ToLowerInvariant();
                        result.Text = hash + (comparison.Length == 0 ? "" : string.Equals(hash, comparison, StringComparison.OrdinalIgnoreCase) ? "\nMATCH" : "\nMISMATCH");
                        Note("File integrity calculation complete.");
                    }
                    catch (Exception ex) { status.Text = "Could not read file: " + ex.Message; }
                }
                break;
            case "Password generator":
                var pass = Card("Local random generator", "Generated secrets are not saved in activity logs. Store them in a trusted password manager.");
                var length = new Slider { Minimum = 16, Maximum = 64, Value = 24, TickFrequency = 1, IsSnapToTickEnabled = true };
                var lengthText = Text("Length: 24", 14, muted); length.ValueChanged += (_, _) => lengthText.Text = "Length: " + (int)length.Value;
                var secret = Input("", true);
                pass.Children.Add(lengthText); pass.Children.Add(length);
                pass.Children.Add(Button("Generate", () => {
                    const string alphabet = "ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789!@#$%&*+-_";
                    secret.Text = new string(Enumerable.Range(0, (int)length.Value).Select(_ => alphabet[RandomNumberGenerator.GetInt32(alphabet.Length)]).ToArray());
                    status.Text = "Password generated locally.";
                }));
                pass.Children.Add(secret);
                pass.Children.Add(Button("Clear", () => secret.Clear()));
                break;
            case "Device diagnostics":
                var device = Card("System information", "Read-only diagnostics. No administrative privileges required.");
                var data = Input("", true); device.Children.Add(data);
                void Refresh() {
                    data.Text = $"Operating system: {Environment.OSVersion}\nArchitecture: {(Environment.Is64BitOperatingSystem ? "64-bit" : "32-bit")}\nLogical processors: {Environment.ProcessorCount}\nRuntime: {Environment.Version}";
                    Note("Device diagnostics refreshed.");
                }
                device.Children.Add(Button("Refresh", Refresh)); Refresh();
                break;
            case "Security checklist":
                var check = Card("Personal security review", "Self-reported checklist for this session; no automated compliance claims.");
                foreach (var task in new[] { "Install operating system and app updates", "Enable multi-factor authentication", "Use unique passwords for important accounts", "Keep backups and verify recovery", "Review account recovery options", "Review app permissions" })
                    check.Children.Add(new CheckBox { Content = task, Foreground = Brushes.White, Margin = new Thickness(0, 9, 0, 9) });
                break;
            case "Activity":
                var activity = Card("Session activity", "Only generic tool events are logged. Passwords and file paths are excluded.");
                activity.Children.Add(Input(report.Length == 0 ? "No recorded events." : report.ToString(), true));
                activity.Children.Add(Button("Export activity", () => { var dialog = new SaveFileDialog { Filter = "Text file|*.txt", FileName = "zuzify-activity.txt" }; if (dialog.ShowDialog() == true) { File.WriteAllText(dialog.FileName, report.ToString()); Note("Activity exported."); } }));
                activity.Children.Add(Button("Clear activity", () => { report.Clear(); ShowPage("Activity"); }));
                break;
            case "Plans":
                Card("Choose a plan", "Pricing preview only. Payments and license enforcement are not connected. Selecting a plan will not charge you.");
                foreach (var plan in new[] { ("Monthly", "$25 / month"), ("Standard", "$45 one-time"), ("Pro", "$85 one-time"), ("Elite", "$125 one-time"), ("Ultimate", "$200 one-time") })
                {
                    var pc = Card(plan.Item1, plan.Item2);
                    pc.Children.Add(Button(selectedPlan == plan.Item1 ? "Selected (preview)" : "Select plan", () => { selectedPlan = plan.Item1; ShowPage("Plans"); }, selectedPlan == plan.Item1));
                }
                break;
            case "Settings":
                var settings = Card("Appearance", "Choose an accent for the current session.");
                foreach (var theme in new[] { ("Rose", Color.FromRgb(234,94,122)), ("Violet", Color.FromRgb(146,100,240)), ("Blue", Color.FromRgb(70,135,225)), ("Graphite", Color.FromRgb(111,111,125)) })
                    settings.Children.Add(Button(theme.Item1, () => { accent.Color = theme.Item2; status.Text = "Accent updated."; }));
                Card("Privacy", "Tools run locally. This preview has no account server, tracking, billing or automatic updater.");
                break;
        }
        page.BeginAnimation(OpacityProperty, new DoubleAnimation(.35, 1, TimeSpan.FromMilliseconds(200)));
    }
}
