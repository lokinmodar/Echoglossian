namespace DalaMock.Core.Mocks.Misc;

/// <summary>
/// An SeString Payload containing an auto-translation/completion chat message.
/// </summary>
public class MockAutoTranslatePayload : Payload, ITextProvider
{
    private readonly ISeStringEvaluator seStringEvaluator;
    private ReadOnlySeString payload;

    public delegate MockAutoTranslatePayload Factory(uint group, uint key);

    /// <summary>
    /// Initializes a new instance of the <see cref="MockAutoTranslatePayload"/> class.
    /// Creates a new auto-translate payload.
    /// </summary>
    /// <param name="group">The group id for this message.</param>
    /// <param name="key">The key/row id for this message.  Which table this is in depends on the group id and details the Completion table.</param>
    /// <remarks>
    /// This table is somewhat complicated in structure, and so using this constructor may not be very nice.
    /// There is probably little use to create one of these, however.
    /// </remarks>
    public MockAutoTranslatePayload(uint group, uint key, ISeStringEvaluator seStringEvaluator)
    {
        this.seStringEvaluator = seStringEvaluator;

        // TODO: friendlier ctor? not sure how to handle that given how weird the tables are
        this.Group = group;
        this.Key = key;

        using var rssb = new RentedSeStringBuilder();

        this.payload = rssb.Builder
            .BeginMacro(MacroCode.Fixed)
            .AppendUIntExpression(group - 1)
            .AppendUIntExpression(key)
            .EndMacro()
            .ToReadOnlySeString();
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="AutoTranslatePayload"/> class.
    /// </summary>
    public MockAutoTranslatePayload(ISeStringEvaluator seStringEvaluator)
    {
        this.seStringEvaluator = seStringEvaluator;
        this.payload = default;
    }

    /// <summary>
    /// Gets the autotranslate group.
    /// </summary>
    [JsonProperty("group")]
    public uint Group { get; private set; }

    /// <summary>
    /// Gets the autotranslate key.
    /// </summary>
    [JsonProperty("key")]
    public uint Key { get; private set; }

    /// <inheritdoc/>
    public override DPayloadType Type => DPayloadType.AutoTranslateText;

    /// <summary>
    /// Gets the actual text displayed in-game for this payload.
    /// </summary>
    /// <remarks>
    /// Value is evaluated lazily and cached.
    /// </remarks>
    public string Text
    {
        get
        {
            if (this.Group is 100 or 200)
            {
                return this.seStringEvaluator.Evaluate(this.payload).ToString();
            }

            // wrap the text in the colored brackets that are used in-game, since those are not actually part of any of the fixed macro payload
            return $"{(char)SeIconChar.AutoTranslateOpen} {this.seStringEvaluator.Evaluate(this.payload)} {(char)SeIconChar.AutoTranslateClose}";
        }
    }

    /// <summary>
    /// Returns a string that represents the current object.
    /// </summary>
    /// <returns>A string that represents the current object.</returns>
    public override string ToString()
    {
        return $"{this.Type} - Group: {this.Group}, Key: {this.Key}, Text: {this.Text}";
    }

    /// <inheritdoc/>
    protected override byte[] EncodeImpl()
    {
        return this.payload.Data.ToArray();
    }

    /// <inheritdoc/>
    protected override void DecodeImpl(BinaryReader reader, long endOfStream)
    {
        var body = reader.ReadBytes((int)(endOfStream - reader.BaseStream.Position));
        var rosps = new ReadOnlySePayloadSpan(ReadOnlySePayloadType.Macro, MacroCode.Fixed, body.AsSpan());

        var span = rosps.EnvelopeByteLength <= 512 ? stackalloc byte[rosps.EnvelopeByteLength] : new byte[rosps.EnvelopeByteLength];
        rosps.WriteEnvelopeTo(span);
        this.payload = new ReadOnlySeString(span);

        if (rosps.TryGetExpression(out var expr1, out var expr2)
            && expr1.TryGetUInt(out var group)
            && expr2.TryGetUInt(out var key))
        {
            this.Group = group + 1;
            this.Key = key;
        }
    }
}
