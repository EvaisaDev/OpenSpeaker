using System.IO;
using System.Text;
using Org.BouncyCastle.Crypto.Digests;
using Org.BouncyCastle.Crypto.Parameters;
using Org.BouncyCastle.Crypto.Signers;
namespace OpenSpeaker.Services;

public static class Minisign
{
    private const string TrustedPrefix = "trusted comment: ";

    public static bool VerifyFile(string filePath, string signatureContent, string publicKey)
    {
        try
        {
            if (!TryDecodePublicKey(publicKey, out var pkKeyId, out var pkKey)) return false;
            if (!TryParseSignature(signatureContent, out var alg, out var sigKeyId, out var signature, out var trustedComment, out var globalSignature)) return false;
            if (!pkKeyId.AsSpan().SequenceEqual(sigKeyId)) return false;

            var content = File.ReadAllBytes(filePath);
            byte[] message;
            if (alg[1] == (byte)'D')
            {
                var digest = new Blake2bDigest(512);
                digest.BlockUpdate(content, 0, content.Length);
                message = new byte[64];
                digest.DoFinal(message, 0);
            }
            else
            {
                message = content;
            }

            if (!Verify(pkKey, message, signature)) return false;

            var trustedBytes = Encoding.UTF8.GetBytes(trustedComment);
            var globalMessage = new byte[signature.Length + trustedBytes.Length];
            Buffer.BlockCopy(signature, 0, globalMessage, 0, signature.Length);
            Buffer.BlockCopy(trustedBytes, 0, globalMessage, signature.Length, trustedBytes.Length);
            return Verify(pkKey, globalMessage, globalSignature);
        }
        catch
        {
            return false;
        }
    }

    private static bool Verify(byte[] publicKey, byte[] message, byte[] signature)
    {
        var verifier = new Ed25519Signer();
        verifier.Init(false, new Ed25519PublicKeyParameters(publicKey, 0));
        verifier.BlockUpdate(message, 0, message.Length);
        return verifier.VerifySignature(signature);
    }

    private static bool TryDecodePublicKey(string text, out byte[] keyId, out byte[] key)
    {
        keyId = Array.Empty<byte>();
        key = Array.Empty<byte>();
        var b64 = ExtractBase64Line(text);
        if (b64 == null) return false;
        var raw = Convert.FromBase64String(b64);
        if (raw.Length != 42) return false;
        keyId = raw[2..10];
        key = raw[10..42];
        return true;
    }

    private static bool TryParseSignature(string content, out byte[] alg, out byte[] keyId, out byte[] signature, out string trustedComment, out byte[] globalSignature)
    {
        alg = Array.Empty<byte>();
        keyId = Array.Empty<byte>();
        signature = Array.Empty<byte>();
        trustedComment = "";
        globalSignature = Array.Empty<byte>();

        var lines = content.Replace("\r\n", "\n").Replace("\r", "\n").Split('\n');
        string? sigB64 = null;
        string? globalB64 = null;
        for (var i = 0; i < lines.Length; i++)
        {
            var line = lines[i];
            if (line.StartsWith("untrusted comment:"))
            {
                if (i + 1 < lines.Length) sigB64 = lines[i + 1].Trim();
            }
            else if (line.StartsWith(TrustedPrefix))
            {
                trustedComment = line.Substring(TrustedPrefix.Length);
                if (i + 1 < lines.Length) globalB64 = lines[i + 1].Trim();
            }
        }

        if (sigB64 == null || globalB64 == null) return false;
        var sigRaw = Convert.FromBase64String(sigB64);
        if (sigRaw.Length != 74) return false;
        alg = sigRaw[0..2];
        keyId = sigRaw[2..10];
        signature = sigRaw[10..74];
        globalSignature = Convert.FromBase64String(globalB64);
        return globalSignature.Length == 64;
    }

    private static string? ExtractBase64Line(string text)
    {
        foreach (var raw in text.Replace("\r\n", "\n").Replace("\r", "\n").Split('\n'))
        {
            var line = raw.Trim();
            if (line.Length == 0) continue;
            if (line.StartsWith("untrusted comment:")) continue;
            if (line.StartsWith("trusted comment:")) continue;
            return line;
        }
        return null;
    }
}
