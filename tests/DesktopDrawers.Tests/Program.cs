using DesktopDrawers.Core.Models;
using DesktopDrawers.Core.Persistence;
using DesktopDrawers.Core.Services;

var tests = new (string Name, Action<string> Run)[]
{
    ("Persistencia, propiedades y copia de seguridad", root =>
    {
        var store = new StateStore(root);
        var state = new AppState { Visible = false, Drawers = [new Drawer { Name = "Juegos", Expanded = false, Left = -200, Opacity = .35 }] };
        store.Save(state);
        state.Drawers[0].Name = "Trabajo"; store.Save(state);
        var loaded = store.Load();
        Check(!loaded.Visible && loaded.Drawers[0].Name == "Trabajo" && loaded.Drawers[0].Left == -200 && loaded.Drawers[0].Opacity == .35, "round trip");
        File.WriteAllText(Path.Combine(root, "state.json"), "{roto");
        Check(store.Load().Drawers[0].Name == "Juegos" && store.RecoveredFromBackup, "backup");
        store.Save(state); Check(store.Load().Drawers[0].Name == "Trabajo", "guardar recuperación");
    }),
    ("Estado corrupto sin backup no se sobrescribe", root =>
    {
        File.WriteAllText(Path.Combine(root, "state.json"), "{roto");
        Throws(() => new StateStore(root).Load());
        Check(File.ReadAllText(Path.Combine(root, "state.json")) == "{roto", "preservar datos");
    }),
    ("Importar, mover, restaurar y evitar colisiones", root =>
    {
        var desktop = Path.Combine(root, "Desktop"); Directory.CreateDirectory(desktop);
        var store = new StateStore(Path.Combine(root, "App"));
        var a = new Drawer(); var b = new Drawer(); var state = new AppState { Drawers = [a, b] };
        var service = new ShortcutService(store, state, [desktop]);
        var original = Path.Combine(desktop, "Juego.lnk"); File.WriteAllText(original, "shortcut fixture");
        var item = service.Import(a, original);
        Check(!File.Exists(original) && File.ReadAllText(item.StoredPath) == "shortcut fixture", "traslado");
        Check(store.Load().Drawers[0].Items[0].OriginalPath == original, "registro durable");
        service.Move(a, b, item); Check(a.Items.Count == 0 && b.Items.Count == 1, "mover entre cajones");
        File.WriteAllText(original, "nuevo");
        var restored = service.Restore(b, item);
        Check(restored != original && File.ReadAllText(original) == "nuevo" && File.ReadAllText(restored) == "shortcut fixture", "no sobrescribir");
        Check(b.Items.Count == 0 && !File.Exists(item.StoredPath), "limpieza administrada");
    }),
    ("Rechazar ejecutables y rutas fuera del escritorio", root =>
    {
        var desk = Path.Combine(root, "Desktop"); Directory.CreateDirectory(desk);
        var drawer = new Drawer(); var state = new AppState { Drawers = [drawer] };
        var service = new ShortcutService(new StateStore(Path.Combine(root, "App")), state, [desk]);
        var exe = Path.Combine(desk, "game.exe"); File.WriteAllText(exe, "keep");
        Throws(() => service.Import(drawer, exe));
        var outside = Path.Combine(root, "other.url"); File.WriteAllText(outside, "keep");
        Throws(() => service.Import(drawer, outside));
        Check(File.Exists(exe) && File.Exists(outside) && drawer.Items.Count == 0, "originales intactos");
    }),
    ("Registro previo a un movimiento fallido permite restaurar", root =>
    {
        var desk = Path.Combine(root, "Desktop"); Directory.CreateDirectory(desk);
        var drawer = new Drawer(); var state = new AppState { Drawers = [drawer] };
        var store = new StateStore(Path.Combine(root, "App"));
        var service = new ShortcutService(store, state, [desk]);
        var original = Path.Combine(desk, "A.url"); File.WriteAllText(original, "url");
        var item = service.Import(drawer, original);
        File.Move(item.StoredPath, original); // Simula metadata guardada, movimiento aún no efectuado.
        service.Restore(drawer, item);
        Check(File.Exists(original) && drawer.Items.Count == 0, "recuperación");
    }),
    ("Ruta administrada y archivos ausentes", root =>
    {
        var d = new Drawer(); var state = new AppState { Drawers = [d] };
        var s = new ShortcutService(new StateStore(Path.Combine(root, "App")), state, [root]);
        var item = new Shortcut { StoredPath = Path.Combine(root, "escape.lnk") }; d.Items.Add(item);
        Throws(() => s.Restore(d, item)); Check(d.Items.Count == 1, "conservar registro");
        Throws(() => s.ValidateManagedPath(Path.Combine(root, "App", "Shortcuts", "..", "escape.lnk")));
    }),
    ("Cerrar configuración conserva residencia; salida explícita", _ =>
    {
        var lifetime = new ResidentLifetime();
        Check(lifetime.ShouldHideOnClose && !lifetime.ExitRequested, "inicio residente");
        Check(lifetime.ShouldHideOnClose, "cerrar configuración no solicita salida");
        lifetime.RequestExit();
        Check(!lifetime.ShouldHideOnClose && lifetime.ExitRequested, "salida de bandeja");
    }),
    ("Inicio por usuario: predeterminado, persistencia, desactivación y rutas con espacios", _ =>
    {
        var backend = new MemoryStartupStore();
        var startup = new StartupRegistration(backend, @"C:\Mis programas\DesktopDrawers.exe");
        Check(!startup.Enabled, "desactivado inicialmente");
        startup.SetEnabled(true);
        Check(backend.Value == "\"C:\\Mis programas\\DesktopDrawers.exe\" --background", "comando citado y segundo plano");
        Check(new StartupRegistration(backend, @"C:\Mis programas\DesktopDrawers.exe").Enabled, "preferencia persistente");
        startup.SetEnabled(false); Check(!startup.Enabled && backend.Value is null, "eliminar solo registro propio");
        var development = StartupRegistration.BuildCommand(@"C:\dotnet\dotnet.exe", @"C:\Code Folder\DesktopDrawers.dll");
        Check(development.EndsWith("DesktopDrawers.dll\" --background"), "ejecución desde SDK");
        Throws(() => StartupRegistration.BuildCommand("bad\"path"));
    }),
    ("Error al registrar inicio no habilita la preferencia", _ =>
    {
        var backend = new MemoryStartupStore { FailWrites = true };
        var startup = new StartupRegistration(backend, "app.exe");
        Throws(() => startup.SetEnabled(true)); Check(!startup.Enabled, "sin falso éxito");
    }),
    ("Actualizar sin perder el JSON v1 y los cajones anteriores", root =>
    {
        var id = Guid.NewGuid();
        File.WriteAllText(Path.Combine(root, "state.json"), $$"""
        {"Version":1,"Visible":false,"Drawers":[{"Id":"{{id}}","Name":"Antes de actualizar","Left":150,"Top":175,"Width":420,"Height":390,"Expanded":false,"Background":"#123456","TitleColor":"#ABCDEF","Opacity":0.45,"Items":[]}]}
        """);
        var store = new StateStore(root); var state = store.Load(); store.Save(state);
        var loaded = store.Load(); var drawer = loaded.Drawers.Single();
        Check(!loaded.Visible && drawer.Id == id && drawer.Name == "Antes de actualizar" && drawer.Left == 150 && drawer.Top == 175 && drawer.Width == 420 && drawer.Height == 390 && !drawer.Expanded && drawer.Background == "#123456" && drawer.TitleColor == "#ABCDEF" && drawer.Opacity == .45, "compatibilidad sin migración destructiva");
    }),
    ("Mover fallido conserva original y revierte metadata y copia parcial", root =>
    {
        var desktop = Path.Combine(root, "Desktop"); Directory.CreateDirectory(desktop);
        foreach (var partial in new[] { false, true })
        {
            var app = Path.Combine(root, partial ? "Partial" : "Fail");
            var store = new StateStore(app); var drawer = new Drawer();
            var state = new AppState { Drawers = [drawer] };
            var service = new ShortcutService(store, state, [desktop], new FailedMover(partial));
            var path = Path.Combine(desktop, partial ? "Partial.lnk" : "Fail.url");
            File.WriteAllText(path, "original intacto");
            Throws(() => service.Import(drawer, path));
            Check(File.ReadAllText(path) == "original intacto", "fuente intacta");
            Check(drawer.Items.Count == 0 && store.Load().Drawers[0].Items.Count == 0, "rollback metadata");
            Check(!Directory.EnumerateFiles(Path.Combine(app, "Shortcuts")).Any(), "sin duplicado administrado");
        }
    }),
    ("Prevalidación acepta escritorio público y .URL sin modificar archivos", root =>
    {
        var user = Path.Combine(root, "User"); var common = Path.Combine(root, "Public");
        Directory.CreateDirectory(user); Directory.CreateDirectory(common);
        var drawer = new Drawer(); var state = new AppState { Drawers = [drawer] };
        var service = new ShortcutService(new StateStore(Path.Combine(root, "App")), state, [user, common]);
        var path = Path.Combine(common, "Enlace.URL"); File.WriteAllText(path, "[InternetShortcut]");
        Check(service.CanImport(path) && File.Exists(path) && drawer.Items.Count == 0, "drag-over sin efectos");
        Check(!service.CanImport(Path.Combine(common, "no existe.lnk")), "inexistente");
        var item = service.Import(drawer, path);
        Check(!File.Exists(path) && item.Name == "Enlace", "sin duplicado y nombre conservado");
        Check(service.Restore(drawer, item) == path && File.Exists(path), "restaurar escritorio público");
    })
};
var failures = 0;
foreach (var (name, run) in tests)
{
    var root = Path.Combine(Path.GetTempPath(), "DesktopDrawers-test-" + Guid.NewGuid()); Directory.CreateDirectory(root);
    try { run(root); Console.WriteLine("PASS " + name); }
    catch (Exception e) { failures++; Console.Error.WriteLine("FAIL " + name + ": " + e); }
    finally { Directory.Delete(root, true); }
}
Console.WriteLine($"{tests.Length - failures}/{tests.Length} pruebas aprobadas");
return failures == 0 ? 0 : 1;
static void Check(bool value, string message) { if (!value) throw new Exception(message); }
static void Throws(Action action) { try { action(); } catch { return; } throw new Exception("Se esperaba un error"); }

sealed class MemoryStartupStore : IStartupStore
{
    public string? Value { get; private set; }
    public bool FailWrites { get; init; }
    public string? Read() => Value;
    public void Write(string command) { if (FailWrites) throw new UnauthorizedAccessException(); Value = command; }
    public void Remove() => Value = null;
}
sealed class FailedMover(bool partial) : IShortcutMover
{
    public void Move(string source, string destination)
    {
        if (partial) File.WriteAllText(destination, "partial");
        throw new IOException("Fallo de movimiento simulado");
    }
}
