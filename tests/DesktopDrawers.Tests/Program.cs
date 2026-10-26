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
