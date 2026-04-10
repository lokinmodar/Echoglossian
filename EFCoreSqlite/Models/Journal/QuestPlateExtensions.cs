// <copyright file="QuestPlateExtensions.cs" company="lokinmodar">
// Copyright (c) lokinmodar. All rights reserved.
// Licensed under the Creative Commons Attribution-NonCommercial-NoDerivatives 4.0 International Public License license.
// </copyright>

namespace Echoglossian.EFCoreSqlite.Models.Journal
{
  public static class QuestPlateExtensions
  {
    /// <summary>
    /// Creates a deep copy of the QuestPlate instance.
    /// </summary>
    /// <param name="original">The QuestPlate to clone.</param>
    /// <param name="resetIdentifiers">If true, resets Id to prepare for new insertion.</param>
    /// <returns>A deep-cloned QuestPlate object.</returns>
    public static QuestPlate Clone(this QuestPlate original, bool resetIdentifiers = false)
    {
      if (original == null)
      {
        throw new ArgumentNullException(nameof(original));
      }

      var serialized = JsonConvert.SerializeObject(original);
      var cloned = JsonConvert.DeserializeObject<QuestPlate>(serialized);

      if (cloned == null)
      {
        throw new InvalidOperationException("Failed to clone the QuestPlate object.");
      }

      if (resetIdentifiers)
      {
        cloned.Id = 0;
      }

      return cloned;
    }
  }
}
