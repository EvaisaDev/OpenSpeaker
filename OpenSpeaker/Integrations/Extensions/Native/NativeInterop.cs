using System.IO;
using System.Runtime.InteropServices;

namespace OpenSpeaker.Extensions.Native;

[UnmanagedFunctionPointer(CallingConvention.Cdecl)] internal delegate int LuaFnL(IntPtr L);
[UnmanagedFunctionPointer(CallingConvention.Cdecl)] internal delegate void LuaFnLV(IntPtr L);
[UnmanagedFunctionPointer(CallingConvention.Cdecl)] internal delegate int LuaFnLI(IntPtr L, int a);
[UnmanagedFunctionPointer(CallingConvention.Cdecl)] internal delegate void LuaFnLIV(IntPtr L, int a);
[UnmanagedFunctionPointer(CallingConvention.Cdecl)] internal delegate int LuaFnLII(IntPtr L, int a, int b);
[UnmanagedFunctionPointer(CallingConvention.Cdecl)] internal delegate void LuaFnLIIV(IntPtr L, int a, int b);
[UnmanagedFunctionPointer(CallingConvention.Cdecl)] internal delegate int LuaFnLIII(IntPtr L, int a, int b, int c);
[UnmanagedFunctionPointer(CallingConvention.Cdecl)] internal delegate void LuaFnXMove(IntPtr from, IntPtr to, int n);
[UnmanagedFunctionPointer(CallingConvention.Cdecl)] internal delegate double LuaFnToNumber(IntPtr L, int idx, IntPtr isnum);
[UnmanagedFunctionPointer(CallingConvention.Cdecl)] internal delegate long LuaFnToInteger(IntPtr L, int idx, IntPtr isnum);
[UnmanagedFunctionPointer(CallingConvention.Cdecl)] internal delegate IntPtr LuaFnLIP(IntPtr L, int idx, IntPtr p);
[UnmanagedFunctionPointer(CallingConvention.Cdecl)] internal delegate ulong LuaFnRawLen(IntPtr L, int idx);
[UnmanagedFunctionPointer(CallingConvention.Cdecl)] internal delegate IntPtr LuaFnLIRetP(IntPtr L, int idx);
[UnmanagedFunctionPointer(CallingConvention.Cdecl)] internal delegate IntPtr LuaFnLIIRetP(IntPtr L, int a, int b);
[UnmanagedFunctionPointer(CallingConvention.Cdecl)] internal delegate void LuaFnPushNumber(IntPtr L, double n);
[UnmanagedFunctionPointer(CallingConvention.Cdecl)] internal delegate void LuaFnPushInteger(IntPtr L, long n);
[UnmanagedFunctionPointer(CallingConvention.Cdecl)] internal delegate IntPtr LuaFnPushString(IntPtr L, IntPtr s, nuint len);
[UnmanagedFunctionPointer(CallingConvention.Cdecl)] internal delegate void LuaFnPushClosure(IntPtr L, IntPtr fn, int n, IntPtr name);
[UnmanagedFunctionPointer(CallingConvention.Cdecl)] internal delegate void LuaFnLP(IntPtr L, IntPtr p);
[UnmanagedFunctionPointer(CallingConvention.Cdecl)] internal delegate int LuaFnLSN(IntPtr L, IntPtr s, nuint len);
[UnmanagedFunctionPointer(CallingConvention.Cdecl)] internal delegate void LuaFnLSNV(IntPtr L, IntPtr s, nuint len);
[UnmanagedFunctionPointer(CallingConvention.Cdecl)] internal delegate int LuaFnLISN(IntPtr L, int idx, IntPtr s, nuint len);
[UnmanagedFunctionPointer(CallingConvention.Cdecl)] internal delegate void LuaFnLISNV(IntPtr L, int idx, IntPtr s, nuint len);
[UnmanagedFunctionPointer(CallingConvention.Cdecl)] internal delegate int LuaFnLIL(IntPtr L, int idx, long n);
[UnmanagedFunctionPointer(CallingConvention.Cdecl)] internal delegate void LuaFnLILV(IntPtr L, int idx, long n);
[UnmanagedFunctionPointer(CallingConvention.Cdecl)] internal delegate int LuaFnLIPRetI(IntPtr L, int idx, IntPtr p);
[UnmanagedFunctionPointer(CallingConvention.Cdecl)] internal delegate void LuaFnLIPV(IntPtr L, int idx, IntPtr p);
[UnmanagedFunctionPointer(CallingConvention.Cdecl)] internal delegate IntPtr LuaFnNewUserData(IntPtr L, nuint size, int nuv);
[UnmanagedFunctionPointer(CallingConvention.Cdecl)] internal delegate int LuaFnLoad(IntPtr L, IntPtr buf, nuint size, IntPtr chunkname, nuint namelen, IntPtr mode);
[UnmanagedFunctionPointer(CallingConvention.Cdecl)] internal delegate int LuaFnDump(IntPtr L, int strip, IntPtr outp, IntPtr outsz);
[UnmanagedFunctionPointer(CallingConvention.Cdecl)] internal delegate void LuaFnFree(IntPtr p);
[UnmanagedFunctionPointer(CallingConvention.Cdecl)] internal delegate nuint LuaFnStringToNumber(IntPtr L, IntPtr s, nuint len);
[UnmanagedFunctionPointer(CallingConvention.Cdecl)] internal delegate void LuaFnWarning(IntPtr L, IntPtr msg, nuint len, int tocont);
[UnmanagedFunctionPointer(CallingConvention.Cdecl)] internal delegate int LuaFnLPP(IntPtr L, IntPtr a, IntPtr b);
[UnmanagedFunctionPointer(CallingConvention.Cdecl)] internal delegate void LuaFnTraceback(IntPtr L, IntPtr L1, IntPtr msg, nuint len, int hasmsg, int level);
[UnmanagedFunctionPointer(CallingConvention.Cdecl)] internal delegate int LuaFnLoadFile(IntPtr L, IntPtr name, nuint len, IntPtr mode);
[UnmanagedFunctionPointer(CallingConvention.Cdecl)] internal delegate IntPtr LuaFnNewThread(IntPtr L);
[UnmanagedFunctionPointer(CallingConvention.Cdecl)] internal delegate int LuaFnResume(IntPtr L, IntPtr from, int narg, IntPtr nres);

[UnmanagedFunctionPointer(CallingConvention.Cdecl)] internal delegate int OsnInit(IntPtr table, int count);
[UnmanagedFunctionPointer(CallingConvention.Cdecl)] internal delegate int OsnHostSize();
[UnmanagedFunctionPointer(CallingConvention.Cdecl)] internal delegate IntPtr OsnNewState();
[UnmanagedFunctionPointer(CallingConvention.Cdecl)] internal delegate void OsnCloseState(IntPtr L);
[UnmanagedFunctionPointer(CallingConvention.Cdecl)] internal delegate IntPtr OsnNewThread(IntPtr main);
[UnmanagedFunctionPointer(CallingConvention.Cdecl)] internal delegate int OsnInvoke(IntPtr L, IntPtr fn);

internal static class NativeShim
{
	public const string LibraryName = "lua54.dll";

	private static readonly object Gate = new();
	private static Delegate[]? _keepAlive;
	private static string? _failure;

	public static OsnInvoke Invoke { get; private set; } = null!;
	public static OsnNewState NewState { get; private set; } = null!;
	public static OsnCloseState CloseState { get; private set; } = null!;
	public static OsnNewThread NewThread { get; private set; } = null!;

	public static string ShimPath => Path.Combine(AppContext.BaseDirectory, LibraryName);

	public static bool TryEnsureLoaded(out string error)
	{
		lock (Gate)
		{
			if (_keepAlive != null)
			{
				error = string.Empty;
				return true;
			}
			if (_failure != null)
			{
				error = _failure;
				return false;
			}
			try
			{
				Load();
				error = string.Empty;
				return true;
			}
			catch (Exception e)
			{
				_failure = $"native Lua bridge unavailable: {e.Message}";
				error = _failure;
				return false;
			}
		}
	}

	private static void Load()
	{
		var path = ShimPath;
		if (!File.Exists(path))
			throw new FileNotFoundException($"{LibraryName} was not found next to OpenSpeaker.exe", path);
		var lib = NativeLibrary.Load(path);
		var hostSize = Marshal.GetDelegateForFunctionPointer<OsnHostSize>(NativeLibrary.GetExport(lib, "osn_hostsize"));
		var init = Marshal.GetDelegateForFunctionPointer<OsnInit>(NativeLibrary.GetExport(lib, "osn_init"));
		var entries = NativeApi.CreateTable();
		if (hostSize() != entries.Length)
			throw new InvalidOperationException($"{LibraryName} does not match this OpenSpeaker build");
		var table = Marshal.AllocHGlobal(IntPtr.Size * entries.Length);
		try
		{
			for (var i = 0; i < entries.Length; i++)
				Marshal.WriteIntPtr(table, i * IntPtr.Size, Marshal.GetFunctionPointerForDelegate(entries[i]));
			if (init(table, entries.Length) == 0)
				throw new InvalidOperationException($"{LibraryName} rejected the host table");
		}
		finally
		{
			Marshal.FreeHGlobal(table);
		}
		Invoke = Marshal.GetDelegateForFunctionPointer<OsnInvoke>(NativeLibrary.GetExport(lib, "osn_invoke"));
		NewState = Marshal.GetDelegateForFunctionPointer<OsnNewState>(NativeLibrary.GetExport(lib, "osn_newstate"));
		CloseState = Marshal.GetDelegateForFunctionPointer<OsnCloseState>(NativeLibrary.GetExport(lib, "osn_closestate"));
		NewThread = Marshal.GetDelegateForFunctionPointer<OsnNewThread>(NativeLibrary.GetExport(lib, "osn_newthread"));
		_keepAlive = entries;
	}
}

internal static class Kernel32
{
	public const uint LoadWithAlteredSearchPath = 0x8;

	[DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
	public static extern IntPtr LoadLibraryExW(string fileName, IntPtr file, uint flags);

	[DllImport("kernel32.dll", CharSet = CharSet.Ansi, SetLastError = true, ExactSpelling = true)]
	public static extern IntPtr GetProcAddress(IntPtr module, string procName);

	[DllImport("kernel32.dll", SetLastError = true)]
	[return: MarshalAs(UnmanagedType.Bool)]
	public static extern bool FreeLibrary(IntPtr module);
}
