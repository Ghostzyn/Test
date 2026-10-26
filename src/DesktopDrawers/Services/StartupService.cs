using System.IO;
using Microsoft.Win32;
using DesktopDrawers.Core.Services;
namespace DesktopDrawers.Services;
public sealed class StartupService : IStartupStore
{
    private const string Key = @"Software\Microsoft\Windows\CurrentVersion\Run";
    private const string Value = "DesktopDrawers";
    public static StartupRegistration Create()
    {
        var executable = Environment.ProcessPath ?? throw new InvalidOperationException("No se encontró el ejecutable.");
        var assembly = Path.GetFileNameWithoutExtension(executable).Equals("dotnet", StringComparison.OrdinalIgnoreCase)
            ? Path.Combine(AppContext.BaseDirectory, "DesktopDrawers.dll") : null;
        return new(new StartupService(), executable, assembly);
    }
    public string? Read()
    {
        using var key = Registry.CurrentUser.OpenSubKey(Key);
        return key?.GetValue(Value) as string;
    }
    public void Write(string command)
    {
        using var key = Registry.CurrentUser.CreateSubKey(Key);
        key.SetValue(Value, command, RegistryValueKind.String);
    }
    public void Remove()
    {
        using var key = Registry.CurrentUser.OpenSubKey(Key, writable: true);
        key?.DeleteValue(Value, throwOnMissingValue: false);
    }
}
