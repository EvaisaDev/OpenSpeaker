using System.Collections.Concurrent;
using System.ComponentModel;
using System.IO;
using System.Runtime.CompilerServices;
using System.Runtime.ExceptionServices;
using System.Runtime.InteropServices;
using System.Text;
using Lua;
using Lua.Runtime;
using OpenSpeaker.Infrastructure.Logging;

namespace OpenSpeaker.Extensions.Native;

internal sealed class NativeFrame
{
	public LuaValue[] Slots = new LuaValue[32];
	public int Top;
	public LuaValue[]? UpValues;
	public LuaState Thread = null!;
	public Exception? Pending;
	public List<int>? Tbc;

	public void Ensure(int extra)
	{
		var need = Top + extra;
		if (need <= Slots.Length)
			return;
		var size = Slots.Length;
		while (size < need)
			size *= 2;
		Array.Resize(ref Slots, size);
	}

	public void Reset()
	{
		Array.Clear(Slots, 0, Slots.Length);
		Top = 0;
		UpValues = null;
		Pending = null;
		Tbc?.Clear();
		Thread = null!;
	}
}

internal sealed class NativeThread
{
	public NativeThread(IntPtr pointer, LuaState thread, LuaState identity)
	{
		Pointer = pointer;
		Identity = identity;
		Frame = new NativeFrame { Thread = thread };
	}

	public IntPtr Pointer { get; }

	public LuaState Identity { get; }

	public NativeFrame Frame { get; }

	public LuaState? Coroutine { get; set; }

	~NativeThread()
	{
		NativeShim.CloseState(Pointer);
	}
}

internal sealed partial class NativeLuaHost : IDisposable
{
	public const int RegistryIndex = -1001000;
	private const int HostSlotOffset = 56;

	private static readonly ConditionalWeakTable<object, StrongBox<IntPtr>> PointerIds = new();
	private static long _nextPointerId = 0x7E00_0000_0000;

	private readonly LuaState _state;
	private readonly string _extensionDir;
	private readonly IAppLogger? _logger;
	private readonly List<NativeFrame> _frames = new();
	private readonly Stack<NativeFrame> _framePool = new();
	private readonly NativeFrame _baseFrame = new();
	private readonly Dictionary<IntPtr, WeakReference<LightPtr>> _lightPointers = new();
	private int _lightPruneAt = 256;
	private readonly Dictionary<(IntPtr, string), CSharpClosure> _lightFunctions = new();
	private readonly ConditionalWeakTable<LuaFunction, StrongBox<IntPtr>> _cFunctions = new();
	private readonly ConcurrentQueue<NativeUserData> _finalizeQueue = new();
	private readonly List<WeakReference<NativeUserData>> _finalizable = new();
	private readonly Dictionary<string, IntPtr> _libraries = new(StringComparer.OrdinalIgnoreCase);
	private int _finalizablePruneAt = 64;
	private bool _draining;
	private IntPtr _L;
	private GCHandle _self;
	private volatile bool _disposed;
	private LuaFunction? _pcall;
	private LuaFunction? _resume;
	private readonly Dictionary<IntPtr, WeakReference<NativeThread>> _threads = new();
	private readonly ConditionalWeakTable<LuaState, NativeThread> _threadOf = new();
	private int _threadPruneAt = 64;
	private IntPtr _activeL;
	private NativeThread? _activeThread;
	private LuaTable? _stringMetatable;
	private StringBuilder? _warning;
	private bool _warningsOn = true;

	private NativeLuaHost(LuaState state, string extensionDir, IAppLogger? logger)
	{
		_state = state;
		_extensionDir = extensionDir;
		_logger = logger;
		_baseFrame.Thread = state;
	}

	public static NativeLuaHost Install(LuaState state, string extensionDir, IAppLogger? logger)
	{
		var host = new NativeLuaHost(state, extensionDir, logger);
		host.InstallPackage();
		return host;
	}

	private NativeFrame MainCur => _frames.Count > 0 ? _frames[^1] : _baseFrame;

	private NativeFrame Cur => _activeThread?.Frame ?? MainCur;

	internal (IntPtr L, NativeThread? Thread) Enter(IntPtr L)
	{
		var saved = (_activeL, _activeThread);
		_activeL = L;
		_activeThread = L == _L ? null : ThreadOf(L);
		return saved;
	}

	internal void Leave((IntPtr L, NativeThread? Thread) saved)
	{
		_activeL = saved.L;
		_activeThread = saved.Thread;
	}

	private NativeThread? ThreadOf(IntPtr L)
	{
		return _threads.TryGetValue(L, out var weak) && weak.TryGetTarget(out var thread) ? thread : null;
	}

	private NativeFrame FrameOf(IntPtr L)
	{
		if (L == _L)
			return MainCur;
		return ThreadOf(L)?.Frame ?? throw Error("invalid lua_State");
	}

	private NativeThread RegisterThread(LuaState thread, LuaState? coroutine)
	{
		var pointer = NativeShim.NewThread(_L);
		if (pointer == IntPtr.Zero)
			throw new OutOfMemoryException();
		var native = new NativeThread(pointer, thread, coroutine ?? thread) { Coroutine = coroutine };
		_threadOf.AddOrUpdate(native.Identity, native);
		if (_threads.Count >= _threadPruneAt)
		{
			foreach (var key in _threads.Where(p => !p.Value.TryGetTarget(out _)).Select(p => p.Key).ToList())
				_threads.Remove(key);
			_threadPruneAt = Math.Max(64, _threads.Count * 2);
		}
		_threads[pointer] = new WeakReference<NativeThread>(native);
		return native;
	}

	internal static NativeLuaHost Of(IntPtr L)
	{
		return (NativeLuaHost)GCHandle.FromIntPtr(Marshal.ReadIntPtr(L, HostSlotOffset)).Target!;
	}

	private void InstallPackage()
	{
		var env = _state.Environment;
		if (env["pcall"].TryRead<LuaFunction>(out var pcall))
			_pcall = pcall;
		if (env["getmetatable"].TryRead<LuaFunction>(out var getmetatable))
		{
			try
			{
				var r = Run(_state.CallAsync(getmetatable, new LuaValue[] { "" }));
				if (r.Length > 0 && r[0].TryRead<LuaTable>(out var meta))
					_stringMetatable = meta;
			}
			catch (Exception)
			{
			}
		}
		if (env["coroutine"].TryRead<LuaTable>(out var coroutine))
		{
			if (coroutine["yield"].Type == LuaValueType.Function)
				coroutine["yield"] = new LuaFunction("yield", Yield);
			if (coroutine["resume"].TryRead<LuaFunction>(out var resume))
				_resume = resume;
		}
		if (!env["package"].TryRead<LuaTable>(out var package))
			return;
		var dir = _extensionDir.Replace('\\', '/');
		package["cpath"] = $"{dir}/?.dll;{dir}/loadall.dll";
		package["loadlib"] = new LuaFunction("loadlib", LoadLib);
		if (package["searchers"].TryRead<LuaTable>(out var searchers))
		{
			var n = searchers.ArrayLength;
			searchers[n + 1] = new LuaFunction("searcher_C", SearcherC);
			searchers[n + 2] = new LuaFunction("searcher_Croot", SearcherCRoot);
		}
	}

	private ValueTask<int> Yield(LuaFunctionExecutionContext ctx, CancellationToken ct)
	{
		foreach (var frame in _frames)
		{
			if (ReferenceEquals(frame.Thread, ctx.State))
				throw new LuaRuntimeException(ctx.State, (LuaValue)"attempt to yield across a C-call boundary", 1);
		}
		return ctx.State.YieldAsync(ctx, ct);
	}

	private bool EnsureStarted(out string error)
	{
		if (_disposed)
		{
			error = "native module host has been shut down";
			return false;
		}
		if (_L != IntPtr.Zero)
		{
			error = string.Empty;
			return true;
		}
		if (!NativeShim.TryEnsureLoaded(out error))
			return false;
		var L = NativeShim.NewState();
		if (L == IntPtr.Zero)
		{
			error = "not enough memory";
			return false;
		}
		_self = GCHandle.Alloc(this);
		Marshal.WriteIntPtr(L, HostSlotOffset, GCHandle.ToIntPtr(_self));
		_L = L;
		var registry = _state.Registry;
		registry[1] = _state.MainThread;
		registry[2] = _state.Environment;
		return true;
	}

	internal static T Run<T>(ValueTask<T> task)
	{
		return task.IsCompletedSuccessfully ? task.Result : task.AsTask().GetAwaiter().GetResult();
	}

	internal static void Run(ValueTask task)
	{
		if (task.IsCompletedSuccessfully)
			return;
		task.AsTask().GetAwaiter().GetResult();
	}

	private LuaRuntimeException Error(string message)
	{
		return new LuaRuntimeException(Cur.Thread, (LuaValue)message, 0);
	}

	internal void Fail(Exception e)
	{
		var f = Cur;
		f.Pending = e;
		LuaValue value = e is LuaRuntimeException r && r.InnerException == null ? r.ErrorObject : (LuaValue)e.Message;
		f.Ensure(1);
		f.Slots[f.Top++] = value;
		var target = _activeL != IntPtr.Zero ? _activeL : _L;
		if (target != IntPtr.Zero)
			Marshal.WriteInt32(target, 0, 1);
	}

	private ValueTask<int> Invoke(IntPtr fn, LuaValue[] upValues, LuaFunctionExecutionContext ctx)
	{
		if (_disposed || _L == IntPtr.Zero)
			throw new LuaRuntimeException(ctx.State, (LuaValue)"native module host has been shut down", 0);
		DrainFinalizers(ctx.State);
		var frame = _framePool.Count > 0 ? _framePool.Pop() : new NativeFrame();
		frame.Thread = ctx.State;
		frame.UpValues = upValues;
		var args = ctx.Arguments;
		frame.Ensure(args.Length + 20);
		args.CopyTo(frame.Slots);
		frame.Top = args.Length;
		_frames.Add(frame);
		var active = Enter(_L);
		var sync = SynchronizationContext.Current;
		SynchronizationContext.SetSynchronizationContext(null);
		try
		{
			var n = NativeShim.Invoke(_L, fn);
			if (n < 0)
			{
				var err = frame.Top > 0 ? frame.Slots[frame.Top - 1] : LuaValue.Nil;
				var pending = frame.Pending;
				CloseAllPending(frame, err);
				if (pending != null)
					ExceptionDispatchInfo.Throw(pending);
				throw new LuaRuntimeException(ctx.State, err, 0);
			}
			n = Math.Min(n, frame.Top);
			if (frame.Tbc is { Count: > 0 })
			{
				var results = frame.Slots.AsSpan(frame.Top - n, n).ToArray();
				CloseAllPending(frame, LuaValue.Nil);
				return new ValueTask<int>(ctx.Return(results));
			}
			return new ValueTask<int>(ctx.Return(frame.Slots.AsSpan(frame.Top - n, n)));
		}
		finally
		{
			SynchronizationContext.SetSynchronizationContext(sync);
			Leave(active);
			_frames.RemoveAt(_frames.Count - 1);
			frame.Reset();
			_framePool.Push(frame);
		}
	}

	private CSharpClosure Wrap(IntPtr fn, LuaValue[] upValues, string name)
	{
		CSharpClosure? closure = null;
		closure = new CSharpClosure(name, upValues, (ctx, _) => Invoke(fn, upValues, ctx));
		_cFunctions.AddOrUpdate(closure, new StrongBox<IntPtr>(fn));
		return closure;
	}

	private CSharpClosure LightFunction(IntPtr fn, string name)
	{
		if (_lightFunctions.TryGetValue((fn, name), out var existing))
			return existing;
		var closure = Wrap(fn, Array.Empty<LuaValue>(), name);
		_lightFunctions[(fn, name)] = closure;
		return closure;
	}

	private LightPtr Light(IntPtr p)
	{
		if (_lightPointers.TryGetValue(p, out var weak) && weak.TryGetTarget(out var existing))
			return existing;
		var light = new LightPtr(p);
		_lightPointers[p] = new WeakReference<LightPtr>(light);
		if (_lightPointers.Count >= _lightPruneAt)
		{
			foreach (var key in _lightPointers.Where(kv => !kv.Value.TryGetTarget(out _)).Select(kv => kv.Key).ToList())
				_lightPointers.Remove(key);
			_lightPruneAt = Math.Max(256, _lightPointers.Count * 2);
		}
		return light;
	}

	private static IntPtr IdOf(object o)
	{
		return PointerIds.GetValue(o, _ => new StrongBox<IntPtr>((IntPtr)Interlocked.Add(ref _nextPointerId, 0x1000))).Value!;
	}

	private void CloseAllPending(NativeFrame frame, LuaValue error)
	{
		var tbc = frame.Tbc;
		if (tbc == null)
			return;
		while (tbc.Count > 0)
		{
			var idx = tbc[^1];
			tbc.RemoveAt(tbc.Count - 1);
			if (idx <= frame.Top)
				CallClose(frame, frame.Slots[idx - 1], error);
		}
	}

	private void CallClose(NativeFrame frame, LuaValue value, LuaValue error)
	{
		if (!value.ToBoolean())
			return;
		var mm = Metamethod(value, "__close");
		if (mm.Type == LuaValueType.Nil)
			return;
		Run(frame.Thread.CallAsync(mm, new[] { value, error }));
	}

	private LuaTable? MetatableOf(LuaValue v)
	{
		switch (v.Type)
		{
			case LuaValueType.Table:
				return v.Read<LuaTable>().Metatable;
			case LuaValueType.UserData:
				return v.TryRead<ILuaUserData>(out var ud) ? ud.Metatable : null;
			case LuaValueType.String:
				return _stringMetatable;
			default:
				return null;
		}
	}

	private LuaValue Metamethod(LuaValue v, string name)
	{
		var mt = MetatableOf(v);
		return mt != null && mt.TryGetValue(name, out var mm) ? mm : LuaValue.Nil;
	}

	internal bool QueueFinalizer(NativeUserData ud)
	{
		if (_disposed)
			return false;
		_finalizeQueue.Enqueue(ud);
		return true;
	}

	private void TrackFinalizer(NativeUserData ud)
	{
		ud.HasFinalizer = true;
		_finalizable.Add(new WeakReference<NativeUserData>(ud));
		if (_finalizable.Count >= _finalizablePruneAt)
		{
			_finalizable.RemoveAll(w => !w.TryGetTarget(out var t) || t.Finalized);
			_finalizablePruneAt = Math.Max(64, _finalizable.Count * 2);
		}
	}

	private void DrainFinalizers(LuaState thread)
	{
		if (_draining || _finalizeQueue.IsEmpty)
			return;
		_draining = true;
		try
		{
			while (_finalizeQueue.TryDequeue(out var ud))
				RunFinalizer(thread, ud);
		}
		finally
		{
			_draining = false;
		}
	}

	private void RunFinalizer(LuaState thread, NativeUserData ud)
	{
		if (ud.Finalized)
			return;
		ud.Finalized = true;
		var mt = ud.Metatable;
		if (mt == null || !mt.TryGetValue("__gc", out var gc) || gc.Type == LuaValueType.Nil)
			return;
		try
		{
			if (_pcall == null)
			{
				Run(thread.CallAsync(gc, new[] { LuaValue.FromUserData(ud) }));
				return;
			}
			var r = Run(thread.CallAsync(_pcall, new[] { gc, LuaValue.FromUserData(ud) }));
			if (r.Length > 1 && !r[0].ToBoolean())
				Warn($"error in __gc metamethod ({r[1]})");
		}
		catch (Exception e)
		{
			Warn($"error in __gc metamethod ({e.Message})");
		}
	}

	private void Warn(string message)
	{
		_logger?.Warn("[Lua native] " + message);
	}

	private ValueTask<int> SearcherC(LuaFunctionExecutionContext ctx, CancellationToken ct)
	{
		var name = ctx.GetArgument<string>(0);
		var file = SearchPath(name);
		if (file == null)
			return new ValueTask<int>(ctx.Return());
		var loader = CreateLoader(ctx.State, name, file, false);
		return new ValueTask<int>(ctx.Return(loader!, file));
	}

	private ValueTask<int> SearcherCRoot(LuaFunctionExecutionContext ctx, CancellationToken ct)
	{
		var name = ctx.GetArgument<string>(0);
		var dot = name.IndexOf('.');
		if (dot < 0)
			return new ValueTask<int>(ctx.Return());
		var file = SearchPath(name[..dot]);
		if (file == null)
			return new ValueTask<int>(ctx.Return());
		var loader = CreateLoader(ctx.State, name, file, true);
		if (loader == null)
			return new ValueTask<int>(ctx.Return($"\n\tno module '{name}' in file '{file}'"));
		return new ValueTask<int>(ctx.Return(loader, file));
	}

	private string? SearchPath(string name)
	{
		if (!_state.Environment["package"].TryRead<LuaTable>(out var package) || !package["cpath"].TryRead<string>(out var cpath))
			return null;
		var stem = name.Replace('.', Path.DirectorySeparatorChar);
		foreach (var template in cpath.Split(';', StringSplitOptions.RemoveEmptyEntries))
		{
			var candidate = template.Replace("?", stem);
			if (File.Exists(candidate))
				return candidate;
		}
		return null;
	}

	private LuaFunction? CreateLoader(LuaState thread, string modname, string file, bool optional)
	{
		IntPtr lib;
		try
		{
			if (!EnsureStarted(out var error))
				throw new InvalidOperationException(error);
			lib = LoadLibrary(file);
		}
		catch (Exception e)
		{
			throw new LuaRuntimeException(thread, (LuaValue)$"error loading module '{modname}' from file '{file}':\n\t{e.Message}", 0);
		}
		var symbol = FindOpenFunction(lib, modname, out var symbolName);
		if (symbol == IntPtr.Zero)
		{
			if (optional)
				return null;
			throw new LuaRuntimeException(thread, (LuaValue)$"error loading module '{modname}' from file '{file}':\n\tthe procedure '{symbolName}' could not be found", 0);
		}
		var opener = Wrap(symbol, Array.Empty<LuaValue>(), symbolName);
		return new LuaFunction(modname, async (ctx, ct) =>
		{
			var results = await ctx.State.CallAsync(opener, new LuaValue[] { modname, file }, ct);
			return ctx.Return(results);
		});
	}

	private static IntPtr FindOpenFunction(IntPtr lib, string modname, out string symbolName)
	{
		var mod = modname.Replace('.', '_');
		var mark = mod.IndexOf('-');
		if (mark >= 0)
		{
			symbolName = "luaopen_" + mod[..mark];
			var p = Kernel32.GetProcAddress(lib, symbolName);
			if (p != IntPtr.Zero)
				return p;
			mod = mod[(mark + 1)..];
		}
		symbolName = "luaopen_" + mod;
		return Kernel32.GetProcAddress(lib, symbolName);
	}

	private IntPtr LoadLibrary(string path)
	{
		var full = Path.GetFullPath(path);
		if (_libraries.TryGetValue(full, out var existing))
			return existing;
		var handle = Kernel32.LoadLibraryExW(full, IntPtr.Zero, Kernel32.LoadWithAlteredSearchPath);
		if (handle == IntPtr.Zero)
			throw new Win32Exception(Marshal.GetLastWin32Error());
		_libraries[full] = handle;
		return handle;
	}

	private ValueTask<int> LoadLib(LuaFunctionExecutionContext ctx, CancellationToken ct)
	{
		var path = ctx.GetArgument<string>(0);
		var func = ctx.GetArgument<string>(1);
		if (!Path.IsPathRooted(path))
			path = Path.Combine(_extensionDir, path);
		if (!EnsureStarted(out var error))
			return new ValueTask<int>(ctx.Return(LuaValue.Nil, error, "open"));
		IntPtr lib;
		try
		{
			lib = LoadLibrary(path);
		}
		catch (Exception e)
		{
			return new ValueTask<int>(ctx.Return(LuaValue.Nil, e.Message, "open"));
		}
		if (func == "*")
			return new ValueTask<int>(ctx.Return(true));
		var symbol = Kernel32.GetProcAddress(lib, func);
		if (symbol == IntPtr.Zero)
			return new ValueTask<int>(ctx.Return(LuaValue.Nil, $"the procedure '{func}' could not be found", "init"));
		return new ValueTask<int>(ctx.Return(Wrap(symbol, Array.Empty<LuaValue>(), func)));
	}

	public void Dispose()
	{
		if (_disposed)
			return;
		if (_L != IntPtr.Zero)
		{
			try
			{
				DrainFinalizers(_state);
				for (var i = _finalizable.Count - 1; i >= 0; i--)
				{
					if (_finalizable[i].TryGetTarget(out var ud) && !ud.Finalized)
						RunFinalizer(_state, ud);
				}
			}
			catch (Exception e)
			{
				Warn($"error while closing native modules ({e.Message})");
			}
		}
		_disposed = true;
		_finalizable.Clear();
		_finalizeQueue.Clear();
		foreach (var lib in _libraries.Values)
			Kernel32.FreeLibrary(lib);
		_libraries.Clear();
		if (_L != IntPtr.Zero)
		{
			NativeShim.CloseState(_L);
			_L = IntPtr.Zero;
		}
		if (_self.IsAllocated)
			_self.Free();
	}
}
