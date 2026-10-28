using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Flowery.Controls;

/// <summary>Reads and writes validated, sparse tour style profiles without reflection metadata.</summary>
public static class DaisyTourStyleProfileSerializer
{
    private const int MaximumJsonLength = 1024 * 1024;
    private static readonly HashSet<string> TourProperties = new(StringComparer.Ordinal)
    {
        "size", "cardPosition", "cardBorderBrush", "highlightPadding", "highlightCornerRadius"
    };
    private static readonly HashSet<string> StepProperties = new(StringComparer.Ordinal) { "placement", "cardPosition", "cardX", "cardY" };
    private static readonly HashSet<string> AnnotationProperties = new(StringComparer.Ordinal)
    {
        "sourceAnchor", "targetAnchor", "routeLimitSide", "routeLimitCoordinate", "routeLimitMargin"
    };

    public static string Serialize(DaisyTourStyleProfile profile)
    {
        Validate(profile);
        var json = JsonSerializer.Serialize(profile, DaisyTourStyleProfileJsonContext.Default.DaisyTourStyleProfile);
        if (Encoding.UTF8.GetByteCount(json) > MaximumJsonLength)
            throw new ArgumentException("The tour style profile exceeds 1 MiB.", nameof(profile));
        return json;
    }

    public static DaisyTourStyleProfile Deserialize(string json)
    {
        ArgumentNullException.ThrowIfNull(json);
        if (Encoding.UTF8.GetByteCount(json) > MaximumJsonLength) throw new JsonException("The tour style profile exceeds 1 MiB.");
        using var document = JsonDocument.Parse(json, new JsonDocumentOptions { MaxDepth = 32 });
        ValidateJson(document.RootElement, "$");
        var profile = JsonSerializer.Deserialize(json, DaisyTourStyleProfileJsonContext.Default.DaisyTourStyleProfile)
            ?? throw new JsonException("A tour style profile object is required.");
        try { Validate(profile); }
        catch (ArgumentException error) { throw new JsonException(error.Message, error); }
        return profile;
    }

    public static DaisyTourStyleProfile Clone(DaisyTourStyleProfile profile) => Deserialize(Serialize(profile));

    /// <summary>Validates the portable data and, optionally, all references against its application tour.</summary>
    public static void Validate(DaisyTourStyleProfile profile, DaisyTour? tour = null)
    {
        ArgumentNullException.ThrowIfNull(profile);
        if (profile.SchemaVersion != 1) throw new ArgumentException("$.schemaVersion: only version 1 is supported.");
        ValidateId(profile.TourId, "$.tourId");
        if (tour is not null && tour.Id != profile.TourId)
            throw new ArgumentException("$.tourId: the profile belongs to a different tour.");
        ValidateScope(profile.Style, "$.style", TourProperties);
        ValidateScope(profile.AnnotationDefaults, "$.annotationDefaults", null);
        if (profile.Steps is null) throw new ArgumentException("$.steps: an array is required.");
        if (profile.Steps.Count > 1000) throw new ArgumentException("$.steps: at most 1000 steps are supported.");
        var ids = new HashSet<string>(StringComparer.Ordinal);
        foreach (var step in profile.Steps)
        {
            if (step is null) throw new ArgumentException("$.steps: null entries are not supported.");
            var path = $"$.steps[{step.Id}]";
            ValidateId(step.Id, path + ".id");
            if (!ids.Add(step.Id)) throw new ArgumentException(path + ": duplicate step ID.");
            var target = tour?.Steps.FirstOrDefault(candidate => candidate.Id == step.Id);
            if (tour is not null && target is null) throw new ArgumentException(path + ": unknown step ID.");
            ValidateScope(step.Style, path + ".style", StepProperties);
            ValidateScope(step.AnnotationDefaults, path + ".annotationDefaults", null);
            if (step.Annotations is null) throw new ArgumentException(path + ".annotations: an array is required.");
            if (step.Annotations.Count > 1000) throw new ArgumentException(path + ".annotations: at most 1000 annotations are supported.");
            var annotationIds = new HashSet<string>(StringComparer.Ordinal);
            foreach (var annotation in step.Annotations)
            {
                if (annotation is null) throw new ArgumentException(path + ".annotations: null entries are not supported.");
                var annotationPath = path + $".annotations[{annotation.Id}]";
                ValidateId(annotation.Id, annotationPath + ".id");
                if (!annotationIds.Add(annotation.Id)) throw new ArgumentException(annotationPath + ": duplicate annotation ID.");
                if (target is not null && !target.Annotations.Any(item => item.Id == annotation.Id) &&
                    !(target.Annotations.Count == 0 && annotation.Id == "default-pointer"))
                    throw new ArgumentException(annotationPath + ": unknown annotation ID.");
                ValidateScope(annotation.Style, annotationPath + ".style", null, true);
            }
        }
        if (tour is not null) ValidateTourIds(tour);
    }

    private static void ValidateTourIds(DaisyTour tour)
    {
        var steps = new HashSet<string>(StringComparer.Ordinal);
        foreach (var step in tour.Steps)
        {
            ValidateId(step.Id, "Tour step ID");
            if (!steps.Add(step.Id)) throw new ArgumentException("Tour step IDs must be unique.");
            var annotations = new HashSet<string>(StringComparer.Ordinal);
            foreach (var annotation in step.Annotations)
            {
                ValidateId(annotation.Id, "Tour annotation ID");
                if (!annotations.Add(annotation.Id)) throw new ArgumentException("Tour annotation IDs must be unique within each step.");
            }
        }
    }

    private static void ValidateId(string? id, string path)
    {
        if (string.IsNullOrWhiteSpace(id) || id.Length > 128 || id.Any(char.IsWhiteSpace) || id.Any(char.IsControl))
            throw new ArgumentException(path + ": IDs require 1 to 128 characters without whitespace or control characters.");
    }

    private static void ValidateScope(DaisyTourStyleOverride? scope, string path, HashSet<string>? allowed, bool annotation = false)
    {
        if (scope is null) return;
        if (scope.Values is null || scope.Inherit is null)
            throw new ArgumentException(path + ": values and inherit cannot be null.");
        var reset = new HashSet<string>(StringComparer.Ordinal);
        foreach (var name in scope.Inherit)
        {
            if (name is null || !IsAllowed(name, allowed, annotation))
                throw new ArgumentException(path + ".inherit: unsupported property '" + name + "'.");
            if (!reset.Add(name)) throw new ArgumentException(path + ".inherit: duplicate property '" + name + "'.");
        }
        foreach (var name in DaisyTourProfileResolver.PropertyNames)
        {
            if (DaisyTourProfileResolver.GetValue(scope, name) is not { } value) continue;
            if (!IsAllowed(name, allowed, annotation)) throw new ArgumentException(path + ".values: unsupported property '" + name + "'.");
            if (reset.Contains(name)) throw new ArgumentException(path + ": '" + name + "' cannot have both a value and inherit.");
            try { DaisyTourProfileResolver.ValidateValue(name, value); }
            catch (ArgumentException error) { throw new ArgumentException(path + ".values." + name + ": " + error.Message, error); }
        }
    }

    private static bool IsAllowed(string name, HashSet<string>? allowed, bool annotation) =>
        DaisyTourProfileResolver.ValueType(name) is not null &&
        (allowed?.Contains(name) ?? (!TourProperties.Contains(name) && !StepProperties.Contains(name) &&
            (annotation || !AnnotationProperties.Contains(name))));

    private static void ValidateJson(JsonElement element, string path)
    {
        if (element.ValueKind == JsonValueKind.Null) throw new JsonException(path + ": omit properties instead of using null.");
        if (element.ValueKind == JsonValueKind.Array)
        {
            var index = 0;
            foreach (var item in element.EnumerateArray()) ValidateJson(item, path + $"[{index++}]");
        }
        if (element.ValueKind != JsonValueKind.Object) return;
        var names = new HashSet<string>(StringComparer.Ordinal);
        foreach (var property in element.EnumerateObject())
        {
            var childPath = path + "." + property.Name;
            if (!names.Add(property.Name)) throw new JsonException(childPath + ": duplicate member.");
            if (path.EndsWith(".values", StringComparison.Ordinal) && DaisyTourProfileResolver.ValueType(property.Name) is { IsEnum: true })
            {
                if (property.Value.ValueKind != JsonValueKind.String ||
                    !DaisyTourProfileResolver.IsEnumValue(property.Name, property.Value.GetString()!))
                    throw new JsonException(childPath + ": a supported, case-sensitive enum name is required.");
            }
            ValidateJson(property.Value, childPath);
        }
    }
}

[JsonSourceGenerationOptions(
    PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase,
    WriteIndented = true,
    DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow,
    UseStringEnumConverter = true,
    GenerationMode = JsonSourceGenerationMode.Metadata)]
[JsonSerializable(typeof(DaisyTourStyleProfile))]
internal partial class DaisyTourStyleProfileJsonContext : JsonSerializerContext
{
}

