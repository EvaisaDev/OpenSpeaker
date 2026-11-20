#ifndef luaconf_h
#define luaconf_h

#include <limits.h>
#include <stddef.h>
#include <stdint.h>

#define LUAI_IS32INT ((UINT_MAX >> 30) >= 3)

#define LUA_INT_INT 1
#define LUA_INT_LONG 2
#define LUA_INT_LONGLONG 3
#define LUA_FLOAT_FLOAT 1
#define LUA_FLOAT_DOUBLE 2
#define LUA_FLOAT_LONGDOUBLE 3
#define LUA_INT_TYPE LUA_INT_LONGLONG
#define LUA_FLOAT_TYPE LUA_FLOAT_DOUBLE

#define LUA_DIRSEP "\\"
#define LUA_PATH_SEP ";"
#define LUA_PATH_MARK "?"
#define LUA_EXEC_DIR "!"
#define LUA_IGMARK "-"

#if defined(LUA_BUILD_AS_DLL)
#if defined(LUA_CORE) || defined(LUA_LIB)
#define LUA_API __declspec(dllexport)
#else
#define LUA_API __declspec(dllimport)
#endif
#else
#define LUA_API extern
#endif

#define LUALIB_API LUA_API
#define LUAMOD_API LUA_API

#define LUAI_FUNC extern
#define LUAI_DDEC(dec) LUAI_FUNC dec
#define LUAI_DDEF

#define LUA_NUMBER double
#define l_floatatt(n) (DBL_##n)
#define LUAI_UACNUMBER double
#define LUA_NUMBER_FRMLEN ""
#define LUA_NUMBER_FMT "%.14g"
#define l_mathop(op) op
#define lua_str2number(s,p) strtod((s), (p))

#define LUA_INTEGER long long
#define LUA_INTEGER_FRMLEN "ll"
#define LUAI_UACINT LUA_INTEGER
#define LUA_INTEGER_FMT "%" LUA_INTEGER_FRMLEN "d"
#define LUA_UNSIGNED unsigned LUAI_UACINT
#define LUA_MAXUNSIGNED ULLONG_MAX
#define LUA_MAXINTEGER LLONG_MAX
#define LUA_MININTEGER LLONG_MIN

#define l_sprintf(s,sz,f,i) snprintf(s,sz,f,i)
#define lua_number2str(s,sz,n) l_sprintf((s), sz, LUA_NUMBER_FMT, (LUAI_UACNUMBER)(n))
#define lua_integer2str(s,sz,n) l_sprintf((s), sz, LUA_INTEGER_FMT, (LUAI_UACINT)(n))
#define LUAI_NUMFFORMAT "%.14g"

#define lua_numbertointeger(n,p) \
	((n) >= (LUA_NUMBER)(LUA_MININTEGER) && \
	(n) < -(LUA_NUMBER)(LUA_MININTEGER) && \
	(*(p) = (LUA_INTEGER)(n), 1))

#define lua_pointer2str(buff,sz,p) l_sprintf(buff,sz,"%p",p)
#define lua_getlocaledecpoint() '.'

#define LUA_KCONTEXT intptr_t

#define LUAI_MAXSTACK 1000000
#define LUA_EXTRASPACE (sizeof(void *))
#define LUA_IDSIZE 60
#define LUAL_BUFFERSIZE ((int)(16 * sizeof(void*) * sizeof(lua_Number)))
#define LUAI_MAXALIGN lua_Number n; double u; void *s; lua_Integer i; long l

#if defined(LUA_COMPAT_5_3)
#define LUA_COMPAT_MATHLIB
#define LUA_COMPAT_APIINTCASTS
#define LUA_COMPAT_LT_LE
#define lua_strlen(L,i) lua_rawlen(L, (i))
#define lua_objlen(L,i) lua_rawlen(L, (i))
#define lua_equal(L,idx1,idx2) lua_compare(L,(idx1),(idx2),LUA_OPEQ)
#define lua_lessthan(L,idx1,idx2) lua_compare(L,(idx1),(idx2),LUA_OPLT)
#endif

#endif
