-- Backfill rerunnable para FotoEstudiante.
-- Ejecutar después de aplicar la migración AddFotoEstudiante.
-- Puede ejecutarse también después de seed_base_2026.sql para completar alumnos nuevos.
-- Se puede ejecutar las veces que se quiera

WITH imagenes AS (
    SELECT ARRAY[
        '/estudiantes/estudiante_amarillo.png',
        '/estudiantes/estudiante_azul.png',
        '/estudiantes/estudiante_gris.png',
        '/estudiantes/estudiante_marron.png',
        '/estudiantes/estudiante_naranja.png',
        '/estudiantes/estudiante_negro.png',
        '/estudiantes/estudiante_rosa.png',
        '/estudiantes/estudiante_rojo.png',
        '/estudiantes/estudiante_verde.png',
        '/estudiantes/estudiante_violeta.png'
    ]::text[] AS rutas
),
estudiantes_ordenados AS (
    SELECT
        e."IdEstudiante",
        row_number() OVER (
            ORDER BY e."Apellido", e."Nombre", e."Documento", e."IdEstudiante"
        ) AS rn
    FROM public."Estudiantes" e
    WHERE e."FotoEstudiante" IS NULL
)
UPDATE public."Estudiantes" e
SET "FotoEstudiante" = i.rutas[((eo.rn - 1) % array_length(i.rutas, 1)) + 1]
FROM estudiantes_ordenados eo
CROSS JOIN imagenes i
WHERE e."IdEstudiante" = eo."IdEstudiante";
