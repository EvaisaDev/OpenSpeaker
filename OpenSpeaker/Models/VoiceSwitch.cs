using System.ComponentModel;
using System.Runtime.CompilerServices;
using LiteDB;
namespace OpenSpeaker.Models;
public class VoiceSwitch : INotifyPropertyChanged
{
	private string _marker = string.Empty;
	private string _aliasName = string.Empty;
	private string _username = string.Empty;
	private bool _enabled = true;

	[BsonId] public ObjectId Id { get; set; } = ObjectId.NewObjectId();
	public string Marker    { get => _marker;    set { _marker    = value; OnPropertyChanged(); } }
	public string AliasName { get => _aliasName; set { _aliasName = value; OnPropertyChanged(); } }
	public string Username  { get => _username;  set { _username  = value; OnPropertyChanged(); OnPropertyChanged(nameof(IsGlobal)); } }
	public bool   Enabled   { get => _enabled;   set { _enabled   = value; OnPropertyChanged(); } }

	[BsonIgnore] public bool IsGlobal => string.IsNullOrWhiteSpace(_username);

	public event PropertyChangedEventHandler? PropertyChanged;
	protected void OnPropertyChanged([CallerMemberName] string? name = null)
		=> PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}
