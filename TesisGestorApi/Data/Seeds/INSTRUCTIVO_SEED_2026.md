# Instructivo Seed Base 2026

## Objetivo
Este instructivo explica como:

1. Ejecutar el seed base `2026`.
2. Validar que quedo consistente.
3. Interpretar rapidamente los resultados.

Contexto para compartir con otra IA: Es seed base 2026 (sin tablas de flujo como `CredencialesQR`), con UUIDs deterministicos via `seed_uuid(seed text)`.

## Archivos involucrados
- `seed_base_2026.sql`
- `post_seed_validations_2026.sql`

Ruta:
- `/home/lenovo/Desktop/untitled-/tesis-gestion-api/TesisGestorApi/Data/Seeds`

## Prerrequisitos
- Base PostgreSQL accesible.
- Tener levantada una db (se puede crear una nueva de prueba para no ensuciar la actual e ir intercambiando entre las dos)
  - Para eso cambiar la connection string(en appsettings.Development.json) a una nueva db y levantarla
- Migraciones aplicadas.
- `psql` instalado.
- Variable `DATABASE_URL` configurada (o usar parametros `-h/-p/-U/-d`). (creo que no es necesario este paso, probar)

Ejemplo:
```bash
export DATABASE_URL="postgresql://usuario:password@localhost:5432/mi_db"
```

## Paso 1: ejecutar seed base
Desde cualquier carpeta:

```bash
psql "$DATABASE_URL" -v ON_ERROR_STOP=1 -f /home/lenovo/Desktop/untitled-/tesis-gestion-api/TesisGestorApi/Data/Seeds/seed_base_2026.sql
```

Notas:
- El seed es idempotente (puede correrse mas de una vez).
- Usa UUIDs deterministicos para mantener dependencias estables entre tablas.
- No carga tablas de flujo (asistencia, clases dictadas, retiros, QR, etc.) solo las base.

## Paso 2: ejecutar validaciones post-seed
```bash
psql "$DATABASE_URL" -v ON_ERROR_STOP=1 -f /home/lenovo/Desktop/untitled-/tesis-gestion-api/TesisGestorApi/Data/Seeds/post_seed_validations_2026.sql
```

## Paso 3: que revisar en la salida

### 3.1 Conteos esperados
Deberias ver estos valores:
- `Anios`: `7`
- `Divisiones`: `3`
- `TiposAsistencia`: `10`
- `Cursos activos 2026`: `21`
- `Estudiantes activos 2026`: `630`
- `DetallesCursado activos 2026`: `630`
- `Espacios curriculares 2026`: `336` (`21 x 16`)
- `Horarios 2026`: `525` (`21 x 25`)
- `Docentes seed`: `>= 5`
- `Preceptores seed`: `2`
- `TutorEstudiante activos 2026`: `630`
- `Combinaciones Nombre+Apellido unicas en estudiantes activos 2026`: `630`

### 3.2 Detalle por curso
Cada curso (`1A-2026` ... `7C-2026`) debe mostrar:
- `estudiantes_activos = 30`
- `espacios_curriculares = 16`
- `horarios = 25`

### 3.3 Consultas de anomalias
La seccion **“Anomalias (deberian devolver 0 filas)”** debe devolver vacio.
Incluye control de duplicados de `Nombre+Apellido` en estudiantes activos 2026.

### 3.4 Resumen final PASS/FAIL
La seccion final debe mostrar todos los checks en `PASS`.

## Paso 4: prueba funcional minima recomendada
Con seed base correcto, proba:
1. Listar cursos.
2. Listar estudiantes por curso.
3. Obtener ficha de estudiante (incluyendo tutor principal).
4. Registrar asistencia manual/rapida para 1-2 estudiantes.
5. Registrar un retiro anticipado.
6. Más pruebas.

## Troubleshooting rapido (!)
- Si sale Error `relation ... does not exist`:
  - Faltan migraciones o estas en otra base.
- Si sale Error de conexion:
  - Revisar `DATABASE_URL`, usuario, password y puerto.
- Conteos distintos a los esperados:
  - Correr nuevamente seed + validaciones.
  - Si persiste, revisar si hay datos previos ajenos al seed.


## (!) Esta en la primera vesión de este seed, en caso de proponer mejores o arreglos, duplicar archivo, modificar lo necesario y versionar (!)
