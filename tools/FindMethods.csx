#!/usr/bin/env dotnet-script
// FindMethods.csx — searches Assembly-CSharp.dll for method names
// Usage: dotnet script FindMethods.csx <GameDir> <SearchTerm>
// Example: dotnet script FindMethods.csx "C:\...\7 Days To Die" "Damage"
//
// Install dotnet-script first: dotnet tool install -g dotnet-script
// Install Mono.Cecil:           dotnet add package Mono.Cecil

#r "nuget: Mono.Cecil, 0.11.5"
using Mono.Cecil;

var gameDir = Args.Count > 0 ? Args[0] : @"C:\Program Files (x86)\Steam\steamapps\common\7 Days To Die";
var search  = Args.Count > 1 ? Args[1] : "Damage";
var dllPath = Path.Combine(gameDir, "7DaysToDie_Data", "Managed", "Assembly-CSharp.dll");

if (!File.Exists(dllPath))
{
    Console.ForegroundColor = ConsoleColor.Red;
    Console.WriteLine($"Not found: {dllPath}");
    Console.WriteLine("Update the GameDir path in FindMethods.csx or pass it as an argument.");
    return;
}

Console.ForegroundColor = ConsoleColor.Cyan;
Console.WriteLine($"Searching '{dllPath}' for '{search}'...\n");
Console.ResetColor();

var module = ModuleDefinition.ReadModule(dllPath);
int hits = 0;

foreach (var type in module.Types)
{
    foreach (var method in type.Methods)
    {
        if (method.Name.Contains(search, StringComparison.OrdinalIgnoreCase)
         || type.Name.Contains(search, StringComparison.OrdinalIgnoreCase))
        {
            // Colour code by type
            Console.ForegroundColor = ConsoleColor.Yellow;
            Console.Write($"  {type.Name}");
            Console.ForegroundColor = ConsoleColor.White;
            Console.Write("::");
            Console.ForegroundColor = ConsoleColor.Green;
            Console.Write(method.Name);
            Console.ResetColor();

            // Print parameter types
            var parms = string.Join(", ", method.Parameters.Select(p => p.ParameterType.Name));
            Console.WriteLine($"({parms})");
            hits++;
        }
    }
}

Console.ForegroundColor = hits > 0 ? ConsoleColor.Cyan : ConsoleColor.Red;
Console.WriteLine($"\n{hits} result(s) for '{search}'");
Console.ResetColor();
