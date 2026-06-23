using System.ComponentModel;
using System.Runtime.CompilerServices;
using LiteDB;
namespace OpenSpeaker.Models;
public class RegexReplacement : INotifyPropertyChanged
{
    private string _pattern = string.Empty;
    private string _replacement = string.Empty;
    private string _mode = "Replace";
    private bool _isRegex = true;
    private bool _wholeWord = false;
    private int _order;
    private bool _enabled = true;

    [BsonId] public ObjectId Id { get; set; } = ObjectId.NewObjectId();
    public string Pattern     { get => _pattern;     set { _pattern     = value; OnPropertyChanged(); } }
    public string Replacement { get => _replacement; set { _replacement = value; OnPropertyChanged(); } }
    public string Mode        { get => _mode;        set { _mode        = value; OnPropertyChanged(); } }
    public bool   IsRegex     { get => _isRegex;     set { _isRegex     = value; OnPropertyChanged(); } }
    public bool   WholeWord   { get => _wholeWord;   set { _wholeWord   = value; OnPropertyChanged(); } }
    public int    Order       { get => _order;       set { _order       = value; OnPropertyChanged(); } }
    public bool   Enabled     { get => _enabled;     set { _enabled     = value; OnPropertyChanged(); } }

    public event PropertyChangedEventHandler? PropertyChanged;
    protected void OnPropertyChanged([CallerMemberName] string? name = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}
