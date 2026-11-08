using System;
using System.IO;
using System.Text;

namespace SelectBar.Setup.Logic;

/// <summary>Découpage d'un Setup en deux parties : le « stub » (exécutable fichier unique, sans charge utile) et la charge utile.</summary>
public readonly record struct OverlayInfo(long StubLength, long PayloadLength)
{
    public long PayloadOffset => StubLength;
}

/// <summary>
/// Format du Setup : <c>[stub][payload.zip][trailer 24 octets]</c>, la charge utile étant ajoutée À LA FIN de l'exécutable par scripts\build-setup.ps1
/// (le stub signé reste intact au début, le runtime .NET ignore ce qui suit le fichier unique).
/// Trailer : magie « SELPAYL1 » (8 octets), longueur du stub (Int64 little-endian), longueur de la charge utile (Int64 little-endian).
/// Le stub seul est exactement ce qu'on installe comme <c>Uninstall.exe</c> : il ne contient pas la charge utile.
/// Logique pure (aucune dépendance Windows), testée dans SelectBar.Setup.Tests.
/// </summary>
public static class OverlayTrailer
{
    public const int Size = 24;
    private static readonly byte[] Magic = Encoding.ASCII.GetBytes("SELPAYL1");

    public static byte[] Build(long stubLength, long payloadLength)
    {
        var t = new byte[Size];
        Array.Copy(Magic, t, Magic.Length);
        BitConverter.GetBytes(stubLength).CopyTo(t, 8);
        BitConverter.GetBytes(payloadLength).CopyTo(t, 16);
        if (!BitConverter.IsLittleEndian) { Array.Reverse(t, 8, 8); Array.Reverse(t, 16, 8); }
        return t;
    }

    /// <summary>Lit le trailer à la fin du flux ; null s'il est absent ou incohérent (exécutable sans charge utile, dev, ancien Setup).</summary>
    public static OverlayInfo? TryRead(Stream s)
    {
        try
        {
            if (!s.CanSeek || s.Length < Size + 1) return null;
            var t = new byte[Size];
            s.Seek(-Size, SeekOrigin.End);
            var read = 0;
            while (read < Size) { var n = s.Read(t, read, Size - read); if (n <= 0) return null; read += n; }
            for (var i = 0; i < Magic.Length; i++) if (t[i] != Magic[i]) return null;
            if (!BitConverter.IsLittleEndian) { Array.Reverse(t, 8, 8); Array.Reverse(t, 16, 8); }
            var stub = BitConverter.ToInt64(t, 8);
            var payload = BitConverter.ToInt64(t, 16);
            if (stub <= 0 || payload <= 0) return null;
            if (stub + payload + Size != s.Length) return null;
            return new OverlayInfo(stub, payload);
        }
        catch (IOException) { return null; }
    }

    public static OverlayInfo? TryReadFile(string? path)
    {
        if (string.IsNullOrEmpty(path)) return null;
        try
        {
            using var fs = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
            return TryRead(fs);
        }
        catch (Exception) { return null; }
    }

    /// <summary>Copie les <paramref name="length"/> premiers octets de <paramref name="source"/> vers <paramref name="destination"/> (écrasé).</summary>
    public static void CopyPrefix(string source, string destination, long length)
    {
        using var src = new FileStream(source, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete, 1 << 16);
        using var dst = new FileStream(destination, FileMode.Create, FileAccess.Write, FileShare.None, 1 << 16);
        var buffer = new byte[1 << 16];
        var left = length;
        while (left > 0)
        {
            var n = src.Read(buffer, 0, (int)Math.Min(buffer.Length, left));
            if (n <= 0) throw new EndOfStreamException("Setup tronqué : " + source);
            dst.Write(buffer, 0, n);
            left -= n;
        }
    }

    /// <summary>Flux en lecture seule et navigable sur une partie d'un fichier (la charge utile), pour ZipArchive.</summary>
    public sealed class Slice : Stream
    {
        private readonly Stream _inner;
        private readonly long _offset, _length;
        private long _pos;

        public Slice(Stream inner, long offset, long length) { _inner = inner; _offset = offset; _length = length; }

        public override bool CanRead => true;
        public override bool CanSeek => true;
        public override bool CanWrite => false;
        public override long Length => _length;
        public override long Position { get => _pos; set => Seek(value, SeekOrigin.Begin); }
        public override void Flush() { }

        public override int Read(byte[] buffer, int offset, int count)
        {
            var left = _length - _pos;
            if (left <= 0) return 0;
            if (count > left) count = (int)left;
            _inner.Seek(_offset + _pos, SeekOrigin.Begin);
            var n = _inner.Read(buffer, offset, count);
            _pos += n;
            return n;
        }

        public override long Seek(long offset, SeekOrigin origin)
        {
            var target = origin switch { SeekOrigin.Begin => offset, SeekOrigin.Current => _pos + offset, _ => _length + offset };
            if (target < 0) throw new IOException("Position négative.");
            _pos = target;
            return _pos;
        }

        public override void SetLength(long value) => throw new NotSupportedException();
        public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
        protected override void Dispose(bool disposing) { if (disposing) _inner.Dispose(); base.Dispose(disposing); }
    }
}
