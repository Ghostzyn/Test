# Validación pendiente en Windows 11 x64 — versión 0.2.0

No se ha ejecutado una sesión gráfica de Windows en el entorno de desarrollo Linux. La compilación y las pruebas del núcleo no verifican Win32/WPF/OLE. Esta lista requiere una persona o un runner gráfico Windows. No se presentan estos casos como aprobados.

Antes de empezar, cierra la versión anterior y copia `%LOCALAPPDATA%\DesktopDrawers` como respaldo. No borres ni cambies `state.json` o `Shortcuts` para actualizar. Extrae el nuevo ZIP completo en una carpeta estable. Usa accesos descartables para las primeras pruebas.

| Caso | Acción | Resultado esperado |
|---|---|---|
| Migración | Abrir 0.2.0 con los datos anteriores | Mismos cajones, GUID, accesos, nombres, colores, geometría y visibilidad global. Si antes estaban ocultos, usar Mostrar/ocultar. |
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

## Diagnóstico de integración

`SetParent` sobre Progman/WorkerW usa una jerarquía de Explorer no documentada. Se busca `SHELLDLL_DefView` únicamente en el proceso real del shell; no se envía el mensaje privado 0x052C ni se crean WorkerW adicionales. Se utiliza el host existente y se mantienen los cajones como ventanas hijas (WS_CHILD), sin Topmost ni hooks globales de teclado/ratón. El único atajo se registra con `RegisterHotKey`.

Cambios de Explorer, DPI o aplicaciones que reemplacen el escritorio pueden impedir el alojamiento. Hay manifiesto PerMonitorV2, pero la compatibilidad entre procesos y la geometría con DPI mixtos requieren esta validación. Ante fallo el administrador muestra el motivo y se reintenta después de 30 segundos; no se borran los datos ni se fuerza una ventana Always on Top. No se garantiza la recuperación en todas las versiones de Windows hasta completar las pruebas.

Si los cajones no aparecen, comprueba primero el interruptor de visibilidad y el mensaje de integración en configuración. Adjunta versión/build de Windows, pantallas y DPI al informar el fallo. No restablezcas AppData como primer intento.

## Actualización de una versión residente

Desde 0.2.0 usa **Salir completamente** antes de sustituir el EXE. Si está habilitado el inicio automático y cambias la carpeta del ejecutable, desactívalo y actívalo de nuevo desde la nueva ubicación para registrar la ruta correcta. Desactivarlo no elimina cajones ni accesos.
