using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;
using Lua;
using Lua.Runtime;

namespace OpenSpeaker.Extensions.Native;

internal sealed partial class NativeLuaHost
{
	private const int TypeNone = -1;
	private const int MultRet = -1;
	private const double TwoPow63 = 9223372036854775808.0;

	private static readonly MethodInfo? GetFuncNameMethod = typeof(LuaState).Assembly
		.GetType("Lua.Internal.LuaDebug")?
		.GetMethod("GetFuncName", BindingFlags.NonPublic | BindingFlags.Static);

	private LuaValue Get(int idx)
	{
		var f = Cur;
		if (idx > 0)
			return idx <= f.Top ? f.Slots[idx - 1] : LuaValue.Nil;
		if (idx > RegistryIndex)
		{
			var i = f.Top + idx;
			return idx != 0 && i >= 0 ? f.Slots[i] : LuaValue.Nil;
		}
		if (idx == RegistryIndex)
			return _state.Registry;
		var u = RegistryIndex - idx;
		var ups = f.UpValues;
		return ups != null && u <= ups.Length ? ups[u - 1] : LuaValue.Nil;
	}

	private bool IsValid(int idx)
	{
		var f = Cur;
		if (idx > 0)
			return idx <= f.Top;
		if (idx > RegistryIndex)
			return idx != 0 && f.Top + idx >= 0;
		if (idx == RegistryIndex)
			return true;
		var u = RegistryIndex - idx;
		return f.UpValues != null && u <= f.UpValues.Length;
	}

	private int Abs(int idx)
	{
		return idx > 0 || idx <= RegistryIndex ? idx : Cur.Top + idx + 1;
	}

	private void Set(int idx, LuaValue value)
	{
		var f = Cur;
		if (idx > 0 && idx <= f.Top)
		{
			f.Slots[idx - 1] = value;
			return;
		}
		if (idx < 0 && idx > RegistryIndex && f.Top + idx >= 0)
		{
			f.Slots[f.Top + idx] = value;
			return;
		}
		if (idx < RegistryIndex)
		{
			var u = RegistryIndex - idx;
			if (f.UpValues != null && u <= f.UpValues.Length)
			{
				f.UpValues[u - 1] = value;
				return;
			}
		}
		throw Error("invalid stack index");
	}

	private void Push(LuaValue value)
	{
		var f = Cur;
		f.Ensure(1);
		f.Slots[f.Top++] = value;
	}

	private LuaValue Pop()
	{
		var f = Cur;
		if (f.Top == 0)
			throw Error("stack underflow in native call");
		var v = f.Slots[--f.Top];
		f.Slots[f.Top] = default;
		return v;
	}

	private LuaValue[] PopN(int n)
	{
		var f = Cur;
		if (n < 0 || n > f.Top)
			throw Error("stack underflow in native call");
		var values = f.Slots.AsSpan(f.Top - n, n).ToArray();
		Array.Clear(f.Slots, f.Top - n, n);
		f.Top -= n;
		return values;
	}

	private void PushResults(LuaValue[] results, int start, int nresults)
	{
		var count = results.Length - start;
		if (nresults == MultRet)
		{
			for (var i = 0; i < count; i++)
				Push(results[start + i]);
			return;
		}
		for (var i = 0; i < nresults; i++)
			Push(i < count ? results[start + i] : LuaValue.Nil);
	}

	private static int TypeCode(LuaValue v)
	{
		return v.Type switch
		{
			LuaValueType.Nil => 0,
			LuaValueType.Boolean => 1,
			LuaValueType.LightUserData => 2,
			LuaValueType.Number => 3,
			LuaValueType.String => 4,
			LuaValueType.Table => 5,
			LuaValueType.Function => 6,
			LuaValueType.UserData => 7,
			LuaValueType.Thread => 8,
			_ => TypeNone,
		};
	}

	private static LuaValue Integer(long n)
	{
		return (double)n;
	}

	private static bool TryNumber(LuaValue v, out double d)
	{
		if (v.Type == LuaValueType.Number)
		{
			d = v.Read<double>();
			return true;
		}
		if (v.Type == LuaValueType.String)
			return LuaBytes.TryParseNumber(v.Read<string>(), out d);
		d = 0;
		return false;
	}

	private static bool TryInteger(double d, out long n)
	{
		if (d >= -TwoPow63 && d < TwoPow63 && Math.Floor(d) == d)
		{
			n = (long)d;
			return true;
		}
		n = 0;
		return false;
	}

	private static bool TryInteger(LuaValue v, out long n)
	{
		if (TryNumber(v, out var d))
			return TryInteger(d, out n);
		n = 0;
		return false;
	}

	private LuaTable TableAt(int idx)
	{
		if (Get(idx).TryRead<LuaTable>(out var t))
			return t;
		throw Error("table expected");
	}

	private static LuaValue CheckKey(LuaValue key)
	{
		if (key.Type == LuaValueType.Nil)
			throw new LuaRuntimeException(null, (LuaValue)"index is nil", 0);
		if (key.Type == LuaValueType.Number && double.IsNaN(key.Read<double>()))
			throw new LuaRuntimeException(null, (LuaValue)"index is NaN", 0);
		return key;
	}

	private static LuaValue RawGet(LuaTable t, LuaValue key)
	{
		if (key.Type == LuaValueType.Nil)
			return LuaValue.Nil;
		if (key.Type == LuaValueType.Number && double.IsNaN(key.Read<double>()))
			return LuaValue.Nil;
		return t.TryGetValue(key, out var v) ? v : LuaValue.Nil;
	}

	private static long Border(LuaTable t)
	{
		long n = t.ArrayLength;
		while (t.TryGetValue(n + 1, out var v) && v.Type != LuaValueType.Nil)
			n++;
		return n;
	}

	private static string? TypeName(LuaValue v)
	{
		return LuaValue.ToString(v.Type);
	}

	internal int ApiAbsIndex(int idx) => Abs(idx);

	internal int ApiGetTop() => Cur.Top;

	internal void ApiSetTop(int idx)
	{
		var f = Cur;
		var newTop = idx >= 0 ? idx : f.Top + idx + 1;
		if (newTop < 0)
			throw Error("invalid new top");
		if (newTop < f.Top)
		{
			if (f.Tbc is { Count: > 0 })
			{
				while (f.Tbc.Count > 0 && f.Tbc[^1] > newTop)
				{
					var slot = f.Tbc[^1];
					f.Tbc.RemoveAt(f.Tbc.Count - 1);
					if (slot <= f.Top)
						CallClose(f, f.Slots[slot - 1], LuaValue.Nil);
				}
			}
			Array.Clear(f.Slots, newTop, f.Top - newTop);
			f.Top = newTop;
			return;
		}
		f.Ensure(newTop - f.Top);
		f.Top = newTop;
	}

	internal void ApiPushValue(int idx) => Push(Get(idx));

	internal void ApiRotate(int idx, int n)
	{
		var f = Cur;
		var start = Abs(idx) - 1;
		var end = f.Top - 1;
		if (start < 0 || start > end)
			throw Error("invalid stack index");
		var segment = end - start + 1;
		n %= segment;
		if (n < 0)
			n += segment;
		if (n == 0)
			return;
		var span = f.Slots.AsSpan(start, segment);
		span.Reverse();
		span[..n].Reverse();
		span[n..].Reverse();
	}

	internal void ApiCopy(int from, int to) => Set(to, Get(from));

	internal int ApiIsNumber(int idx) => TryNumber(Get(idx), out _) ? 1 : 0;

	internal int ApiIsString(int idx)
	{
		var t = Get(idx).Type;
		return t == LuaValueType.String || t == LuaValueType.Number ? 1 : 0;
	}

	internal int ApiIsCFunction(int idx)
	{
		var v = Get(idx);
		return v.Type == LuaValueType.Function && v.Read<LuaFunction>() is not LuaClosure ? 1 : 0;
	}

	internal int ApiIsInteger(int idx)
	{
		var v = Get(idx);
		return v.Type == LuaValueType.Number && TryInteger(v.Read<double>(), out _) ? 1 : 0;
	}

	internal int ApiIsUserData(int idx)
	{
		var t = Get(idx).Type;
		return t == LuaValueType.UserData || t == LuaValueType.LightUserData ? 1 : 0;
	}

	internal int ApiType(int idx) => IsValid(idx) ? TypeCode(Get(idx)) : TypeNone;

	internal double ApiToNumber(int idx, IntPtr isnum)
	{
		var ok = TryNumber(Get(idx), out var d);
		if (isnum != IntPtr.Zero)
			Marshal.WriteInt32(isnum, ok ? 1 : 0);
		return ok ? d : 0;
	}

	internal long ApiToInteger(int idx, IntPtr isnum)
	{
		var ok = TryInteger(Get(idx), out var n);
		if (isnum != IntPtr.Zero)
			Marshal.WriteInt32(isnum, ok ? 1 : 0);
		return ok ? n : 0;
	}

	internal int ApiToBoolean(int idx) => Get(idx).ToBoolean() ? 1 : 0;

	internal IntPtr ApiToLString(int idx, IntPtr len)
	{
		var v = Get(idx);
		string s;
		if (v.Type == LuaValueType.String)
			s = v.Read<string>();
		else if (v.Type == LuaValueType.Number)
		{
			s = v.ToString();
			if (IsValid(idx) && idx > RegistryIndex)
				Set(idx, s);
		}
		else
		{
			if (len != IntPtr.Zero)
				Marshal.WriteIntPtr(len, IntPtr.Zero);
			return IntPtr.Zero;
		}
		var native = LuaBytes.Native(s);
		if (len != IntPtr.Zero)
			Marshal.WriteIntPtr(len, native.Length);
		return native.Pointer;
	}

	internal ulong ApiRawLen(int idx)
	{
		var v = Get(idx);
		switch (v.Type)
		{
			case LuaValueType.String:
				return (ulong)LuaBytes.ByteLength(v.Read<string>());
			case LuaValueType.Table:
				return (ulong)Border(v.Read<LuaTable>());
			case LuaValueType.UserData:
				return v.TryRead<NativeUserData>(out var ud) ? ud.Size : 0;
			default:
				return 0;
		}
	}

	internal IntPtr ApiToCFunction(int idx)
	{
		var v = Get(idx);
		if (v.Type == LuaValueType.Function && _cFunctions.TryGetValue(v.Read<LuaFunction>(), out var box))
			return box.Value;
		return IntPtr.Zero;
	}

	internal IntPtr ApiToUserData(int idx)
	{
		var v = Get(idx);
		if (v.Type == LuaValueType.UserData && v.TryRead<NativeUserData>(out var ud))
			return ud.Block;
		if (v.Type == LuaValueType.LightUserData && v.TryRead<LightPtr>(out var light))
			return light.Pointer;
		return IntPtr.Zero;
	}

	internal IntPtr ApiToThread(int idx)
	{
		var v = Get(idx);
		if (v.Type != LuaValueType.Thread || !v.TryRead<LuaState>(out var s))
			return IntPtr.Zero;
		if (ReferenceEquals(s, _state.MainThread))
			return _L;
		if (_threadOf.TryGetValue(s, out var known))
			return known.Pointer;
		return s.IsCoroutine ? RegisterThread(_state.CreateThread(), s).Pointer : _L;
	}

	internal IntPtr ApiNewThread()
	{
		var native = RegisterThread(_state.CreateThread(), null);
		Push(native.Identity);
		return native.Pointer;
	}

	internal void ApiXMove(IntPtr to, int n)
	{
		var src = Cur;
		var dst = FrameOf(to);
		if (ReferenceEquals(src, dst) || n == 0)
			return;
		if (n < 0 || n > src.Top)
			throw Error("stack underflow in lua_xmove");
		dst.Ensure(n);
		Array.Copy(src.Slots, src.Top - n, dst.Slots, dst.Top, n);
		Array.Clear(src.Slots, src.Top - n, n);
		src.Top -= n;
		dst.Top += n;
	}

	internal void ApiCloseThread()
	{
		var thread = _activeThread;
		if (thread == null)
			return;
		ApiSetTop(0);
		thread.Frame.Pending = null;
		if (!ReferenceEquals(thread.Coroutine, thread.Identity))
			thread.Coroutine = null;
	}

	internal int ApiResume(int narg, IntPtr nres)
	{
		var thread = _activeThread;
		var f = Cur;
		if (nres != IntPtr.Zero)
			Marshal.WriteInt32(nres, 0);
		if (thread == null)
			return ResumeFailed(f, "cannot resume non-suspended coroutine");
		if (narg < 0 || narg > f.Top)
			return ResumeFailed(f, "stack underflow in lua_resume");
		var co = thread.Coroutine;
		if (co == null || co.GetStatus() == LuaThreadStatus.Dead)
		{
			if (ReferenceEquals(co, thread.Identity))
				return ResumeFailed(f, "cannot resume dead coroutine");
			var fnIndex = f.Top - narg - 1;
			if (fnIndex < 0 || !f.Slots[fnIndex].TryRead<LuaFunction>(out var fn))
				return ResumeFailed(f, "cannot resume dead coroutine");
			co = _state.CreateCoroutine(fn, true);
			thread.Coroutine = co;
			Array.Copy(f.Slots, fnIndex + 1, f.Slots, fnIndex, narg);
			f.Slots[--f.Top] = default;
		}
		else if (co.GetStatus() != LuaThreadStatus.Suspended)
		{
			return ResumeFailed(f, "cannot resume non-suspended coroutine");
		}
		if (_resume == null)
			return ResumeFailed(f, "coroutine library is unavailable");
		var args = new LuaValue[narg + 1];
		args[0] = co;
		PopN(narg).CopyTo(args, 1);
		LuaValue[] results;
		try
		{
			results = Run(f.Thread.CallAsync(_resume, args));
		}
		catch (Exception e)
		{
			ApiSetTop(0);
			Push(e is LuaRuntimeException r && r.InnerException == null ? r.ErrorObject : (LuaValue)e.Message);
			return 2;
		}
		if (results.Length == 0 || !results[0].ToBoolean())
		{
			ApiSetTop(0);
			Push(results.Length > 1 ? results[1] : LuaValue.Nil);
			return 2;
		}
		f.Ensure(results.Length);
		for (var i = 1; i < results.Length; i++)
			f.Slots[f.Top++] = results[i];
		if (nres != IntPtr.Zero)
			Marshal.WriteInt32(nres, results.Length - 1);
		return co.GetStatus() == LuaThreadStatus.Suspended ? 1 : 0;
	}

	private int ResumeFailed(NativeFrame f, string message)
	{
		f.Ensure(1);
		f.Slots[f.Top++] = message;
		return 2;
	}

	internal IntPtr ApiToPointer(int idx)
	{
		var v = Get(idx);
		switch (v.Type)
		{
			case LuaValueType.UserData:
				return v.TryRead<NativeUserData>(out var ud) ? ud.Block : IdOf(v.Read<object>());
			case LuaValueType.LightUserData:
				return v.TryRead<LightPtr>(out var light) ? light.Pointer : IdOf(v.Read<object>());
			case LuaValueType.String:
				return LuaBytes.Native(v.Read<string>()).Pointer;
			case LuaValueType.Table:
			case LuaValueType.Function:
			case LuaValueType.Thread:
				return IdOf(v.Read<object>());
			default:
				return IntPtr.Zero;
		}
	}

	internal void ApiArith(int op)
	{
		var thread = Cur.Thread;
		if (op == 12 || op == 13)
		{
			var a = Pop();
			Push(op == 12 ? Run(thread.UnmAsync(a)) : BitNot(thread, a));
			return;
		}
		var y = Pop();
		var x = Pop();
		LuaValue r = op switch
		{
			0 => Run(thread.AddAsync(x, y)),
			1 => Run(thread.SubAsync(x, y)),
			2 => Run(thread.MulAsync(x, y)),
			3 => Run(thread.ModAsync(x, y)),
			4 => Run(thread.PowAsync(x, y)),
			5 => Run(thread.DivAsync(x, y)),
			6 => IntDiv(thread, x, y),
			7 or 8 or 9 or 10 or 11 => Bitwise(thread, op, x, y),
			_ => throw Error("invalid arithmetic operator"),
		};
		Push(r);
	}

	private LuaValue IntDiv(LuaState thread, LuaValue x, LuaValue y)
	{
		if (TryNumber(x, out var a) && TryNumber(y, out var b))
			return Math.Floor(a / b);
		return BinaryMetamethod(thread, "__idiv", x, y, "perform arithmetic on");
	}

	private LuaValue Bitwise(LuaState thread, int op, LuaValue x, LuaValue y)
	{
		var events = new[] { "__band", "__bor", "__bxor", "__shl", "__shr" };
		var ev = events[op - 7];
		if (!TryNumber(x, out var dx) || !TryNumber(y, out var dy))
			return BinaryMetamethod(thread, ev, x, y, "perform bitwise operation on");
		if (!TryInteger(dx, out var a) || !TryInteger(dy, out var b))
		{
			var mm = Metamethod(x, ev);
			if (mm.Type == LuaValueType.Nil)
				mm = Metamethod(y, ev);
			if (mm.Type != LuaValueType.Nil)
				return First(Run(thread.CallAsync(mm, new[] { x, y })));
			throw Error("number has no integer representation");
		}
		return op switch
		{
			7 => Integer(a & b),
			8 => Integer(a | b),
			9 => Integer(a ^ b),
			10 => Integer(ShiftLeft(a, b)),
			_ => Integer(ShiftLeft(a, b == long.MinValue ? long.MaxValue : -b)),
		};
	}

	private static long ShiftLeft(long x, long y)
	{
		if (y <= -64 || y >= 64)
			return 0;
		if (y >= 0)
			return (long)((ulong)x << (int)y);
		return (long)((ulong)x >> (int)-y);
	}

	private LuaValue BitNot(LuaState thread, LuaValue x)
	{
		if (TryNumber(x, out var d))
		{
			if (TryInteger(d, out var n))
				return Integer(~n);
			throw Error("number has no integer representation");
		}
		var mm = Metamethod(x, "__bnot");
		if (mm.Type != LuaValueType.Nil)
			return First(Run(thread.CallAsync(mm, new[] { x, x })));
		throw Error($"attempt to perform bitwise operation on a {TypeName(x)} value");
	}

	private LuaValue BinaryMetamethod(LuaState thread, string ev, LuaValue x, LuaValue y, string verb)
	{
		var mm = Metamethod(x, ev);
		if (mm.Type == LuaValueType.Nil)
			mm = Metamethod(y, ev);
		if (mm.Type == LuaValueType.Nil)
		{
			var bad = TryNumber(x, out _) ? y : x;
			throw Error($"attempt to {verb} a {TypeName(bad)} value");
		}
		return First(Run(thread.CallAsync(mm, new[] { x, y })));
	}

	private static LuaValue First(LuaValue[] values) => values.Length > 0 ? values[0] : LuaValue.Nil;

	internal int ApiRawEqual(int a, int b)
	{
		if (!IsValid(a) || !IsValid(b))
			return 0;
		return Get(a) == Get(b) ? 1 : 0;
	}

	internal int ApiCompare(int a, int b, int op)
	{
		if (!IsValid(a) || !IsValid(b))
			return 0;
		var x = Get(a);
		var y = Get(b);
		var thread = Cur.Thread;
		return op switch
		{
			0 => Run(thread.EqualsAsync(x, y)) ? 1 : 0,
			1 => Run(thread.LessThanAsync(x, y)) ? 1 : 0,
			2 => Run(thread.LessThanOrEqualsAsync(x, y)) ? 1 : 0,
			_ => throw Error("invalid comparison operator"),
		};
	}

	internal void ApiPushNil() => Push(LuaValue.Nil);

	internal void ApiPushNumber(double n) => Push(n);

	internal void ApiPushInteger(long n) => Push(Integer(n));

	internal IntPtr ApiPushLString(IntPtr s, nuint len)
	{
		var str = LuaBytes.FromPointer(s, len, true);
		Push(str);
		return LuaBytes.Native(str).Pointer;
	}

	internal void ApiPushCClosure(IntPtr fn, int n, IntPtr name)
	{
		var fname = LuaBytes.FromCString(name) ?? "?";
		if (n <= 0)
		{
			Push(LightFunction(fn, fname));
			return;
		}
		Push(Wrap(fn, PopN(n), fname));
	}

	internal void ApiPushBoolean(int b) => Push(b != 0);

	internal void ApiPushLightUserData(IntPtr p) => Push(LuaValue.FromObject(Light(p)));

	internal int ApiPushThread()
	{
		var thread = _activeThread?.Identity ?? Cur.Thread;
		Push(thread);
		return ReferenceEquals(thread, _state.MainThread) ? 1 : 0;
	}

	internal int ApiGetGlobal(IntPtr name, nuint len)
	{
		var v = Run(Cur.Thread.GetTableAsync(_state.Environment, LuaBytes.FromPointer(name, len)));
		Push(v);
		return TypeCode(v);
	}

	internal int ApiGetTable(int idx)
	{
		var t = Get(idx);
		var key = Pop();
		var v = Run(Cur.Thread.GetTableAsync(t, key));
		Push(v);
		return TypeCode(v);
	}

	internal int ApiGetField(int idx, IntPtr k, nuint len)
	{
		var t = Get(idx);
		var v = Run(Cur.Thread.GetTableAsync(t, LuaBytes.FromPointer(k, len)));
		Push(v);
		return TypeCode(v);
	}

	internal int ApiGetI(int idx, long n)
	{
		var t = Get(idx);
		var v = Run(Cur.Thread.GetTableAsync(t, Integer(n)));
		Push(v);
		return TypeCode(v);
	}

	internal int ApiRawGet(int idx)
	{
		var t = TableAt(idx);
		var v = RawGet(t, Pop());
		Push(v);
		return TypeCode(v);
	}

	internal int ApiRawGetI(int idx, long n)
	{
		var v = RawGet(TableAt(idx), Integer(n));
		Push(v);
		return TypeCode(v);
	}

	internal int ApiRawGetP(int idx, IntPtr p)
	{
		var t = TableAt(idx);
		var v = _lightPointers.TryGetValue(p, out var weak) && weak.TryGetTarget(out var light) ? RawGet(t, LuaValue.FromObject(light)) : LuaValue.Nil;
		Push(v);
		return TypeCode(v);
	}

	internal void ApiCreateTable(int narr, int nrec) => Push(new LuaTable(Math.Max(narr, 0), Math.Max(nrec, 0)));

	internal IntPtr ApiNewUserData(nuint size, int nuv)
	{
		var ud = new NativeUserData(this, size, Math.Max(nuv, 0));
		Push(LuaValue.FromUserData(ud));
		return ud.Block;
	}

	internal int ApiGetMetatable(int idx)
	{
		var mt = MetatableOf(Get(idx));
		if (mt == null)
			return 0;
		Push(mt);
		return 1;
	}

	internal int ApiGetIUserValue(int idx, int n)
	{
		var v = Get(idx);
		if (v.Type == LuaValueType.UserData && v.TryRead<NativeUserData>(out var ud) && n >= 1 && n <= ud.UserValues.Length)
		{
			var uv = ud.UserValues[n - 1];
			Push(uv);
			return TypeCode(uv);
		}
		Push(LuaValue.Nil);
		return TypeNone;
	}

	internal void ApiSetGlobal(IntPtr name, nuint len)
	{
		var key = LuaBytes.FromPointer(name, len);
		var v = Pop();
		Run(Cur.Thread.SetTableAsync(_state.Environment, key, v));
	}

	internal void ApiSetTable(int idx)
	{
		var t = Get(idx);
		var v = Pop();
		var key = Pop();
		Run(Cur.Thread.SetTableAsync(t, key, v));
	}

	internal void ApiSetField(int idx, IntPtr k, nuint len)
	{
		var t = Get(idx);
		var key = LuaBytes.FromPointer(k, len);
		var v = Pop();
		Run(Cur.Thread.SetTableAsync(t, key, v));
	}

	internal void ApiSetI(int idx, long n)
	{
		var t = Get(idx);
		var v = Pop();
		Run(Cur.Thread.SetTableAsync(t, Integer(n), v));
	}

	internal void ApiRawSet(int idx)
	{
		var t = TableAt(idx);
		var v = Pop();
		var key = CheckKey(Pop());
		t[key] = v;
	}

	internal void ApiRawSetI(int idx, long n)
	{
		var t = TableAt(idx);
		var v = Pop();
		t[Integer(n)] = v;
	}

	internal void ApiRawSetP(int idx, IntPtr p)
	{
		var t = TableAt(idx);
		var v = Pop();
		t[LuaValue.FromObject(Light(p))] = v;
	}

	internal int ApiSetMetatable(int idx)
	{
		var target = Get(idx);
		var mtValue = Pop();
		LuaTable? mt = null;
		if (mtValue.Type == LuaValueType.Table)
			mt = mtValue.Read<LuaTable>();
		else if (mtValue.Type != LuaValueType.Nil)
			throw Error("table expected");
		if (target.Type == LuaValueType.Table)
			target.Read<LuaTable>().Metatable = mt;
		else if (target.Type == LuaValueType.UserData && target.TryRead<ILuaUserData>(out var ud))
		{
			ud.Metatable = mt;
			if (ud is NativeUserData native && !native.HasFinalizer && mt != null && mt.TryGetValue("__gc", out var gc) && gc.Type != LuaValueType.Nil)
				TrackFinalizer(native);
		}
		else if (target.Type == LuaValueType.String)
			_stringMetatable = mt;
		return 1;
	}

	internal int ApiSetIUserValue(int idx, int n)
	{
		var target = Get(idx);
		var v = Pop();
		if (target.Type == LuaValueType.UserData && target.TryRead<NativeUserData>(out var ud) && n >= 1 && n <= ud.UserValues.Length)
		{
			ud.UserValues[n - 1] = v;
			return 1;
		}
		return 0;
	}

	internal void ApiCall(int nargs, int nresults)
	{
		var f = Cur;
		var args = PopN(nargs);
		var func = Pop();
		var results = Run(f.Thread.CallAsync(func, args));
		PushResults(results, 0, nresults);
	}

	internal int ApiPCall(int nargs, int nresults, int errfunc)
	{
		var f = Cur;
		var handler = errfunc != 0 ? Get(errfunc) : LuaValue.Nil;
		var args = PopN(nargs);
		var func = Pop();
		if (_pcall == null)
		{
			var direct = Run(f.Thread.CallAsync(func, args));
			PushResults(direct, 0, nresults);
			return 0;
		}
		var callArgs = new LuaValue[args.Length + 1];
		callArgs[0] = func;
		args.CopyTo(callArgs, 1);
		var results = Run(f.Thread.CallAsync(_pcall, callArgs));
		if (results.Length > 0 && results[0].ToBoolean())
		{
			PushResults(results, 1, nresults);
			return 0;
		}
		var err = results.Length > 1 ? results[1] : LuaValue.Nil;
		if (handler.Type == LuaValueType.Nil)
		{
			Push(err);
			return 2;
		}
		var handled = Run(f.Thread.CallAsync(_pcall, new[] { handler, err }));
		if (handled.Length > 0 && handled[0].ToBoolean())
		{
			Push(handled.Length > 1 ? handled[1] : LuaValue.Nil);
			return 2;
		}
		Push(handled.Length > 1 ? handled[1] : LuaValue.Nil);
		return 5;
	}

	internal int ApiLoad(IntPtr buf, nuint size, IntPtr chunkname, nuint namelen, IntPtr mode)
	{
		var bytes = LuaBytes.CopyBytes(buf, size);
		var name = LuaBytes.FromPointer(chunkname, namelen);
		var m = LuaBytes.FromCString(mode) ?? "bt";
		try
		{
			Push(Cur.Thread.Load(bytes, name, m, null));
			return 0;
		}
		catch (OperationCanceledException)
		{
			throw;
		}
		catch (Exception e)
		{
			Push(e.Message);
			return 3;
		}
	}

	internal int ApiDump(int strip, IntPtr outp, IntPtr outsz)
	{
		var v = Get(-1);
		if (v.Type != LuaValueType.Function || v.Read<LuaFunction>() is not LuaClosure closure)
			return 1;
		var bytes = closure.Proto.ToBytecode();
		var block = Marshal.AllocHGlobal(Math.Max(bytes.Length, 1));
		Marshal.Copy(bytes, 0, block, bytes.Length);
		Marshal.WriteIntPtr(outp, block);
		Marshal.WriteIntPtr(outsz, bytes.Length);
		return 0;
	}

	internal static void ApiFree(IntPtr p)
	{
		if (p != IntPtr.Zero)
			Marshal.FreeHGlobal(p);
	}

	internal int ApiNext(int idx)
	{
		var t = TableAt(idx);
		var key = Pop();
		if (t.TryGetNext(key, out var pair))
		{
			Push(pair.Key);
			Push(pair.Value);
			return 1;
		}
		return 0;
	}

	internal void ApiConcat(int n)
	{
		if (n == 0)
		{
			Push(string.Empty);
			return;
		}
		if (n == 1)
			return;
		var values = PopN(n);
		Push(Run(Cur.Thread.ConcatAsync(values)));
	}

	internal void ApiLen(int idx)
	{
		var v = Get(idx);
		if (v.Type == LuaValueType.String)
		{
			Push(LuaBytes.ByteLength(v.Read<string>()));
			return;
		}
		if (v.Type == LuaValueType.Table)
		{
			var t = v.Read<LuaTable>();
			var mm = Metamethod(v, "__len");
			if (mm.Type == LuaValueType.Nil)
			{
				Push(Border(t));
				return;
			}
			Push(First(Run(Cur.Thread.CallAsync(mm, new[] { v }))));
			return;
		}
		Push(Run(Cur.Thread.LenAsync(v)));
	}

	internal nuint ApiStringToNumber(IntPtr s, nuint len)
	{
		if (!LuaBytes.TryParseNumber(LuaBytes.FromPointer(s, len), out var d))
			return 0;
		Push(d);
		return len + 1;
	}

	internal int ApiGc(int what, int arg)
	{
		switch (what)
		{
			case 2:
				GC.Collect();
				GC.WaitForPendingFinalizers();
				GC.Collect();
				DrainFinalizers(Cur.Thread);
				return 0;
			case 3:
				return (int)Math.Min(GC.GetTotalMemory(false) / 1024, int.MaxValue);
			case 4:
				return (int)(GC.GetTotalMemory(false) % 1024);
			case 5:
				GC.Collect(0);
				DrainFinalizers(Cur.Thread);
				return 0;
			case 6:
				return 200;
			case 7:
				return 100;
			case 9:
				return 1;
			case 10:
			case 11:
				return 11;
			default:
				return 0;
		}
	}

	internal void ApiToClose(int idx)
	{
		var f = Cur;
		var slot = Abs(idx);
		if (slot <= 0 || slot > f.Top)
			throw Error("invalid stack index");
		var v = f.Slots[slot - 1];
		if (v.ToBoolean() && Metamethod(v, "__close").Type == LuaValueType.Nil)
			throw Error("variable got a non-closable value");
		f.Tbc ??= new List<int>();
		f.Tbc.Add(slot);
	}

	internal void ApiCloseSlot(int idx)
	{
		var f = Cur;
		var slot = Abs(idx);
		if (slot <= 0 || slot > f.Top)
			throw Error("invalid stack index");
		if (f.Tbc != null)
		{
			var at = f.Tbc.LastIndexOf(slot);
			if (at >= 0)
			{
				f.Tbc.RemoveAt(at);
				CallClose(f, f.Slots[slot - 1], LuaValue.Nil);
			}
		}
		f.Slots[slot - 1] = LuaValue.Nil;
	}

	internal void ApiWarning(IntPtr msg, nuint len, int tocont)
	{
		var text = LuaBytes.FromPointer(msg, len);
		if (_warning == null || _warning.Length == 0)
		{
			if (tocont == 0 && text.Length > 0 && text[0] == '@')
			{
				if (text == "@on")
					_warningsOn = true;
				else if (text == "@off")
					_warningsOn = false;
				return;
			}
		}
		_warning ??= new StringBuilder();
		_warning.Append(text);
		if (tocont != 0)
			return;
		var full = _warning.ToString();
		_warning.Clear();
		if (_warningsOn)
			Warn(full);
	}

	internal int ApiGetStack(int level, IntPtr ar)
	{
		if (level < 0)
			return 0;
		var frames = Cur.Thread.GetCallStackFrames();
		var index = frames.Length - 1 - level;
		if (index < 0)
			return 0;
		Marshal.WriteIntPtr(ar, 128, (IntPtr)(index + 1));
		return 1;
	}

	internal int ApiGetInfo(IntPtr whatPtr, IntPtr ar)
	{
		var what = LuaBytes.FromCString(whatPtr) ?? string.Empty;
		var thread = Cur.Thread;
		LuaFunction? function;
		var frameIndex = -1;
		ReadOnlySpan<CallStackFrame> frames = default;
		if (what.StartsWith('>'))
		{
			what = what[1..];
			var fv = Pop();
			if (fv.Type != LuaValueType.Function)
				throw Error("function expected");
			function = fv.Read<LuaFunction>();
		}
		else
		{
			frames = thread.GetCallStackFrames();
			frameIndex = (int)Marshal.ReadIntPtr(ar, 128) - 1;
			if (frameIndex < 0 || frameIndex >= frames.Length)
				return 0;
			function = frames[frameIndex].Function;
		}
		var closure = function as LuaClosure;
		var ok = 1;
		foreach (var c in what)
		{
			switch (c)
			{
				case 'S':
					WriteSource(ar, closure);
					break;
				case 'l':
					{
						var line = -1;
						if (closure != null && frameIndex >= 0 && frameIndex + 1 < frames.Length)
						{
							var pc = frames[frameIndex + 1].CallerInstructionIndex;
							var lines = closure.Proto.LineInfo;
							if (pc >= 0 && pc < lines.Length)
								line = lines[pc];
						}
						Marshal.WriteInt32(ar, 48, line);
						break;
					}
				case 'u':
					if (closure != null)
					{
						Marshal.WriteByte(ar, 60, (byte)Math.Min(closure.UpValues.Length, 255));
						Marshal.WriteByte(ar, 61, (byte)Math.Min(closure.Proto.ParameterCount, 255));
						Marshal.WriteByte(ar, 62, closure.Proto.HasVariableArguments ? (byte)1 : (byte)0);
					}
					else
					{
						var ups = function is CSharpClosure cs ? cs.UpValues.Length : 0;
						Marshal.WriteByte(ar, 60, (byte)Math.Min(ups, 255));
						Marshal.WriteByte(ar, 61, 0);
						Marshal.WriteByte(ar, 62, 1);
					}
					break;
				case 'n':
					{
						string? name = null;
						string? namewhat = null;
						if (frameIndex > 0 && frames[frameIndex - 1].Function is LuaClosure caller)
							namewhat = FuncName(caller.Proto, frames[frameIndex].CallerInstructionIndex, out name);
						if (namewhat == null)
						{
							namewhat = string.Empty;
							name = null;
						}
						else if (name == "?")
							name = function.Name;
						Marshal.WriteIntPtr(ar, 8, name != null ? LuaBytes.Native(name).Pointer : IntPtr.Zero);
						Marshal.WriteIntPtr(ar, 16, LuaBytes.Native(namewhat).Pointer);
						break;
					}
				case 't':
					Marshal.WriteByte(ar, 63, 0);
					break;
				case 'r':
					Marshal.WriteInt16(ar, 64, 0);
					Marshal.WriteInt16(ar, 66, 0);
					break;
				case 'f':
				case 'L':
					break;
				default:
					ok = 0;
					break;
			}
		}
		if (what.Contains('f'))
			Push(function);
		if (what.Contains('L'))
		{
			if (closure == null)
				Push(LuaValue.Nil);
			else
			{
				var lines = new LuaTable();
				foreach (var line in closure.Proto.LineInfo)
					lines[line] = true;
				Push(lines);
			}
		}
		return ok;
	}

	private static string? FuncName(Prototype proto, int pc, out string? name)
	{
		name = null;
		if (GetFuncNameMethod == null || pc < 0 || pc >= proto.Code.Length)
			return null;
		try
		{
			var args = new object?[] { proto, pc, null };
			var result = (string?)GetFuncNameMethod.Invoke(null, args);
			name = (string?)args[2];
			return result;
		}
		catch (Exception)
		{
			return null;
		}
	}

	private static void WriteSource(IntPtr ar, LuaClosure? closure)
	{
		string source;
		string what;
		int lineDefined;
		int lastLineDefined;
		if (closure != null)
		{
			source = closure.Proto.ChunkName;
			lineDefined = closure.Proto.LineDefined;
			lastLineDefined = closure.Proto.LastLineDefined;
			what = lineDefined == 0 ? "main" : "Lua";
		}
		else
		{
			source = "=[C]";
			lineDefined = -1;
			lastLineDefined = -1;
			what = "C";
		}
		var native = LuaBytes.Native(source);
		Marshal.WriteIntPtr(ar, 24, LuaBytes.Native(what).Pointer);
		Marshal.WriteIntPtr(ar, 32, native.Pointer);
		Marshal.WriteIntPtr(ar, 40, native.Length);
		Marshal.WriteInt32(ar, 52, lineDefined);
		Marshal.WriteInt32(ar, 56, lastLineDefined);
		var shortSrc = LuaBytes.Encode(LuaBytes.ChunkId(source));
		var n = Math.Min(shortSrc.Length, 59);
		Marshal.Copy(shortSrc, 0, ar + 68, n);
		Marshal.WriteByte(ar, 68 + n, 0);
	}

	internal IntPtr ApiGetUpvalue(int funcIndex, int n)
	{
		var v = Get(funcIndex);
		if (v.Type != LuaValueType.Function)
			return IntPtr.Zero;
		var fn = v.Read<LuaFunction>();
		if (fn is LuaClosure closure)
		{
			var ups = closure.UpValues;
			if (n < 1 || n > ups.Length)
				return IntPtr.Zero;
			Push(ups[n - 1].GetValue());
			return LuaBytes.Native(UpvalueName(closure, n)).Pointer;
		}
		if (fn is CSharpClosure cs && n >= 1 && n <= cs.UpValues.Length)
		{
			Push(cs.UpValues[n - 1]);
			return LuaBytes.Native(string.Empty).Pointer;
		}
		return IntPtr.Zero;
	}

	internal IntPtr ApiSetUpvalue(int funcIndex, int n)
	{
		var v = Get(funcIndex);
		if (v.Type != LuaValueType.Function)
			return IntPtr.Zero;
		var fn = v.Read<LuaFunction>();
		if (fn is LuaClosure closure)
		{
			var ups = closure.UpValues;
			if (n < 1 || n > ups.Length)
				return IntPtr.Zero;
			ups[n - 1].SetValue(Pop());
			return LuaBytes.Native(UpvalueName(closure, n)).Pointer;
		}
		if (fn is CSharpClosure cs && n >= 1 && n <= cs.UpValues.Length)
		{
			cs.UpValues[n - 1] = Pop();
			return LuaBytes.Native(string.Empty).Pointer;
		}
		return IntPtr.Zero;
	}

	private static string UpvalueName(LuaClosure closure, int n)
	{
		var descs = closure.Proto.UpValues;
		if (n - 1 < descs.Length && !string.IsNullOrEmpty(descs[n - 1].Name))
			return descs[n - 1].Name;
		return "(no name)";
	}

	internal IntPtr ApiUpvalueId(int funcIndex, int n)
	{
		var v = Get(funcIndex);
		if (v.Type != LuaValueType.Function)
			return IntPtr.Zero;
		var fn = v.Read<LuaFunction>();
		if (fn is LuaClosure closure)
		{
			var ups = closure.UpValues;
			return n >= 1 && n <= ups.Length ? IdOf(ups[n - 1]) : IntPtr.Zero;
		}
		if (fn is CSharpClosure cs && n >= 1 && n <= cs.UpValues.Length)
			return IdOf(cs.UpValues) + n * 8;
		return IntPtr.Zero;
	}

	internal void ApiTraceback(IntPtr L1, IntPtr msg, nuint len, int hasmsg, int level)
	{
		var message = hasmsg != 0 ? (LuaValue)LuaBytes.FromPointer(msg, len) : LuaValue.Nil;
		LuaState target;
		if (L1 == _activeL || L1 == IntPtr.Zero)
			target = _activeThread?.Coroutine ?? Cur.Thread;
		else if (L1 == _L)
			target = MainCur.Thread;
		else
			target = ThreadOf(L1) is { } nt ? nt.Coroutine ?? nt.Frame.Thread : Cur.Thread;
		Push(Traceback.CreateTracebackMessage(target, message, Math.Max(level, 0)));
	}

	internal int ApiLoadFile(IntPtr name, nuint len, IntPtr mode)
	{
		var path = LuaBytes.FromPointer(name, len);
		var m = LuaBytes.FromCString(mode) ?? "bt";
		try
		{
			Push(Run(Cur.Thread.LoadFileAsync(path, m, null, CancellationToken.None)));
			return 0;
		}
		catch (OperationCanceledException)
		{
			throw;
		}
		catch (Exception e) when (e is FileNotFoundException or DirectoryNotFoundException or IOException or UnauthorizedAccessException)
		{
			Push($"cannot open {path}");
			return 6;
		}
		catch (Exception e)
		{
			Push(e.Message);
			return 3;
		}
	}

	internal void ApiOpenLib(IntPtr namePtr)
	{
		var name = LuaBytes.FromCString(namePtr) ?? string.Empty;
		var env = _state.Environment;
		if (name == "_G")
		{
			Push(env);
			return;
		}
		if (env.TryGetValue(name, out var v) && v.Type == LuaValueType.Table)
		{
			Push(v);
			return;
		}
		if (_state.LoadedModules.TryGetValue(name, out var loaded) && loaded.Type == LuaValueType.Table)
		{
			Push(loaded);
			return;
		}
		Push(new LuaTable());
	}
}
