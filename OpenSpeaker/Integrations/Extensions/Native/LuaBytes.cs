using System.Buffers;
using System.Globalization;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.Unicode;

namespace OpenSpeaker.Extensions.Native;

internal sealed class NativeString
{
	public readonly IntPtr Pointer;
	public readonly int Length;

	public NativeString(ReadOnlySpan<byte> bytes)
	{
		Length = bytes.Length;
		Pointer = Marshal.AllocHGlobal(bytes.Length + 1);
		var buffer = ArrayPool<byte>.Shared.Rent(bytes.Length + 1);
		try
		{
			bytes.CopyTo(buffer);
			buffer[bytes.Length] = 0;
			Marshal.Copy(buffer, 0, Pointer, bytes.Length + 1);
		}
		finally
		{
			ArrayPool<byte>.Shared.Return(buffer);
		}
	}

	~NativeString()
	{
		Marshal.FreeHGlobal(Pointer);
	}
}

internal static class LuaBytes
{
	private static readonly ConditionalWeakTable<string, NativeString> Cache = new();

	public static NativeString Native(string s)
	{
		if (Cache.TryGetValue(s, out var existing))
			return existing;
		var native = new NativeString(Encode(s));
		return Cache.GetValue(s, _ => native);
	}

	public static string Intern(ReadOnlySpan<byte> bytes)
	{
		var s = Decode(bytes);
		if (!Cache.TryGetValue(s, out _))
		{
			var native = new NativeString(bytes);
			Cache.GetValue(s, _ => native);
		}
		return s;
	}

	public static int ByteLength(string s)
	{
		var n = 0;
		for (var i = 0; i < s.Length; i++)
		{
			var c = s[i];
			if (c < 0x80)
				n++;
			else if (c < 0x800)
				n += 2;
			else if (c >= 0xDC80 && c <= 0xDCFF)
				n++;
			else if (char.IsHighSurrogate(c) && i + 1 < s.Length && char.IsLowSurrogate(s[i + 1]))
			{
				n += 4;
				i++;
			}
			else
				n += 3;
		}
		return n;
	}

	public static byte[] Encode(string s)
	{
		var result = new byte[ByteLength(s)];
		var o = 0;
		for (var i = 0; i < s.Length; i++)
		{
			int c = s[i];
			if (c < 0x80)
				result[o++] = (byte)c;
			else if (c < 0x800)
			{
				result[o++] = (byte)(0xC0 | (c >> 6));
				result[o++] = (byte)(0x80 | (c & 0x3F));
			}
			else if (c >= 0xDC80 && c <= 0xDCFF)
				result[o++] = (byte)(c - 0xDC00);
			else if (char.IsHighSurrogate((char)c) && i + 1 < s.Length && char.IsLowSurrogate(s[i + 1]))
			{
				var cp = char.ConvertToUtf32((char)c, s[i + 1]);
				i++;
				result[o++] = (byte)(0xF0 | (cp >> 18));
				result[o++] = (byte)(0x80 | ((cp >> 12) & 0x3F));
				result[o++] = (byte)(0x80 | ((cp >> 6) & 0x3F));
				result[o++] = (byte)(0x80 | (cp & 0x3F));
			}
			else
			{
				result[o++] = (byte)(0xE0 | (c >> 12));
				result[o++] = (byte)(0x80 | ((c >> 6) & 0x3F));
				result[o++] = (byte)(0x80 | (c & 0x3F));
			}
		}
		return result;
	}

	public static string Decode(ReadOnlySpan<byte> bytes)
	{
		if (bytes.Length == 0)
			return string.Empty;
		if (Utf8.IsValid(bytes))
			return Encoding.UTF8.GetString(bytes);
		var sb = new StringBuilder(bytes.Length);
		var i = 0;
		while (i < bytes.Length)
		{
			var b = bytes[i];
			if (b < 0x80)
			{
				sb.Append((char)b);
				i++;
				continue;
			}
			var len = b >= 0xF0 && b <= 0xF4 ? 4 : b >= 0xE0 ? (b <= 0xEF ? 3 : 0) : b >= 0xC2 ? 2 : 0;
			if (len > 0 && i + len <= bytes.Length && Rune.DecodeFromUtf8(bytes.Slice(i, len), out var rune, out var consumed) == OperationStatus.Done && consumed == len)
			{
				sb.Append(rune.ToString());
				i += len;
				continue;
			}
			sb.Append((char)(0xDC00 + b));
			i++;
		}
		return sb.ToString();
	}

	public static string FromPointer(IntPtr p, nuint len, bool intern = false)
	{
		if (p == IntPtr.Zero || len == 0)
			return string.Empty;
		var n = checked((int)len);
		var buffer = ArrayPool<byte>.Shared.Rent(n);
		try
		{
			Marshal.Copy(p, buffer, 0, n);
			return intern ? Intern(buffer.AsSpan(0, n)) : Decode(buffer.AsSpan(0, n));
		}
		finally
		{
			ArrayPool<byte>.Shared.Return(buffer);
		}
	}

	public static byte[] CopyBytes(IntPtr p, nuint len)
	{
		var n = checked((int)len);
		var result = new byte[n];
		if (n > 0)
			Marshal.Copy(p, result, 0, n);
		return result;
	}

	public static string? FromCString(IntPtr p)
	{
		if (p == IntPtr.Zero)
			return null;
		var n = 0;
		while (Marshal.ReadByte(p, n) != 0)
			n++;
		return FromPointer(p, (nuint)n);
	}

	public static bool TryParseNumber(string s, out double value)
	{
		value = 0;
		var t = s.AsSpan().Trim(" \f\n\r\t\v");
		if (t.Length == 0)
			return false;
		var negative = false;
		var body = t;
		if (body[0] == '+' || body[0] == '-')
		{
			negative = body[0] == '-';
			body = body[1..];
		}
		if (body.Length > 1 && body[0] == '0' && (body[1] == 'x' || body[1] == 'X'))
		{
			if (!TryParseHex(body[2..], out value))
				return false;
			if (negative)
				value = -value;
			return true;
		}
		if (body.Length == 0)
			return false;
		var digits = false;
		foreach (var c in body)
		{
			if (c >= '0' && c <= '9')
				digits = true;
			else if (c != '.' && c != 'e' && c != 'E' && c != '+' && c != '-')
				return false;
		}
		if (!digits)
			return false;
		return double.TryParse(t, NumberStyles.Float, CultureInfo.InvariantCulture, out value);
	}

	private static bool TryParseHex(ReadOnlySpan<char> s, out double value)
	{
		value = 0;
		ulong mantissa = 0;
		var exponent = 0;
		var anyDigit = false;
		var seenDot = false;
		var isFloat = false;
		var i = 0;
		for (; i < s.Length; i++)
		{
			var c = s[i];
			if (c == '.')
			{
				if (seenDot)
					return false;
				seenDot = true;
				isFloat = true;
				continue;
			}
			var d = HexDigit(c);
			if (d < 0)
				break;
			anyDigit = true;
			if (isFloat)
			{
				if (mantissa < (1UL << 60))
				{
					mantissa = (mantissa << 4) | (uint)d;
					exponent -= 4;
				}
			}
			else
				mantissa = (mantissa << 4) | (uint)d;
		}
		if (!anyDigit)
			return false;
		if (i < s.Length && (s[i] == 'p' || s[i] == 'P'))
		{
			isFloat = true;
			i++;
			var expNeg = false;
			if (i < s.Length && (s[i] == '+' || s[i] == '-'))
			{
				expNeg = s[i] == '-';
				i++;
			}
			var e = 0;
			var expDigits = false;
			for (; i < s.Length && s[i] >= '0' && s[i] <= '9'; i++)
			{
				expDigits = true;
				e = Math.Min(e * 10 + (s[i] - '0'), 100000);
			}
			if (!expDigits)
				return false;
			exponent += expNeg ? -e : e;
		}
		if (i != s.Length)
			return false;
		value = isFloat ? mantissa * Math.Pow(2, exponent) : (long)mantissa;
		return true;
	}

	private static int HexDigit(char c)
	{
		if (c >= '0' && c <= '9')
			return c - '0';
		if (c >= 'a' && c <= 'f')
			return c - 'a' + 10;
		if (c >= 'A' && c <= 'F')
			return c - 'A' + 10;
		return -1;
	}

	public static string ChunkId(string source)
	{
		const int max = 59;
		if (source.Length > 0 && source[0] == '=')
		{
			var rest = source[1..];
			return rest.Length <= max ? rest : rest[..max];
		}
		if (source.Length > 0 && source[0] == '@')
		{
			var rest = source[1..];
			return rest.Length <= max ? rest : "..." + rest[^(max - 3)..];
		}
		var nl = source.IndexOf('\n');
		const string pre = "[string \"";
		const string pos = "\"]";
		var room = max - pre.Length - 3 - pos.Length;
		if (source.Length <= room && nl < 0)
			return pre + source + pos;
		var line = nl >= 0 ? source[..nl] : source;
		if (line.Length > room)
			line = line[..room];
		return pre + line + "..." + pos;
	}
}
