#include <stdarg.h>
#include <stdio.h>
#include <stdlib.h>
#include <string.h>

#include "osn_internal.h"

osn_host osn_H;

#define H osn_H

_Static_assert(offsetof(lua_Debug, i_ci) == 128, "lua_Debug layout");
_Static_assert(sizeof(lua_Debug) == 136, "lua_Debug size");
_Static_assert(offsetof(struct lua_State, errflag) == 0, "lua_State layout");
_Static_assert(offsetof(struct lua_State, host) == 56, "lua_State host slot");
_Static_assert(offsetof(struct lua_State, mainthread) == 64, "lua_State main thread slot");

static void *osn_alloc(void *ud, void *ptr, size_t osize, size_t nsize)
{
	(void)ud;
	(void)osize;
	if (nsize == 0) {
		free(ptr);
		return NULL;
	}
	return realloc(ptr, nsize);
}

__declspec(dllexport) int osn_hostsize(void)
{
	return (int)(sizeof(osn_host) / sizeof(void *));
}

__declspec(dllexport) int osn_init(const void *table, int count)
{
	if (count != osn_hostsize())
		return 0;
	memcpy(&H, table, sizeof(osn_host));
	return 1;
}

__declspec(dllexport) lua_State *osn_newstate(void)
{
	char *block = (char *)calloc(1, LUA_EXTRASPACE + sizeof(struct lua_State));
	lua_State *L;
	if (block == NULL)
		return NULL;
	L = (lua_State *)(block + LUA_EXTRASPACE);
	L->allocf = osn_alloc;
	L->mainthread = L;
	return L;
}

__declspec(dllexport) lua_State *osn_newthread(lua_State *M)
{
	char *block = (char *)calloc(1, LUA_EXTRASPACE + sizeof(struct lua_State));
	lua_State *L;
	if (block == NULL)
		return NULL;
	L = (lua_State *)(block + LUA_EXTRASPACE);
	L->host = M->host;
	L->mainthread = G(M);
	return L;
}

__declspec(dllexport) void osn_closestate(lua_State *L)
{
	if (L != NULL)
		free((char *)L - LUA_EXTRASPACE);
}

__declspec(dllexport) int osn_invoke(lua_State *L, lua_CFunction f)
{
	osn_jmp j;
	j.prev = L->jmp;
	L->jmp = &j;
	if (setjmp(j.buf) == 0) {
		int n = f(L);
		L->jmp = j.prev;
		return n < 0 ? 0 : n;
	}
	L->jmp = j.prev;
	L->errflag = 0;
	return -1;
}

void osn_throw(lua_State *L)
{
	lua_State *M = G(L);
	L->errflag = 0;
	if (L->jmp != NULL)
		longjmp(L->jmp->buf, 1);
	if (M != NULL && M != L && M->jmp != NULL) {
		if (H.gettop(L) > 0)
			H.xmove(L, M, 1);
		else
			H.pushlstring(M, "error in thread", 15);
		L->errflag = 0;
		M->errflag = 0;
		longjmp(M->jmp->buf, 1);
	}
	if (M != NULL && M->panic != NULL)
		M->panic(L);
	abort();
}

__declspec(noreturn) static void osn_fail(lua_State *L, const char *msg)
{
	H.pushlstring(L, msg, strlen(msg));
	osn_throw(L);
}

void osn_pushnamedcclosure(lua_State *L, lua_CFunction fn, int n, const char *name)
{
	H.pushcclosure(L, fn, n, name);
	OSN_CHECK(L);
}

int osn_loadbuffer(lua_State *L, const char *buf, size_t size, const char *chunkname, const char *mode)
{
	int st;
	if (chunkname == NULL)
		chunkname = "?";
	st = H.load(L, buf != NULL ? buf : "", size, chunkname, strlen(chunkname), mode != NULL ? mode : "bt");
	OSN_CHECK(L);
	return st;
}

LUA_API lua_State *lua_newstate(lua_Alloc f, void *ud)
{
	(void)f;
	(void)ud;
	return NULL;
}

LUA_API void lua_close(lua_State *L)
{
	(void)L;
}

LUA_API lua_State *lua_newthread(lua_State *L)
{
	lua_State *L1 = H.newthread(L);
	OSN_CHECK(L);
	return L1;
}

LUA_API int lua_closethread(lua_State *L, lua_State *from)
{
	(void)from;
	H.closethread(L);
	OSN_CHECK(L);
	L->status = LUA_OK;
	return LUA_OK;
}

LUA_API int lua_resetthread(lua_State *L)
{
	return lua_closethread(L, NULL);
}

LUA_API lua_CFunction lua_atpanic(lua_State *L, lua_CFunction panicf)
{
	lua_CFunction old = G(L)->panic;
	G(L)->panic = panicf;
	return old;
}

LUA_API lua_Number lua_version(lua_State *L)
{
	(void)L;
	return LUA_VERSION_NUM;
}

LUA_API int lua_absindex(lua_State *L, int idx)
{
	int r = H.absindex(L, idx);
	OSN_CHECK(L);
	return r;
}

LUA_API int lua_gettop(lua_State *L)
{
	int r = H.gettop(L);
	OSN_CHECK(L);
	return r;
}

LUA_API void lua_settop(lua_State *L, int idx)
{
	H.settop(L, idx);
	OSN_CHECK(L);
}

LUA_API void lua_pushvalue(lua_State *L, int idx)
{
	H.pushvalue(L, idx);
	OSN_CHECK(L);
}

LUA_API void lua_rotate(lua_State *L, int idx, int n)
{
	H.rotate(L, idx, n);
	OSN_CHECK(L);
}

LUA_API void lua_copy(lua_State *L, int fromidx, int toidx)
{
	H.copy(L, fromidx, toidx);
	OSN_CHECK(L);
}

LUA_API int lua_checkstack(lua_State *L, int n)
{
	(void)L;
	return n >= 0 && n < LUAI_MAXSTACK;
}

LUA_API void lua_xmove(lua_State *from, lua_State *to, int n)
{
	if (from == to || n <= 0)
		return;
	H.xmove(from, to, n);
	OSN_CHECK(from);
}

LUA_API int lua_isnumber(lua_State *L, int idx)
{
	int r = H.isnumber(L, idx);
	OSN_CHECK(L);
	return r;
}

LUA_API int lua_isstring(lua_State *L, int idx)
{
	int r = H.isstring(L, idx);
	OSN_CHECK(L);
	return r;
}

LUA_API int lua_iscfunction(lua_State *L, int idx)
{
	int r = H.iscfunction(L, idx);
	OSN_CHECK(L);
	return r;
}

LUA_API int lua_isinteger(lua_State *L, int idx)
{
	int r = H.isinteger(L, idx);
	OSN_CHECK(L);
	return r;
}

LUA_API int lua_isuserdata(lua_State *L, int idx)
{
	int r = H.isuserdata(L, idx);
	OSN_CHECK(L);
	return r;
}

LUA_API int lua_type(lua_State *L, int idx)
{
	int r = H.type(L, idx);
	OSN_CHECK(L);
	return r;
}

LUA_API const char *lua_typename(lua_State *L, int t)
{
	static const char *const names[] = {
		"no value", "nil", "boolean", "userdata", "number",
		"string", "table", "function", "userdata", "thread"
	};
	(void)L;
	if (t < LUA_TNONE || t >= LUA_NUMTYPES)
		return "?";
	return names[t + 1];
}

LUA_API lua_Number lua_tonumberx(lua_State *L, int idx, int *isnum)
{
	int ok = 0;
	lua_Number r = H.tonumberx(L, idx, &ok);
	OSN_CHECK(L);
	if (isnum != NULL)
		*isnum = ok;
	return r;
}

LUA_API lua_Integer lua_tointegerx(lua_State *L, int idx, int *isnum)
{
	int ok = 0;
	lua_Integer r = H.tointegerx(L, idx, &ok);
	OSN_CHECK(L);
	if (isnum != NULL)
		*isnum = ok;
	return r;
}

LUA_API int lua_toboolean(lua_State *L, int idx)
{
	int r = H.toboolean(L, idx);
	OSN_CHECK(L);
	return r;
}

LUA_API const char *lua_tolstring(lua_State *L, int idx, size_t *len)
{
	size_t n = 0;
	const char *r = H.tolstring(L, idx, &n);
	OSN_CHECK(L);
	if (len != NULL)
		*len = r != NULL ? n : 0;
	return r;
}

LUA_API lua_Unsigned lua_rawlen(lua_State *L, int idx)
{
	lua_Unsigned r = H.rawlen(L, idx);
	OSN_CHECK(L);
	return r;
}

LUA_API lua_CFunction lua_tocfunction(lua_State *L, int idx)
{
	lua_CFunction r = H.tocfunction(L, idx);
	OSN_CHECK(L);
	return r;
}

LUA_API void *lua_touserdata(lua_State *L, int idx)
{
	void *r = H.touserdata(L, idx);
	OSN_CHECK(L);
	return r;
}

LUA_API lua_State *lua_tothread(lua_State *L, int idx)
{
	lua_State *r = H.tothread(L, idx);
	OSN_CHECK(L);
	return r;
}

LUA_API const void *lua_topointer(lua_State *L, int idx)
{
	const void *r = H.topointer(L, idx);
	OSN_CHECK(L);
	return r;
}

LUA_API void lua_arith(lua_State *L, int op)
{
	H.arith(L, op);
	OSN_CHECK(L);
}

LUA_API int lua_rawequal(lua_State *L, int idx1, int idx2)
{
	int r = H.rawequal(L, idx1, idx2);
	OSN_CHECK(L);
	return r;
}

LUA_API int lua_compare(lua_State *L, int idx1, int idx2, int op)
{
	int r = H.compare(L, idx1, idx2, op);
	OSN_CHECK(L);
	return r;
}

LUA_API void lua_pushnil(lua_State *L)
{
	H.pushnil(L);
	OSN_CHECK(L);
}

LUA_API void lua_pushnumber(lua_State *L, lua_Number n)
{
	H.pushnumber(L, n);
	OSN_CHECK(L);
}

LUA_API void lua_pushinteger(lua_State *L, lua_Integer n)
{
	H.pushinteger(L, n);
	OSN_CHECK(L);
}

LUA_API const char *lua_pushlstring(lua_State *L, const char *s, size_t len)
{
	const char *r = H.pushlstring(L, len == 0 || s == NULL ? "" : s, s == NULL ? 0 : len);
	OSN_CHECK(L);
	return r;
}

LUA_API const char *lua_pushstring(lua_State *L, const char *s)
{
	if (s == NULL) {
		lua_pushnil(L);
		return NULL;
	}
	return lua_pushlstring(L, s, strlen(s));
}

typedef struct osn_sb {
	char *p;
	size_t n;
	size_t cap;
} osn_sb;

static int sb_add(osn_sb *b, const char *s, size_t l)
{
	if (b->n + l + 1 > b->cap) {
		size_t cap = b->cap == 0 ? 128 : b->cap;
		char *np;
		while (cap < b->n + l + 1)
			cap *= 2;
		np = (char *)realloc(b->p, cap);
		if (np == NULL)
			return 0;
		b->p = np;
		b->cap = cap;
	}
	if (l > 0)
		memcpy(b->p + b->n, s, l);
	b->n += l;
	return 1;
}

static size_t osn_utf8esc(char *buff, unsigned long x)
{
	char tmp[8];
	int n = 1;
	if (x < 0x80) {
		buff[0] = (char)x;
		return 1;
	}
	{
		unsigned int mfb = 0x3f;
		do {
			tmp[8 - (n++)] = (char)(0x80 | (x & 0x3f));
			x >>= 6;
			mfb >>= 1;
		} while (x > mfb);
		tmp[8 - n] = (char)((~mfb << 1) | x);
	}
	memcpy(buff, tmp + 8 - n, (size_t)n);
	return (size_t)n;
}

static size_t osn_fmtnumber(char *buff, size_t sz, lua_Number num)
{
	int n = snprintf(buff, sz, LUA_NUMBER_FMT, num);
	if (n < 0)
		return 0;
	if (buff[strspn(buff, "-0123456789")] == '\0' && (size_t)n + 2 < sz) {
		buff[n++] = '.';
		buff[n++] = '0';
		buff[n] = '\0';
	}
	return (size_t)n;
}

LUA_API const char *lua_pushvfstring(lua_State *L, const char *fmt, va_list argp)
{
	osn_sb b = { NULL, 0, 0 };
	const char *e;
	char tmp[64];
	int ok = 1;
	while (ok && (e = strchr(fmt, '%')) != NULL) {
		ok = sb_add(&b, fmt, (size_t)(e - fmt));
		switch (e[1]) {
		case 's': {
			const char *s = va_arg(argp, char *);
			if (s == NULL)
				s = "(null)";
			ok = ok && sb_add(&b, s, strlen(s));
			break;
		}
		case 'c':
			tmp[0] = (char)va_arg(argp, int);
			ok = ok && sb_add(&b, tmp, 1);
			break;
		case 'd': {
			int n = snprintf(tmp, sizeof(tmp), "%d", va_arg(argp, int));
			ok = ok && sb_add(&b, tmp, n > 0 ? (size_t)n : 0);
			break;
		}
		case 'I': {
			int n = snprintf(tmp, sizeof(tmp), LUA_INTEGER_FMT, (LUAI_UACINT)va_arg(argp, LUAI_UACINT));
			ok = ok && sb_add(&b, tmp, n > 0 ? (size_t)n : 0);
			break;
		}
		case 'f': {
			size_t n = osn_fmtnumber(tmp, sizeof(tmp), (lua_Number)va_arg(argp, LUAI_UACNUMBER));
			ok = ok && sb_add(&b, tmp, n);
			break;
		}
		case 'p': {
			int n = snprintf(tmp, sizeof(tmp), "%p", va_arg(argp, void *));
			ok = ok && sb_add(&b, tmp, n > 0 ? (size_t)n : 0);
			break;
		}
		case 'U': {
			size_t n = osn_utf8esc(tmp, (unsigned long)va_arg(argp, long));
			ok = ok && sb_add(&b, tmp, n);
			break;
		}
		case '%':
			ok = ok && sb_add(&b, "%", 1);
			break;
		default: {
			int n = snprintf(tmp, sizeof(tmp), "invalid conversion '%%%c' to 'lua_pushfstring'", e[1] != '\0' ? e[1] : ' ');
			free(b.p);
			H.pushlstring(L, tmp, n > 0 ? (size_t)n : 0);
			osn_throw(L);
		}
		}
		fmt = e + 2;
	}
	if (ok)
		ok = sb_add(&b, fmt, strlen(fmt));
	if (!ok) {
		free(b.p);
		osn_fail(L, "not enough memory");
	}
	{
		const char *r = H.pushlstring(L, b.p != NULL ? b.p : "", b.n);
		free(b.p);
		OSN_CHECK(L);
		return r;
	}
}

LUA_API const char *lua_pushfstring(lua_State *L, const char *fmt, ...)
{
	const char *r;
	va_list argp;
	va_start(argp, fmt);
	r = lua_pushvfstring(L, fmt, argp);
	va_end(argp);
	return r;
}

LUA_API void lua_pushcclosure(lua_State *L, lua_CFunction fn, int n)
{
	H.pushcclosure(L, fn, n, NULL);
	OSN_CHECK(L);
}

LUA_API void lua_pushboolean(lua_State *L, int b)
{
	H.pushboolean(L, b);
	OSN_CHECK(L);
}

LUA_API void lua_pushlightuserdata(lua_State *L, void *p)
{
	H.pushlightuserdata(L, p);
	OSN_CHECK(L);
}

LUA_API int lua_pushthread(lua_State *L)
{
	int r = H.pushthread(L);
	OSN_CHECK(L);
	return r;
}

LUA_API int lua_getglobal(lua_State *L, const char *name)
{
	int r = H.getglobal(L, name, strlen(name));
	OSN_CHECK(L);
	return r;
}

LUA_API int lua_gettable(lua_State *L, int idx)
{
	int r = H.gettable(L, idx);
	OSN_CHECK(L);
	return r;
}

LUA_API int lua_getfield(lua_State *L, int idx, const char *k)
{
	int r = H.getfield(L, idx, k, strlen(k));
	OSN_CHECK(L);
	return r;
}

LUA_API int lua_geti(lua_State *L, int idx, lua_Integer n)
{
	int r = H.geti(L, idx, n);
	OSN_CHECK(L);
	return r;
}

LUA_API int lua_rawget(lua_State *L, int idx)
{
	int r = H.rawget(L, idx);
	OSN_CHECK(L);
	return r;
}

LUA_API int lua_rawgeti(lua_State *L, int idx, lua_Integer n)
{
	int r = H.rawgeti(L, idx, n);
	OSN_CHECK(L);
	return r;
}

LUA_API int lua_rawgetp(lua_State *L, int idx, const void *p)
{
	int r = H.rawgetp(L, idx, p);
	OSN_CHECK(L);
	return r;
}

LUA_API void lua_createtable(lua_State *L, int narr, int nrec)
{
	H.createtable(L, narr, nrec);
	OSN_CHECK(L);
}

LUA_API void *lua_newuserdatauv(lua_State *L, size_t sz, int nuvalue)
{
	void *r = H.newuserdatauv(L, sz, nuvalue);
	OSN_CHECK(L);
	return r;
}

LUA_API int lua_getmetatable(lua_State *L, int objindex)
{
	int r = H.getmetatable(L, objindex);
	OSN_CHECK(L);
	return r;
}

LUA_API int lua_getiuservalue(lua_State *L, int idx, int n)
{
	int r = H.getiuservalue(L, idx, n);
	OSN_CHECK(L);
	return r;
}

LUA_API void lua_setglobal(lua_State *L, const char *name)
{
	H.setglobal(L, name, strlen(name));
	OSN_CHECK(L);
}

LUA_API void lua_settable(lua_State *L, int idx)
{
	H.settable(L, idx);
	OSN_CHECK(L);
}

LUA_API void lua_setfield(lua_State *L, int idx, const char *k)
{
	H.setfield(L, idx, k, strlen(k));
	OSN_CHECK(L);
}

LUA_API void lua_seti(lua_State *L, int idx, lua_Integer n)
{
	H.seti(L, idx, n);
	OSN_CHECK(L);
}

LUA_API void lua_rawset(lua_State *L, int idx)
{
	H.rawset(L, idx);
	OSN_CHECK(L);
}

LUA_API void lua_rawseti(lua_State *L, int idx, lua_Integer n)
{
	H.rawseti(L, idx, n);
	OSN_CHECK(L);
}

LUA_API void lua_rawsetp(lua_State *L, int idx, const void *p)
{
	H.rawsetp(L, idx, p);
	OSN_CHECK(L);
}

LUA_API int lua_setmetatable(lua_State *L, int objindex)
{
	int r = H.setmetatable(L, objindex);
	OSN_CHECK(L);
	return r;
}

LUA_API int lua_setiuservalue(lua_State *L, int idx, int n)
{
	int r = H.setiuservalue(L, idx, n);
	OSN_CHECK(L);
	return r;
}

LUA_API void lua_callk(lua_State *L, int nargs, int nresults, lua_KContext ctx, lua_KFunction k)
{
	(void)ctx;
	(void)k;
	H.call(L, nargs, nresults);
	OSN_CHECK(L);
}

LUA_API int lua_pcallk(lua_State *L, int nargs, int nresults, int errfunc, lua_KContext ctx, lua_KFunction k)
{
	int r;
	(void)ctx;
	(void)k;
	r = H.pcall(L, nargs, nresults, errfunc);
	OSN_CHECK(L);
	return r;
}

LUA_API int lua_load(lua_State *L, lua_Reader reader, void *data, const char *chunkname, const char *mode)
{
	osn_sb b = { NULL, 0, 0 };
	const char *p;
	size_t sz;
	int st;
	while ((p = reader(L, data, &sz)) != NULL && sz > 0) {
		if (!sb_add(&b, p, sz)) {
			free(b.p);
			H.pushlstring(L, "not enough memory", 17);
			OSN_CHECK(L);
			return LUA_ERRMEM;
		}
	}
	st = osn_loadbuffer(L, b.p, b.n, chunkname, mode);
	free(b.p);
	return st;
}

LUA_API int lua_dump(lua_State *L, lua_Writer writer, void *data, int strip)
{
	char *out = NULL;
	size_t sz = 0;
	int st = H.dump(L, strip, &out, &sz);
	OSN_CHECK(L);
	if (st == 0 && out != NULL)
		st = writer(L, out, sz, data);
	if (out != NULL)
		H.freemem(out);
	return st;
}

LUA_API int lua_yieldk(lua_State *L, int nresults, lua_KContext ctx, lua_KFunction k)
{
	(void)nresults;
	(void)ctx;
	(void)k;
	osn_fail(L, "lua_yield is not supported by the OpenSpeaker native bridge");
}

LUA_API int lua_resume(lua_State *L, lua_State *from, int narg, int *nres)
{
	int n = 0;
	int st = H.resume(L, from, narg, &n);
	OSN_CHECK(L);
	if (nres != NULL)
		*nres = n;
	L->status = st;
	return st;
}

LUA_API int lua_status(lua_State *L)
{
	return L->status;
}

LUA_API int lua_isyieldable(lua_State *L)
{
	(void)L;
	return 0;
}

LUA_API void lua_setwarnf(lua_State *L, lua_WarnFunction f, void *ud)
{
	G(L)->warnf = f;
	G(L)->warnud = ud;
}

LUA_API void lua_warning(lua_State *L, const char *msg, int tocont)
{
	if (msg == NULL)
		return;
	if (G(L)->warnf != NULL) {
		G(L)->warnf(G(L)->warnud, msg, tocont);
		return;
	}
	H.warning(L, msg, strlen(msg), tocont);
	OSN_CHECK(L);
}

LUA_API int lua_gc(lua_State *L, int what, ...)
{
	int arg = 0;
	int r;
	va_list argp;
	va_start(argp, what);
	if (what == LUA_GCSTEP || what == LUA_GCSETPAUSE || what == LUA_GCSETSTEPMUL || what == LUA_GCGEN || what == LUA_GCINC)
		arg = va_arg(argp, int);
	va_end(argp);
	r = H.gc(L, what, arg);
	OSN_CHECK(L);
	return r;
}

LUA_API int lua_error(lua_State *L)
{
	osn_throw(L);
}

LUA_API int lua_next(lua_State *L, int idx)
{
	int r = H.next(L, idx);
	OSN_CHECK(L);
	return r;
}

LUA_API void lua_concat(lua_State *L, int n)
{
	H.concat(L, n);
	OSN_CHECK(L);
}

LUA_API void lua_len(lua_State *L, int idx)
{
	H.len(L, idx);
	OSN_CHECK(L);
}

LUA_API size_t lua_stringtonumber(lua_State *L, const char *s)
{
	size_t r = H.stringtonumber(L, s, strlen(s));
	OSN_CHECK(L);
	return r;
}

LUA_API lua_Alloc lua_getallocf(lua_State *L, void **ud)
{
	if (ud != NULL)
		*ud = G(L)->allocud;
	return G(L)->allocf;
}

LUA_API void lua_setallocf(lua_State *L, lua_Alloc f, void *ud)
{
	G(L)->allocf = f;
	G(L)->allocud = ud;
}

LUA_API void lua_toclose(lua_State *L, int idx)
{
	H.toclose(L, idx);
	OSN_CHECK(L);
}

LUA_API void lua_closeslot(lua_State *L, int idx)
{
	H.closeslot(L, idx);
	OSN_CHECK(L);
}

LUA_API int lua_getstack(lua_State *L, int level, lua_Debug *ar)
{
	int r = H.getstack(L, level, ar);
	OSN_CHECK(L);
	return r;
}

LUA_API int lua_getinfo(lua_State *L, const char *what, lua_Debug *ar)
{
	int r = H.getinfo(L, what, ar);
	OSN_CHECK(L);
	return r;
}

LUA_API const char *lua_getlocal(lua_State *L, const lua_Debug *ar, int n)
{
	(void)L;
	(void)ar;
	(void)n;
	return NULL;
}

LUA_API const char *lua_setlocal(lua_State *L, const lua_Debug *ar, int n)
{
	(void)L;
	(void)ar;
	(void)n;
	return NULL;
}

LUA_API const char *lua_getupvalue(lua_State *L, int funcindex, int n)
{
	const char *r = H.getupvalue(L, funcindex, n);
	OSN_CHECK(L);
	return r;
}

LUA_API const char *lua_setupvalue(lua_State *L, int funcindex, int n)
{
	const char *r = H.setupvalue(L, funcindex, n);
	OSN_CHECK(L);
	return r;
}

LUA_API void *lua_upvalueid(lua_State *L, int fidx, int n)
{
	void *r = H.upvalueid(L, fidx, n);
	OSN_CHECK(L);
	return r;
}

LUA_API void lua_upvaluejoin(lua_State *L, int fidx1, int n1, int fidx2, int n2)
{
	(void)fidx1;
	(void)n1;
	(void)fidx2;
	(void)n2;
	osn_fail(L, "lua_upvaluejoin is not supported by the OpenSpeaker native bridge");
}

LUA_API void lua_sethook(lua_State *L, lua_Hook func, int mask, int count)
{
	(void)L;
	(void)func;
	(void)mask;
	(void)count;
}

LUA_API lua_Hook lua_gethook(lua_State *L)
{
	(void)L;
	return NULL;
}

LUA_API int lua_gethookmask(lua_State *L)
{
	(void)L;
	return 0;
}

LUA_API int lua_gethookcount(lua_State *L)
{
	(void)L;
	return 0;
}

LUA_API int lua_setcstacklimit(lua_State *L, unsigned int limit)
{
	(void)L;
	(void)limit;
	return 200;
}
