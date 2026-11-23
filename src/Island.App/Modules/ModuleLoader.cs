using System.IO;
using System.Reflection;
using System.Runtime.Loader;
using Island.App.Infrastructure;
using Island.Core;

namespace Island.App.Modules;

/// <summary>
/// 掃描程式目錄下的 Island.Modules.*.dll,建立所有 <see cref="IIslandModule"/> 實作。
/// 外殼不直接引用任何模組型別,新增模組不需要改這裡。
/// </summary>
internal static class ModuleLoader
{
    private const string Pattern = "Island.Modules.*.dll";

    /// <summary>找到的所有模組(含停用的),給設定視窗的「模組」頁使用。</summary>
    public static IReadOnlyList<(string Id, string Title, bool DefaultOn)> Discovered { get; private set; } = [];

    /// <summary>模組是否要載入:使用者關掉的不載入;預設關閉的模組要使用者打開才載入。</summary>
    public static bool IsEnabled(IslandSettings settings, string id, bool defaultOn)
    {
        if (settings.DisabledModules.Any(x => string.Equals(x, id, StringComparison.OrdinalIgnoreCase))) return false;
        return defaultOn || settings.EnabledModules.Any(x => string.Equals(x, id, StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>設定頁切換模組開關。</summary>
    public static void SetEnabled(IslandSettings settings, string id, bool defaultOn, bool on)
    {
        settings.DisabledModules.RemoveAll(x => string.Equals(x, id, StringComparison.OrdinalIgnoreCase));
        settings.EnabledModules.RemoveAll(x => string.Equals(x, id, StringComparison.OrdinalIgnoreCase));
        if (on && !defaultOn) settings.EnabledModules.Add(id);
        if (!on && defaultOn) settings.DisabledModules.Add(id);
    }

    public static IReadOnlyList<IIslandModule> LoadAll(IslandSettings settings, IIslandHost host)
    {
        var found = new List<IIslandModule>();

        foreach (var path in Directory.EnumerateFiles(AppContext.BaseDirectory, Pattern))
        {
            Assembly assembly;
            try
            {
                assembly = AssemblyLoadContext.Default.LoadFromAssemblyPath(path);
            }
            catch (Exception ex)
            {
                Log.Write($"模組組件載入失敗 {Path.GetFileName(path)}:{ex.Message}");
                continue;
            }

            foreach (var type in SafeGetTypes(assembly))
            {
                if (type is not { IsClass: true, IsAbstract: false, IsPublic: true }) continue;
                if (!typeof(IIslandModule).IsAssignableFrom(type)) continue;
                if (type.GetConstructor(Type.EmptyTypes) is null) continue;

                try
                {
                    found.Add((IIslandModule)Activator.CreateInstance(type)!);
                }
                catch (Exception ex)
                {
                    Log.Write($"模組建立失敗 {type.FullName}:{ex.Message}");
                }
            }
        }

        Discovered = found.Select(m => (m.Id, m.Title, m.EnabledByDefault)).ToList();
        var ordered = found
            .Where(m => IsEnabled(settings, m.Id, m.EnabledByDefault))
            .OrderBy(m => OrderOf(settings.Modules, m.Id))
            .ThenBy(m => m.Id, StringComparer.OrdinalIgnoreCase)
            .ToList();

        foreach (var m in found.Where(m => !IsEnabled(settings, m.Id, m.EnabledByDefault))) m.Dispose();

        var loaded = new List<IIslandModule>(ordered.Count);
        foreach (var module in ordered)
        {
            try
            {
                module.Initialize(host);
                loaded.Add(module);
            }
            catch (Exception ex)
            {
                Log.Write($"模組初始化失敗 {module.Id}:{ex.Message}");
                module.Dispose();
            }
        }

        Log.Write("已載入模組:" + string.Join(", ", loaded.Select(m => m.Id)));
        return loaded;
    }

    private static int OrderOf(List<string> order, string id)
    {
        var i = order.FindIndex(x => string.Equals(x, id, StringComparison.OrdinalIgnoreCase));
        return i < 0 ? int.MaxValue : i;
    }

    private static IEnumerable<Type> SafeGetTypes(Assembly assembly)
    {
        try { return assembly.GetExportedTypes(); }
        catch (ReflectionTypeLoadException ex) { return ex.Types.Where(t => t is not null)!; }
    }
}
