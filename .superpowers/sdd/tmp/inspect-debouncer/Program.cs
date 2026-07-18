using System.Reflection;
using System.Runtime.InteropServices;

string dalamudPath = @"C:\Users\lokin\AppData\Roaming\XIVLauncher\addon\Hooks\dev\Dalamud.dll";
string dalaMockPath = @"C:\Users\lokin\.nuget\packages\dalamock.core\6.1.7\lib\net10.0-windows7.0\DalaMock.Core.dll";

var paths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
var runtimeDir = RuntimeEnvironment.GetRuntimeDirectory();
foreach (var file in Directory.GetFiles(runtimeDir, "*.dll"))
{
    paths.Add(file);
}

foreach (var file in Directory.GetFiles(Path.GetDirectoryName(dalamudPath)!, "*.dll"))
{
    paths.Add(file);
}

foreach (var file in Directory.GetFiles(Path.GetDirectoryName(dalaMockPath)!, "*.dll"))
{
    paths.Add(file);
}

paths.Add(dalamudPath);
paths.Add(dalaMockPath);

var resolver = new PathAssemblyResolver(paths);
using var mlc = new MetadataLoadContext(resolver);

var dalamud = mlc.LoadFromAssemblyPath(dalamudPath);
var dalaMock = mlc.LoadFromAssemblyPath(dalaMockPath);

DumpType("IFramework", dalamud.GetType("Dalamud.Plugin.Services.IFramework", throwOnError: false));
DumpType("FrameworkPluginScoped", dalamud.GetType("Dalamud.Game.FrameworkPluginScoped", throwOnError: false));
DumpType("MockFramework", dalaMock.GetType("DalaMock.Core.Mocks.DalamudServices.MockFramework", throwOnError: false));
DumpType("IDebouncer", dalamud.GetType("Dalamud.Utility.IDebouncer", throwOnError: false));

static void DumpType(string label, Type? type)
{
    Console.WriteLine($"=== {label} ===");
    if (type is null)
    {
        Console.WriteLine("TYPE NOT FOUND");
        Console.WriteLine();
        return;
    }

    Console.WriteLine(type.FullName);
    foreach (var property in type.GetProperties(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly))
    {
        Console.WriteLine($"PROPERTY {property.PropertyType.FullName} {property.Name}");
    }

    foreach (var method in type.GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly))
    {
        var parameters = string.Join(", ", method.GetParameters().Select(parameter => $"{parameter.ParameterType.FullName} {parameter.Name}"));
        Console.WriteLine($"METHOD {method.ReturnType.FullName} {method.Name}({parameters})");
    }

    Console.WriteLine("Interfaces:");
    foreach (var iface in type.GetInterfaces())
    {
        Console.WriteLine(iface.FullName);
    }

    Console.WriteLine();
}
