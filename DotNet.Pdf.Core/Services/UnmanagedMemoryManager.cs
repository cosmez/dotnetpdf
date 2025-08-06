using System.Buffers;
using System.Runtime.InteropServices;

namespace DotNet.Pdf.Core.Services;

/// <summary>
/// A MemoryManager over a raw pointer
/// https://github.com/Dtronix/PDFiumCore/blob/master/src/PDFiumCoreDemo/UnmanagedMemoryManager.cs
/// </summary>
/// <remarks>The pointer is assumed to be fully unmanaged, or externally pinned - no attempt will be made to pin this data</remarks>
public sealed unsafe class UnmanagedMemoryManager<T> : MemoryManager<T>
    where T : unmanaged
{
    private readonly T* _pointer;
    private readonly int _length;

    /// <summary>
    /// Create a new UnmanagedMemoryManager instance at the given pointer and size
    /// </summary>
    /// <remarks>It is assumed that the span provided is already unmanaged or externally pinned</remarks>
    public UnmanagedMemoryManager(Span<T> span)
    {
        fixed (T* ptr = &MemoryMarshal.GetReference(span))
        {
            _pointer = ptr;
            _length = span.Length;
        }
    }
    /// <summary>
    /// Create a new UnmanagedMemoryManager instance at the given pointer and size
    /// </summary>
    public UnmanagedMemoryManager(T* pointer, int length)
    {
        if (length < 0) throw new ArgumentOutOfRangeException(nameof(length));
        _pointer = pointer;
        _length = length;
    }
    /// <summary>
    /// Obtains a span that represents the region
    /// </summary>
    public override Span<T> GetSpan() => new Span<T>(_pointer, _length);

    /// <summary>
    /// Provides access to a pointer that represents the data (note: no actual pin occurs)
    /// </summary>
    public override MemoryHandle Pin(int elementIndex = 0)
    {
        if (elementIndex < 0 || elementIndex >= _length)
            throw new ArgumentOutOfRangeException(nameof(elementIndex));
        return new MemoryHandle(_pointer + elementIndex);
    }
    /// <summary>
    /// Has no effect
    /// </summary>
    public override void Unpin() { }

    /// <summary>
    /// Releases all resources associated with this object
    /// </summary>
    protected override void Dispose(bool disposing) { }
}
