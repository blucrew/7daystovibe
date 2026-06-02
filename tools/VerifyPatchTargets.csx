#!/usr/bin/env dotnet-script
// VerifyPatchTargets.csx — checks that every method our Harmony patches target
// actually exists in Assembly-CSharp.dll.
// Run this before building to catch name mismatches early.
//
// Usage: dotnet script VerifyPatchTargets.csx [GameDir]

#r "nuget: Mono.Cecil, 0.11.5"
using Mono.Cecil;

var gameDir = Args.Count > 0 ? Args[0] : @"C:\Program Files (x86)\Steam\steamapps\common\7 Days To Die";
var dllPath = Path.Combine(gameDir, "7DaysToDie_Data", "Managed", "Assembly-CSharp.dll");

// ── Edit this list to match your [HarmonyPatch] attributes ──────────────────
// Targets marked (VERIFY) are best-guess names — run FindMethods.csx to confirm.
var targets = new (string ClassName, string MethodName)[]
{
    // Combat
    ("EntityPlayer",          "DamageEntity"),
    ("EntityPlayer",          "Heal"),
    ("EntityPlayer",          "OnEntityDeath"),
    ("EntityAlive",           "Kill"),           // (VERIFY) may lack DamageResponse param
    ("Explosion",             "Explode"),
    ("ItemActionRanged",      "FireShot"),
    ("ItemActionMelee",       "HitTarget"),
    ("ItemActionAttack",      "HitBlock"),        // (VERIFY) mining/chopping
    ("BloodMoonParty",        "StartBloodMoon"),
    ("BloodMoonParty",        "StopBloodMoon"),

    // Status effects
    ("EntityPlayer",          "updateCurrentBiomeAndWeather"),
    ("EntityBuffs",           "AddBuff"),
    ("EntityPlayer",          "OnFallingDamage"),
    ("EntityPlayer",          "OnEntitySpawned"),

    // World events
    ("GameManager",           "StartGame"),       // session reset
    ("BlockLandMine",         "OnEntityWalksOnBlock"),
    ("BlockSpikes",           "OnEntityWalksOnBlock"),
    ("BlockElectricWireRelay","OnEntityTouched"),  // (VERIFY) may not exist
    ("AirDropManager",        "DropAirDrop"),
    ("EntityPlayer",          "onBlockDestroyed"),

    // Activities
    ("XUiC_RecipeCraftCount", "RecipeCraftingDone"),
    ("XUiC_LootWindow",       "Open"),
    ("EntityPlayer",          "GrabItem"),         // (VERIFY) may need different name
    ("EntityPlayer",          "LevelUpStats"),
    ("QuestEventManager",     "CompleteQuest"),

    // Vehicles
    ("EntityVehicle",         "OnCollisionEnter"),
    ("EntityVehicle",         "DamageEntity"),
    ("EntityVehicle",         "Kill"),
    ("EntityVehicle",         "updateSteeringAndThrottle"),

    // Stealth
    ("EAISetNearestTarget",   "SetNearestTarget"),
    ("EAIScreamer",           "StartScream"),
    ("EntityAlive",           "OnHitResponse"),
};
// ─────────────────────────────────────────────────────────────────────────────

if (!File.Exists(dllPath))
{
    Console.ForegroundColor = ConsoleColor.Red;
    Console.WriteLine($"DLL not found: {dllPath}");
    return;
}

var module = ModuleDefinition.ReadModule(dllPath);
var methods = module.Types
    .SelectMany(t => t.Methods.Select(m => (Type: t.Name, Method: m.Name)))
    .ToHashSet();

Console.WriteLine($"\nVerifying {targets.Length} patch targets against {Path.GetFileName(dllPath)}...\n");

bool allOk = true;
foreach (var (cls, mth) in targets)
{
    bool found = methods.Contains((cls, mth));
    Console.ForegroundColor = found ? ConsoleColor.Green : ConsoleColor.Red;
    string status = found ? "  ✓ FOUND  " : "  ✗ MISSING";
    Console.WriteLine($"{status}  {cls}::{mth}");
    if (!found) allOk = false;
}

Console.ResetColor();
Console.WriteLine();
if (!allOk)
{
    Console.ForegroundColor = ConsoleColor.Yellow;
    Console.WriteLine("Some targets are missing. Run FindMethods.csx to find the correct names.");
    Console.WriteLine("Example: dotnet script FindMethods.csx \"<GameDir>\" \"BloodMoon\"");
}
else
{
    Console.ForegroundColor = ConsoleColor.Cyan;
    Console.WriteLine("All patch targets verified! Safe to build.");
}
Console.ResetColor();
