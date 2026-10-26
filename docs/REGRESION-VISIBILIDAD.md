# Regresión de visibilidad — 0.2.1

## Hallazgos confirmados en el código 0.2.0

El texto «Cajones alojados en el escritorio; sin ocultación por foco» solo significaba que no se había lanzado una excepción. No comprobaba que hubiera contenido visible en pantalla.

- `FindDesktopHost()` devolvía el padre de `SHELLDLL_DefView` (Progman/WorkerW). Al usarlo en `SetParent`, los cajones quedaban como hermanos de la superficie de iconos, cuyo orden y composición controla Explorer. Un `SetParent` correcto no demuestra que estén por encima del fondo ni de DefView.
- `Place()` usaba `SWP_NOSIZE | SWP_NOZORDER`, conservando un tamaño/orden nativo que podía quedar obsoleto después de cambiar las ventanas WPF a WS_CHILD. No se comprobaban los resultados de `ScreenToClient` ni `SetWindowPos`.
- Se cambiaba la opacidad de toda la ventana de 0 a 1 tras alojarla. WPF puede recalcular la superficie layered y su geometría; no había comprobación posterior al render.
- Los eventos de geometría durante Show/SetParent podían guardar coordenadas transitorias de la ventana antes de que `IsDesktopHosted` estuviera marcado. Ahora se bloquea esa captura durante la inicialización.
- No se registraban HWND, rectángulos, visibilidad, estilos ni capas de Explorer y se repetían reconstrucciones fallidas cada 30 segundos indefinidamente.

Estos son defectos verificables en el código. Sin una sesión Windows ni un diagnóstico de la máquina afectada **no se puede afirmar cuál de las capas/condiciones produjo los píxeles invisibles en esa máquina**. La compilación en Linux no reproduce DWM/Explorer.

## Recuperación prioritaria

0.2.1 inicia en **Compatibilidad** cuando carga un JSON anterior que no contiene modo. Conserva GUID, nombres, accesos, dimensiones, colores, expansión y visibilidad global. No borra ni reinicializa AppData.

En configuración:

- **Modo de escritorio → Compatibilidad**: ventanas WPF independientes; sin SetParent. Estilos ToolWindow/NoActivate, sin Topmost. Se ordenan por debajo de las aplicaciones y por encima del escritorio existente; no se usa HWND_BOTTOM, que puede colocar una ventana tras el fondo. Se conservan los controles y el drop de la misma vista existente.
- **Integrado con Explorer (experimental)**: selección explícita. Busca la superficie `SHELLDLL_DefView` visible del proceso real del shell, aloja los cajones como hijos en esa superficie y los sitúa arriba entre sus hijos, con tamaño explícito en píxeles y conversión screen→client verificada. No crea WorkerW ni envía mensajes privados para cambiar el fondo.
- **Recuperar cajones**: activa compatibilidad y visibilidad global, y reconstruye las ventanas de los modelos existentes. Solo recoloca títulos fuera de pantallas reales; no crea cajones nuevos. Las ventanas WPF independientes usan coordenadas WPF de pantalla: el propietario oculto de una tool window no se interpreta como un padre de coordenadas cliente.
- **Diagnóstico…**: guarda una captura nativa para inspección, sin ejecutar ni recorrer destinos de los accesos.

## Validación del modo integrado

Se comprueba al crear las ventanas y una vez más después de un segundo de render. No es una prueba de píxeles ni una garantía de composición:

- HWND propio/padre válidos, padre esperado y visibilidad de ancestros.
- Visibility/Opacity WPF, IsWindowVisible y DWM cloaking.
- Rectángulo nativo no vacío, intersección con monitor y ausencia de recorte por el padre.
- WS_CHILD activo, WS_POPUP y WS_EX_TOPMOST ausentes.
- Hijo encontrado en el punto del título dentro de la superficie de iconos.
- Capas Progman/WorkerW visibles del mismo shell por encima de la raíz, sus rectángulos y cloaking.
- Resultados de SetWindowPos/ScreenToClient/SetParent y dimensiones explícitas, incluyendo DPI.

Un fallo estructural activa compatibilidad y persiste esa preferencia. No se vuelve a probar integración sin una elección del usuario. Las señales de recuperación del host se agrupan y están limitadas a dos reconstrucciones automáticas por sesión/elección; después se usa compatibilidad. No hay sondeo continuo del foco ni temporizador que reintente indefinidamente. Aunque las comprobaciones pasen, el texto pide confirmación visual y mantiene disponible Recuperar cajones.

## Registro de diagnóstico

`%LOCALAPPDATA%\DesktopDrawers\desktop-diagnostics.jsonl`

Se rota aproximadamente a 1 MiB (`.previous`). Contiene versión/SO, modo, GUID del cajón, HWND/padres, clases, estilos, DPI, rectángulos nativos y del padre, geometría del modelo/WPF, opacidad, Visibility/IsVisible, capas del shell, fallos y cambios a compatibilidad. No registra claves, ni lee/ejecuta los destinos de los accesos. Los errores de escritura del diagnóstico no impiden el inicio; el botón informa si no puede guardarlo.

## Archivos de implementación

- `NativeInterop/DesktopVisibility.cs`: selección de superficie, colocación/tamaño, modos nativos e inspección.
- `Services/Workspace.cs`: selección persistida, reconstrucción, validación y fallback limitado.
- `Services/DiagnosticLog.cs`: registro con rotación.
- `Views/DrawerWindow.xaml.cs`: movimiento según modo, protección de geometría transitoria y reporte de fallos.
- `Views/ManagerWindow.xaml` y `.xaml.cs`: selector, recuperación y diagnóstico.
- `DesktopDrawers.Core/Services/DesktopIntegration.cs`: política y comprobaciones independientes de Windows.
- `DesktopDrawers.Core/Models/AppState.cs` y `Persistence/StateStore.cs`: preferencia compatible con JSON v1; un modo numérico desconocido vuelve a compatibilidad sin descartar cajones.
- Versionado del proyecto/manifiesto, documentación y pruebas del núcleo.

No se cambia la implementación del inicio automático, la bandeja, los accesos ni sus rutas de almacenamiento.

## Estado de verificación

Compilación WPF cruzada y pruebas del núcleo ejecutadas en Linux. La nueva prueba de migración usa un fixture con cajones llamados «Test» y «Nuevo cajón»; no se ha accedido al AppData real del usuario. No se han ejecutado WPF, DWM ni Explorer. Las dos modalidades requieren la lista manual en `PRUEBAS-WINDOWS.md`; compatibilidad no promete equivalencia completa con un elemento nativo del escritorio (especialmente Win+D, fondos de terceros y DPI mixtos).
