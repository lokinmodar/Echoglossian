// <copyright file="OpenAIModelEntry.cs" company="lokinmodar">
// Copyright (c) lokinmodar. All rights reserved.
// Licensed under the Creative Commons Attribution-NonCommercial-NoDerivatives 4.0 International Public License license.
// </copyright>

namespace Echoglossian.Translators.OpenAI;

public class OpenAIModelResponse
{
  [JsonProperty("object")]
  public string Object { get; set; } = string.Empty;

  [JsonProperty("data")]
  public List<OpenAIModelEntry> Data { get; set; } = new();
}

public class OpenAIModelEntry
{
  [JsonProperty("id")]
  public string Id { get; set; } = string.Empty;

  [JsonProperty("object")]
  public string Object { get; set; } = string.Empty;

  [JsonProperty("created")]
  public long Created { get; set; }

  [JsonProperty("owned_by")]
  public string OwnedBy { get; set; } = string.Empty;
}
