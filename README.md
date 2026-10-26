# Desktop Drawers 0.2.1

Aplicación nativa C# / WPF para Windows 11 x64, sin navegador ni dependencias de terceros. Usa .NET 10 LTS. El repositorio incluye la solución, la aplicación, una biblioteca independiente de WPF y pruebas ejecutables sin paquetes de pruebas externos.

## Descargar para Windows 11 x64

[Descargar ZIP autónomo](https://github.com/Ghostzyn/Test/raw/refs/heads/main/downloads/DesktopDrawers-win-x64-portable.zip)

Extrae el ZIP y abre `DesktopDrawers/DesktopDrawers.exe`. No necesitas Visual Studio ni instalar .NET por separado. Si GitHub solicita iniciar sesión, usa una cuenta con acceso a este repositorio. También puedes abrir `downloads/DesktopDrawers-win-x64-portable.zip` en GitHub y pulsar **Download raw file**. El checksum está en `downloads/SHA256SUMS.txt`.

Esta es una versión preliminar. Compilada para Windows x64 y con pruebas del núcleo aprobadas; la interfaz y la integración con Explorer están pendientes de validación en Windows 11. Prueba primero con accesos descartables.

## Recuperar cajones que no se veían en 0.2.0

Esta actualización inicia en **Compatibilidad** al cargar los datos de 0.2.0: reutiliza tus cajones y accesos como ventanas independientes, sin SetParent ni Topmost. Si estaban ocultos, abre configuración desde la bandeja y pulsa **Recuperar cajones**. No borres AppData.

En **Modo de escritorio** puedes elegir Compatibilidad o Integrado con Explorer (experimental). Ante un fallo detectado vuelve a compatibilidad y no repite indefinidamente intentos fallidos. **Diagnóstico…** guarda el estado nativo en `%LOCALAPPDATA%\DesktopDrawers\desktop-diagnostics.jsonl`. El estado integrado solo informa comprobaciones estructurales; no asegura que DWM haya dibujado los cajones.

[Hallazgos y archivos modificados](docs/REGRESION-VISIBILIDAD.md). Las interacciones gráficas todavía requieren pruebas Windows 11.

## Compilar y ejecutar en Windows

Instala el SDK .NET **10.0.403** x64 (Visual Studio es opcional). Desde la raíz del repositorio:

```powershell
dotnet restore DesktopDrawers.sln
dotnet build DesktopDrawers.sln -c Release
dotnet run --project tests/DesktopDrawers.Tests -c Release
dotnet run --project src/DesktopDrawers -c Release
```

El proyecto de pruebas es un runner de consola: `dotnet run` ejecuta las pruebas y devuelve código distinto de cero si hay fallos. `dotnet test` no ejecuta este runner.

Publicación autónoma, sin necesitar .NET instalado en el equipo de destino:

```powershell
dotnet publish src/DesktopDrawers/DesktopDrawers.csproj -c Release -r win-x64 --self-contained true -o artifacts/win-x64 -p:PublishSingleFile=true -p:EnableCompressionInSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true
```

Ejecuta `artifacts/win-x64/DesktopDrawers.exe`. Es una aplicación portátil sin instalador, sin firma Authenticode; Windows puede mostrar su advertencia habitual. El archivo único extrae bibliotecas nativas en la carpeta temporal administrada por .NET. No requiere permisos de administrador para el uso habitual.

## Uso

1. Abre el administrador y pulsa **Nuevo cajón**. Puedes crear varios.
2. Arrastra la barra superior para mover el cajón; usa la esquina inferior derecha para redimensionarlo. La flecha contrae o expande conservando el tamaño expandido.
3. El engranaje permite cambiar nombre, fondo HEX, título HEX y opacidad del fondo (10–100 %). Los textos e iconos conservan su opacidad. Para RGB, convierte cada componente a su par HEX; todavía no hay selector RGB gráfico.
4. Arrastra un `.lnk` o `.url` que esté directamente en el escritorio del usuario o público hacia el cajón. El archivo se traslada a AppData después de guardar su registro de recuperación. No se mueve el ejecutable al que apunta.
5. Doble clic abre el acceso mediante el shell de Windows. Solo deben importarse accesos de confianza: ejecutarlos tiene el mismo efecto que abrirlos desde Explorer.
6. Arrastra un elemento de un cajón a otro para reorganizarlo. El menú contextual permite abrirlo o **restaurarlo al escritorio**, quitándolo del cajón sin borrarlo permanentemente.
7. **Restaurar todo** devuelve los accesos antes de dejar de usar la aplicación. Eliminar un cajón requiere confirmación y restaura sus accesos primero; un error detiene la eliminación.
8. Ctrl+Alt+D y el botón del administrador alternan la visibilidad global. Si el atajo está ocupado se informa al iniciar y sigue disponible el botón.
9. La **X** y **Ocultar configuración** cierran solamente la vista de administración; los cajones y la aplicación siguen activos. La bandeja permite abrir configuración, mostrar/ocultar cajones, activar/desactivar inicio con Windows y **Salir completamente**, la única salida normal del programa.
10. **Iniciar Desktop Drawers con Windows** está desactivado por defecto. Al activarlo se guarda un valor por usuario en `HKCU\Software\Microsoft\Windows\CurrentVersion\Run`, sin administrador. Inicia con `--background`, sin configuración y respetando la visibilidad guardada. Desactivarlo elimina solo el valor propio. Si mueves el EXE a otra carpeta, desactiva/reactiva esta opción para actualizar la ruta.

Para actualizar, cierra primero la versión anterior, conserva `%LOCALAPPDATA%\DesktopDrawers` y extrae el nuevo ZIP. El formato JSON v1 permanece compatible. [Cambios 0.2.1](CHANGELOG.md) y [pruebas manuales Windows](docs/PRUEBAS-WINDOWS.md).

## Datos y recuperación

Datos en `%LOCALAPPDATA%\DesktopDrawers`:

- `state.json`: cajones, geometría, colores, estado global y rutas de los accesos.
- `state.json.bak`: versión anterior del estado; se usa si la principal es ilegible. Si ambas son ilegibles, el inicio falla visiblemente y no las reemplaza por un estado vacío.
- `Shortcuts`: accesos con nombres GUID que evitan colisiones. No borres esta carpeta al quitar los binarios de la aplicación.

El JSON se escribe en un temporal, se fuerza a disco y se sustituye mediante `File.Replace`. El registro del acceso se guarda antes de retirar el original. El traslado usa `File.Move` sin sobrescritura; ante fallo con el original presente se revierte la entrada y cualquier destino parcial. Un corte del proceso puede dejar una entrada pendiente de recuperación, priorizando conservar los archivos. Si una operación informa un error, revisa la entrada y usa Restaurar; no repitas manualmente borrados. La restauración elige un nombre libre (`Nombre (1).lnk`, etc.) sin sobrescribir el archivo existente. Guarda la nueva ruta antes de borrar la copia administrada. Si faltan todas las copias, conserva el registro y muestra un error.

Se consulta `Environment.SpecialFolder.DesktopDirectory` y `CommonDesktopDirectory`, respetando ubicaciones redirigidas. La versión inicial rechaza archivos marcados Offline o ReparsePoint para evitar mover placeholders de OneDrive o enlaces del sistema: materializa una copia local antes de importarlos. Solo acepta accesos directamente en esos escritorios, no en subcarpetas. Los accesos públicos protegidos pueden dar error de permisos; no se elevan privilegios. Si el escritorio original no existe, se intenta un escritorio disponible. No se sincronizan cambios externos al JSON durante la ejecución. Una exclusión mutua evita dos instancias por usuario.

Ante un corte de energía, realiza copia de seguridad de toda la carpeta de datos antes de intervenir. Los restos `.pending` de la versión anterior corresponden a copias interrumpidas y no se borran automáticamente al actualizar. Esta estrategia no sustituye una copia de seguridad del disco.

## Integración con Explorer

Se eliminó la regla que ocultaba cajones al cambiar el foco (`CanShowDrawers` / `EVENT_SYSTEM_FOREGROUND`), causante de su desaparición al seleccionar/arrastrar iconos. La visibilidad depende únicamente del interruptor global.

El modo predeterminado **Compatibilidad** conserva ventanas independientes ToolWindow/NoActivate y las ordena por debajo de aplicaciones, sin Topmost. La selección del escritorio no cambia el interruptor de visibilidad. Windows sigue administrando sus ventanas (Win+D, escalados y fondos de terceros necesitan pruebas específicas).

El modo **Integrado**, experimental y explícito, aloja los HWND como hijos de `SHELLDLL_DefView` del shell (superficie de iconos), verifica padre/visibilidad/estilos/rectángulos/recorte/capas y vuelve a compatibilidad ante fallo. No se inyecta código, se modifica Explorer ni se usan hooks globales de teclado o ratón. Se conserva el atajo Ctrl+Alt+D y las señales de recuperación, ahora limitadas.

La jerarquía de Explorer no es una API pública estable. Los controles nativos no demuestran visibilidad gráfica. [Causa de la regresión y recuperación](docs/REGRESION-VISIBILIDAD.md) y [procedimiento de prueba](docs/PRUEBAS-WINDOWS.md).

El doble clic en una zona vacía del escritorio sigue pendiente; se conserva Ctrl+Alt+D y el menú de bandeja.

## Arquitectura

- `DesktopDrawers.Core/Models`: estado independiente de UI.
- `DesktopDrawers.Core/Persistence`: JSON, validación básica y recuperación.
- `DesktopDrawers.Core/Services`: operaciones de archivos y restauración.
- `DesktopDrawers/Views`: ventanas WPF y controles.
- `DesktopDrawers/ViewModels`: representación de accesos e iconos.
- `DesktopDrawers/Services`: coordinación, guardado diferido y extracción de iconos.
- `DesktopDrawers/NativeInterop`: alojamiento del escritorio, recuperación y atajo.

El guardado de geometría se difiere 400 ms para agrupar cambios; no existe sondeo periódico del foco; solo hay comprobaciones diferidas y recuperación limitada ante señales concretas. Los handles de icono se liberan mediante `DestroyIcon`, los bitmaps se congelan y el hook se libera al salir. Actualmente los iconos se cargan al refrescar el cajón: la carga diferida y virtualización de grandes colecciones quedan pendientes. No hay mediciones de consumo ni afirmaciones numéricas de rendimiento.

## Validación y estado de las fases

En este entorno Linux se verificó compilación WPF cruzada y se ejecutaron las pruebas del núcleo. **WPF y las API de Windows no pueden ejecutarse aquí**; una compilación correcta no valida la interfaz ni la integración. La publicación Windows se genera mediante cross-compilation; debe probarse en Windows 11 antes de usar accesos importantes.

- Fase 1: código implementado para ventanas, geometría, nombre, expansión, HEX/opacidad y persistencia; comprobación gráfica pendiente. Sin animación, por simplicidad.
- Fase 2: código implementado para importar, iconos del shell, abrir, mover y restaurar. Lógica de archivos probada con fixtures; accesos reales e iconos pendientes de Windows.
- Fase 3: alojamiento Progman/WorkerW, residencia, inicio por usuario y recuperación implementados; comprobación Windows pendiente. Doble clic del fondo pendiente.
- Fase 4: pruebas del núcleo, compilación y publicación; quedan las pruebas manuales Windows, perfiles de rendimiento, firma e instalador.

### Comprobaciones manuales obligatorias en Windows 11

La lista detallada de las ocho verificaciones solicitadas está en [PRUEBAS-WINDOWS.md](docs/PRUEBAS-WINDOWS.md). Las 17 pruebas automáticas cubren el núcleo; no se han ejecutado el Registro ni la bandeja reales en este entorno.

Usa primero accesos descartables y una copia de seguridad:

- Crear varios cajones, mover y redimensionar; contraer, reiniciar y comprobar geometría, colores y visibilidad.
- Probar HEX inválido, opacidad mínima y escalados DPI 100/150/200 %, cambio/desconexión de monitores.
- Importar un `.lnk` y un `.url` reales; comprobar iconos y apertura, traslado entre cajones y restauración con nombres duplicados.
- Probar escritorio público sin permisos, OneDrive local y placeholder, ruta inexistente y error de guardado.
- Restaurar todo, eliminar un cajón y cancelar su confirmación; verificar conservación de los archivos.
- Activar otras aplicaciones y un juego a pantalla completa; probar Alt+Tab, barra de tareas, Win+D, Ctrl+Alt+D y conflicto del atajo.
- Reiniciar Explorer, volver al escritorio y comprobar reaparición e interacción. Si la clase del escritorio cambia, usar el administrador y registrar la versión de Windows antes de ajustar la detección.
- Cerrar y abrir, intentar segunda instancia y probar recuperación de JSON dañado con copia previa de los datos.

## Entorno cloud

Linux permite desarrollar, compilar y probar el núcleo; no puede alojar la interfaz WPF. No hay servicios web ni URL de previsualización.

```bash
bash scripts/setup-cloud.sh
source scripts/activate-cloud.sh
```

El script instala el SDK fijado desde Microsoft con comprobación SHA-512 si falta, conserva TLS, usa caches dentro de `/workspace`, restaura dependencias, compila y ejecuta las pruebas. Cada tarea ya tiene un checkout aislado: usar el existente, sin crear worktrees salvo petición explícita.
