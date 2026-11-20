#ifndef osn_internal_h
#define osn_internal_h

#include <setjmp.h>

#include "lua.h"
#include "lauxlib.h"

typedef struct osn_jmp {
	struct osn_jmp *prev;
	jmp_buf buf;
} osn_jmp;

struct lua_State {
	int errflag;
	int status;
	osn_jmp *jmp;
	lua_CFunction panic;
	lua_WarnFunction warnf;
	void *warnud;
	lua_Alloc allocf;
	void *allocud;
	void *host;
	struct lua_State *mainthread;
};

typedef struct osn_host {
	int (*absindex)(lua_State *L, int idx);
	int (*gettop)(lua_State *L);
	void (*settop)(lua_State *L, int idx);
	void (*pushvalue)(lua_State *L, int idx);
	void (*rotate)(lua_State *L, int idx, int n);
	void (*copy)(lua_State *L, int fromidx, int toidx);
	void (*xmove)(lua_State *from, lua_State *to, int n);
	int (*isnumber)(lua_State *L, int idx);
	int (*isstring)(lua_State *L, int idx);
	int (*iscfunction)(lua_State *L, int idx);
	int (*isinteger)(lua_State *L, int idx);
	int (*isuserdata)(lua_State *L, int idx);
	int (*type)(lua_State *L, int idx);
	lua_Number (*tonumberx)(lua_State *L, int idx, int *isnum);
	lua_Integer (*tointegerx)(lua_State *L, int idx, int *isnum);
	int (*toboolean)(lua_State *L, int idx);
	const char *(*tolstring)(lua_State *L, int idx, size_t *len);
	lua_Unsigned (*rawlen)(lua_State *L, int idx);
	lua_CFunction (*tocfunction)(lua_State *L, int idx);
	void *(*touserdata)(lua_State *L, int idx);
	lua_State *(*tothread)(lua_State *L, int idx);
	const void *(*topointer)(lua_State *L, int idx);
	void (*arith)(lua_State *L, int op);
	int (*rawequal)(lua_State *L, int idx1, int idx2);
	int (*compare)(lua_State *L, int idx1, int idx2, int op);
	void (*pushnil)(lua_State *L);
	void (*pushnumber)(lua_State *L, lua_Number n);
	void (*pushinteger)(lua_State *L, lua_Integer n);
	const char *(*pushlstring)(lua_State *L, const char *s, size_t len);
	void (*pushcclosure)(lua_State *L, lua_CFunction fn, int n, const char *name);
	void (*pushboolean)(lua_State *L, int b);
	void (*pushlightuserdata)(lua_State *L, void *p);
	int (*pushthread)(lua_State *L);
	int (*getglobal)(lua_State *L, const char *name, size_t len);
	int (*gettable)(lua_State *L, int idx);
	int (*getfield)(lua_State *L, int idx, const char *k, size_t len);
	int (*geti)(lua_State *L, int idx, lua_Integer n);
	int (*rawget)(lua_State *L, int idx);
	int (*rawgeti)(lua_State *L, int idx, lua_Integer n);
	int (*rawgetp)(lua_State *L, int idx, const void *p);
	void (*createtable)(lua_State *L, int narr, int nrec);
	void *(*newuserdatauv)(lua_State *L, size_t sz, int nuvalue);
	int (*getmetatable)(lua_State *L, int idx);
	int (*getiuservalue)(lua_State *L, int idx, int n);
	void (*setglobal)(lua_State *L, const char *name, size_t len);
	void (*settable)(lua_State *L, int idx);
	void (*setfield)(lua_State *L, int idx, const char *k, size_t len);
	void (*seti)(lua_State *L, int idx, lua_Integer n);
	void (*rawset)(lua_State *L, int idx);
	void (*rawseti)(lua_State *L, int idx, lua_Integer n);
	void (*rawsetp)(lua_State *L, int idx, const void *p);
	int (*setmetatable)(lua_State *L, int idx);
	int (*setiuservalue)(lua_State *L, int idx, int n);
	void (*call)(lua_State *L, int nargs, int nresults);
	int (*pcall)(lua_State *L, int nargs, int nresults, int errfunc);
	int (*load)(lua_State *L, const char *buf, size_t size, const char *chunkname, size_t namelen, const char *mode);
	int (*dump)(lua_State *L, int strip, char **out, size_t *outsz);
	void (*freemem)(void *p);
	int (*next)(lua_State *L, int idx);
	void (*concat)(lua_State *L, int n);
	void (*len)(lua_State *L, int idx);
	size_t (*stringtonumber)(lua_State *L, const char *s, size_t len);
	int (*gc)(lua_State *L, int what, int arg);
	void (*toclose)(lua_State *L, int idx);
	void (*closeslot)(lua_State *L, int idx);
	void (*warning)(lua_State *L, const char *msg, size_t len, int tocont);
	int (*getstack)(lua_State *L, int level, lua_Debug *ar);
	int (*getinfo)(lua_State *L, const char *what, lua_Debug *ar);
	const char *(*getupvalue)(lua_State *L, int funcindex, int n);
	const char *(*setupvalue)(lua_State *L, int funcindex, int n);
	void *(*upvalueid)(lua_State *L, int fidx, int n);
	void (*traceback)(lua_State *L, lua_State *L1, const char *msg, size_t len, int hasmsg, int level);
	int (*loadfile)(lua_State *L, const char *filename, size_t len, const char *mode);
	void (*openlib)(lua_State *L, const char *name);
	lua_State *(*newthread)(lua_State *L);
	int (*resume)(lua_State *L, lua_State *from, int narg, int *nres);
	void (*closethread)(lua_State *L);
} osn_host;

extern osn_host osn_H;

__declspec(noreturn) void osn_throw(lua_State *L);
void osn_pushnamedcclosure(lua_State *L, lua_CFunction fn, int n, const char *name);
int osn_loadbuffer(lua_State *L, const char *buf, size_t size, const char *chunkname, const char *mode);

#define G(L) ((L)->mainthread)

#define OSN_CHECK(L) do { if ((L)->errflag) osn_throw(L); } while (0)

#endif
