// <copyright file="GlobalUsings.cs" company="lokinmodar">
// Copyright (c) lokinmodar. All rights reserved.
// Licensed under the Creative Commons Attribution-NonCommercial-NoDerivatives 4.0 International Public License license.
// </copyright>

global using System;
global using System.Collections.Concurrent;
global using System.Collections.Generic;
global using System.Diagnostics;
global using System.Globalization;
global using System.Numerics;
global using System.Reflection;
global using System.Threading;

global using Dalamud.Game;
global using Dalamud.Game.Addon.Lifecycle;
global using Dalamud.Game.Addon.Lifecycle.AddonArgTypes;
global using Dalamud.Game.Command;
global using Dalamud.Game.Text.Sanitizer;
global using Dalamud.Game.Text.SeStringHandling;
global using Dalamud.Interface.Textures.TextureWraps;
global using Dalamud.Interface.Utility;
global using Dalamud.IoC;
global using Dalamud.Memory;
global using Dalamud.Plugin;
global using Dalamud.Plugin.Services;
global using Dalamud.Utility;

global using Echoglossian.EFCoreSqlite;
global using Echoglossian.EFCoreSqlite.Models;
global using Echoglossian.LanguagesHandling;
global using Echoglossian.NativeUI.Handlers;
global using Echoglossian.Properties;
global using Echoglossian.Translators;
global using Echoglossian.UIOverlays.TranslationOverlay;
global using Echoglossian.EFCoreSqlite.Models.Journal;

global using FFXIVClientStructs.FFXIV.Component.GUI;
global using Humanizer;
global using ImGuiNET;
global using Newtonsoft.Json;
global using Microsoft.EntityFrameworkCore;
global using Microsoft.EntityFrameworkCore.Design;