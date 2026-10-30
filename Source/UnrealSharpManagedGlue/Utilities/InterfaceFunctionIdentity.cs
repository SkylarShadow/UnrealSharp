using System;
using System.Collections.Generic;
using EpicGames.UHT.Types;

namespace UnrealSharpManagedGlue.Utilities;

// Normalizes UHT interface function names for matching
internal static class InterfaceFunctionIdentity
{
    // Gets source, engine, and C++ names without _Implementation
    public static HashSet<string> GetNames(UhtFunction function)
    {
        HashSet<string> names = new(StringComparer.Ordinal);
        AddName(names, function.SourceName);
        AddName(names, function.EngineName);
        AddName(names, function.CppImplName);
        return names;
    }

    /// Adds a non-empty normalized name.
    private static void AddName(HashSet<string> names, string? name)
    {
        if (string.IsNullOrEmpty(name))
        {
            return;
        }

        const string suffix = "_Implementation";
        names.Add(name.EndsWith(suffix, StringComparison.Ordinal) ? name[..^suffix.Length] : name);
    }
}
