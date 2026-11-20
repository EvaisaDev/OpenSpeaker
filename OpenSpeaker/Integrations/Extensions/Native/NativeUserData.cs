using System.Runtime.InteropServices;
using Lua;

namespace OpenSpeaker.Extensions.Native;

internal sealed class NativeUserData : ILuaUserData
{
	private readonly LuaValue[] _userValues;
	private readonly NativeLuaHost _host;
	private IntPtr _block;

	public NativeUserData(NativeLuaHost host, nuint size, int userValueCount)
	{
		_host = host;
		Size = size;
		_block = Marshal.AllocHGlobal((nint)Math.Max(size, 1));
		_userValues = userValueCount > 0 ? new LuaValue[userValueCount] : Array.Empty<LuaValue>();
	}

	public IntPtr Block => _block;

	public nuint Size { get; }

	public LuaTable? Metatable { get; set; }

	public Span<LuaValue> UserValues => _userValues;

	public bool HasFinalizer { get; set; }

	public bool Finalized { get; set; }

	~NativeUserData()
	{
		if (HasFinalizer && !Finalized && _host.QueueFinalizer(this))
		{
			GC.ReRegisterForFinalize(this);
			return;
		}
		var block = _block;
		_block = IntPtr.Zero;
		if (block != IntPtr.Zero)
			Marshal.FreeHGlobal(block);
	}
}

internal sealed class LightPtr
{
	public LightPtr(IntPtr pointer)
	{
		Pointer = pointer;
	}

	public IntPtr Pointer { get; }
}
