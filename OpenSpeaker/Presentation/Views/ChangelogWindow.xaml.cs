using System.Diagnostics;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Navigation;
namespace OpenSpeaker.Views;

public partial class ChangelogWindow : System.Windows.Window
{
    private static readonly Regex InlinePattern = new(
        @"\*\*(?<bold>.+?)\*\*|(?<!\*)\*(?<italic>[^*]+?)\*(?!\*)|`(?<code>.+?)`|\[(?<linkText>.+?)\]\((?<linkUrl>.+?)\)",
        RegexOptions.Compiled);

    public ChangelogWindow(string version, string markdown)
    {
        InitializeComponent();
        VersionLabel.Text = "Updated to v" + version;
        Render(markdown);
    }

    private void Render(string markdown)
    {
        foreach (var rawLine in markdown.Replace("\r\n", "\n").Split('\n'))
        {
            var line = rawLine.TrimEnd();

            if (string.IsNullOrWhiteSpace(line))
            {
                ContentPanel.Children.Add(new Border { Height = 6 });
                continue;
            }

            if (Regex.IsMatch(line, @"^\s*([-*_])(\s*\1){2,}\s*$"))
            {
                ContentPanel.Children.Add(new Border
                {
                    Height = 1,
                    Margin = new Thickness(0, 8, 0, 8),
                    Background = (System.Windows.Media.Brush?)TryFindResource("BorderBrush0")
                });
                continue;
            }

            if (line.StartsWith("### "))
            {
                ContentPanel.Children.Add(Header(line.Substring(4), 14, new Thickness(0, 8, 0, 2)));
                continue;
            }

            if (line.StartsWith("## "))
            {
                ContentPanel.Children.Add(Header(line.Substring(3), 16, new Thickness(0, 10, 0, 4)));
                continue;
            }

            if (line.StartsWith("# "))
            {
                ContentPanel.Children.Add(Header(line.Substring(2), 18, new Thickness(0, 10, 0, 4)));
                continue;
            }

            if (line.StartsWith("> "))
            {
                ContentPanel.Children.Add(Quote(line.Substring(2)));
                continue;
            }

            var bullet = Regex.Match(line, @"^\s*[-*+]\s+(.*)$");
            if (bullet.Success)
            {
                ContentPanel.Children.Add(Bullet(bullet.Groups[1].Value));
                continue;
            }

            ContentPanel.Children.Add(Paragraph(line));
        }
    }

    private static TextBlock Header(string text, double size, Thickness margin)
    {
        var tb = new TextBlock
        {
            FontSize = size,
            FontWeight = FontWeights.Bold,
            TextWrapping = TextWrapping.Wrap,
            Margin = margin
        };
        AddInlines(tb.Inlines, text);
        return tb;
    }

    private static TextBlock Paragraph(string text)
    {
        var tb = new TextBlock { TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 2, 0, 2) };
        AddInlines(tb.Inlines, text);
        return tb;
    }

    private TextBlock Quote(string text)
    {
        var tb = new TextBlock
        {
            TextWrapping = TextWrapping.Wrap,
            Margin = new Thickness(8, 2, 0, 2),
            Foreground = (System.Windows.Media.Brush?)TryFindResource("TextSecondary")
                ?? (System.Windows.Media.Brush?)TryFindResource("TextPrimary")
        };
        AddInlines(tb.Inlines, text);
        return tb;
    }

    private Grid Bullet(string text)
    {
        var grid = new Grid { Margin = new Thickness(2, 1, 0, 1) };
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

        var dot = new TextBlock { Text = "•", Margin = new Thickness(0, 0, 8, 0) };
        Grid.SetColumn(dot, 0);

        var body = new TextBlock { TextWrapping = TextWrapping.Wrap };
        AddInlines(body.Inlines, text);
        Grid.SetColumn(body, 1);

        grid.Children.Add(dot);
        grid.Children.Add(body);
        return grid;
    }

    private static void AddInlines(InlineCollection target, string text)
    {
        var pos = 0;
        foreach (Match m in InlinePattern.Matches(text))
        {
            if (m.Index > pos)
                target.Add(new Run(text.Substring(pos, m.Index - pos)));

            if (m.Groups["bold"].Success)
            {
                var bold = new Bold();
                AddInlines(bold.Inlines, m.Groups["bold"].Value);
                target.Add(bold);
            }
            else if (m.Groups["italic"].Success)
            {
                var italic = new Italic();
                AddInlines(italic.Inlines, m.Groups["italic"].Value);
                target.Add(italic);
            }
            else if (m.Groups["code"].Success)
            {
                target.Add(new Run(m.Groups["code"].Value) { FontFamily = new System.Windows.Media.FontFamily("Consolas") });
            }
            else if (m.Groups["linkText"].Success)
            {
                var link = new Hyperlink { NavigateUri = TryUri(m.Groups["linkUrl"].Value) };
                AddInlines(link.Inlines, m.Groups["linkText"].Value);
                link.RequestNavigate += OnNavigate;
                target.Add(link);
            }

            pos = m.Index + m.Length;
        }

        if (pos < text.Length)
            target.Add(new Run(text.Substring(pos)));
    }

    private static System.Uri? TryUri(string value)
        => System.Uri.TryCreate(value, System.UriKind.Absolute, out var uri) ? uri : null;

    private static void OnNavigate(object sender, RequestNavigateEventArgs e)
    {
        if (e.Uri == null) return;
        try { Process.Start(new ProcessStartInfo(e.Uri.AbsoluteUri) { UseShellExecute = true }); }
        catch { }
        e.Handled = true;
    }

    private void Close_Click(object sender, RoutedEventArgs e) => Close();
}
