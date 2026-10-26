# Cambios

## 0.2.0 — revisión residente

Correcciones sobre el proyecto existente; se mantienen los datos JSON v1 y su ubicación en AppData.

- La X y «Ocultar configuración» ocultan solo el administrador. La bandeja abre configuración, alterna cajones, activa/desactiva el inicio con Windows y permite «Salir completamente».
- Exclusión mutua de una instancia por usuario/sesión, liberación de icono/hook/atajo y guardado al ocultar, salir y terminar sesión.
- Inicio automático por usuario con HKCU Run, desactivado cuando no existe el registro propio. Comando citado con `--background`, sin configuración al iniciar sesión. No se modifican otras entradas de inicio.
- Eliminada la causa del fallo de foco: `EVENT_SYSTEM_FOREGROUND` + `CanShowDrawers()` ocultaban todos los cajones al no reconocer la ventana activa. Ya no se consulta el primer plano para decidir su visibilidad.
- Alojamiento nativo como hijos del host del escritorio que contiene `SHELLDLL_DefView` (Progman/WorkerW del proceso del shell). Las ventanas normales quedan por encima por jerarquía, sin alternar Topmost. Movimiento con captura local de ratón y control de tamaño en la esquina inferior derecha.
- Recuperación por `TaskbarCreated`, destrucción del host y cambio de pantallas. Reintentos solo ante fallo de integración. El administrador informa del error; si no hay host compatible no se crean ventanas flotantes sobre otras aplicaciones.
- Drag/drop de Explorer mediante CF_HDROP (`FileDrop`) en eventos Preview, validación y señal visual. Se rechazan formatos ajenos y operaciones que solo permiten copiar. El traslado real evita mantener una copia de origen cuando termina con éxito; si falla con el origen presente, revierte su entrada y el destino parcial.
- Se conservan iconos del shell, apertura, traslado entre cajones, restauración, nombres, colores, expansión y visibilidad global.

Validación en Linux: compilación WPF cruzada y 12 pruebas de núcleo aprobadas. Las pruebas de residencia modelan la decisión de cierre; las de inicio usan un almacén simulado. No equivalen a pruebas reales de bandeja, Registro, OLE, DPI o Explorer. Ver `docs/PRUEBAS-WINDOWS.md` antes de utilizar accesos importantes.
