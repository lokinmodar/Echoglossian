// <copyright file="AtkTextNodeBufferWrapper.cs" company="lokinmodar">
// Copyright (c) lokinmodar. All rights reserved.
// Licensed under the Creative Commons Attribution-NonCommercial-NoDerivatives 4.0 International Public License license.
// </copyright>

namespace Echoglossian.NativeUI.Helpers;

/// <summary>
///     Provides a wrapper for managing a UTF-8 encoded text buffer using unmanaged
///     memory.
/// </summary>
internal unsafe class AtkTextNodeBufferWrapper
{
    private int bufferLen;

    /// <summary>
    ///     Gets the buffer containing the UTF-8 encoded string.
    /// </summary>
    public byte* GetBuffer { get; private set; }

    /// <summary>
    ///     Sets the buffer to the UTF-8 representation of the given string.
    ///     The buffer is reallocated if it doesn't already exist or is too small.
    /// </summary>
    /// <param name="text">The string to be encoded and stored in the buffer.</param>
    public void SetBuffer(string text)
    {
        // Get the length of the string as UTF-8 bytes
        var strLen = Encoding.UTF8.GetByteCount(text);

        // Reallocate buffer if it doesn't already exist or is too small
        if (this.GetBuffer == null || strLen + 1 > this.bufferLen)
        {
            NativeMemory.Free(this.GetBuffer);
            this.GetBuffer =
                (byte*)NativeMemory.Alloc(
                    (nuint)(strLen +
                            1)); // Need one extra byte for the null terminator
            this.bufferLen = strLen + 1;
        }

        // Wrap buffer in a span to use GetBytes
        Span<byte> bufferSpan = new(this.GetBuffer, strLen + 1);

        // Convert string to UTF-8 and store in buffer
        Encoding.UTF8.GetBytes(text, bufferSpan);

        // Add null terminator to the end of the string
        bufferSpan[strLen] = 0;
    }

    /// <summary>
    ///     Frees the previously allocated buffer.
    /// </summary>
    public void FreeBuffer()
    {
        NativeMemory.Free(this.GetBuffer);
        this.bufferLen = 0;
    }
}