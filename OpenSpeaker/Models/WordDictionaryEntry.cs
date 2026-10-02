using System.ComponentModel;
using System.Runtime.CompilerServices;
using LiteDB;
namespace OpenSpeaker.Models;
public class WordDictionaryEntry : INotifyPropertyChanged
{
	private string _word = string.Empty;
	private string _replacement = string.Empty;
	private string _aliasName = string.Empty;
	private bool _enabled = true;

	[BsonId] public ObjectId Id { get; set; } = ObjectId.NewObjectId();
	public string Word        { get => _word;        set { _word        = value; OnPropertyChanged(); } }
	public string Replacement { get => _replacement; set { _replacement = value; OnPropertyChanged(); } }
	public string AliasName   { get => _aliasName;   set { _aliasName   = value; OnPropertyChanged(); OnPropertyChanged(nameof(IsGlobal)); } }
	public bool   Enabled     { get => _enabled;     set { _enabled     = value; OnPropertyChanged(); } }

	[BsonIgnore] public bool IsGlobal => string.IsNullOrWhiteSpace(_aliasName);

	public event PropertyChangedEventHandler? PropertyChanged;
	protected void OnPropertyChanged([CallerMemberName] string? name = null)
		=> PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}
