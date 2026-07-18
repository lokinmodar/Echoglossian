namespace DalaMock.Core.Mocks.DalamudServices.SeStringEvaluator.Macros;

public sealed unsafe class MockMacroDecoder : IDisposable
{
    private readonly GCHandle tmHandle;
    private Tm tmValue;

    private readonly List<TextParameter> parameters = new();

    private GCHandle blockHandle;
    private GCHandle mapHandle;
    private TextParameter[] blockArray = Array.Empty<TextParameter>();
    private IntPtr[] mapArray = Array.Empty<IntPtr>();

    private StdDeque<TextParameter> deque;

    private bool disposed;

    public MockMacroDecoder()
    {
        this.tmValue = TmFromDateTime(DateTime.Now);
        this.tmHandle = GCHandle.Alloc(this.tmValue, GCHandleType.Pinned);
        this.RebuildNativeDeque();
    }

    /// <summary>Seeds the macro time from a <see cref="DateTime"/>.</summary>
    public void SetMacroTime(DateTime dateTime)
    {
        this.tmValue = TmFromDateTime(dateTime);
        this.WriteTmBack();
    }

    /// <summary>Seeds the macro time from a raw <see cref="Tm"/>.</summary>
    public void SetMacroTime(Tm tm)
    {
        this.tmValue = tm;
        this.WriteTmBack();
    }

    /// <summary>
    /// Returns a pointer to the pinned <see cref="Tm"/>.
    /// </summary>
    public Tm* GetMacroTime()
    {
        this.ThrowIfDisposed();
        return (Tm*)this.tmHandle.AddrOfPinnedObject();
    }

    /// <summary>Reads back the pinned time as a <see cref="DateTime"/>.</summary>
    public DateTime GetMacroTimeAsDateTime()
    {
        var tm = *this.GetMacroTime();
        return TmToDateTime(tm);
    }

    /// <summary>
    /// Returns a reference to the <see cref="StdDeque{TextParameter}"/> that
    /// mirrors <c>MacroDecoder.GlobalParameters</c>.  The deque's native memory
    /// is kept valid for the lifetime of this mock.
    /// </summary>
    public ref StdDeque<TextParameter> GetGlobalParameters()
    {
        this.ThrowIfDisposed();
        return ref this.deque;
    }

    /// <summary>Sets or replaces a global parameter slot with an integer value.</summary>
    public void SetGlobalParameter(int index, int value)
    {
        this.EnsureCapacity(index);
        this.parameters[index] = new TextParameter
        {
            IntValue = value,
            Type = TextParameterType.Integer,
        };
        this.RebuildNativeDeque();
    }

    /// <summary>Sets or replaces a global parameter slot with a UTF-8 string value.</summary>
    public void SetGlobalParameter(int index, string value)
    {
        this.EnsureCapacity(index);

        var bytes = System.Text.Encoding.UTF8.GetBytes(value + '\0');
        var strPin = GCHandle.Alloc(bytes, GCHandleType.Pinned);
        var ptr = (byte*)strPin.AddrOfPinnedObject();

        this.stringPins.Add(strPin);

        this.parameters[index] = new TextParameter
        {
            StringValue = new CStringPointer { Value = ptr },
            Type = TextParameterType.String,
        };
        this.RebuildNativeDeque();
    }

    /// <summary>Removes all global parameters.</summary>
    public void ClearGlobalParameters()
    {
        this.parameters.Clear();
        this.RebuildNativeDeque();
    }

    /// <summary>Returns a read-only snapshot of the current parameters as managed values.</summary>
    public IReadOnlyList<TextParameter> GetGlobalParameterSnapshot() => this.parameters.AsReadOnly();

    public void Dispose()
    {
        if (this.disposed)
        {
            return;
        }

        this.disposed = true;

        if (this.tmHandle.IsAllocated)
        {
            this.tmHandle.Free();
        }

        if (this.blockHandle.IsAllocated)
        {
            this.blockHandle.Free();
        }

        if (this.mapHandle.IsAllocated)
        {
            this.mapHandle.Free();
        }

        foreach (var pin in this.stringPins)
        {
            if (pin.IsAllocated)
            {
                pin.Free();
            }
        }

        this.stringPins.Clear();
    }

    public static Tm TmFromDateTime(DateTime dt) => new Tm
    {
        tm_sec = dt.Second,
        tm_min = dt.Minute,
        tm_hour = dt.Hour,
        tm_mday = dt.Day,
        tm_mon = dt.Month - 1,
        tm_year = dt.Year - 1900,
        tm_wday = (int)dt.DayOfWeek,
        tm_yday = dt.DayOfYear - 1,
        tm_isdst = dt.IsDaylightSavingTime() ? 1 : 0,
    };

    public static DateTime TmToDateTime(Tm tm)
    {
        try
        {
            return new DateTime(
                tm.tm_year + 1900,
                tm.tm_mon + 1,
                tm.tm_mday,
                tm.tm_hour,
                tm.tm_min,
                tm.tm_sec);
        }
        catch (ArgumentOutOfRangeException)
        {
            return DateTime.MinValue;
        }
    }

    private readonly List<GCHandle> stringPins = new();

    /// <summary>
    /// Rebuilds the pinned native block and map so that <see cref="deque"/>
    /// reflects the current contents of <see cref="parameters"/>.
    ///
    /// Layout chosen to match what <see cref="StdDeque{T}"/> expects:
    ///   - BlockSize for TextParameter (24 bytes, sizeof > 8) = 1, so every
    ///     element gets its own "block" pointer in the map.  We instead use a
    ///     simpler flat layout: one contiguous block for all elements and a
    ///     single map entry pointing to it, which is valid when Count ≤ BlockSize.
    ///     For the mock we keep count within one block by using capacity = max(1, Count).
    /// </summary>
    private void RebuildNativeDeque()
    {
        if (this.blockHandle.IsAllocated)
        {
            this.blockHandle.Free();
        }

        if (this.mapHandle.IsAllocated)
        {
            this.mapHandle.Free();
        }

        var count = this.parameters.Count;

        this.blockArray = new TextParameter[Math.Max(1, count)];
        for (var i = 0; i < count; i++)
        {
            this.blockArray[i] = this.parameters[i];
        }

        this.blockHandle = GCHandle.Alloc(this.blockArray, GCHandleType.Pinned);
        var blockPtr = (TextParameter*)this.blockHandle.AddrOfPinnedObject();

        this.mapArray = new IntPtr[1];
        this.mapArray[0] = (IntPtr)blockPtr;
        this.mapHandle = GCHandle.Alloc(this.mapArray, GCHandleType.Pinned);
        var mapPtr = (TextParameter**)this.mapHandle.AddrOfPinnedObject();

        this.deque = default;
        this.deque.Map = mapPtr;
        this.deque.MapSize = 1;
        this.deque.MyOff = 0;
        this.deque.MySize = (ulong)count;
    }

    /// <summary>Expands <see cref="parameters"/> to at least <paramref name="index"/> + 1 slots.</summary>
    private void EnsureCapacity(int index)
    {
        while (this.parameters.Count <= index)
        {
            this.parameters.Add(new TextParameter { Type = TextParameterType.Uninitialized });
        }
    }

    private void WriteTmBack()
    {
        this.ThrowIfDisposed();
        *(Tm*)this.tmHandle.AddrOfPinnedObject() = this.tmValue;
    }

    private void ThrowIfDisposed()
    {
        if (this.disposed)
        {
            throw new ObjectDisposedException(nameof(MockMacroDecoder));
        }
    }
}
