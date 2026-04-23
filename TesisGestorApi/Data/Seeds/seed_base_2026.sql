BEGIN;
SET search_path TO public;
SET TIME ZONE 'UTC';

-- UUID deterministico para mantener dependencias estables entre tablas.
CREATE OR REPLACE FUNCTION public.seed_uuid(seed text)
RETURNS uuid
LANGUAGE SQL
IMMUTABLE
PARALLEL SAFE
AS $$
    SELECT (
        substr(m, 1, 8) || '-' ||
        substr(m, 9, 4) || '-' ||
        '4' || substr(m, 14, 3) || '-' ||
        'a' || substr(m, 18, 3) || '-' ||
        substr(m, 21, 12)
    )::uuid
    FROM (SELECT md5(seed) AS m) t;
$$;

-- =========================================================
-- 1) CATALOGOS BASE
-- =========================================================

-- Anios (1..7)
WITH anios AS (
    SELECT gs AS numero
    FROM generate_series(1, 7) gs
)
INSERT INTO public."Anios" ("IdAnio", "Numero")
SELECT
    public.seed_uuid('anio|' || a.numero::text),
    a.numero
FROM anios a
ON CONFLICT ("IdAnio") DO UPDATE
SET "Numero" = EXCLUDED."Numero";

-- Divisiones (A/B/C)
WITH divisiones AS (
    SELECT d::char(1) AS nombre
    FROM (VALUES ('A'), ('B'), ('C')) v(d)
)
INSERT INTO public."Divisiones" ("IdDivision", "Nombre")
SELECT
    public.seed_uuid('division|' || d.nombre::text),
    d.nombre
FROM divisiones d
ON CONFLICT ("IdDivision") DO UPDATE
SET "Nombre" = EXCLUDED."Nombre";

-- Tipos de asistencia
WITH tipos AS (
    SELECT *
    FROM (VALUES
        ('P',    'Presente',                    0.00::numeric),
        ('A',    'Ausente',                     1.00::numeric),
        ('LLT',  'Llegada Tarde',               0.25::numeric),
        ('LLTE', 'Llegada Tarde Extendida',     0.50::numeric),
        ('LLTC', 'Llegada Tarde Completa',      1.00::numeric),
        ('RA',   'Retiro Anticipado',           0.50::numeric),
        ('RE',   'Retiro Anticipado Express',   0.00::numeric),
        ('RAE',  'Retiro Anticipado Extendido', 1.00::numeric),
        ('ANC',  'Ausente No Computable',       0.00::numeric),
        ('SA',   'Sin Asistencia',              0.00::numeric)
    ) t(codigo, descripcion, valor_base)
)
INSERT INTO public."TiposAsistencia" ("IdTipo", "Codigo", "Descripcion", "ValorBase")
SELECT
    public.seed_uuid('tipo_asistencia|' || t.codigo),
    t.codigo,
    t.descripcion,
    t.valor_base
FROM tipos t
ON CONFLICT ("IdTipo") DO UPDATE
SET
    "Codigo"      = EXCLUDED."Codigo",
    "Descripcion" = EXCLUDED."Descripcion",
    "ValorBase"   = EXCLUDED."ValorBase";

-- =========================================================
-- 2) SEGURIDAD MINIMA PARA DOCENTES DE PRUEBA
-- =========================================================

WITH docentes_seed AS (
    SELECT *
    FROM (VALUES
        ('docente.seed.01@example.test', 'Docente', 'Uno',   'DOC-0001'),
        ('docente.seed.02@example.test', 'Docente', 'Dos',   'DOC-0002'),
        ('docente.seed.03@example.test', 'Docente', 'Tres',  'DOC-0003'),
        ('docente.seed.04@example.test', 'Docente', 'Cuatro','DOC-0004'),
        ('docente.seed.05@example.test', 'Docente', 'Cinco', 'DOC-0005')
    ) d(mail, nombre, apellido, documento)
)
INSERT INTO public."Usuarios" ("IdUsuario", "Contraseña", "Mail", "Activo", "Verificado", "FechaCreacion")
SELECT
    public.seed_uuid('usuario|' || d.mail),
    '123456',
    d.mail,
    true,
    true,
    '2026-01-01 00:00:00+00'::timestamptz
FROM docentes_seed d
ON CONFLICT ("IdUsuario") DO UPDATE
SET
    "Mail"        = EXCLUDED."Mail",
    "Activo"      = EXCLUDED."Activo",
    "Verificado"  = EXCLUDED."Verificado",
    "FechaCreacion" = EXCLUDED."FechaCreacion";

WITH preceptores_seed AS (
    SELECT *
    FROM (VALUES
        ('preceptor.seed.01@example.test', 'Preceptor', 'Uno', 'PREC-0001', '1A-2026'),
        ('preceptor.seed.02@example.test', 'Preceptor', 'Dos', 'PREC-0002', '1B-2026')
    ) p(mail, nombre, apellido, documento, curso_codigo)
)
INSERT INTO public."Usuarios" ("IdUsuario", "Contraseña", "Mail", "Activo", "Verificado", "FechaCreacion")
SELECT
    public.seed_uuid('usuario|' || p.mail),
    '123456',
    p.mail,
    true,
    true,
    '2026-01-01 00:00:00+00'::timestamptz
FROM preceptores_seed p
ON CONFLICT ("IdUsuario") DO UPDATE
SET
    "Mail"        = EXCLUDED."Mail",
    "Activo"      = EXCLUDED."Activo",
    "Verificado"  = EXCLUDED."Verificado",
    "FechaCreacion" = EXCLUDED."FechaCreacion";

WITH docentes_seed AS (
    SELECT *
    FROM (VALUES
        ('docente.seed.01@example.test', 'Docente', 'Uno',   'DOC-0001'),
        ('docente.seed.02@example.test', 'Docente', 'Dos',   'DOC-0002'),
        ('docente.seed.03@example.test', 'Docente', 'Tres',  'DOC-0003'),
        ('docente.seed.04@example.test', 'Docente', 'Cuatro','DOC-0004'),
        ('docente.seed.05@example.test', 'Docente', 'Cinco', 'DOC-0005')
    ) d(mail, nombre, apellido, documento)
)
INSERT INTO public."Docentes" ("IdDocente", "IdUsuario", "Documento", "Nombre", "Apellido", "Email")
SELECT
    public.seed_uuid('docente|' || d.mail),
    public.seed_uuid('usuario|' || d.mail),
    d.documento,
    d.nombre,
    d.apellido,
    d.mail
FROM docentes_seed d
ON CONFLICT ("IdDocente") DO UPDATE
SET
    "IdUsuario" = EXCLUDED."IdUsuario",
    "Documento" = EXCLUDED."Documento",
    "Nombre"    = EXCLUDED."Nombre",
    "Apellido"  = EXCLUDED."Apellido",
    "Email"     = EXCLUDED."Email";

-- =========================================================
-- 3) CURRICULAS BASE (Estado es TEXT)
-- =========================================================

WITH curriculas AS (
    SELECT *
    FROM (VALUES
        ('ARTVI', 'Artes Visuales'),
        ('GEOGR', 'Geografia'),
        ('INGLS', 'Ingles'),
        ('LENLI', 'Lengua y Literatura'),
        ('FMHCR', 'Formacion Humana y Cristiana'),
        ('EDTEC', 'Educacion Tecnologica'),
        ('FISIC', 'Fisica'),
        ('MATEM', 'Matematica'),
        ('EDFIS', 'Educacion Fisica'),
        ('COMPU', 'Computacion'),
        ('BIOLG', 'Biologia'),
        ('CIDPA', 'Ciudadania y Participacion'),
        ('HISTO', 'Historia'),
        ('QUIMC', 'Quimica'),
        ('MUSIC', 'Musica'),
        ('DCSIG', 'Doctrina Social de la Iglesia'),
        ('FILOS', 'Filosofia'),
        ('SOCIO', 'Sociologia'),
        ('ECONM', 'Economia'),
        ('DERCH', 'Derecho')
    ) c(codigo, nombre)
)
INSERT INTO public."Curriculas" ("IdCurricula", "Nombre", "Descripcion", "Codigo", "EsContraturno", "Estado")
SELECT
    public.seed_uuid('curricula|' || c.codigo),
    c.nombre,
    'Pendiente de completar',
    c.codigo,
    false,
    'ACTIVA'
FROM curriculas c
ON CONFLICT ("IdCurricula") DO UPDATE
SET
    "Nombre"        = EXCLUDED."Nombre",
    "Descripcion"   = EXCLUDED."Descripcion",
    "Codigo"        = EXCLUDED."Codigo",
    "EsContraturno" = EXCLUDED."EsContraturno",
    "Estado"        = EXCLUDED."Estado";

-- =========================================================
-- 4) CURSOS (21 = 7 anios x 3 divisiones)
-- =========================================================

WITH anios AS (
    SELECT gs AS numero
    FROM generate_series(1, 7) gs
),
divisiones AS (
    SELECT d::char(1) AS nombre
    FROM (VALUES ('A'), ('B'), ('C')) v(d)
)
INSERT INTO public."Cursos" ("IdCurso", "IdAnio", "IdDivision", "Codigo", "Estado", "AñoLectivo")
SELECT
    public.seed_uuid(format('curso|%s%s|2026', a.numero, d.nombre)),
    public.seed_uuid('anio|' || a.numero::text),
    public.seed_uuid('division|' || d.nombre::text),
    format('%s%s-2026', a.numero, d.nombre),
    true,
    '2026-01-01 00:00:00+00'::timestamptz
FROM anios a
CROSS JOIN divisiones d
ORDER BY a.numero, d.nombre
ON CONFLICT ("IdCurso") DO UPDATE
SET
    "IdAnio"      = EXCLUDED."IdAnio",
    "IdDivision"  = EXCLUDED."IdDivision",
    "Codigo"      = EXCLUDED."Codigo",
    "Estado"      = EXCLUDED."Estado",
    "AñoLectivo"  = EXCLUDED."AñoLectivo";

-- Preceptores seed (2) con su usuario.
WITH preceptores_seed AS (
    SELECT *
    FROM (VALUES
        ('preceptor.seed.01@example.test', 'Preceptor', 'Uno', 'PREC-0001', '1A-2026'),
        ('preceptor.seed.02@example.test', 'Preceptor', 'Dos', 'PREC-0002', '1B-2026')
    ) p(mail, nombre, apellido, documento, curso_codigo)
)
INSERT INTO public."Preceptores" ("IdPreceptor", "Nombre", "Apellido", "Documento", "IdCurso", "CursoIdCurso", "IdUsuario")
SELECT
    public.seed_uuid('preceptor|' || p.mail),
    p.nombre,
    p.apellido,
    p.documento,
    c."IdCurso",
    c."IdCurso",
    public.seed_uuid('usuario|' || p.mail)
FROM preceptores_seed p
INNER JOIN public."Cursos" c
    ON c."Codigo" = p.curso_codigo
ON CONFLICT ("IdPreceptor") DO UPDATE
SET
    "Nombre"      = EXCLUDED."Nombre",
    "Apellido"    = EXCLUDED."Apellido",
    "Documento"   = EXCLUDED."Documento",
    "IdCurso"     = EXCLUDED."IdCurso",
    "CursoIdCurso" = EXCLUDED."CursoIdCurso",
    "IdUsuario"   = EXCLUDED."IdUsuario";

-- =========================================================
-- 5) ESPACIOS CURRICULARES (docente asignado)
-- =========================================================

WITH materias_plan AS (
    SELECT *
    FROM (VALUES
        (1,  'MATEM'),
        (2,  'LENLI'),
        (3,  'HISTO'),
        (4,  'GEOGR'),
        (5,  'BIOLG'),
        (6,  'FISIC'),
        (7,  'QUIMC'),
        (8,  'INGLS'),
        (9,  'COMPU'),
        (10, 'EDTEC'),
        (11, 'CIDPA'),
        (12, 'EDFIS'),
        (13, 'FMHCR'),
        (14, 'ARTVI'),
        (15, 'MUSIC'),
        (16, 'SOCIO')
    ) m(ord, codigo)
),
cursos AS (
    SELECT
        c."IdCurso",
        c."Codigo",
        row_number() OVER (ORDER BY c."Codigo") AS curso_pos
    FROM public."Cursos" c
    WHERE c."Estado" = true
      AND c."AñoLectivo"::date = '2026-01-01'::date
),
doc_pool AS (
    SELECT
        array_agg(d."IdDocente" ORDER BY d."Email") AS ids,
        count(*)::int AS n
    FROM public."Docentes" d
    WHERE d."Email" LIKE 'docente.seed.%@example.test'
)
INSERT INTO public."EspaciosCurriculares" ("IdEC", "IdCurso", "IdCurricula", "IdDocente")
SELECT
    public.seed_uuid('ec|' || c."Codigo" || '|' || m.codigo),
    c."IdCurso",
    public.seed_uuid('curricula|' || m.codigo),
    dp.ids[((c.curso_pos + m.ord - 2) % dp.n) + 1]
FROM cursos c
CROSS JOIN materias_plan m
CROSS JOIN doc_pool dp
ON CONFLICT ("IdEC") DO UPDATE
SET
    "IdCurso"     = EXCLUDED."IdCurso",
    "IdCurricula" = EXCLUDED."IdCurricula",
    "IdDocente"   = EXCLUDED."IdDocente";

-- =========================================================
-- 6) HORARIOS POR CURSO (grilla particular por curso)
-- =========================================================

WITH materias_plan AS (
    SELECT *
    FROM (VALUES
        (1,  'MATEM'),
        (2,  'LENLI'),
        (3,  'HISTO'),
        (4,  'GEOGR'),
        (5,  'BIOLG'),
        (6,  'FISIC'),
        (7,  'QUIMC'),
        (8,  'INGLS'),
        (9,  'COMPU'),
        (10, 'EDTEC'),
        (11, 'CIDPA'),
        (12, 'EDFIS'),
        (13, 'FMHCR'),
        (14, 'ARTVI'),
        (15, 'MUSIC'),
        (16, 'SOCIO')
    ) m(ord, codigo)
),
ec_map AS (
    SELECT
        ec."IdCurso",
        mp.ord,
        ec."IdEC"
    FROM public."EspaciosCurriculares" ec
    INNER JOIN public."Curriculas" cu
        ON cu."IdCurricula" = ec."IdCurricula"
    INNER JOIN materias_plan mp
        ON mp.codigo = cu."Codigo"
),
cursos AS (
    SELECT
        c."IdCurso",
        c."Codigo",
        row_number() OVER (ORDER BY c."Codigo") AS curso_pos,
        regexp_replace(split_part(c."Codigo", '-', 1), '[^0-9]', '', 'g')::int AS anio_num,
        right(split_part(c."Codigo", '-', 1), 1) AS division
    FROM public."Cursos" c
    WHERE c."Estado" = true
      AND c."AñoLectivo"::date = '2026-01-01'::date
),
slots AS (
    SELECT *
    FROM (VALUES
        (1, 1, '07:30:00'::interval, '08:40:00'::interval),
        (1, 2, '08:50:00'::interval, '10:00:00'::interval),
        (1, 3, '10:10:00'::interval, '11:20:00'::interval),
        (1, 4, '11:30:00'::interval, '12:40:00'::interval),
        (1, 5, '13:30:00'::interval, '14:40:00'::interval),

        (2, 1, '07:30:00'::interval, '08:40:00'::interval),
        (2, 2, '08:50:00'::interval, '10:00:00'::interval),
        (2, 3, '10:10:00'::interval, '11:20:00'::interval),
        (2, 4, '11:30:00'::interval, '12:40:00'::interval),
        (2, 5, '13:30:00'::interval, '14:40:00'::interval),

        (3, 1, '07:30:00'::interval, '08:40:00'::interval),
        (3, 2, '08:50:00'::interval, '10:00:00'::interval),
        (3, 3, '10:10:00'::interval, '11:20:00'::interval),
        (3, 4, '11:30:00'::interval, '12:40:00'::interval),
        (3, 5, '13:30:00'::interval, '14:40:00'::interval),

        (4, 1, '07:30:00'::interval, '08:40:00'::interval),
        (4, 2, '08:50:00'::interval, '10:00:00'::interval),
        (4, 3, '10:10:00'::interval, '11:20:00'::interval),
        (4, 4, '11:30:00'::interval, '12:40:00'::interval),
        (4, 5, '13:30:00'::interval, '14:40:00'::interval),

        (5, 1, '07:30:00'::interval, '08:40:00'::interval),
        (5, 2, '08:50:00'::interval, '10:00:00'::interval),
        (5, 3, '10:10:00'::interval, '11:20:00'::interval),
        (5, 4, '11:30:00'::interval, '12:40:00'::interval),
        (5, 5, '13:30:00'::interval, '14:40:00'::interval)
    ) s(dia, slot, base_entrada, base_salida)
)
INSERT INTO public."Horarios" ("IdHorario", "DíaSemana", "HorarioEntrada", "HorarioSalida", "IdCurso", "IdEC")
SELECT
    public.seed_uuid('horario|' || c."Codigo" || '|d' || s.dia::text || '|s' || s.slot::text),
    s.dia,
    s.base_entrada
        + make_interval(mins => (
            CASE c.division WHEN 'A' THEN 0 WHEN 'B' THEN 5 ELSE 10 END
            + ((c.anio_num - 1) % 3) * 2
        )),
    s.base_salida
        + make_interval(mins => (
            CASE c.division WHEN 'A' THEN 0 WHEN 'B' THEN 5 ELSE 10 END
            + ((c.anio_num - 1) % 3) * 2
        )),
    c."IdCurso",
    em."IdEC"
FROM cursos c
CROSS JOIN slots s
INNER JOIN ec_map em
    ON em."IdCurso" = c."IdCurso"
   AND em.ord = (((s.dia - 1) * 5 + s.slot + c.curso_pos - 2) % 16) + 1
ON CONFLICT ("IdHorario") DO UPDATE
SET
    "DíaSemana"     = EXCLUDED."DíaSemana",
    "HorarioEntrada" = EXCLUDED."HorarioEntrada",
    "HorarioSalida"  = EXCLUDED."HorarioSalida",
    "IdCurso"        = EXCLUDED."IdCurso",
    "IdEC"           = EXCLUDED."IdEC";

-- =========================================================
-- 7) ESTUDIANTES (30 por curso = 630)
-- =========================================================

WITH cursos AS (
    SELECT
        c."IdCurso",
        c."Codigo",
        row_number() OVER (ORDER BY c."Codigo") AS curso_pos
    FROM public."Cursos" c
    WHERE c."Estado" = true
      AND c."AñoLectivo"::date = '2026-01-01'::date
),
gen AS (
    SELECT
        c."IdCurso",
        c."Codigo",
        c.curso_pos,
        n AS nro,
        row_number() OVER (ORDER BY c."Codigo", n) AS global_idx
    FROM cursos c
    CROSS JOIN generate_series(1, 30) n
),
cat AS (
    SELECT
        ARRAY[
            'Lucas','Sofia','Mateo','Emma','Thiago','Olivia','Benjamin','Mia','Valentino','Isabella',
            'Joaquin','Camila','Tomas','Martina','Felipe','Valentina','Bruno','Julieta','Ignacio','Renata',
            'Agustin','Carla','Franco','Pilar','Santiago','Lucia','Nicolas','Ambar','Ramiro','Elena'
        ]::text[] AS nombres,
        ARRAY[
            'Gomez','Fernandez','Lopez','Martinez','Sanchez','Perez','Rodriguez','Diaz','Garcia','Ruiz',
            'Torres','Flores','Romero','Alvarez','Castro','Molina','Herrera','Acosta','Medina','Suarez',
            'Rios','Benitez','Navarro','Moreno','Arias','Vega','Silva','Cabrera','Ibarra','Paz'
        ]::text[] AS apellidos
)
INSERT INTO public."Estudiantes"
("IdEstudiante", "Nombre", "Apellido", "Documento", "FechaNacimiento", "Domicilio", "Sexo", "TeaGeneral")
SELECT
    public.seed_uuid('estudiante|' || g."Codigo" || '|' || lpad(g.nro::text, 2, '0')),
    cat.nombres[(
        ((((g.global_idx - 1)::int * 47)
          % (array_length(cat.nombres, 1) * array_length(cat.apellidos, 1)))
         % array_length(cat.nombres, 1)) + 1
    )],
    cat.apellidos[(
        ((((g.global_idx - 1)::int * 47)
          % (array_length(cat.nombres, 1) * array_length(cat.apellidos, 1)))
         / array_length(cat.nombres, 1)) + 1
    )],
    (50000000 + g.global_idx)::text,
    '2008-01-01 00:00:00+00'::timestamptz
        + make_interval(days => (((g.global_idx * 13) % 2555)::int)),
    'Calle ' || (100 + (g.global_idx % 900))::text || ' N ' || (1000 + ((g.global_idx * 17) % 9000))::text,
    ((g.global_idx % 3) + 1),
    false
FROM gen g
CROSS JOIN cat
ON CONFLICT ("IdEstudiante") DO UPDATE
SET
    "Nombre"         = EXCLUDED."Nombre",
    "Apellido"       = EXCLUDED."Apellido",
    "Documento"      = EXCLUDED."Documento",
    "FechaNacimiento" = EXCLUDED."FechaNacimiento",
    "Domicilio"      = EXCLUDED."Domicilio",
    "Sexo"           = EXCLUDED."Sexo",
    "TeaGeneral"     = EXCLUDED."TeaGeneral";

-- =========================================================
-- 8) DETALLES CURSADO (1 activo por estudiante)
-- =========================================================

WITH cursos AS (
    SELECT
        c."IdCurso",
        c."Codigo"
    FROM public."Cursos" c
    WHERE c."Estado" = true
      AND c."AñoLectivo"::date = '2026-01-01'::date
),
gen AS (
    SELECT
        c."IdCurso",
        c."Codigo",
        n AS nro
    FROM cursos c
    CROSS JOIN generate_series(1, 30) n
)
INSERT INTO public."DetallesCursado" ("IdCursado", "Estado", "IdEstudiante", "IdCurso")
SELECT
    public.seed_uuid('cursado|' || g."Codigo" || '|' || lpad(g.nro::text, 2, '0')),
    true,
    public.seed_uuid('estudiante|' || g."Codigo" || '|' || lpad(g.nro::text, 2, '0')),
    g."IdCurso"
FROM gen g
ON CONFLICT ("IdCursado") DO UPDATE
SET
    "Estado"       = EXCLUDED."Estado",
    "IdEstudiante" = EXCLUDED."IdEstudiante",
    "IdCurso"      = EXCLUDED."IdCurso";

-- =========================================================
-- 9) TUTORES + VINCULO TUTOR/ESTUDIANTE
-- =========================================================

WITH cursos AS (
    SELECT
        c."Codigo"
    FROM public."Cursos" c
    WHERE c."Estado" = true
      AND c."AñoLectivo"::date = '2026-01-01'::date
),
base AS (
    SELECT
        c."Codigo",
        n AS nro,
        row_number() OVER (ORDER BY c."Codigo", n) AS global_idx
    FROM cursos c
    CROSS JOIN generate_series(1, 30) n
),
cat AS (
    SELECT
        ARRAY[
            'Ana','Carlos','Mariana','Diego','Laura','Jorge','Paula','Martin','Gabriela','Sergio',
            'Silvia','Fernando','Patricia','Ricardo','Adriana','Miguel','Claudia','Ruben','Veronica','Hector'
        ]::text[] AS nombres,
        ARRAY[
            'Gomez','Fernandez','Lopez','Martinez','Sanchez','Perez','Rodriguez','Diaz','Garcia','Ruiz',
            'Torres','Flores','Romero','Alvarez','Castro','Molina','Herrera','Acosta','Medina','Suarez'
        ]::text[] AS apellidos
)
INSERT INTO public."Tutores"
("IdTutor", "Nombre", "Apellido", "Documento", "Telefono", "Correo", "RelacionEstudiante",
 "FechaNacimiento", "Disponibilidad", "Domicilio", "FechaUltimaActualizacion", "FechaUltimaNotificacion")
SELECT
    public.seed_uuid('tutor|' || b."Codigo" || '|' || lpad(b.nro::text, 2, '0')),
    cat.nombres[((b.global_idx * 5 - 1) % array_length(cat.nombres, 1)) + 1],
    cat.apellidos[((b.global_idx * 9 - 1) % array_length(cat.apellidos, 1)) + 1],
    (20000000 + b.global_idx)::text,
    (3000000000 + b.global_idx)::bigint,
    'tutor.' || lpad(b.global_idx::text, 4, '0') || '@example.test',
    CASE (b.global_idx % 3)
        WHEN 0 THEN 'Madre'
        WHEN 1 THEN 'Padre'
        ELSE 'Tutor Legal'
    END,
    '1970-01-01 00:00:00+00'::timestamptz
        + make_interval(days => (((b.global_idx * 29) % 10000)::int)),
    '08:00-18:00',
    'Domicilio tutor ' || b.global_idx::text,
    '2026-02-01 00:00:00+00'::timestamptz,
    NULL
FROM base b
CROSS JOIN cat
ON CONFLICT ("IdTutor") DO UPDATE
SET
    "Nombre"                  = EXCLUDED."Nombre",
    "Apellido"                = EXCLUDED."Apellido",
    "Documento"               = EXCLUDED."Documento",
    "Telefono"                = EXCLUDED."Telefono",
    "Correo"                  = EXCLUDED."Correo",
    "RelacionEstudiante"      = EXCLUDED."RelacionEstudiante",
    "FechaNacimiento"         = EXCLUDED."FechaNacimiento",
    "Disponibilidad"          = EXCLUDED."Disponibilidad",
    "Domicilio"               = EXCLUDED."Domicilio",
    "FechaUltimaActualizacion" = EXCLUDED."FechaUltimaActualizacion";

WITH cursos AS (
    SELECT
        c."Codigo"
    FROM public."Cursos" c
    WHERE c."Estado" = true
      AND c."AñoLectivo"::date = '2026-01-01'::date
),
base AS (
    SELECT
        c."Codigo",
        n AS nro
    FROM cursos c
    CROSS JOIN generate_series(1, 30) n
)
INSERT INTO public."TutorEstudiante" ("IdTutor", "IdEstudiante", "EsPrincipal")
SELECT
    public.seed_uuid('tutor|' || b."Codigo" || '|' || lpad(b.nro::text, 2, '0')),
    public.seed_uuid('estudiante|' || b."Codigo" || '|' || lpad(b.nro::text, 2, '0')),
    true
FROM base b
ON CONFLICT ("IdTutor", "IdEstudiante") DO UPDATE
SET
    "EsPrincipal" = EXCLUDED."EsPrincipal";

COMMIT;
