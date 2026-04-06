// <copyright file="ToDoItem.cs" company="lokinmodar">
// Copyright (c) lokinmodar. All rights reserved.
// Licensed under the Creative Commons Attribution-NonCommercial-NoDerivatives 4.0 International Public License license.
// </copyright>

namespace Echoglossian;

/// <summary>
/// Represents a ToDo list entry and the node coordinates it belongs to.
/// </summary>
public class ToDoItem
{
    /// <summary>
    /// Initializes a new instance of the <see cref="ToDoItem"/> class.
    /// </summary>
    /// <param name="text">The entry text.</param>
    /// <param name="indexI">The first index component.</param>
    /// <param name="indexJ">The second index component.</param>
    /// <param name="nodeID">The backing node identifier.</param>
    public ToDoItem(string text, int indexI, int indexJ, uint nodeID)
    {
        this.Text = text;
        this.IndexI = indexI;
        this.IndexJ = indexJ;
        this.NodeId = nodeID;
    }

    /// <summary>
    /// Gets or sets the entry text.
    /// </summary>
    public string Text { get; set; }

    /// <summary>
    /// Gets or sets the first index component.
    /// </summary>
    public int IndexI { get; set; }

    /// <summary>
    /// Gets or sets the second index component.
    /// </summary>
    public int IndexJ { get; set; }

    /// <summary>
    /// Gets or sets the backing node identifier.
    /// </summary>
    public uint NodeId { get; set; }

    /// <inheritdoc/>
    public override string ToString()
    {
        return this.Text;
    }
}
