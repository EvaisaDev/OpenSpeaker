namespace OpenSpeaker.Extensions.Native;

internal static class NativeApi
{
	private static TR Call<TA, TR>(IntPtr L, TA a, Func<NativeLuaHost, TA, TR> body)
	{
		var h = NativeLuaHost.Of(L);
		var saved = h.Enter(L);
		try
		{
			return body(h, a);
		}
		catch (Exception e)
		{
			h.Fail(e);
			return default!;
		}
		finally
		{
			h.Leave(saved);
		}
	}

	private static void Do<TA>(IntPtr L, TA a, Action<NativeLuaHost, TA> body)
	{
		var h = NativeLuaHost.Of(L);
		var saved = h.Enter(L);
		try
		{
			body(h, a);
		}
		catch (Exception e)
		{
			h.Fail(e);
		}
		finally
		{
			h.Leave(saved);
		}
	}

	public static Delegate[] CreateTable()
	{
		return new Delegate[]
		{
			new LuaFnLI((L, i) => Call(L, i, static (h, x) => h.ApiAbsIndex(x))),
			new LuaFnL(L => Call(L, 0, static (h, _) => h.ApiGetTop())),
			new LuaFnLIV((L, i) => Do(L, i, static (h, x) => h.ApiSetTop(x))),
			new LuaFnLIV((L, i) => Do(L, i, static (h, x) => h.ApiPushValue(x))),
			new LuaFnLIIV((L, i, n) => Do(L, (i, n), static (h, x) => h.ApiRotate(x.i, x.n))),
			new LuaFnLIIV((L, a, b) => Do(L, (a, b), static (h, x) => h.ApiCopy(x.a, x.b))),
			new LuaFnXMove((from, to, n) => Do(from, (to, n), static (h, x) => h.ApiXMove(x.to, x.n))),
			new LuaFnLI((L, i) => Call(L, i, static (h, x) => h.ApiIsNumber(x))),
			new LuaFnLI((L, i) => Call(L, i, static (h, x) => h.ApiIsString(x))),
			new LuaFnLI((L, i) => Call(L, i, static (h, x) => h.ApiIsCFunction(x))),
			new LuaFnLI((L, i) => Call(L, i, static (h, x) => h.ApiIsInteger(x))),
			new LuaFnLI((L, i) => Call(L, i, static (h, x) => h.ApiIsUserData(x))),
			new LuaFnLI((L, i) => Call(L, i, static (h, x) => h.ApiType(x))),
			new LuaFnToNumber((L, i, p) => Call(L, (i, p), static (h, x) => h.ApiToNumber(x.i, x.p))),
			new LuaFnToInteger((L, i, p) => Call(L, (i, p), static (h, x) => h.ApiToInteger(x.i, x.p))),
			new LuaFnLI((L, i) => Call(L, i, static (h, x) => h.ApiToBoolean(x))),
			new LuaFnLIP((L, i, p) => Call(L, (i, p), static (h, x) => h.ApiToLString(x.i, x.p))),
			new LuaFnRawLen((L, i) => Call(L, i, static (h, x) => h.ApiRawLen(x))),
			new LuaFnLIRetP((L, i) => Call(L, i, static (h, x) => h.ApiToCFunction(x))),
			new LuaFnLIRetP((L, i) => Call(L, i, static (h, x) => h.ApiToUserData(x))),
			new LuaFnLIRetP((L, i) => Call(L, i, static (h, x) => h.ApiToThread(x))),
			new LuaFnLIRetP((L, i) => Call(L, i, static (h, x) => h.ApiToPointer(x))),
			new LuaFnLIV((L, op) => Do(L, op, static (h, x) => h.ApiArith(x))),
			new LuaFnLII((L, a, b) => Call(L, (a, b), static (h, x) => h.ApiRawEqual(x.a, x.b))),
			new LuaFnLIII((L, a, b, op) => Call(L, (a, b, op), static (h, x) => h.ApiCompare(x.a, x.b, x.op))),
			new LuaFnLV(L => Do(L, 0, static (h, _) => h.ApiPushNil())),
			new LuaFnPushNumber((L, n) => Do(L, n, static (h, x) => h.ApiPushNumber(x))),
			new LuaFnPushInteger((L, n) => Do(L, n, static (h, x) => h.ApiPushInteger(x))),
			new LuaFnPushString((L, s, n) => Call(L, (s, n), static (h, x) => h.ApiPushLString(x.s, x.n))),
			new LuaFnPushClosure((L, fn, n, name) => Do(L, (fn, n, name), static (h, x) => h.ApiPushCClosure(x.fn, x.n, x.name))),
			new LuaFnLIV((L, b) => Do(L, b, static (h, x) => h.ApiPushBoolean(x))),
			new LuaFnLP((L, p) => Do(L, p, static (h, x) => h.ApiPushLightUserData(x))),
			new LuaFnL(L => Call(L, 0, static (h, _) => h.ApiPushThread())),
			new LuaFnLSN((L, s, n) => Call(L, (s, n), static (h, x) => h.ApiGetGlobal(x.s, x.n))),
			new LuaFnLI((L, i) => Call(L, i, static (h, x) => h.ApiGetTable(x))),
			new LuaFnLISN((L, i, s, n) => Call(L, (i, s, n), static (h, x) => h.ApiGetField(x.i, x.s, x.n))),
			new LuaFnLIL((L, i, n) => Call(L, (i, n), static (h, x) => h.ApiGetI(x.i, x.n))),
			new LuaFnLI((L, i) => Call(L, i, static (h, x) => h.ApiRawGet(x))),
			new LuaFnLIL((L, i, n) => Call(L, (i, n), static (h, x) => h.ApiRawGetI(x.i, x.n))),
			new LuaFnLIPRetI((L, i, p) => Call(L, (i, p), static (h, x) => h.ApiRawGetP(x.i, x.p))),
			new LuaFnLIIV((L, a, b) => Do(L, (a, b), static (h, x) => h.ApiCreateTable(x.a, x.b))),
			new LuaFnNewUserData((L, size, nuv) => Call(L, (size, nuv), static (h, x) => h.ApiNewUserData(x.size, x.nuv))),
			new LuaFnLI((L, i) => Call(L, i, static (h, x) => h.ApiGetMetatable(x))),
			new LuaFnLII((L, i, n) => Call(L, (i, n), static (h, x) => h.ApiGetIUserValue(x.i, x.n))),
			new LuaFnLSNV((L, s, n) => Do(L, (s, n), static (h, x) => h.ApiSetGlobal(x.s, x.n))),
			new LuaFnLIV((L, i) => Do(L, i, static (h, x) => h.ApiSetTable(x))),
			new LuaFnLISNV((L, i, s, n) => Do(L, (i, s, n), static (h, x) => h.ApiSetField(x.i, x.s, x.n))),
			new LuaFnLILV((L, i, n) => Do(L, (i, n), static (h, x) => h.ApiSetI(x.i, x.n))),
			new LuaFnLIV((L, i) => Do(L, i, static (h, x) => h.ApiRawSet(x))),
			new LuaFnLILV((L, i, n) => Do(L, (i, n), static (h, x) => h.ApiRawSetI(x.i, x.n))),
			new LuaFnLIPV((L, i, p) => Do(L, (i, p), static (h, x) => h.ApiRawSetP(x.i, x.p))),
			new LuaFnLI((L, i) => Call(L, i, static (h, x) => h.ApiSetMetatable(x))),
			new LuaFnLII((L, i, n) => Call(L, (i, n), static (h, x) => h.ApiSetIUserValue(x.i, x.n))),
			new LuaFnLIIV((L, a, r) => Do(L, (a, r), static (h, x) => h.ApiCall(x.a, x.r))),
			new LuaFnLIII((L, a, r, e) => Call(L, (a, r, e), static (h, x) => h.ApiPCall(x.a, x.r, x.e))),
			new LuaFnLoad((L, buf, size, name, len, mode) => Call(L, (buf, size, name, len, mode), static (h, x) => h.ApiLoad(x.buf, x.size, x.name, x.len, x.mode))),
			new LuaFnDump((L, strip, p, sz) => Call(L, (strip, p, sz), static (h, x) => h.ApiDump(x.strip, x.p, x.sz))),
			new LuaFnFree(NativeLuaHost.ApiFree),
			new LuaFnLI((L, i) => Call(L, i, static (h, x) => h.ApiNext(x))),
			new LuaFnLIV((L, n) => Do(L, n, static (h, x) => h.ApiConcat(x))),
			new LuaFnLIV((L, i) => Do(L, i, static (h, x) => h.ApiLen(x))),
			new LuaFnStringToNumber((L, s, n) => Call(L, (s, n), static (h, x) => h.ApiStringToNumber(x.s, x.n))),
			new LuaFnLII((L, w, a) => Call(L, (w, a), static (h, x) => h.ApiGc(x.w, x.a))),
			new LuaFnLIV((L, i) => Do(L, i, static (h, x) => h.ApiToClose(x))),
			new LuaFnLIV((L, i) => Do(L, i, static (h, x) => h.ApiCloseSlot(x))),
			new LuaFnWarning((L, m, n, c) => Do(L, (m, n, c), static (h, x) => h.ApiWarning(x.m, x.n, x.c))),
			new LuaFnLIPRetI((L, level, ar) => Call(L, (level, ar), static (h, x) => h.ApiGetStack(x.level, x.ar))),
			new LuaFnLPP((L, what, ar) => Call(L, (what, ar), static (h, x) => h.ApiGetInfo(x.what, x.ar))),
			new LuaFnLIIRetP((L, f, n) => Call(L, (f, n), static (h, x) => h.ApiGetUpvalue(x.f, x.n))),
			new LuaFnLIIRetP((L, f, n) => Call(L, (f, n), static (h, x) => h.ApiSetUpvalue(x.f, x.n))),
			new LuaFnLIIRetP((L, f, n) => Call(L, (f, n), static (h, x) => h.ApiUpvalueId(x.f, x.n))),
			new LuaFnTraceback((L, L1, m, n, has, level) => Do(L, (L1, m, n, has, level), static (h, x) => h.ApiTraceback(x.L1, x.m, x.n, x.has, x.level))),
			new LuaFnLoadFile((L, name, n, mode) => Call(L, (name, n, mode), static (h, x) => h.ApiLoadFile(x.name, x.n, x.mode))),
			new LuaFnLP((L, name) => Do(L, name, static (h, x) => h.ApiOpenLib(x))),
			new LuaFnNewThread(L => Call(L, 0, static (h, _) => h.ApiNewThread())),
			new LuaFnResume((L, from, narg, nres) => Call(L, (narg, nres), static (h, x) => h.ApiResume(x.narg, x.nres))),
			new LuaFnLV(L => Do(L, 0, static (h, _) => h.ApiCloseThread())),
		};
	}
}
