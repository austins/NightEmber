using NightEmber.Core.Models;
using System.Text.Json.Serialization;

namespace NightEmber.Core.Services;

/// <summary>
/// Source-generated serialization metadata for the portable configuration, keeping it trim-safe.
/// </summary>
[JsonSourceGenerationOptions(WriteIndented = true)]
[JsonSerializable(typeof(AppSettings))]
internal sealed partial class SettingsJsonContext : JsonSerializerContext;
