# Validación pendiente en Windows 11 x64 — versión 0.2.1

No se ha ejecutado una sesión gráfica de Windows en el entorno de desarrollo Linux. La compilación y las pruebas del núcleo no verifican Win32/WPF/OLE. Esta lista requiere una persona o un runner gráfico Windows. No se presentan estos casos como aprobados.

Antes de empezar, cierra la versión anterior y copia `%LOCALAPPDATA%\DesktopDrawers` como respaldo. No borres ni cambies `state.json` o `Shortcuts` para actualizar. Extrae el nuevo ZIP completo en una carpeta estable. Usa accesos descartables para las primeras pruebas.

| Caso | Acción | Resultado esperado |
|---|---|---|
| Migración | Abrir 0.2.1 con los datos anteriores | Mismos cajones, GUID, accesos, nombres, colores, geometría y visibilidad global. Si antes estaban ocultos, usar Mostrar/ocultar. |
| Residencia | Crear un cajón y cerrar la configuración con X | El proceso, el icono de bandeja y el cajón siguen activos. La bandeja puede estar en los iconos ocultos de Windows. |
| Reabrir | Doble clic en la bandeja o Abrir configuración | Vuelve el administrador sin duplicar cajones. |
| Salida real | Bandeja → Salir completamente | Desaparecen bandeja, proceso y cajones; los accesos permanecen administrados, no se borran. Al reabrir, se recuperan. |
| Instancia única | Ejecutar otra vez el EXE mientras está residente | Mensaje de instancia existente; no aparece una segunda bandeja ni se cambia el JSON. |
| Inicio predeterminado | En un usuario sin configuración previa, abrir configuración | Inicio automático desactivado; no se crea entrada Run hasta activarlo. |
| Activar inicio | Marcar «Iniciar Desktop Drawers con Windows» | Aparece solo `HKCU\Software\Microsoft\Windows\CurrentVersion\Run\DesktopDrawers`, con EXE citado y `--background`. Reabrir configuración conserva el estado. |
| Sesión Windows | Con inicio activado, cerrar sesión y entrar | Bandeja y cajones guardados aparecen sin ventana de configuración. Si la visibilidad global estaba apagada, siguen ocultos. |
| Desactivar inicio | Desmarcar y volver a iniciar sesión | Se elimina únicamente el valor DesktopDrawers; no arranca la aplicación automáticamente. |
| Foco de escritorio | Seleccionar una vez NVIDIA App y otros iconos; arrastrarlos sin soltarlos | Los cajones no se ocultan por el foco y siguen disponibles como destino. |
| Orden de ventanas | Abrir aplicación normal, superponerla parcialmente al cajón; después maximizar y probar juego a pantalla completa | Ningún cajón se superpone a aplicaciones. Al volver al escritorio sigue presente. Probar Win+D, Alt+Tab y barra de tareas. |
| Importar .lnk real | Arrastrar un .lnk desde Explorer/escritorio del usuario | Borde azul y aviso al pasar; al soltar aparece icono/nombre y desaparece el .lnk de origen. Comprobar que el EXE del programa no cambió. |
| Importar .url real | Repetir con .url, incluso extensión .URL | Se traslada sin duplicados y el doble clic abre su destino. |
| Formato incorrecto | Arrastrar EXE, carpeta, archivo inexistente o elemento virtual de shell sin CF_HDROP | No se acepta ni se mueve; solo se admiten .lnk/.url reales del escritorio. |
| Permisos | Arrastrar acceso del escritorio público no modificable por el usuario | Error visible, original intacto; no queda una entrada nueva ni copia parcial si la limpieza es posible. No hay elevación UAC. |
| OneDrive | Probar enlace local y placeholder Offline/ReparsePoint | Local normal se admite; placeholder/enlace de sistema se rechaza intacto. No forzar descargas ni modificar destinos. |
| Mover/restaurar | Mover entre cajones y restaurar con un nombre ya existente en el escritorio | Se conserva icono/nombre; la restauración crea un nombre libre y no sobrescribe. |
| Explorer reiniciado | Reiniciar «Explorador de Windows» desde Administrador de tareas | La bandeja se recupera; los cajones se recrean tras la señal de Explorer, manteniendo datos. Si falla, abrir configuración y anotar su diagnóstico. |
| DPI/monitores | 100/150/200 %, monitor secundario, mover, redimensionar por esquina, desconectar monitor | Posición y tamaño utilizables, títulos accesibles y drop en el lugar visible. Validar especialmente DPI mixtos y modo de fondos/pantallas de Windows 11. |
| Guardado | Mover, redimensionar, contraer, cambiar colores; cerrar administrador y salir desde bandeja | Se conserva todo al volver a abrir. |
| Reposo | Dejar administrador oculto sin interacción | Sin sondeo del foco ni actualizaciones periódicas de UI. Medir en Administrador de tareas antes de atribuir cifras de CPU/memoria. |

## Verificaciones de la regresión de cajones invisibles

1. Con una copia del AppData de 0.2.0 que contenga «Test» y «Nuevo cajón», iniciar 0.2.1: el selector debe estar en Compatibilidad. Comprobar ambos cajones y su contenido sin crear sustitutos. Si la visibilidad global estaba apagada, pulsar Recuperar cajones.
2. En compatibilidad, mover, redimensionar, personalizar, contraer y restaurar; seleccionar un icono ajeno y arrastrar .lnk/.url desde Explorer. Comprobar indicación azul, traslado sin duplicado y restauración. Repetir doble clic/apertura y traslado entre cajones.
3. Superponer ventanas normales y juego a pantalla completa; los cajones no deben permanecer encima. Probar también Win+D y volver al escritorio: anotar diferencias de comportamiento del modo de ventanas independientes. No interpretar el modo de compatibilidad como una garantía de integración perfecta con Explorer.
4. Cambiar explícitamente a Integrado. Si se detecta padre oculto, recorte, tamaño/capas/estilos inválidos, debe regresar una sola vez a Compatibilidad y conservarla tras reiniciar. Si se ve el texto de comprobaciones nativas pero no los cajones, pulsar Diagnóstico y Recuperar cajones; no reinicializar datos.
5. Con integración visible, seleccionar iconos y hacer drop, cambiar fondo, reiniciar Explorer, probar DPI mixtos y desconectar pantallas. Comprobar que el título permanece alcanzable y que no se confunden coordenadas de pantalla/cliente. La recuperación automática no debe quedar en un bucle.
6. Reiniciar después de cambiar el modo y verificar persistencia. Probar ocultación global intencionada, cerrar configuración hacia bandeja, inicio Windows y salida completa.
7. Pulsar Diagnóstico en ambos modos. Comprobar que `desktop-diagnostics.jsonl` muestra HWND/padre/clase, estilos, rectángulos, DPI, Visibility/Opacity y capas. Confirmar rotación y mensaje de error si no se puede escribir. Comparar rectángulo real con la pantalla y revisar entradas anteriores al fallback.

Todos estos casos permanecen **pendientes de ejecución en Windows 11**. En Linux se prueban únicamente carga/persistencia, política de fallback y evaluación de snapshots simulados, además de la lógica de archivos.

## Diagnóstico de integración

El modo Integrado usa una superficie `SHELLDLL_DefView` de Explorer y sus ancestros Progman/WorkerW, una jerarquía no documentada. Se utiliza el shell real, sin mensajes privados 0x052C ni creación de nuevos WorkerW. Se comprueban estructura, dimensiones, recorte y orden nativo; no se puede demostrar composición real con `IsWindowVisible`.

Ante fallo se registran detalles y se recrean como ventanas independientes en Compatibilidad, conservando datos. La selección no vuelve a Integrado sin intervención explícita. No hay temporizador de reintentos indefinidos. El manifiesto PerMonitorV2 no sustituye la validación con monitores reales. Ver [REGRESION-VISIBILIDAD.md](REGRESION-VISIBILIDAD.md).

Si los cajones no aparecen, pulsa **Recuperar cajones** en configuración: activa Compatibilidad y visibilidad global. Usa **Diagnóstico…** antes de cambiar de modo si quieres capturar el fallo. Adjunta versión/build de Windows, pantallas y DPI al informar el fallo. No restablezcas AppData como primer intento.

## Actualización de una versión residente

Desde 0.2.0 usa **Salir completamente** antes de sustituir el EXE. Si está habilitado el inicio automático y cambias la carpeta del ejecutable, desactívalo y actívalo de nuevo desde la nueva ubicación para registrar la ruta correcta. Desactivarlo no elimina cajones ni accesos.
