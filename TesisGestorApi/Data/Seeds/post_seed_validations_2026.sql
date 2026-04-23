-- =========================================================
-- Post-Seed Validations (base 2026)
-- Archivo de validaciones para ejecutar DESPUES de seed_base_2026.sql
-- =========================================================

SET search_path TO public;
SET TIME ZONE 'UTC';

-- =========================================================
-- 0) Parametros
-- =========================================================
WITH params AS (
    SELECT '2026-01-01'::date AS anio_lectivo
)
SELECT 'ANIO_LECTIVO_VALIDADO' AS check_name, anio_lectivo
FROM params;

-- =========================================================
-- 1) Conteos generales esperados
-- =========================================================

SELECT 'Anios (esperado 7)' AS check_name, COUNT(*) AS actual
FROM public."Anios";

SELECT 'Divisiones (esperado 3)' AS check_name, COUNT(*) AS actual
FROM public."Divisiones";

SELECT 'TiposAsistencia (esperado 10)' AS check_name, COUNT(*) AS actual
FROM public."TiposAsistencia";

WITH params AS (
    SELECT '2026-01-01'::date AS anio_lectivo
)
SELECT 'Cursos activos 2026 (esperado 21)' AS check_name, COUNT(*) AS actual
FROM public."Cursos" c
CROSS JOIN params p
WHERE c."Estado" = true
  AND c."AñoLectivo"::date = p.anio_lectivo;

WITH params AS (
    SELECT '2026-01-01'::date AS anio_lectivo
)
SELECT 'Estudiantes con cursado activo en cursos 2026 (esperado 630)' AS check_name,
       COUNT(DISTINCT dc."IdEstudiante") AS actual
FROM public."DetallesCursado" dc
JOIN public."Cursos" c ON c."IdCurso" = dc."IdCurso"
CROSS JOIN params p
WHERE dc."Estado" = true
  AND c."Estado" = true
  AND c."AñoLectivo"::date = p.anio_lectivo;

WITH params AS (
    SELECT '2026-01-01'::date AS anio_lectivo
)
SELECT 'DetallesCursado activos en cursos 2026 (esperado 630)' AS check_name,
       COUNT(*) AS actual
FROM public."DetallesCursado" dc
JOIN public."Cursos" c ON c."IdCurso" = dc."IdCurso"
CROSS JOIN params p
WHERE dc."Estado" = true
  AND c."Estado" = true
  AND c."AñoLectivo"::date = p.anio_lectivo;

WITH params AS (
    SELECT '2026-01-01'::date AS anio_lectivo
)
SELECT 'Espacios curriculares en cursos 2026 (esperado 336 = 21x16)' AS check_name,
       COUNT(*) AS actual
FROM public."EspaciosCurriculares" ec
JOIN public."Cursos" c ON c."IdCurso" = ec."IdCurso"
CROSS JOIN params p
WHERE c."Estado" = true
  AND c."AñoLectivo"::date = p.anio_lectivo;

WITH params AS (
    SELECT '2026-01-01'::date AS anio_lectivo
)
SELECT 'Horarios en cursos 2026 (esperado 525 = 21x25)' AS check_name,
       COUNT(*) AS actual
FROM public."Horarios" h
JOIN public."Cursos" c ON c."IdCurso" = h."IdCurso"
CROSS JOIN params p
WHERE c."Estado" = true
  AND c."AñoLectivo"::date = p.anio_lectivo;

SELECT 'Docentes seed (esperado >= 5)' AS check_name,
       COUNT(*) AS actual
FROM public."Docentes" d
WHERE d."Email" LIKE 'docente.seed.%@example.test';

SELECT 'Preceptores seed (esperado 2)' AS check_name,
       COUNT(*) AS actual
FROM public."Preceptores" p
JOIN public."Usuarios" u ON u."IdUsuario" = p."IdUsuario"
WHERE u."Mail" LIKE 'preceptor.seed.%@example.test';

WITH params AS (
    SELECT '2026-01-01'::date AS anio_lectivo
)
SELECT 'TutorEstudiante vinculados a estudiantes activos 2026 (esperado 630)' AS check_name,
       COUNT(*) AS actual
FROM public."TutorEstudiante" te
JOIN public."DetallesCursado" dc ON dc."IdEstudiante" = te."IdEstudiante" AND dc."Estado" = true
JOIN public."Cursos" c ON c."IdCurso" = dc."IdCurso"
CROSS JOIN params p
WHERE c."Estado" = true
  AND c."AñoLectivo"::date = p.anio_lectivo;

-- =========================================================
-- 2) Detalle por curso (debe dar 30 / 16 / 25)
-- =========================================================

WITH params AS (
    SELECT '2026-01-01'::date AS anio_lectivo
)
SELECT
    c."Codigo" AS curso,
    COUNT(dc."IdCursado") FILTER (WHERE dc."Estado" = true) AS estudiantes_activos,
    COUNT(DISTINCT ec."IdEC") AS espacios_curriculares,
    COUNT(DISTINCT h."IdHorario") AS horarios
FROM public."Cursos" c
CROSS JOIN params p
LEFT JOIN public."DetallesCursado" dc ON dc."IdCurso" = c."IdCurso"
LEFT JOIN public."EspaciosCurriculares" ec ON ec."IdCurso" = c."IdCurso"
LEFT JOIN public."Horarios" h ON h."IdCurso" = c."IdCurso"
WHERE c."Estado" = true
  AND c."AñoLectivo"::date = p.anio_lectivo
GROUP BY c."Codigo"
ORDER BY c."Codigo";

-- =========================================================
-- 3) Anomalias (deberian devolver 0 filas)
-- =========================================================

-- 3.1 Cursos activos 2026 con cantidad distinta de 30 alumnos activos
WITH params AS (
    SELECT '2026-01-01'::date AS anio_lectivo
)
SELECT
    c."Codigo" AS curso,
    COUNT(dc."IdCursado") FILTER (WHERE dc."Estado" = true) AS estudiantes_activos
FROM public."Cursos" c
CROSS JOIN params p
LEFT JOIN public."DetallesCursado" dc ON dc."IdCurso" = c."IdCurso"
WHERE c."Estado" = true
  AND c."AñoLectivo"::date = p.anio_lectivo
GROUP BY c."Codigo"
HAVING COUNT(dc."IdCursado") FILTER (WHERE dc."Estado" = true) <> 30
ORDER BY c."Codigo";

-- 3.2 Cursos activos 2026 con cantidad distinta de 16 espacios curriculares
WITH params AS (
    SELECT '2026-01-01'::date AS anio_lectivo
)
SELECT
    c."Codigo" AS curso,
    COUNT(DISTINCT ec."IdEC") AS espacios
FROM public."Cursos" c
CROSS JOIN params p
LEFT JOIN public."EspaciosCurriculares" ec ON ec."IdCurso" = c."IdCurso"
WHERE c."Estado" = true
  AND c."AñoLectivo"::date = p.anio_lectivo
GROUP BY c."Codigo"
HAVING COUNT(DISTINCT ec."IdEC") <> 16
ORDER BY c."Codigo";

-- 3.3 Cursos activos 2026 con cantidad distinta de 25 horarios
WITH params AS (
    SELECT '2026-01-01'::date AS anio_lectivo
)
SELECT
    c."Codigo" AS curso,
    COUNT(DISTINCT h."IdHorario") AS horarios
FROM public."Cursos" c
CROSS JOIN params p
LEFT JOIN public."Horarios" h ON h."IdCurso" = c."IdCurso"
WHERE c."Estado" = true
  AND c."AñoLectivo"::date = p.anio_lectivo
GROUP BY c."Codigo"
HAVING COUNT(DISTINCT h."IdHorario") <> 25
ORDER BY c."Codigo";

-- 3.4 Horarios cuyo IdEC no pertenece al mismo IdCurso del horario
SELECT
    h."IdHorario",
    h."IdCurso" AS idcurso_horario,
    ec."IdCurso" AS idcurso_ec,
    h."IdEC"
FROM public."Horarios" h
JOIN public."EspaciosCurriculares" ec ON ec."IdEC" = h."IdEC"
WHERE h."IdCurso" <> ec."IdCurso";

-- 3.5 Estudiantes con mas de un cursado activo
SELECT
    dc."IdEstudiante",
    COUNT(*) AS cantidad_cursados_activos
FROM public."DetallesCursado" dc
WHERE dc."Estado" = true
GROUP BY dc."IdEstudiante"
HAVING COUNT(*) > 1;

-- 3.6 Estudiantes activos 2026 sin tutor principal
WITH params AS (
    SELECT '2026-01-01'::date AS anio_lectivo
),
estudiantes_activos AS (
    SELECT DISTINCT dc."IdEstudiante"
    FROM public."DetallesCursado" dc
    JOIN public."Cursos" c ON c."IdCurso" = dc."IdCurso"
    CROSS JOIN params p
    WHERE dc."Estado" = true
      AND c."Estado" = true
      AND c."AñoLectivo"::date = p.anio_lectivo
)
SELECT ea."IdEstudiante"
FROM estudiantes_activos ea
LEFT JOIN public."TutorEstudiante" te
    ON te."IdEstudiante" = ea."IdEstudiante"
   AND te."EsPrincipal" = true
WHERE te."IdTutor" IS NULL;

-- 3.7 Estudiantes activos 2026 con mas de un tutor principal
WITH params AS (
    SELECT '2026-01-01'::date AS anio_lectivo
),
estudiantes_activos AS (
    SELECT DISTINCT dc."IdEstudiante"
    FROM public."DetallesCursado" dc
    JOIN public."Cursos" c ON c."IdCurso" = dc."IdCurso"
    CROSS JOIN params p
    WHERE dc."Estado" = true
      AND c."Estado" = true
      AND c."AñoLectivo"::date = p.anio_lectivo
)
SELECT
    te."IdEstudiante",
    COUNT(*) AS cantidad_principales
FROM public."TutorEstudiante" te
JOIN estudiantes_activos ea ON ea."IdEstudiante" = te."IdEstudiante"
WHERE te."EsPrincipal" = true
GROUP BY te."IdEstudiante"
HAVING COUNT(*) > 1;

-- 3.8 Docentes sin usuario vinculado
SELECT d."IdDocente"
FROM public."Docentes" d
LEFT JOIN public."Usuarios" u ON u."IdUsuario" = d."IdUsuario"
WHERE u."IdUsuario" IS NULL;

-- 3.9 Espacios curriculares sin docente / curricula / curso (defensivo)
SELECT ec."IdEC"
FROM public."EspaciosCurriculares" ec
LEFT JOIN public."Docentes" d ON d."IdDocente" = ec."IdDocente"
LEFT JOIN public."Curriculas" cu ON cu."IdCurricula" = ec."IdCurricula"
LEFT JOIN public."Cursos" c ON c."IdCurso" = ec."IdCurso"
WHERE d."IdDocente" IS NULL
   OR cu."IdCurricula" IS NULL
   OR c."IdCurso" IS NULL;

-- 3.10 Codigos de curso faltantes esperados (1A..7C)
WITH esperados AS (
    SELECT format('%s%s-2026', a, d) AS codigo
    FROM generate_series(1, 7) a
    CROSS JOIN (VALUES ('A'), ('B'), ('C')) div(d)
),
actuales AS (
    SELECT c."Codigo" AS codigo
    FROM public."Cursos" c
    WHERE c."Estado" = true
      AND c."AñoLectivo"::date = '2026-01-01'::date
)
SELECT e.codigo AS codigo_faltante
FROM esperados e
LEFT JOIN actuales a ON a.codigo = e.codigo
WHERE a.codigo IS NULL
ORDER BY e.codigo;

-- 3.11 Codigos de tipo asistencia obligatorios faltantes
WITH obligatorios AS (
    SELECT code
    FROM (VALUES ('P'), ('A'), ('LLT'), ('LLTE'), ('LLTC'), ('RA'), ('RE'), ('RAE'), ('ANC'), ('SA')) v(code)
),
actuales AS (
    SELECT t."Codigo" AS code
    FROM public."TiposAsistencia" t
)
SELECT o.code AS tipo_faltante
FROM obligatorios o
LEFT JOIN actuales a ON a.code = o.code
WHERE a.code IS NULL
ORDER BY o.code;

-- 3.12 Combinaciones Nombre+Apellido repetidas en estudiantes activos 2026
WITH params AS (
    SELECT '2026-01-01'::date AS anio_lectivo
)
SELECT
    e."Nombre",
    e."Apellido",
    COUNT(*) AS repeticiones
FROM public."DetallesCursado" dc
JOIN public."Cursos" c ON c."IdCurso" = dc."IdCurso"
JOIN public."Estudiantes" e ON e."IdEstudiante" = dc."IdEstudiante"
CROSS JOIN params p
WHERE dc."Estado" = true
  AND c."Estado" = true
  AND c."AñoLectivo"::date = p.anio_lectivo
GROUP BY e."Nombre", e."Apellido"
HAVING COUNT(*) > 1
ORDER BY repeticiones DESC, e."Apellido", e."Nombre";

-- =========================================================
-- 4) Comprobaciones de columnas/tipos sensibles
-- =========================================================

-- Curriculas.Estado debe ser tipo texto
SELECT
    c.table_name,
    c.column_name,
    c.data_type,
    c.udt_name
FROM information_schema.columns c
WHERE c.table_schema = 'public'
  AND c.table_name = 'Curriculas'
  AND c.column_name = 'Estado';

-- Tutores.Telefono debe ser bigint
SELECT
    c.table_name,
    c.column_name,
    c.data_type,
    c.udt_name
FROM information_schema.columns c
WHERE c.table_schema = 'public'
  AND c.table_name = 'Tutores'
  AND c.column_name = 'Telefono';

-- =========================================================
-- 5) Opcional: tablas de flujo (informativo, no hard-fail)
-- =========================================================
SELECT 'Asistencias'                  AS tabla, COUNT(*) AS registros FROM public."Asistencias"
UNION ALL
SELECT 'ClasesDictadas'               AS tabla, COUNT(*) AS registros FROM public."ClasesDictadas"
UNION ALL
SELECT 'AsistenciasPorEspacio'        AS tabla, COUNT(*) AS registros FROM public."AsistenciasPorEspacio"
UNION ALL
SELECT 'RetirosAnticipados'           AS tabla, COUNT(*) AS registros FROM public."RetirosAnticipados"
UNION ALL
SELECT 'PartesDiarios'                AS tabla, COUNT(*) AS registros FROM public."PartesDiarios"
UNION ALL
SELECT 'ComentariosParte'             AS tabla, COUNT(*) AS registros FROM public."ComentariosParte"
UNION ALL
SELECT 'CredencialesQR'               AS tabla, COUNT(*) AS registros FROM public."CredencialesQR"
UNION ALL
SELECT 'AsistenciaResumenAnual'       AS tabla, COUNT(*) AS registros FROM public."AsistenciaResumenAnual"
UNION ALL
SELECT 'AsistenciaUmbralNotificacion' AS tabla, COUNT(*) AS registros FROM public."AsistenciaUmbralNotificacion";

-- =========================================================
-- 6) Resumen rapido de estado (PASS/FAIL)
-- =========================================================
WITH params AS (
    SELECT '2026-01-01'::date AS anio_lectivo
),
check_rows AS (
    SELECT 'cursos_2026_eq_21' AS check_id,
           (SELECT COUNT(*) FROM public."Cursos" c, params p
             WHERE c."Estado" = true
               AND c."AñoLectivo"::date = p.anio_lectivo) = 21 AS ok
    UNION ALL
    SELECT 'detallecursado_activo_2026_eq_630',
           (SELECT COUNT(*) FROM public."DetallesCursado" dc
             JOIN public."Cursos" c ON c."IdCurso" = dc."IdCurso", params p
             WHERE dc."Estado" = true
               AND c."Estado" = true
               AND c."AñoLectivo"::date = p.anio_lectivo) = 630
    UNION ALL
    SELECT 'estudiantes_distintos_activos_2026_eq_630',
           (SELECT COUNT(DISTINCT dc."IdEstudiante") FROM public."DetallesCursado" dc
             JOIN public."Cursos" c ON c."IdCurso" = dc."IdCurso", params p
             WHERE dc."Estado" = true
               AND c."Estado" = true
               AND c."AñoLectivo"::date = p.anio_lectivo) = 630
    UNION ALL
    SELECT 'espacios_2026_eq_336',
           (SELECT COUNT(*) FROM public."EspaciosCurriculares" ec
             JOIN public."Cursos" c ON c."IdCurso" = ec."IdCurso", params p
             WHERE c."Estado" = true
               AND c."AñoLectivo"::date = p.anio_lectivo) = 336
    UNION ALL
    SELECT 'horarios_2026_eq_525',
           (SELECT COUNT(*) FROM public."Horarios" h
             JOIN public."Cursos" c ON c."IdCurso" = h."IdCurso", params p
             WHERE c."Estado" = true
               AND c."AñoLectivo"::date = p.anio_lectivo) = 525
    UNION ALL
    SELECT 'estudiantes_nombre_apellido_unicos_2026_eq_630',
           (SELECT COUNT(DISTINCT (e."Nombre", e."Apellido")) FROM public."DetallesCursado" dc
             JOIN public."Cursos" c ON c."IdCurso" = dc."IdCurso"
             JOIN public."Estudiantes" e ON e."IdEstudiante" = dc."IdEstudiante", params p
             WHERE dc."Estado" = true
               AND c."Estado" = true
               AND c."AñoLectivo"::date = p.anio_lectivo) = 630
)
SELECT
    check_id,
    CASE WHEN ok THEN 'PASS' ELSE 'FAIL' END AS status
FROM check_rows
ORDER BY check_id;
