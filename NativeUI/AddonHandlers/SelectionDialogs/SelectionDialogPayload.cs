// <copyright file="SelectionDialogPayload.cs" company="lokinmodar">
// Copyright (c) lokinmodar. All rights reserved.
// Licensed under the Creative Commons Attribution-NonCommercial-NoDerivatives 4.0 International Public License license.
// </copyright>

namespace Echoglossian.NativeUI.AddonHandlers.SelectionDialogs;

/// <summary>
///     Represents one captured generic selection-dialog payload together with
///     the live addon surface that produced it.
/// </summary>
internal sealed class SelectionDialogPayload
{
    /// <summary>
    ///     Initializes a new instance of the <see cref="SelectionDialogPayload" />
    ///     class.
    /// </summary>
    /// <param name="sourceKind">The capture source kind.</param>
    /// <param name="texts">The ordered captured texts.</param>
    /// <param name="atkValueIndices">The captured ATK value indices.</param>
    /// <param name="stringArrayIndex">
    ///     The captured string-array index, when any.
    /// </param>
    /// <param name="stringArrayValueIndices">
    ///     The captured string-array value indices.
    /// </param>
    /// <param name="textNodeAddresses">The captured text-node addresses.</param>
    private SelectionDialogPayload(
        SelectionDialogCaptureSourceKind sourceKind,
        IReadOnlyList<string> texts,
        IReadOnlyList<int>? atkValueIndices = null,
        int? stringArrayIndex = null,
        IReadOnlyList<int>? stringArrayValueIndices = null,
        IReadOnlyList<nint>? textNodeAddresses = null)
    {
        this.SourceKind = sourceKind;
        this.Texts = [.. texts];
        this.AtkValueIndices = atkValueIndices != null ? [.. atkValueIndices] : [];
        this.StringArrayIndex = stringArrayIndex;
        this.StringArrayValueIndices = stringArrayValueIndices != null
            ? [.. stringArrayValueIndices]
            : [];
        this.TextNodeAddresses = textNodeAddresses != null
            ? [.. textNodeAddresses]
            : [];
    }

    /// <summary>
    ///     Gets the capture source kind.
    /// </summary>
    public SelectionDialogCaptureSourceKind SourceKind { get; }

    /// <summary>
    ///     Gets the captured texts in display order.
    /// </summary>
    public IReadOnlyList<string> Texts { get; }

    /// <summary>
    ///     Gets the ATK value indices captured for this payload.
    /// </summary>
    public IReadOnlyList<int> AtkValueIndices { get; }

    /// <summary>
    ///     Gets the string-array index captured for this payload.
    /// </summary>
    public int? StringArrayIndex { get; }

    /// <summary>
    ///     Gets the string-array value indices captured for this payload.
    /// </summary>
    public IReadOnlyList<int> StringArrayValueIndices { get; }

    /// <summary>
    ///     Gets the text-node addresses captured for this payload.
    /// </summary>
    public IReadOnlyList<nint> TextNodeAddresses { get; }

    /// <summary>
    ///     Creates one ATK-value-backed selection-dialog payload.
    /// </summary>
    /// <param name="indices">The ordered ATK value indices.</param>
    /// <param name="texts">The ordered texts.</param>
    /// <returns>The created payload.</returns>
    public static SelectionDialogPayload FromAtkValues(
        IReadOnlyList<int> indices,
        IReadOnlyList<string> texts)
    {
        return new SelectionDialogPayload(
            SelectionDialogCaptureSourceKind.AtkValues,
            texts,
            atkValueIndices: indices);
    }

    /// <summary>
    ///     Creates one string-array-backed selection-dialog payload.
    /// </summary>
    /// <param name="stringArrayIndex">The live string-array index.</param>
    /// <param name="indices">The ordered string-array value indices.</param>
    /// <param name="texts">The ordered texts.</param>
    /// <returns>The created payload.</returns>
    public static SelectionDialogPayload FromStringArrayData(
        int stringArrayIndex,
        IReadOnlyList<int> indices,
        IReadOnlyList<string> texts)
    {
        return new SelectionDialogPayload(
            SelectionDialogCaptureSourceKind.StringArrayData,
            texts,
            stringArrayIndex: stringArrayIndex,
            stringArrayValueIndices: indices);
    }

    /// <summary>
    ///     Creates one text-node-backed selection-dialog payload.
    /// </summary>
    /// <param name="textNodeAddresses">The ordered text-node addresses.</param>
    /// <param name="texts">The ordered texts.</param>
    /// <returns>The created payload.</returns>
    public static SelectionDialogPayload FromTextNodes(
        IReadOnlyList<nint> textNodeAddresses,
        IReadOnlyList<string> texts)
    {
        return new SelectionDialogPayload(
            SelectionDialogCaptureSourceKind.TextNodes,
            texts,
            textNodeAddresses: textNodeAddresses);
    }

    /// <summary>
    ///     Determines whether this payload describes the same live capture
    ///     surface as another payload.
    /// </summary>
    /// <param name="other">The other payload.</param>
    /// <returns>
    ///     <see langword="true" /> when both payloads describe the same live
    ///     capture structure; otherwise, <see langword="false" />.
    /// </returns>
    public bool MatchesStructure(SelectionDialogPayload? other)
    {
        if (other == null || this.SourceKind != other.SourceKind)
        {
            return false;
        }

        return this.SourceKind switch
        {
            SelectionDialogCaptureSourceKind.AtkValues => this.SequenceEqual(
                this.AtkValueIndices,
                other.AtkValueIndices),
            SelectionDialogCaptureSourceKind.StringArrayData =>
                this.StringArrayIndex == other.StringArrayIndex &&
                this.SequenceEqual(
                    this.StringArrayValueIndices,
                    other.StringArrayValueIndices),
            SelectionDialogCaptureSourceKind.TextNodes => this.SequenceEqual(
                this.TextNodeAddresses,
                other.TextNodeAddresses),
            _ => false,
        };
    }

    /// <summary>
    ///     Determines whether this payload's ordered texts match another text
    ///     sequence according to the supplied comparer.
    /// </summary>
    /// <param name="otherTexts">The other ordered texts.</param>
    /// <param name="matches">The string comparison delegate.</param>
    /// <returns>
    ///     <see langword="true" /> when both sequences match; otherwise,
    ///     <see langword="false" />.
    /// </returns>
    public bool TextsMatch(
        IReadOnlyList<string> otherTexts,
        Func<string?, string?, bool> matches)
    {
        ArgumentNullException.ThrowIfNull(matches);

        if (this.Texts.Count != otherTexts.Count)
        {
            return false;
        }

        for (var index = 0; index < this.Texts.Count; index++)
        {
            if (!matches(this.Texts[index], otherTexts[index]))
            {
                return false;
            }
        }

        return true;
    }

    /// <summary>
    ///     Builds one retry-suppression key for the payload.
    /// </summary>
    /// <param name="normalize">The text normalization delegate.</param>
    /// <returns>The stable payload key.</returns>
    public string BuildSourceKey(Func<string?, string> normalize)
    {
        ArgumentNullException.ThrowIfNull(normalize);

        var keyParts = new List<string>(this.Texts.Count + 1)
        {
            this.SourceKind.ToString(),
        };

        keyParts.AddRange(this.Texts.Select(normalize));
        return string.Join('\u001F', keyParts);
    }

    /// <summary>
    ///     Splits the payload into an overlay title and body.
    /// </summary>
    /// <returns>The overlay title/body pair.</returns>
    public (string Title, string Body) ToOverlayParts()
    {
        if (this.Texts.Count == 0)
        {
            return (string.Empty, string.Empty);
        }

        if (this.Texts.Count == 1)
        {
            return (string.Empty, this.Texts[0]);
        }

        return (this.Texts[0], string.Join('\n', this.Texts.Skip(1)));
    }

    private bool SequenceEqual<T>(
        IReadOnlyList<T> left,
        IReadOnlyList<T> right)
    {
        if (left.Count != right.Count)
        {
            return false;
        }

        for (var index = 0; index < left.Count; index++)
        {
            if (!EqualityComparer<T>.Default.Equals(left[index], right[index]))
            {
                return false;
            }
        }

        return true;
    }
}
