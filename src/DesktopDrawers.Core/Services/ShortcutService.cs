using DesktopDrawers.Core.Models;
using DesktopDrawers.Core.Persistence;

namespace DesktopDrawers.Core.Services;

// Write-ahead metadata: a crash can leave a duplicate, never an unrecorded moved file.
public interface IShortcutMover
{
    void Move(string source, string destination);
}
public sealed class ShortcutMover : IShortcutMover
{
    public void Move(string source, string destination) => File.Move(source, destination, overwrite: false);
}
public sealed class ShortcutService(StateStore store, AppState state, IEnumerable<string> desktopRoots, IShortcutMover? mover = null)
{
    private readonly string[] roots = desktopRoots.Where(s => !string.IsNullOrWhiteSpace(s)).Select(Path.GetFullPath).ToArray();

    public bool CanImport(string source)
    {
        try { ValidateImport(source); return true; }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException or ArgumentException or InvalidOperationException or NotSupportedException) { return false; }
    }
    private void ValidateImport(string source)
    {
        source = Path.GetFullPath(source);
        var ext = Path.GetExtension(source).ToLowerInvariant();
        if (ext is not (".lnk" or ".url")) throw new InvalidOperationException("Solo se admiten accesos .lnk y .url.");
        if (!File.Exists(source)) throw new FileNotFoundException("No se encuentra el acceso directo.", source);
        var parent = Path.GetDirectoryName(source);
        if (!roots.Any(root => string.Equals(Path.TrimEndingDirectorySeparator(root), parent, StringComparison.OrdinalIgnoreCase)))
            throw new InvalidOperationException("El acceso debe estar en el escritorio del usuario o público.");
        if ((File.GetAttributes(source) & (FileAttributes.ReparsePoint | FileAttributes.Offline)) != 0)
            throw new InvalidOperationException("Descarga primero el archivo de OneDrive; no se mueven enlaces de sistema ni archivos sin conexión.");
        if (state.Drawers.SelectMany(d => d.Items).Any(i => string.Equals(i.OriginalPath, source, StringComparison.OrdinalIgnoreCase)))
            throw new InvalidOperationException("Este acceso ya está registrado; restaura primero la entrada anterior.");
    }
    public Shortcut Import(Drawer drawer, string source)
    {
        if (!state.Drawers.Contains(drawer)) throw new InvalidOperationException("El cajón ya no existe.");
        source = Path.GetFullPath(source);
        ValidateImport(source);
        var ext = Path.GetExtension(source).ToLowerInvariant();
        var item = new Shortcut { Name = Path.GetFileNameWithoutExtension(source), OriginalPath = source };
        item.StoredPath = Path.Combine(store.DirectoryPath, "Shortcuts", item.Id + ext);
        Directory.CreateDirectory(Path.GetDirectoryName(item.StoredPath)!);
        drawer.Items.Add(item);
        try { store.Save(state); }
        catch { drawer.Items.Remove(item); throw; }
        try { (mover ?? new ShortcutMover()).Move(source, item.StoredPath); }
        catch
        {
            // File.Move is atomic on the same volume. On a failed cross-volume
            // transfer, remove only our unique destination if the source survives.
            // If the source vanished, retain the durable recovery entry.
            if (File.Exists(source))
            {
                if (File.Exists(item.StoredPath)) File.Delete(item.StoredPath);
                drawer.Items.Remove(item);
                try { store.Save(state); }
                catch { drawer.Items.Add(item); throw; }
            }
            throw;
        }
        return item;
    }

    public void Move(Drawer from, Drawer to, Shortcut item)
    {
        if (from == to) return;
        if (!from.Items.Remove(item)) throw new InvalidOperationException("El acceso ya no pertenece al cajón.");
        to.Items.Add(item);
        try { store.Save(state); }
        catch { to.Items.Remove(item); from.Items.Add(item); throw; }
    }

    public string Restore(Drawer drawer, Shortcut item)
    {
        ValidateManagedPath(item.StoredPath);
        string destination;
        if (File.Exists(item.StoredPath))
        {
            var originalParent = Path.GetDirectoryName(item.OriginalPath);
            var safeOriginal = roots.Any(r => string.Equals(Path.TrimEndingDirectorySeparator(r), originalParent, StringComparison.OrdinalIgnoreCase));
            var target = safeOriginal && Directory.Exists(originalParent) ? originalParent! : roots.FirstOrDefault(Directory.Exists)
                ?? throw new DirectoryNotFoundException("No se encuentra un escritorio disponible.");
            destination = CopyUnique(item.StoredPath, target, Path.GetFileName(item.OriginalPath));
            // Remove the managed copy only after the restored file is durable.
            var previousOriginal = item.OriginalPath;
            item.OriginalPath = destination;
            try { store.Save(state); }
            catch { item.OriginalPath = previousOriginal; throw; }
            File.Delete(item.StoredPath);
        }
        else if (File.Exists(item.OriginalPath)) destination = item.OriginalPath;
        else throw new FileNotFoundException("No se encuentra ninguna copia del acceso. Se conserva su registro.");
        drawer.Items.Remove(item);
        try { store.Save(state); }
        catch { drawer.Items.Add(item); throw; }
        return destination;
    }

    public void ValidateManagedPath(string path)
    {
        var folder = Path.Combine(store.DirectoryPath, "Shortcuts") + Path.DirectorySeparatorChar;
        if (!Path.GetFullPath(path).StartsWith(folder, StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("La ruta no pertenece a la carpeta administrada.");
    }

    private static string CopyUnique(string source, string directory, string name)
    {
        for (var n = 0; n < 10000; n++)
        {
            var candidate = Path.Combine(directory, n == 0 ? name : $"{Path.GetFileNameWithoutExtension(name)} ({n}){Path.GetExtension(name)}");
            FileStream output;
            try { output = new FileStream(candidate, FileMode.CreateNew, FileAccess.Write, FileShare.None); }
            catch (IOException) when (File.Exists(candidate)) { continue; }
            try
            {
                using (output)
                using (var input = File.OpenRead(source))
                { input.CopyTo(output); output.Flush(true); }
                return candidate;
            }
            catch
            {
                output.Dispose();
                File.Delete(candidate); // Only the file created by this operation.
                throw;
            }
        }
        throw new IOException("No hay un nombre de archivo disponible.");
    }
}
