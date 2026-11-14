using System;
using System.Diagnostics;
using System.IO;

namespace SurgiTwinXR.Simulation
{
    /// <summary>
    /// Phase 1.1 measurement (Task 3): write-only pass-through stream that times the calls into the underlying
    /// file stream, so an export can report file I/O separately from JSON formatting (which are interleaved,
    /// because the JSON is streamed). Not thread-safe; used by one export worker at a time.
    /// </summary>
    sealed class TimedWriteStream : Stream
    {
        readonly Stream inner;
        readonly Stopwatch timer = new Stopwatch();

        public TimedWriteStream(Stream inner)
        {
            this.inner = inner ?? throw new ArgumentNullException(nameof(inner));
        }

        public long BytesWritten { get; private set; }
        public double WriteMilliseconds => timer.Elapsed.TotalMilliseconds;

        public override bool CanRead => false;
        public override bool CanSeek => false;
        public override bool CanWrite => true;
        public override long Length => BytesWritten;
        public override long Position
        {
            get => BytesWritten;
            set => throw new NotSupportedException();
        }

        public override void Write(byte[] buffer, int offset, int count)
        {
            timer.Start();
            inner.Write(buffer, offset, count);
            timer.Stop();
            BytesWritten += count;
        }

        public override void Flush()
        {
            timer.Start();
            inner.Flush();
            timer.Stop();
        }

        public override int Read(byte[] buffer, int offset, int count) => throw new NotSupportedException();
        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
        public override void SetLength(long value) => throw new NotSupportedException();
    }
}
