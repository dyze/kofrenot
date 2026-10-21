using System.Buffers.Binary;
using System.Security.Cryptography;

namespace Kofrenot.Core.Storage;

/// <summary>
/// Encrypts and authenticates a stream of any size with AES-256-GCM, in chunks of
/// <see cref="ChunkSize"/>: <c>header · nonce prefix · (chunk · tag)…</c>, the header a magic and
/// maybe more plain bytes (a key epoch), as in <see cref="SecretBox"/>. Each chunk's nonce is the
/// prefix and its index, and its associated data says whether it is the last, so chunks cannot be
/// changed, moved, left out, added or cut off without failing to open. <c>associated</c> binds the
/// data to its place, as in <see cref="SecretBox"/>.
/// </summary>
public sealed class ChunkedBox
{
    public const int ChunkSize = 1 << 20;
    private const int PrefixSize = 8;
    private const int NonceSize = 12;
    private const int TagSize = 16;

    private readonly byte[] _key;

    public ChunkedBox(byte[] key)
    {
        if (key.Length != SecretBox.KeySize)
        {
            throw new ArgumentException($"The key must be {SecretBox.KeySize} bytes", nameof(key));
        }

        _key = key;
    }

    /// <summary>Reads <paramref name="plain"/> to its end and writes it sealed to <paramref name="output"/>.</summary>
    public void Seal(ReadOnlySpan<byte> header, Stream plain, Stream output, ReadOnlySpan<byte> associated)
    {
        SecretBox.CheckHeader(header);
        var start = new byte[header.Length + PrefixSize];
        header.CopyTo(start);
        RandomNumberGenerator.Fill(start.AsSpan(header.Length));
        output.Write(start);

        using var chunks = new Chunks(_key, start.AsSpan(header.Length), header, associated);
        var (current, next) = (new byte[ChunkSize], new byte[ChunkSize]);
        var cipher = new byte[ChunkSize + TagSize];
        var length = plain.ReadAtLeast(current, ChunkSize, throwOnEndOfStream: false);
        while (true)
        {
            // A full chunk is the last one only when nothing follows it.
            var nextLength = length == ChunkSize ? plain.ReadAtLeast(next, ChunkSize, throwOnEndOfStream: false) : 0;
            var isLast = nextLength == 0;
            chunks.Seal(current.AsSpan(0, length), cipher.AsSpan(0, length + TagSize), isLast);
            output.Write(cipher, 0, length + TagSize);
            if (isLast)
            {
                return;
            }

            (current, next, length) = (next, current, nextLength);
        }
    }

    /// <summary>A stream reading <paramref name="sealedData"/> decrypted, chunk by chunk. It owns <paramref name="sealedData"/>.</summary>
    /// <exception cref="InvalidDataException">
    /// Not this kind of data; or, while reading, damaged, cut off, or another key or place.
    /// </exception>
    public Stream Open(ReadOnlySpan<byte> header, Stream sealedData, ReadOnlySpan<byte> associated)
    {
        SecretBox.CheckHeader(header);
        var start = new byte[header.Length + PrefixSize];
        if (sealedData.ReadAtLeast(start, start.Length, throwOnEndOfStream: false) < start.Length
            || !start.AsSpan(0, header.Length).SequenceEqual(header))
        {
            sealedData.Dispose();
            throw new InvalidDataException("Not the expected kind of kofrenot data");
        }

        return new OpenedStream(sealedData, new Chunks(_key, start.AsSpan(header.Length), header, associated));
    }

    /// <summary>The chunks of one sealed stream, in order: the nonce of each, and the associated data.</summary>
    private sealed class Chunks : IDisposable
    {
        private readonly AesGcm _aes;
        private readonly byte[] _nonce = new byte[NonceSize];
        private readonly byte[] _associated;
        private uint _index;

        public Chunks(byte[] key, ReadOnlySpan<byte> prefix, ReadOnlySpan<byte> header, ReadOnlySpan<byte> associated)
        {
            _aes = new AesGcm(key, TagSize);
            prefix.CopyTo(_nonce);

            // header · place · last (1 byte)
            _associated = new byte[header.Length + associated.Length + 1];
            header.CopyTo(_associated);
            associated.CopyTo(_associated.AsSpan(header.Length));
        }

        /// <param name="output">The chunk sealed, then its tag.</param>
        public void Seal(ReadOnlySpan<byte> plain, Span<byte> output, bool isLast)
        {
            Next(isLast);
            _aes.Encrypt(_nonce, plain, output[..plain.Length], output[plain.Length..], _associated);
        }

        /// <exception cref="InvalidDataException">Damaged, out of place, or another key.</exception>
        public void Open(ReadOnlySpan<byte> sealedChunk, Span<byte> plain, bool isLast)
        {
            Next(isLast);
            var length = sealedChunk.Length - TagSize;
            try
            {
                _aes.Decrypt(_nonce, sealedChunk[..length], sealedChunk[length..], plain[..length], _associated);
            }
            catch (AuthenticationTagMismatchException ex)
            {
                throw new InvalidDataException("The data cannot be decrypted: damaged, cut off, or another key", ex);
            }
        }

        public void Dispose() => _aes.Dispose();

        private void Next(bool isLast)
        {
            BinaryPrimitives.WriteUInt32BigEndian(_nonce.AsSpan(PrefixSize), checked(_index++));
            _associated[^1] = isLast ? (byte)1 : (byte)0;
        }
    }

    /// <summary>Reads one sealed chunk ahead, to know whether the current one is the last.</summary>
    private sealed class OpenedStream(Stream sealedData, Chunks chunks) : Stream
    {
        private const int RecordSize = ChunkSize + TagSize;

        private byte[] _record = new byte[RecordSize];
        private byte[] _next = new byte[RecordSize];
        private readonly byte[] _plain = new byte[ChunkSize];
        private int _recordLength = -1;
        private int _plainLength;
        private int _plainRead;
        private bool _isDone;

        public override bool CanRead => true;

        public override bool CanSeek => false;

        public override bool CanWrite => false;

        public override long Length => throw new NotSupportedException();

        public override long Position
        {
            get => throw new NotSupportedException();
            set => throw new NotSupportedException();
        }

        public override int Read(byte[] buffer, int offset, int count) => Read(buffer.AsSpan(offset, count));

        public override int Read(Span<byte> buffer)
        {
            while (_plainRead == _plainLength)
            {
                if (_isDone)
                {
                    return 0;
                }

                OpenNextChunk();
            }

            var count = Math.Min(buffer.Length, _plainLength - _plainRead);
            _plain.AsSpan(_plainRead, count).CopyTo(buffer);
            _plainRead += count;
            return count;
        }

        public override void Flush()
        {
        }

        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();

        public override void SetLength(long value) => throw new NotSupportedException();

        public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                sealedData.Dispose();
                chunks.Dispose();
            }

            base.Dispose(disposing);
        }

        private void OpenNextChunk()
        {
            if (_recordLength < 0)
            {
                _recordLength = sealedData.ReadAtLeast(_record, RecordSize, throwOnEndOfStream: false);
            }

            if (_recordLength < TagSize)
            {
                throw new InvalidDataException("The data is cut off");
            }

            // A full record is the last one only when nothing follows it.
            var nextLength = _recordLength == RecordSize ? sealedData.ReadAtLeast(_next, RecordSize, throwOnEndOfStream: false) : 0;
            _isDone = nextLength == 0;
            chunks.Open(_record.AsSpan(0, _recordLength), _plain, _isDone);
            (_plainLength, _plainRead) = (_recordLength - TagSize, 0);
            (_record, _next, _recordLength) = (_next, _record, nextLength);
        }
    }
}
