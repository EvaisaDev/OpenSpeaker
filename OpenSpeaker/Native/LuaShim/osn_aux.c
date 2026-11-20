#include <errno.h>
#include <stdarg.h>
#include <stdio.h>
#include <stdlib.h>
#include <string.h>

#include "osn_internal.h"
#include "lualib.h"

#define H osn_H

#define OSN_FREELIST (LUA_RIDX_LAST + 1)

static int findfield(lua_State *L, int objidx, int level)
{
	if (level == 0 || !lua_istable(L, -1))
		return 0;
	lua_pushnil(L);
	while (lua_next(L, -2)) {
		if (lua_type(L, -2) == LUA_TSTRING) {
			if (lua_rawequal(L, objidx, -1)) {
				lua_pop(L, 1);
				return 1;
			} else if (findfield(L, objidx, level - 1)) {
				lua_pushliteral(L, ".");
				lua_replace(L, -3);
				lua_concat(L, 3);
				return 1;
			}
		}
		lua_pop(L, 1);
	}
	return 0;
}

static int pushglobalfuncname(lua_State *L, lua_Debug *ar)
{
	int top = lua_gettop(L);
	lua_getinfo(L, "f", ar);
	lua_getfield(L, LUA_REGISTRYINDEX, LUA_LOADED_TABLE);
	if (findfield(L, top + 1, 2)) {
		const char *name = lua_tostring(L, -1);
		if (strncmp(name, LUA_GNAME ".", 3) == 0) {
			lua_pushstring(L, name + 3);
			lua_remove(L, -2);
		}
		lua_copy(L, -1, top + 1);
		lua_settop(L, top + 1);
		return 1;
	}
	lua_settop(L, top);
	return 0;
}

LUALIB_API void luaL_traceback(lua_State *L, lua_State *L1, const char *msg, int level)
{
	H.traceback(L, L1, msg != NULL ? msg : "", msg != NULL ? strlen(msg) : 0, msg != NULL, level);
	OSN_CHECK(L);
}

LUALIB_API void luaL_where(lua_State *L, int level)
{
	lua_Debug ar;
	if (lua_getstack(L, level, &ar)) {
		lua_getinfo(L, "Sl", &ar);
		if (ar.currentline > 0) {
			lua_pushfstring(L, "%s:%d: ", ar.short_src, ar.currentline);
			return;
		}
	}
	lua_pushliteral(L, "");
}

LUALIB_API int luaL_error(lua_State *L, const char *fmt, ...)
{
	va_list argp;
	va_start(argp, fmt);
	luaL_where(L, 1);
	lua_pushvfstring(L, fmt, argp);
	va_end(argp);
	lua_concat(L, 2);
	return lua_error(L);
}

LUALIB_API int luaL_argerror(lua_State *L, int arg, const char *extramsg)
{
	lua_Debug ar;
	if (!lua_getstack(L, 0, &ar))
		return luaL_error(L, "bad argument #%d (%s)", arg, extramsg);
	lua_getinfo(L, "n", &ar);
	if (ar.namewhat != NULL && strcmp(ar.namewhat, "method") == 0) {
		arg--;
		if (arg == 0)
			return luaL_error(L, "calling '%s' on bad self (%s)", ar.name, extramsg);
	}
	if (ar.name == NULL)
		ar.name = pushglobalfuncname(L, &ar) ? lua_tostring(L, -1) : "?";
	return luaL_error(L, "bad argument #%d to '%s' (%s)", arg, ar.name, extramsg);
}

LUALIB_API int luaL_typeerror(lua_State *L, int arg, const char *tname)
{
	const char *msg;
	const char *typearg;
	if (luaL_getmetafield(L, arg, "__name") == LUA_TSTRING)
		typearg = lua_tostring(L, -1);
	else if (lua_type(L, arg) == LUA_TLIGHTUSERDATA)
		typearg = "light userdata";
	else
		typearg = luaL_typename(L, arg);
	msg = lua_pushfstring(L, "%s expected, got %s", tname, typearg);
	return luaL_argerror(L, arg, msg);
}

static void tag_error(lua_State *L, int arg, int tag)
{
	luaL_typeerror(L, arg, lua_typename(L, tag));
}

LUALIB_API int luaL_fileresult(lua_State *L, int stat, const char *fname)
{
	int en = errno;
	const char *msg;
	if (stat) {
		lua_pushboolean(L, 1);
		return 1;
	}
	luaL_pushfail(L);
	msg = en != 0 ? strerror(en) : "(no extra info)";
	if (fname != NULL)
		lua_pushfstring(L, "%s: %s", fname, msg);
	else
		lua_pushstring(L, msg);
	lua_pushinteger(L, en);
	return 3;
}

LUALIB_API int luaL_execresult(lua_State *L, int stat)
{
	if (stat != 0 && errno != 0)
		return luaL_fileresult(L, 0, NULL);
	if (stat == 0)
		lua_pushboolean(L, 1);
	else
		luaL_pushfail(L);
	lua_pushstring(L, "exit");
	lua_pushinteger(L, stat);
	return 3;
}

LUALIB_API int luaL_newmetatable(lua_State *L, const char *tname)
{
	if (luaL_getmetatable(L, tname) != LUA_TNIL)
		return 0;
	lua_pop(L, 1);
	lua_createtable(L, 0, 2);
	lua_pushstring(L, tname);
	lua_setfield(L, -2, "__name");
	lua_pushvalue(L, -1);
	lua_setfield(L, LUA_REGISTRYINDEX, tname);
	return 1;
}

LUALIB_API void luaL_setmetatable(lua_State *L, const char *tname)
{
	luaL_getmetatable(L, tname);
	lua_setmetatable(L, -2);
}

LUALIB_API void *luaL_testudata(lua_State *L, int ud, const char *tname)
{
	void *p = lua_touserdata(L, ud);
	if (p != NULL) {
		if (lua_getmetatable(L, ud)) {
			luaL_getmetatable(L, tname);
			if (!lua_rawequal(L, -1, -2))
				p = NULL;
			lua_pop(L, 2);
			return p;
		}
	}
	return NULL;
}

LUALIB_API void *luaL_checkudata(lua_State *L, int ud, const char *tname)
{
	void *p = luaL_testudata(L, ud, tname);
	luaL_argexpected(L, p != NULL, ud, tname);
	return p;
}

LUALIB_API int luaL_checkoption(lua_State *L, int arg, const char *def, const char *const lst[])
{
	const char *name = def != NULL ? luaL_optstring(L, arg, def) : luaL_checkstring(L, arg);
	int i;
	for (i = 0; lst[i] != NULL; i++)
		if (strcmp(lst[i], name) == 0)
			return i;
	return luaL_argerror(L, arg, lua_pushfstring(L, "invalid option '%s'", name));
}

LUALIB_API void luaL_checkstack(lua_State *L, int space, const char *msg)
{
	if (!lua_checkstack(L, space)) {
		if (msg != NULL)
			luaL_error(L, "stack overflow (%s)", msg);
		else
			luaL_error(L, "stack overflow");
	}
}

LUALIB_API void luaL_checktype(lua_State *L, int arg, int t)
{
	if (lua_type(L, arg) != t)
		tag_error(L, arg, t);
}

LUALIB_API void luaL_checkany(lua_State *L, int arg)
{
	if (lua_type(L, arg) == LUA_TNONE)
		luaL_argerror(L, arg, "value expected");
}

LUALIB_API const char *luaL_checklstring(lua_State *L, int arg, size_t *len)
{
	const char *s = lua_tolstring(L, arg, len);
	if (s == NULL)
		tag_error(L, arg, LUA_TSTRING);
	return s;
}

LUALIB_API const char *luaL_optlstring(lua_State *L, int arg, const char *def, size_t *len)
{
	if (lua_isnoneornil(L, arg)) {
		if (len != NULL)
			*len = def != NULL ? strlen(def) : 0;
		return def;
	}
	return luaL_checklstring(L, arg, len);
}

LUALIB_API lua_Number luaL_checknumber(lua_State *L, int arg)
{
	int isnum;
	lua_Number d = lua_tonumberx(L, arg, &isnum);
	if (!isnum)
		tag_error(L, arg, LUA_TNUMBER);
	return d;
}

LUALIB_API lua_Number luaL_optnumber(lua_State *L, int arg, lua_Number def)
{
	return luaL_opt(L, luaL_checknumber, arg, def);
}

static void interror(lua_State *L, int arg)
{
	if (lua_isnumber(L, arg))
		luaL_argerror(L, arg, "number has no integer representation");
	else
		tag_error(L, arg, LUA_TNUMBER);
}

LUALIB_API lua_Integer luaL_checkinteger(lua_State *L, int arg)
{
	int isnum;
	lua_Integer d = lua_tointegerx(L, arg, &isnum);
	if (!isnum)
		interror(L, arg);
	return d;
}

LUALIB_API lua_Integer luaL_optinteger(lua_State *L, int arg, lua_Integer def)
{
	return luaL_opt(L, luaL_checkinteger, arg, def);
}

typedef struct UBox {
	void *box;
	size_t bsize;
} UBox;

static void *resizebox(lua_State *L, int idx, size_t newsize)
{
	void *ud;
	lua_Alloc allocf = lua_getallocf(L, &ud);
	UBox *box = (UBox *)lua_touserdata(L, idx);
	void *temp;
	if (box == NULL)
		return NULL;
	temp = allocf(ud, box->box, box->bsize, newsize);
	if (temp == NULL && newsize > 0) {
		lua_pushliteral(L, "not enough memory");
		lua_error(L);
	}
	box->box = temp;
	box->bsize = newsize;
	return temp;
}

static int boxgc(lua_State *L)
{
	resizebox(L, 1, 0);
	return 0;
}

static const luaL_Reg boxmt[] = {
	{ "__gc", boxgc },
	{ "__close", boxgc },
	{ NULL, NULL }
};

static void newbox(lua_State *L)
{
	UBox *box = (UBox *)lua_newuserdatauv(L, sizeof(UBox), 0);
	box->box = NULL;
	box->bsize = 0;
	if (luaL_newmetatable(L, "_UBOX*"))
		luaL_setfuncs(L, boxmt, 0);
	lua_setmetatable(L, -2);
}

#define buffonstack(B) ((B)->b != (B)->init.b)

static size_t newbuffsize(luaL_Buffer *B, size_t sz)
{
	size_t newsize = (B->size / 2) * 3;
	if ((size_t)-1 - sz < B->n)
		return (size_t)luaL_error(B->L, "buffer too large");
	if (newsize < B->n + sz)
		newsize = B->n + sz;
	return newsize;
}

static char *prepbuffsize(luaL_Buffer *B, size_t sz, int boxidx)
{
	lua_State *L;
	char *newbuff;
	size_t newsize;
	if (B->size - B->n >= sz)
		return B->b + B->n;
	L = B->L;
	newsize = newbuffsize(B, sz);
	if (buffonstack(B)) {
		newbuff = (char *)resizebox(L, boxidx, newsize);
	} else {
		lua_remove(L, boxidx);
		newbox(L);
		lua_insert(L, boxidx);
		lua_toclose(L, boxidx);
		newbuff = (char *)resizebox(L, boxidx, newsize);
		memcpy(newbuff, B->b, B->n);
	}
	B->b = newbuff;
	B->size = newsize;
	return newbuff + B->n;
}

LUALIB_API char *luaL_prepbuffsize(luaL_Buffer *B, size_t sz)
{
	return prepbuffsize(B, sz, -1);
}

LUALIB_API void luaL_addlstring(luaL_Buffer *B, const char *s, size_t l)
{
	if (l > 0) {
		char *b = prepbuffsize(B, l, -1);
		memcpy(b, s, l);
		luaL_addsize(B, l);
	}
}

LUALIB_API void luaL_addstring(luaL_Buffer *B, const char *s)
{
	luaL_addlstring(B, s, strlen(s));
}

LUALIB_API void luaL_pushresult(luaL_Buffer *B)
{
	lua_State *L = B->L;
	lua_pushlstring(L, B->b, B->n);
	if (buffonstack(B))
		lua_closeslot(L, -2);
	lua_remove(L, -2);
}

LUALIB_API void luaL_pushresultsize(luaL_Buffer *B, size_t sz)
{
	luaL_addsize(B, sz);
	luaL_pushresult(B);
}

LUALIB_API void luaL_addvalue(luaL_Buffer *B)
{
	lua_State *L = B->L;
	size_t len;
	const char *s = lua_tolstring(L, -1, &len);
	char *b = prepbuffsize(B, len, -2);
	memcpy(b, s, len);
	luaL_addsize(B, len);
	lua_pop(L, 1);
}

LUALIB_API void luaL_buffinit(lua_State *L, luaL_Buffer *B)
{
	B->L = L;
	B->b = B->init.b;
	B->n = 0;
	B->size = LUAL_BUFFERSIZE;
	lua_pushlightuserdata(L, (void *)B);
}

LUALIB_API char *luaL_buffinitsize(lua_State *L, luaL_Buffer *B, size_t sz)
{
	luaL_buffinit(L, B);
	return prepbuffsize(B, sz, -1);
}

LUALIB_API int luaL_ref(lua_State *L, int t)
{
	int ref;
	if (lua_isnil(L, -1)) {
		lua_pop(L, 1);
		return LUA_REFNIL;
	}
	t = lua_absindex(L, t);
	if (lua_rawgeti(L, t, OSN_FREELIST) == LUA_TNIL) {
		ref = 0;
		lua_pushinteger(L, 0);
		lua_rawseti(L, t, OSN_FREELIST);
	} else {
		ref = (int)lua_tointeger(L, -1);
	}
	lua_pop(L, 1);
	if (ref != 0) {
		lua_rawgeti(L, t, ref);
		lua_rawseti(L, t, OSN_FREELIST);
	} else {
		ref = (int)lua_rawlen(L, t) + 1;
	}
	lua_rawseti(L, t, ref);
	return ref;
}

LUALIB_API void luaL_unref(lua_State *L, int t, int ref)
{
	if (ref >= 0) {
		t = lua_absindex(L, t);
		lua_rawgeti(L, t, OSN_FREELIST);
		lua_rawseti(L, t, ref);
		lua_pushinteger(L, ref);
		lua_rawseti(L, t, OSN_FREELIST);
	}
}

LUALIB_API int luaL_loadfilex(lua_State *L, const char *filename, const char *mode)
{
	int st;
	if (filename == NULL) {
		lua_pushliteral(L, "cannot read stdin from a native module");
		return LUA_ERRFILE;
	}
	st = H.loadfile(L, filename, strlen(filename), mode != NULL ? mode : "bt");
	OSN_CHECK(L);
	return st;
}

LUALIB_API int luaL_loadbufferx(lua_State *L, const char *buff, size_t size, const char *name, const char *mode)
{
	return osn_loadbuffer(L, buff, size, name, mode);
}

LUALIB_API int luaL_loadstring(lua_State *L, const char *s)
{
	return luaL_loadbuffer(L, s, strlen(s), s);
}

LUALIB_API int luaL_getmetafield(lua_State *L, int obj, const char *event)
{
	int tt;
	if (!lua_getmetatable(L, obj))
		return LUA_TNIL;
	lua_pushstring(L, event);
	tt = lua_rawget(L, -2);
	if (tt == LUA_TNIL)
		lua_pop(L, 2);
	else
		lua_remove(L, -2);
	return tt;
}

LUALIB_API int luaL_callmeta(lua_State *L, int obj, const char *event)
{
	obj = lua_absindex(L, obj);
	if (luaL_getmetafield(L, obj, event) == LUA_TNIL)
		return 0;
	lua_pushvalue(L, obj);
	lua_call(L, 1, 1);
	return 1;
}

LUALIB_API lua_Integer luaL_len(lua_State *L, int idx)
{
	lua_Integer l;
	int isnum;
	lua_len(L, idx);
	l = lua_tointegerx(L, -1, &isnum);
	if (!isnum)
		luaL_error(L, "object length is not an integer");
	lua_pop(L, 1);
	return l;
}

LUALIB_API const char *luaL_tolstring(lua_State *L, int idx, size_t *len)
{
	idx = lua_absindex(L, idx);
	if (luaL_callmeta(L, idx, "__tostring")) {
		if (!lua_isstring(L, -1))
			luaL_error(L, "'__tostring' must return a string");
	} else {
		switch (lua_type(L, idx)) {
		case LUA_TNUMBER:
			if (lua_isinteger(L, idx))
				lua_pushfstring(L, "%I", (LUAI_UACINT)lua_tointeger(L, idx));
			else
				lua_pushfstring(L, "%f", (LUAI_UACNUMBER)lua_tonumber(L, idx));
			break;
		case LUA_TSTRING:
			lua_pushvalue(L, idx);
			break;
		case LUA_TBOOLEAN:
			lua_pushstring(L, lua_toboolean(L, idx) ? "true" : "false");
			break;
		case LUA_TNIL:
			lua_pushliteral(L, "nil");
			break;
		default: {
			int tt = luaL_getmetafield(L, idx, "__name");
			const char *kind = tt == LUA_TSTRING ? lua_tostring(L, -1) : luaL_typename(L, idx);
			lua_pushfstring(L, "%s: %p", kind, lua_topointer(L, idx));
			if (tt != LUA_TNIL)
				lua_remove(L, -2);
			break;
		}
		}
	}
	return lua_tolstring(L, -1, len);
}

LUALIB_API void luaL_setfuncs(lua_State *L, const luaL_Reg *l, int nup)
{
	luaL_checkstack(L, nup, "too many upvalues");
	for (; l->name != NULL; l++) {
		if (l->func == NULL) {
			lua_pushboolean(L, 0);
		} else {
			int i;
			for (i = 0; i < nup; i++)
				lua_pushvalue(L, -nup);
			osn_pushnamedcclosure(L, l->func, nup, l->name);
		}
		lua_setfield(L, -(nup + 2), l->name);
	}
	lua_pop(L, nup);
}

LUALIB_API int luaL_getsubtable(lua_State *L, int idx, const char *fname)
{
	if (lua_getfield(L, idx, fname) == LUA_TTABLE)
		return 1;
	lua_pop(L, 1);
	idx = lua_absindex(L, idx);
	lua_newtable(L);
	lua_pushvalue(L, -1);
	lua_setfield(L, idx, fname);
	return 0;
}

LUALIB_API void luaL_requiref(lua_State *L, const char *modname, lua_CFunction openf, int glb)
{
	luaL_getsubtable(L, LUA_REGISTRYINDEX, LUA_LOADED_TABLE);
	lua_getfield(L, -1, modname);
	if (!lua_toboolean(L, -1)) {
		lua_pop(L, 1);
		osn_pushnamedcclosure(L, openf, 0, modname);
		lua_pushstring(L, modname);
		lua_call(L, 1, 1);
		lua_pushvalue(L, -1);
		lua_setfield(L, -3, modname);
	}
	lua_remove(L, -2);
	if (glb) {
		lua_pushvalue(L, -1);
		lua_setglobal(L, modname);
	}
}

LUALIB_API void luaL_addgsub(luaL_Buffer *b, const char *s, const char *p, const char *r)
{
	const char *wild;
	size_t l = strlen(p);
	while ((wild = strstr(s, p)) != NULL) {
		luaL_addlstring(b, s, (size_t)(wild - s));
		luaL_addstring(b, r);
		s = wild + l;
	}
	luaL_addstring(b, s);
}

LUALIB_API const char *luaL_gsub(lua_State *L, const char *s, const char *p, const char *r)
{
	luaL_Buffer b;
	luaL_buffinit(L, &b);
	luaL_addgsub(&b, s, p, r);
	luaL_pushresult(&b);
	return lua_tostring(L, -1);
}

LUALIB_API lua_State *luaL_newstate(void)
{
	return NULL;
}

LUALIB_API void luaL_checkversion_(lua_State *L, lua_Number ver, size_t sz)
{
	lua_Number v = lua_version(L);
	if (sz != LUAL_NUMSIZES)
		luaL_error(L, "core and library have incompatible numeric types");
	else if (v != ver)
		luaL_error(L, "version mismatch: app. needs %f, Lua core provides %f", (LUAI_UACNUMBER)ver, (LUAI_UACNUMBER)v);
}

LUALIB_API void luaL_openlibs(lua_State *L)
{
	(void)L;
}

static int openlib(lua_State *L, const char *name)
{
	H.openlib(L, name);
	OSN_CHECK(L);
	return 1;
}

LUAMOD_API int luaopen_base(lua_State *L)
{
	return openlib(L, LUA_GNAME);
}

LUAMOD_API int luaopen_coroutine(lua_State *L)
{
	return openlib(L, LUA_COLIBNAME);
}

LUAMOD_API int luaopen_table(lua_State *L)
{
	return openlib(L, LUA_TABLIBNAME);
}

LUAMOD_API int luaopen_io(lua_State *L)
{
	return openlib(L, LUA_IOLIBNAME);
}

LUAMOD_API int luaopen_os(lua_State *L)
{
	return openlib(L, LUA_OSLIBNAME);
}

LUAMOD_API int luaopen_string(lua_State *L)
{
	return openlib(L, LUA_STRLIBNAME);
}

LUAMOD_API int luaopen_utf8(lua_State *L)
{
	return openlib(L, LUA_UTF8LIBNAME);
}

LUAMOD_API int luaopen_math(lua_State *L)
{
	return openlib(L, LUA_MATHLIBNAME);
}

LUAMOD_API int luaopen_debug(lua_State *L)
{
	return openlib(L, LUA_DBLIBNAME);
}

LUAMOD_API int luaopen_package(lua_State *L)
{
	return openlib(L, LUA_LOADLIBNAME);
}
