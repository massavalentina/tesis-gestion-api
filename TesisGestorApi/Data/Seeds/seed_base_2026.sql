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
-- 2) ROLES
-- =========================================================

INSERT INTO public."Roles" ("IdRol", "Nombre")
VALUES
    ('11111111-1111-1111-1111-111111111111', 'Admin'),
    ('22222222-2222-2222-2222-222222222222', 'Docente'),
    ('33333333-3333-3333-3333-333333333333', 'Preceptor'),
    ('44444444-4444-4444-4444-444444444444', 'Equipo Directivo'),
    ('55555555-5555-5555-5555-555555555555', 'Secretario')
ON CONFLICT ("IdRol") DO UPDATE
SET "Nombre" = EXCLUDED."Nombre";

-- =========================================================
-- 3) CURRICULAS (20 existentes + 41 nuevas = 61 total)
-- =========================================================

WITH curriculas AS (
    SELECT *
    FROM (VALUES
        -- Existentes
        ('ARTVI',  'Artes Visuales'),
        ('GEOGR',  'Geografía'),
        ('INGLS',  'Inglés'),
        ('LENLI',  'Lengua y Literatura'),
        ('FMHCR',  'Formación Humana y Cristiana'),
        ('EDTEC',  'Educación Tecnológica'),
        ('FISIC',  'Física'),
        ('MATEM',  'Matemática'),
        ('EDFIS',  'Educación Física'),
        ('COMPU',  'Computación'),
        ('BIOLG',  'Biología'),
        ('CIDPA',  'Ciudadanía y Participación'),
        ('HISTO',  'Historia'),
        ('QUIMC',  'Química'),
        ('MUSIC',  'Música'),
        ('DCSIG',  'Doctrina Social de la Iglesia'),
        ('FILOS',  'Filosofía'),
        ('SOCIO',  'Sociología'),
        ('ECONM',  'Economía'),
        ('DERCH',  'Derecho'),
        -- Nuevas (a partir de 3er año)
        ('DBTEC',  'Dibujo Técnico'),
        ('TALLL',  'Taller - Laboratorio'),
        ('FPVYT',  'Formación para Vida y Trabajo'),
        ('SISINF', 'Sistemas de Información'),
        ('ADMIN',  'Administración'),
        ('MJORG',  'Marco Jurídico de las Organizaciones'),
        ('ANTRO',  'Antropología'),
        ('METOD',  'Metodología'),
        ('INFEL1', 'Informática Electrónica I'),
        ('ELTEN1', 'Electrotecnia I'),
        ('ELANA1', 'Electrónica Analógica I'),
        ('ELDGT1', 'Electrónica Digital I'),
        ('ADMPC',  'Administración de la Producción y Comercialización'),
        ('PSICO',  'Psicología'),
        ('TIC',    'Tecnologías de la Información y Comunicación'),
        ('ELANA2', 'Electrónica Analógica II'),
        ('ELDGT2', 'Electrónica Digital II'),
        ('ELTEN2', 'Electrotecnia II'),
        ('INFEL2', 'Informática Electrónica II'),
        ('SINCON', 'Sistemas de Información Contable'),
        ('ADMRH',  'Administración de Recursos Humanos'),
        ('CIDPO',  'Ciudadanía y Política'),
        ('TEATR',  'Teatro'),
        ('GORG',   'Gestión de las Organizaciones Sociales'),
        ('ECOPO',  'Economía Política'),
        ('EDPOL',  'Educación Política'),
        ('EGPIN',  'Economía y Gestión de Producción Industrial'),
        ('ELIND1', 'Electrónica Industrial I'),
        ('INSIND', 'Instalación Industrial'),
        ('ELDGT3', 'Electrónica Digital III'),
        ('ANMAT',  'Análisis Matemático'),
        ('TELCO1', 'Telecomunicaciones I'),
        ('EMPRD',  'Emprendimientos'),
        ('ELIND2', 'Electrónica Industrial II'),
        ('FAMBT',  'Formación en Ambientes de Trabajo'),
        ('PRYIN',  'Proyecto Integrador'),
        ('ELDGT4', 'Electrónica Digital IV'),
        ('MJAI',   'Marco Jurídico de las Actividades Industriales'),
        ('TELCO2', 'Telecomunicaciones II'),
        ('INGTE',  'Inglés Técnico'),
        ('HIGSEG', 'Higiene y Seguridad Laboral')
    ) c(codigo, nombre)
)
INSERT INTO public."Curriculas" ("IdCurricula", "Nombre", "Descripcion", "Codigo", "EsContraturno", "Estado")
SELECT
    public.seed_uuid('curricula|' || c.codigo),
    c.nombre,
    'Pendiente de completar',
    c.codigo,
    (c.codigo = 'TALLL'),
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
-- 4) CURSOS (19: años 1-6 × A/B/C + solo 7C)
-- =========================================================

WITH combos AS (
    SELECT *
    FROM (VALUES
        (1,'A'),(1,'B'),(1,'C'),
        (2,'A'),(2,'B'),(2,'C'),
        (3,'A'),(3,'B'),(3,'C'),
        (4,'A'),(4,'B'),(4,'C'),
        (5,'A'),(5,'B'),(5,'C'),
        (6,'A'),(6,'B'),(6,'C'),
        (7,'C')
    ) c(anio_num, division)
)
INSERT INTO public."Cursos" ("IdCurso", "IdAnio", "IdDivision", "Codigo", "Estado", "AñoLectivo")
SELECT
    public.seed_uuid(format('curso|%s%s|2026', c.anio_num, c.division)),
    public.seed_uuid('anio|' || c.anio_num::text),
    public.seed_uuid('division|' || c.division::text),
    format('%s%s-2026', c.anio_num, c.division),
    true,
    '2026-01-01 00:00:00+00'::timestamptz
FROM combos c
ORDER BY c.anio_num, c.division
ON CONFLICT ("IdCurso") DO UPDATE
SET
    "IdAnio"      = EXCLUDED."IdAnio",
    "IdDivision"  = EXCLUDED."IdDivision",
    "Codigo"      = EXCLUDED."Codigo",
    "Estado"      = EXCLUDED."Estado",
    "AñoLectivo"  = EXCLUDED."AñoLectivo";

-- Desactivar cursos sin existencia real en la institución
UPDATE public."Cursos"
SET "Estado" = false
WHERE "Codigo" IN ('7A-2026', '7B-2026')
  AND "AñoLectivo"::date = '2026-01-01'::date;

-- =========================================================
-- Datos CSV de horarios 2026 (tablas temporales)
-- Se eliminan automáticamente al hacer COMMIT.
-- =========================================================

CREATE TEMP TABLE _horarios_csv (
    dia_semana   int,
    entrada      interval,
    salida       interval,
    materia      text,
    curso_codigo text
) ON COMMIT DROP;

INSERT INTO _horarios_csv (dia_semana, entrada, salida, materia, curso_codigo) VALUES
    -- 1A-2026
    (1, '07:25', '08:50', 'Artes Visuales',               '1A-2026'),
    (1, '09:00', '10:20', 'Geografía',                    '1A-2026'),
    (1, '10:30', '11:10', 'Inglés',                       '1A-2026'),
    (1, '11:10', '11:50', 'Lengua y Literatura',           '1A-2026'),
    (1, '12:00', '13:20', 'Formación Humana y Cristiana',  '1A-2026'),
    (2, '07:25', '08:50', 'Formación Humana y Cristiana',  '1A-2026'),
    (2, '09:00', '10:20', 'Educación Tecnológica',         '1A-2026'),
    (2, '10:30', '11:50', 'Física',                        '1A-2026'),
    (2, '12:00', '13:20', 'Matemática',                    '1A-2026'),
    (2, '13:20', '14:00', 'Educación Física',              '1A-2026'),
    (3, '07:25', '08:50', 'Geografía',                     '1A-2026'),
    (3, '09:00', '10:20', 'Computación',                   '1A-2026'),
    (3, '10:30', '11:50', 'Biología',                      '1A-2026'),
    (3, '12:00', '12:40', 'Física',                        '1A-2026'),
    (4, '07:25', '08:50', 'Lengua y Literatura',           '1A-2026'),
    (4, '09:00', '09:40', 'Ciudadanía y Participación',    '1A-2026'),
    (4, '09:40', '10:20', 'Artes Visuales',               '1A-2026'),
    (4, '10:30', '11:50', 'Matemática',                    '1A-2026'),
    (4, '12:00', '12:40', 'Biología',                      '1A-2026'),
    (4, '12:40', '13:20', 'Geografía',                     '1A-2026'),
    (5, '07:25', '08:50', 'Lengua y Literatura',           '1A-2026'),
    (5, '09:00', '10:20', 'Educación Física',              '1A-2026'),
    (5, '10:30', '11:50', 'Inglés',                        '1A-2026'),
    (5, '12:00', '13:20', 'Ciudadanía y Participación',    '1A-2026'),
    -- 1B-2026
    (1, '07:25', '08:50', 'Matemática',                    '1B-2026'),
    (1, '09:00', '10:20', 'Biología',                      '1B-2026'),
    (1, '10:30', '11:50', 'Artes Visuales',               '1B-2026'),
    (1, '12:00', '13:20', 'Educación Física',              '1B-2026'),
    (2, '07:25', '08:10', 'Biología',                      '1B-2026'),
    (2, '08:10', '08:50', 'Física',                        '1B-2026'),
    (2, '09:00', '10:20', 'Formación Humana y Cristiana',  '1B-2026'),
    (2, '10:30', '11:50', 'Computación',                   '1B-2026'),
    (2, '12:00', '13:20', 'Inglés',                        '1B-2026'),
    (3, '07:25', '08:50', 'Geografía',                     '1B-2026'),
    (3, '09:00', '09:40', 'Biología',                      '1B-2026'),
    (3, '09:40', '10:20', 'Lengua y Literatura',           '1B-2026'),
    (3, '10:30', '11:50', 'Física',                        '1B-2026'),
    (3, '12:00', '13:20', 'Computación',                   '1B-2026'),
    (4, '07:25', '08:50', 'Ciudadanía y Participación',    '1B-2026'),
    (4, '09:00', '10:20', 'Lengua y Literatura',           '1B-2026'),
    (4, '10:30', '11:10', 'Artes Visuales',               '1B-2026'),
    (4, '11:20', '12:40', 'Educación Física',              '1B-2026'),
    (4, '12:40', '13:20', 'Inglés',                        '1B-2026'),
    (5, '07:25', '08:50', 'Geografía',                     '1B-2026'),
    (5, '09:00', '10:20', 'Matemática',                    '1B-2026'),
    (5, '10:30', '11:50', 'Lengua y Literatura',           '1B-2026'),
    (5, '12:00', '13:20', 'Educación Tecnológica',         '1B-2026'),
    -- 1C-2026
    (1, '07:25', '08:50', 'Inglés',                        '1C-2026'),
    (1, '09:00', '09:40', 'Matemática',                    '1C-2026'),
    (1, '09:40', '10:20', 'Física',                        '1C-2026'),
    (1, '10:30', '11:50', 'Geografía',                     '1C-2026'),
    (1, '12:00', '13:20', 'Biología',                      '1C-2026'),
    (1, '13:20', '14:00', 'Educación Física',              '1C-2026'),
    (1, '14:30', '16:30', 'Taller - Laboratorio',          '1C-2026'),
    (2, '07:25', '08:50', 'Educación Tecnológica',         '1C-2026'),
    (2, '09:00', '10:20', 'Computación',                   '1C-2026'),
    (2, '10:30', '11:50', 'Formación Humana y Cristiana',  '1C-2026'),
    (2, '12:00', '12:40', 'Biología',                      '1C-2026'),
    (2, '12:40', '13:20', 'Inglés',                        '1C-2026'),
    (2, '13:40', '16:30', 'Taller - Laboratorio',          '1C-2026'),
    (3, '07:25', '08:50', 'Matemática',                    '1C-2026'),
    (3, '09:00', '10:20', 'Física',                        '1C-2026'),
    (3, '10:30', '11:50', 'Lengua y Literatura',           '1C-2026'),
    (3, '12:00', '13:20', 'Educación Física',              '1C-2026'),
    (4, '07:25', '08:50', 'Artes Visuales',               '1C-2026'),
    (4, '09:00', '11:00', 'Geografía',                     '1C-2026'),
    (4, '11:10', '11:50', 'Física',                        '1C-2026'),
    (4, '12:00', '12:40', 'Ciudadanía y Participación',    '1C-2026'),
    (4, '12:40', '13:20', 'Lengua y Literatura',           '1C-2026'),
    (5, '07:25', '08:50', 'Dibujo Técnico',                '1C-2026'),
    (5, '09:00', '10:20', 'Lengua y Literatura',           '1C-2026'),
    (5, '10:30', '11:50', 'Ciudadanía y Participación',    '1C-2026'),
    (5, '12:00', '13:20', 'Matemática',                    '1C-2026'),
    -- 2A-2026
    (1, '07:25', '08:50', 'Computación',                   '2A-2026'),
    (1, '09:00', '10:20', 'Inglés',                        '2A-2026'),
    (1, '10:30', '11:50', 'Biología',                      '2A-2026'),
    (1, '12:00', '13:20', 'Lengua y Literatura',           '2A-2026'),
    (1, '13:30', '14:50', 'Música',                        '2A-2026'),
    (2, '07:25', '08:10', 'Química',                       '2A-2026'),
    (2, '08:10', '10:20', 'Matemática',                    '2A-2026'),
    (2, '10:30', '11:50', 'Historia',                      '2A-2026'),
    (2, '12:00', '12:40', 'Educación Física',              '2A-2026'),
    (3, '07:25', '09:40', 'Lengua y Literatura',           '2A-2026'),
    (3, '09:40', '10:20', 'Biología',                      '2A-2026'),
    (3, '10:30', '11:10', 'Inglés',                        '2A-2026'),
    (3, '11:10', '11:50', 'Música',                        '2A-2026'),
    (3, '12:00', '13:20', 'Matemática',                    '2A-2026'),
    (4, '07:25', '08:50', 'Química',                       '2A-2026'),
    (4, '09:00', '10:20', 'Formación Humana y Cristiana',  '2A-2026'),
    (4, '10:30', '11:50', 'Historia',                      '2A-2026'),
    (4, '12:00', '13:20', 'Educación Física',              '2A-2026'),
    (5, '07:25', '08:10', 'Historia',                      '2A-2026'),
    (5, '08:10', '10:10', 'Formación Humana y Cristiana',  '2A-2026'),
    (5, '10:30', '13:20', 'Educación Tecnológica',         '2A-2026'),
    -- 2B-2026
    (1, '07:25', '08:50', 'Lengua y Literatura',           '2B-2026'),
    (1, '09:00', '10:20', 'Computación',                   '2B-2026'),
    (1, '10:30', '11:50', 'Historia',                      '2B-2026'),
    (1, '12:00', '13:20', 'Formación Humana y Cristiana',  '2B-2026'),
    (2, '07:25', '08:50', 'Inglés',                        '2B-2026'),
    (2, '09:00', '11:40', 'Educación Tecnológica',         '2B-2026'),
    (2, '12:00', '13:20', 'Ciudadanía y Participación',    '2B-2026'),
    (2, '13:40', '15:00', 'Educación Física',              '2B-2026'),
    (3, '07:25', '08:50', 'Música',                        '2B-2026'),
    (3, '09:00', '10:20', 'Matemática',                    '2B-2026'),
    (3, '10:30', '11:50', 'Inglés',                        '2B-2026'),
    (3, '12:00', '13:20', 'Biología',                      '2B-2026'),
    (4, '07:25', '08:50', 'Matemática',                    '2B-2026'),
    (4, '09:00', '10:20', 'Biología',                      '2B-2026'),
    (4, '10:30', '12:30', 'Lengua y Literatura',           '2B-2026'),
    (4, '12:40', '13:20', 'Educación Física',              '2B-2026'),
    (5, '07:25', '08:10', 'Ciudadanía y Participación',    '2B-2026'),
    (5, '08:10', '10:10', 'Química',                       '2B-2026'),
    (5, '10:30', '12:30', 'Historia',                      '2B-2026'),
    -- 2C-2026
    (1, '07:25', '08:50', 'Biología',                      '2C-2026'),
    (1, '09:00', '11:00', 'Lengua y Literatura',           '2C-2026'),
    (1, '11:20', '13:20', 'Ciudadanía y Participación',    '2C-2026'),
    (1, '14:00', '14:40', 'Educación Física',              '2C-2026'),
    (2, '07:25', '08:50', 'Computación',                   '2C-2026'),
    (2, '09:00', '10:20', 'Inglés',                        '2C-2026'),
    (2, '10:30', '11:50', 'Química',                       '2C-2026'),
    (2, '12:00', '13:20', 'Dibujo Técnico',                '2C-2026'),
    (3, '07:25', '08:50', 'Inglés',                        '2C-2026'),
    (3, '09:00', '10:20', 'Música',                        '2C-2026'),
    (3, '10:30', '11:10', 'Matemática',                    '2C-2026'),
    (3, '12:00', '13:20', 'Biología',                      '2C-2026'),
    (3, '13:20', '14:20', 'Educación Física',              '2C-2026'),
    (3, '14:30', '16:30', 'Taller - Laboratorio',          '2C-2026'),
    (4, '07:25', '10:20', 'Educación Tecnológica',         '2C-2026'),
    (4, '10:30', '11:50', 'Formación Humana y Cristiana',  '2C-2026'),
    (4, '12:00', '12:40', 'Historia',                      '2C-2026'),
    (4, '13:45', '15:45', 'Taller - Laboratorio',          '2C-2026'),
    (5, '07:25', '08:50', 'Matemática',                    '2C-2026'),
    (5, '09:00', '11:40', 'Historia',                      '2C-2026'),
    (5, '12:00', '13:20', 'Lengua y Literatura',           '2C-2026'),
    -- 3A-2026
    (1, '07:25', '08:50', 'Lengua y Literatura',           '3A-2026'),
    (1, '09:00', '10:20', 'Música',                        '3A-2026'),
    (1, '10:30', '11:50', 'Matemática',                    '3A-2026'),
    (1, '12:00', '12:40', 'Educación Tecnológica',         '3A-2026'),
    (1, '12:40', '13:20', 'Química',                       '3A-2026'),
    (2, '07:25', '08:50', 'Matemática',                    '3A-2026'),
    (2, '09:00', '11:40', 'Inglés',                        '3A-2026'),
    (2, '12:00', '13:20', 'Educación Física',              '3A-2026'),
    (3, '07:25', '08:50', 'Formación Humana y Cristiana',  '3A-2026'),
    (3, '09:00', '10:20', 'Educación Tecnológica',         '3A-2026'),
    (3, '10:30', '11:50', 'Geografía',                     '3A-2026'),
    (3, '12:00', '13:20', 'Computación',                   '3A-2026'),
    (3, '13:20', '14:20', 'Lengua y Literatura',           '3A-2026'),
    (3, '14:20', '16:00', 'Formación para Vida y Trabajo', '3A-2026'),
    (4, '07:25', '08:50', 'Geografía',                     '3A-2026'),
    (4, '09:00', '11:00', 'Física',                        '3A-2026'),
    (4, '11:10', '11:50', 'Música',                        '3A-2026'),
    (4, '12:00', '13:20', 'Historia',                      '3A-2026'),
    (5, '07:25', '08:50', 'Lengua y Literatura',           '3A-2026'),
    (5, '09:00', '10:20', 'Química',                       '3A-2026'),
    (5, '10:30', '11:50', 'Historia',                      '3A-2026'),
    (5, '12:00', '13:20', 'Formación para Vida y Trabajo', '3A-2026'),
    (5, '13:20', '14:20', 'Educación Física',              '3A-2026'),
    -- 3B-2026
    (1, '07:25', '08:50', 'Física',                        '3B-2026'),
    (1, '09:00', '10:20', 'Matemática',                    '3B-2026'),
    (1, '10:30', '11:50', 'Inglés',                        '3B-2026'),
    (1, '12:00', '13:20', 'Música',                        '3B-2026'),
    (1, '13:20', '14:30', 'Educación Física',              '3B-2026'),
    (2, '07:25', '08:50', 'Lengua y Literatura',           '3B-2026'),
    (2, '09:00', '10:20', 'Matemática',                    '3B-2026'),
    (2, '10:30', '12:30', 'Educación Tecnológica',         '3B-2026'),
    (2, '13:20', '14:30', 'Computación',                   '3B-2026'),
    (3, '07:25', '08:50', 'Matemática',                    '3B-2026'),
    (3, '09:00', '10:20', 'Lengua y Literatura',           '3B-2026'),
    (3, '10:30', '11:50', 'Formación Humana y Cristiana',  '3B-2026'),
    (3, '12:00', '12:40', 'Química',                       '3B-2026'),
    (3, '12:40', '13:20', 'Geografía',                     '3B-2026'),
    (3, '14:00', '15:15', 'Formación para Vida y Trabajo', '3B-2026'),
    (4, '07:25', '08:50', 'Música',                        '3B-2026'),
    (4, '09:00', '10:20', 'Geografía',                     '3B-2026'),
    (4, '10:30', '11:50', 'Inglés',                        '3B-2026'),
    (4, '12:40', '13:20', 'Lengua y Literatura',           '3B-2026'),
    (4, '13:30', '14:30', 'Educación Física',              '3B-2026'),
    (5, '07:25', '08:50', 'Formación para Vida y Trabajo', '3B-2026'),
    (5, '09:00', '10:20', 'Historia',                      '3B-2026'),
    (5, '10:30', '12:30', 'Química',                       '3B-2026'),
    (5, '13:20', '14:00', 'Geografía',                     '3B-2026'),
    -- 3C-2026
    (1, '07:25', '08:50', 'Geografía',                     '3C-2026'),
    (1, '09:00', '10:20', 'Matemática',                    '3C-2026'),
    (1, '10:30', '11:50', 'Física',                        '3C-2026'),
    (1, '12:00', '12:40', 'Química',                       '3C-2026'),
    (1, '13:20', '14:30', 'Lengua y Literatura',           '3C-2026'),
    (1, '14:30', '17:30', 'Taller - Laboratorio',          '3C-2026'),
    (2, '07:25', '08:50', 'Matemática',                    '3C-2026'),
    (2, '09:00', '11:00', 'Formación para Vida y Trabajo', '3C-2026'),
    (2, '11:10', '11:50', 'Inglés',                        '3C-2026'),
    (2, '12:00', '13:20', 'Educación Tecnológica',         '3C-2026'),
    (2, '13:40', '14:40', 'Educación Física',              '3C-2026'),
    (2, '14:45', '17:45', 'Taller - Laboratorio',          '3C-2026'),
    (3, '07:25', '08:50', 'Química',                       '3C-2026'),
    (3, '09:00', '10:20', 'Geografía',                     '3C-2026'),
    (3, '10:30', '11:50', 'Historia',                      '3C-2026'),
    (3, '12:00', '12:40', 'Matemática',                    '3C-2026'),
    (3, '12:40', '13:20', 'Formación para Vida y Trabajo', '3C-2026'),
    (3, '13:30', '14:50', 'Computación',                   '3C-2026'),
    (4, '07:25', '08:50', 'Inglés',                        '3C-2026'),
    (4, '09:00', '10:20', 'Música',                        '3C-2026'),
    (4, '10:30', '11:50', 'Física',                        '3C-2026'),
    (4, '12:00', '13:20', 'Dibujo Técnico',                '3C-2026'),
    (4, '13:20', '14:00', 'Lengua y Literatura',           '3C-2026'),
    (4, '14:30', '15:30', 'Educación Física',              '3C-2026'),
    (5, '07:25', '08:50', 'Historia',                      '3C-2026'),
    (5, '09:00', '10:20', 'Educación Tecnológica',         '3C-2026'),
    (5, '10:30', '11:50', 'Dibujo Técnico',                '3C-2026'),
    (5, '12:00', '12:40', 'Formación Humana y Cristiana',  '3C-2026'),
    (5, '12:40', '13:20', 'Lengua y Literatura',           '3C-2026'),
    -- 4A-2026
    (1, '07:25', '08:50', 'Inglés',                        '4A-2026'),
    (1, '09:00', '10:20', 'Lengua y Literatura',           '4A-2026'),
    (1, '10:30', '11:50', 'Sistemas de Información',        '4A-2026'),
    (1, '12:00', '13:20', 'Formación para Vida y Trabajo', '4A-2026'),
    (1, '13:20', '14:40', 'Artes Visuales',               '4A-2026'),
    (2, '07:25', '08:50', 'Biología',                      '4A-2026'),
    (2, '09:00', '10:20', 'Historia',                      '4A-2026'),
    (2, '10:30', '11:50', 'Matemática',                    '4A-2026'),
    (2, '12:00', '12:40', 'Lengua y Literatura',           '4A-2026'),
    (2, '12:40', '13:20', 'Educación Física',              '4A-2026'),
    (3, '07:25', '08:50', 'Biología',                      '4A-2026'),
    (3, '09:00', '10:20', 'Doctrina Social de la Iglesia', '4A-2026'),
    (3, '10:30', '11:50', 'Matemática',                    '4A-2026'),
    (3, '12:00', '13:20', 'Lengua y Literatura',           '4A-2026'),
    (4, '07:25', '08:50', 'Geografía',                     '4A-2026'),
    (4, '09:00', '09:40', 'Administración',                '4A-2026'),
    (4, '09:40', '10:20', 'Inglés',                        '4A-2026'),
    (4, '10:30', '11:50', 'Sistemas de Información',        '4A-2026'),
    (4, '12:00', '13:00', 'Educación Física',              '4A-2026'),
    (5, '07:25', '08:50', 'Administración',                '4A-2026'),
    (5, '09:00', '11:00', 'Marco Jurídico de las Org.',    '4A-2026'),
    (5, '11:10', '11:50', 'Geografía',                     '4A-2026'),
    (5, '12:00', '13:20', 'Formación para Vida y Trabajo', '4A-2026'),
    -- 4B-2026
    (1, '07:25', '08:50', 'Educación Física',              '4B-2026'),
    (1, '09:00', '09:40', 'Metodología',                   '4B-2026'),
    (1, '09:40', '10:20', 'Historia',                      '4B-2026'),
    (1, '10:30', '11:50', 'Antropología',                  '4B-2026'),
    (1, '12:00', '13:20', 'Inglés',                        '4B-2026'),
    (2, '07:25', '08:50', 'Geografía',                     '4B-2026'),
    (2, '09:00', '10:20', 'Historia',                      '4B-2026'),
    (2, '10:30', '11:50', 'Biología',                      '4B-2026'),
    (2, '12:00', '13:20', 'Metodología',                   '4B-2026'),
    (3, '07:25', '08:50', 'Lengua y Literatura',           '4B-2026'),
    (3, '09:00', '10:20', 'Doctrina Social de la Iglesia', '4B-2026'),
    (3, '10:30', '12:40', 'Artes Visuales',               '4B-2026'),
    (3, '13:20', '14:00', 'Formación para Vida y Trabajo', '4B-2026'),
    (4, '07:25', '08:50', 'Historia',                      '4B-2026'),
    (4, '09:00', '10:20', 'Matemática',                    '4B-2026'),
    (4, '10:30', '11:50', 'Biología',                      '4B-2026'),
    (4, '12:00', '13:20', 'Formación para Vida y Trabajo', '4B-2026'),
    (5, '07:25', '08:40', 'Educación Física',              '4B-2026'),
    (5, '09:00', '10:20', 'Matemática',                    '4B-2026'),
    (5, '10:30', '11:50', 'Geografía',                     '4B-2026'),
    (5, '12:00', '13:20', 'Antropología',                  '4B-2026'),
    (5, '13:40', '15:00', 'Lengua y Literatura',           '4B-2026'),
    -- 4C-2026
    (1, '07:25', '08:50', 'Matemática',                    '4C-2026'),
    (1, '09:00', '10:20', 'Física',                        '4C-2026'),
    (1, '10:30', '11:50', 'Lengua y Literatura',           '4C-2026'),
    (1, '12:00', '13:20', 'Informática Electrónica I',     '4C-2026'),
    (1, '13:45', '15:05', 'Informática Electrónica I',     '4C-2026'),
    (1, '15:20', '17:40', 'Electrotecnia I',               '4C-2026'),
    (2, '07:25', '08:50', 'Física',                        '4C-2026'),
    (2, '09:00', '10:20', 'Biología',                      '4C-2026'),
    (2, '10:30', '11:50', 'Historia',                      '4C-2026'),
    (2, '12:00', '13:20', 'Inglés',                        '4C-2026'),
    (2, '13:40', '15:00', 'Educación Física',              '4C-2026'),
    (3, '07:25', '08:50', 'Matemática',                    '4C-2026'),
    (3, '09:00', '10:20', 'Inglés',                        '4C-2026'),
    (3, '10:30', '11:50', 'Doctrina Social de la Iglesia', '4C-2026'),
    (3, '12:00', '13:20', 'Electrónica Analógica I',       '4C-2026'),
    (3, '13:45', '18:00', 'Electrónica Digital I',         '4C-2026'),
    (4, '07:25', '08:50', 'Biología',                      '4C-2026'),
    (4, '09:00', '10:20', 'Lengua y Literatura',           '4C-2026'),
    (4, '10:30', '11:50', 'Geografía',                     '4C-2026'),
    (4, '12:00', '13:10', 'Educación Física',              '4C-2026'),
    (4, '13:20', '14:00', 'Electrónica Analógica I',       '4C-2026'),
    (5, '07:25', '08:50', 'Matemática',                    '4C-2026'),
    (5, '09:00', '10:20', 'Electrónica Analógica I',       '4C-2026'),
    (5, '10:30', '11:50', 'Química',                       '4C-2026'),
    (5, '12:00', '13:20', 'Artes Visuales',               '4C-2026'),
    -- 5A-2026
    (1, '07:25', '08:50', 'Física',                        '5A-2026'),
    (1, '09:00', '10:20', 'Inglés',                        '5A-2026'),
    (1, '10:30', '11:50', 'Matemática',                    '5A-2026'),
    (1, '12:00', '13:20', 'Sistemas de Información',        '5A-2026'),
    (1, '13:20', '14:20', 'Educación Física',              '5A-2026'),
    (2, '07:25', '08:50', 'Historia',                      '5A-2026'),
    (2, '09:00', '10:20', 'Matemática',                    '5A-2026'),
    (2, '10:30', '11:10', 'Inglés',                        '5A-2026'),
    (2, '11:10', '13:10', 'Economía',                      '5A-2026'),
    (2, '13:45', '15:30', 'Sistemas de Información',        '5A-2026'),
    (2, '15:30', '16:10', 'Administración',                '5A-2026'),
    (3, '07:25', '08:50', 'Doctrina Social de la Iglesia', '5A-2026'),
    (3, '09:00', '10:20', 'Física',                        '5A-2026'),
    (3, '10:30', '11:50', 'Lengua y Literatura',           '5A-2026'),
    (3, '12:00', '12:40', 'Historia',                      '5A-2026'),
    (3, '13:20', '14:00', 'Geografía',                     '5A-2026'),
    (4, '07:25', '09:40', 'Formación para Vida y Trabajo', '5A-2026'),
    (4, '09:40', '11:50', 'Música',                        '5A-2026'),
    (4, '12:00', '13:20', 'Administración',                '5A-2026'),
    (4, '13:40', '15:30', 'ADM de la Producción y Comercialización', '5A-2026'),
    (5, '07:25', '08:50', 'Psicología',                    '5A-2026'),
    (5, '09:00', '10:20', 'Geografía',                     '5A-2026'),
    (5, '10:30', '11:50', 'Lengua y Literatura',           '5A-2026'),
    (5, '12:00', '13:00', 'Educación Física',              '5A-2026'),
    (5, '13:20', '14:00', 'Psicología',                    '5A-2026'),
    -- 5B-2026
    (1, '07:25', '08:50', 'Sociología',                    '5B-2026'),
    (1, '09:00', '10:20', 'Lengua y Literatura',           '5B-2026'),
    (1, '10:30', '11:50', 'Música',                        '5B-2026'),
    (1, '12:00', '13:20', 'Historia',                      '5B-2026'),
    (1, '13:40', '15:00', 'Formación para Vida y Trabajo', '5B-2026'),
    (2, '07:25', '08:10', 'Historia',                      '5B-2026'),
    (2, '08:10', '08:50', 'Sociología',                    '5B-2026'),
    (2, '09:00', '10:20', 'Geografía',                     '5B-2026'),
    (2, '10:30', '11:50', 'Inglés',                        '5B-2026'),
    (2, '12:00', '13:20', 'Física',                        '5B-2026'),
    (2, '13:20', '14:00', 'Educación Física',              '5B-2026'),
    (3, '07:25', '08:50', 'Doctrina Social de la Iglesia', '5B-2026'),
    (3, '09:00', '10:20', 'Matemática',                    '5B-2026'),
    (3, '10:30', '11:50', 'Geografía',                     '5B-2026'),
    (3, '12:00', '12:40', 'Formación para Vida y Trabajo', '5B-2026'),
    (3, '12:40', '13:20', 'Inglés',                        '5B-2026'),
    (4, '07:25', '08:50', 'Matemática',                    '5B-2026'),
    (4, '09:00', '10:20', 'Historia',                      '5B-2026'),
    (4, '10:30', '11:10', 'Tecnologías de la Información y Comunicación', '5B-2026'),
    (4, '11:10', '11:50', 'Lengua y Literatura',           '5B-2026'),
    (4, '12:00', '13:20', 'Psicología',                    '5B-2026'),
    (4, '13:40', '15:00', 'Tecnologías de la Información y Comunicación', '5B-2026'),
    (5, '07:25', '08:50', 'Educación Física',              '5B-2026'),
    (5, '09:00', '10:20', 'Psicología',                    '5B-2026'),
    (5, '10:30', '11:50', 'Lengua y Literatura',           '5B-2026'),
    (5, '12:00', '13:20', 'Física',                        '5B-2026'),
    -- 5C-2026 ("Formación Cristiana y Humana" normalizado a FMHCR)
    (1, '07:25', '08:50', 'Formación Humana y Cristiana',  '5C-2026'),
    (1, '09:00', '11:40', 'Electrónica Analógica II',      '5C-2026'),
    (1, '12:00', '13:20', 'Inglés',                        '5C-2026'),
    (1, '14:10', '15:10', 'Educación Física',              '5C-2026'),
    (1, '15:10', '18:00', 'Electrónica Digital II',        '5C-2026'),
    (2, '07:25', '08:50', 'Física',                        '5C-2026'),
    (2, '09:00', '10:20', 'Química',                       '5C-2026'),
    (2, '10:30', '11:50', 'Historia',                      '5C-2026'),
    (2, '12:00', '13:20', 'Matemática',                    '5C-2026'),
    (2, '13:20', '14:00', 'Matemática',                    '5C-2026'),
    (3, '07:25', '08:50', 'Química',                       '5C-2026'),
    (3, '09:00', '10:20', 'Geografía',                     '5C-2026'),
    (3, '10:30', '11:50', 'Física',                        '5C-2026'),
    (3, '12:00', '13:20', 'Psicología',                    '5C-2026'),
    (3, '13:20', '14:00', 'Inglés',                        '5C-2026'),
    (3, '14:30', '15:30', 'Educación Física',              '5C-2026'),
    (4, '07:25', '08:50', 'Lengua y Literatura',           '5C-2026'),
    (4, '09:00', '10:20', 'Matemática',                    '5C-2026'),
    (4, '10:30', '11:50', 'Geografía',                     '5C-2026'),
    (4, '12:00', '13:20', 'Música',                        '5C-2026'),
    (4, '13:45', '18:00', 'Electrotecnia II',              '5C-2026'),
    (5, '07:25', '10:20', 'Electrónica Analógica II',      '5C-2026'),
    (5, '10:30', '11:10', 'Psicología',                    '5C-2026'),
    (5, '11:20', '13:20', 'Informática Electrónica II',    '5C-2026'),
    -- 6A-2026
    (1, '07:25', '08:50', 'Sistemas de Información Contable',        '6A-2026'),
    (1, '09:00', '10:20', 'Administración de Recursos Humanos',      '6A-2026'),
    (1, '10:30', '11:50', 'Química',                                 '6A-2026'),
    (1, '12:00', '13:20', 'Lengua y Literatura',                     '6A-2026'),
    (2, '07:25', '08:50', 'Sistemas de Información Contable',        '6A-2026'),
    (2, '09:00', '10:20', 'Formación para Vida y Trabajo',           '6A-2026'),
    (2, '10:30', '11:50', 'Lengua y Literatura',                     '6A-2026'),
    (2, '12:00', '13:20', 'Matemática',                              '6A-2026'),
    (2, '14:10', '15:10', 'Educación Física',                        '6A-2026'),
    (3, '07:25', '08:50', 'Ciudadanía y Política',                   '6A-2026'),
    (3, '09:00', '10:20', 'Matemática',                              '6A-2026'),
    (3, '10:30', '11:50', 'Química',                                 '6A-2026'),
    (3, '12:00', '13:20', 'Administración de Recursos Humanos',      '6A-2026'),
    (3, '13:45', '16:20', 'Administración de Recursos Humanos',      '6A-2026'),
    (3, '16:30', '17:40', 'Derecho',                                 '6A-2026'),
    (4, '07:25', '08:10', 'Ciudadanía y Política',                   '6A-2026'),
    (4, '08:10', '08:50', 'Sistemas de Información Contable',        '6A-2026'),
    (4, '09:00', '10:20', 'Filosofía',                               '6A-2026'),
    (4, '10:30', '11:50', 'Doctrina Social de la Iglesia',           '6A-2026'),
    (4, '12:00', '13:20', 'Inglés',                                  '6A-2026'),
    (5, '07:25', '08:50', 'Inglés',                                  '6A-2026'),
    (5, '09:00', '11:10', 'Economía',                                '6A-2026'),
    (5, '11:00', '11:50', 'Derecho',                                 '6A-2026'),
    (5, '12:00', '13:00', 'Educación Física',                        '6A-2026'),
    (5, '13:30', '15:30', 'Teatro',                                  '6A-2026'),
    -- 6B-2026
    (1, '07:25', '08:50', 'Lengua y Literatura',                     '6B-2026'),
    (1, '09:00', '10:20', 'Doctrina Social de la Iglesia',           '6B-2026'),
    (1, '10:30', '11:50', 'Gestión de las Organizaciones Sociales',  '6B-2026'),
    (1, '12:00', '13:20', 'Matemática',                              '6B-2026'),
    (1, '14:30', '15:50', 'Teatro',                                  '6B-2026'),
    (2, '07:25', '08:50', 'Formación para Vida y Trabajo',           '6B-2026'),
    (2, '09:00', '09:40', 'Gestión de las Organizaciones Sociales',  '6B-2026'),
    (2, '09:40', '10:20', 'Inglés',                                  '6B-2026'),
    (2, '10:30', '11:50', 'Química',                                 '6B-2026'),
    (2, '12:00', '13:20', 'Matemática',                              '6B-2026'),
    (2, '14:00', '14:40', 'Educación Política',                      '6B-2026'),
    (2, '14:50', '15:50', 'Educación Física',                        '6B-2026'),
    (3, '07:25', '08:10', 'Filosofía',                               '6B-2026'),
    (3, '08:10', '08:50', 'Gestión de las Organizaciones Sociales',  '6B-2026'),
    (3, '09:00', '10:20', 'Ciudadanía y Política',                   '6B-2026'),
    (3, '10:30', '11:50', 'Economía Política',                       '6B-2026'),
    (3, '12:00', '13:20', 'Geografía',                               '6B-2026'),
    (3, '13:20', '14:00', 'Geografía',                               '6B-2026'),
    (3, '14:30', '15:10', 'Formación para Vida y Trabajo',           '6B-2026'),
    (4, '07:25', '08:50', 'Inglés',                                  '6B-2026'),
    (4, '09:00', '10:20', 'Ciudadanía y Política',                   '6B-2026'),
    (4, '10:30', '11:50', 'Historia',                                '6B-2026'),
    (4, '12:00', '13:20', 'Lengua y Literatura',                     '6B-2026'),
    (4, '13:30', '14:10', 'Historia',                                '6B-2026'),
    (5, '07:25', '08:50', 'Química',                                 '6B-2026'),
    (5, '09:00', '11:10', 'Filosofía',                               '6B-2026'),
    (5, '11:10', '11:50', 'Teatro',                                  '6B-2026'),
    (5, '12:00', '12:40', 'Lengua y Literatura',                     '6B-2026'),
    (5, '13:20', '14:20', 'Educación Física',                        '6B-2026'),
    -- 6C-2026
    (1, '07:25', '08:50', 'Doctrina Social de la Iglesia',           '6C-2026'),
    (1, '09:00', '10:20', 'Economía y Gestión de Producción Industrial', '6C-2026'),
    (1, '10:30', '11:50', 'Inglés',                                  '6C-2026'),
    (1, '12:00', '12:40', 'Educación Física',                        '6C-2026'),
    (1, '12:40', '13:20', 'Electrónica Industrial I',                '6C-2026'),
    (1, '13:20', '15:30', 'Electrónica Industrial I',                '6C-2026'),
    (2, '08:10', '10:20', 'Lengua y Literatura',                     '6C-2026'),
    (2, '10:30', '11:50', 'Ciudadanía y Política',                   '6C-2026'),
    (2, '12:00', '13:20', 'Economía y Gestión de Producción Industrial', '6C-2026'),
    (2, '13:45', '18:00', 'Instalación Industrial',                  '6C-2026'),
    (3, '07:25', '08:10', 'Electrónica Digital III',                 '6C-2026'),
    (3, '08:10', '10:20', 'Filosofía',                               '6C-2026'),
    (3, '10:30', '11:50', 'Análisis Matemático',                     '6C-2026'),
    (3, '12:00', '12:40', 'Inglés',                                  '6C-2026'),
    (3, '12:40', '13:20', 'Ciudadanía y Política',                   '6C-2026'),
    (3, '13:20', '14:00', 'Análisis Matemático',                     '6C-2026'),
    (3, '14:30', '18:00', 'Telecomunicaciones I',                    '6C-2026'),
    (4, '07:25', '10:20', 'Electrónica Digital III',                 '6C-2026'),
    (4, '10:30', '11:50', 'Análisis Matemático',                     '6C-2026'),
    (5, '07:25', '08:50', 'Electrónica Industrial I',                '6C-2026'),
    (5, '09:00', '10:20', 'Instalación Industrial',                  '6C-2026'),
    (5, '10:30', '11:50', 'Educación Física',                        '6C-2026'),
    (5, '12:00', '13:20', 'Teatro',                                  '6C-2026'),
    -- 7C-2026
    (1, '07:25', '08:50', 'Emprendimientos',                                    '7C-2026'),
    (1, '09:00', '12:40', 'Electrónica Industrial II',                          '7C-2026'),
    (2, '07:25', '08:50', 'Emprendimientos',                                    '7C-2026'),
    (2, '09:00', '12:40', 'Formación en Ambientes de Trabajo',                  '7C-2026'),
    (2, '12:40', '13:20', 'Proyecto Integrador',                                '7C-2026'),
    (2, '13:45', '17:15', 'Proyecto Integrador',                                '7C-2026'),
    (3, '08:10', '11:50', 'Electrónica Digital IV',                             '7C-2026'),
    (3, '12:00', '13:20', 'Marco Jurídico de las Actividades Industriales',     '7C-2026'),
    (3, '13:20', '14:00', 'Marco Jurídico de las Actividades Industriales',     '7C-2026'),
    (4, '07:25', '10:20', 'Telecomunicaciones II',                              '7C-2026'),
    (4, '10:30', '11:50', 'Inglés Técnico',                                     '7C-2026'),
    (4, '12:00', '13:20', 'Higiene y Seguridad Laboral',                        '7C-2026'),
    (5, '09:00', '10:20', 'Inglés Técnico',                                     '7C-2026'),
    (5, '10:30', '13:20', 'Formación en Ambientes de Trabajo',                  '7C-2026');

CREATE TEMP TABLE _materia_codigo (materia text, codigo text) ON COMMIT DROP;
INSERT INTO _materia_codigo (materia, codigo) VALUES
    ('Artes Visuales',                                     'ARTVI'),
    ('Geografía',                                          'GEOGR'),
    ('Inglés',                                             'INGLS'),
    ('Lengua y Literatura',                                'LENLI'),
    ('Formación Humana y Cristiana',                       'FMHCR'),
    ('Educación Tecnológica',                              'EDTEC'),
    ('Física',                                             'FISIC'),
    ('Matemática',                                         'MATEM'),
    ('Educación Física',                                   'EDFIS'),
    ('Computación',                                        'COMPU'),
    ('Biología',                                           'BIOLG'),
    ('Ciudadanía y Participación',                         'CIDPA'),
    ('Historia',                                           'HISTO'),
    ('Química',                                            'QUIMC'),
    ('Música',                                             'MUSIC'),
    ('Doctrina Social de la Iglesia',                      'DCSIG'),
    ('Filosofía',                                          'FILOS'),
    ('Sociología',                                         'SOCIO'),
    ('Economía',                                           'ECONM'),
    ('Derecho',                                            'DERCH'),
    ('Dibujo Técnico',                                     'DBTEC'),
    ('Taller - Laboratorio',                               'TALLL'),
    ('Formación para Vida y Trabajo',                      'FPVYT'),
    ('Sistemas de Información',                            'SISINF'),
    ('Administración',                                     'ADMIN'),
    ('Marco Jurídico de las Org.',                         'MJORG'),
    ('Antropología',                                       'ANTRO'),
    ('Metodología',                                        'METOD'),
    ('Informática Electrónica I',                          'INFEL1'),
    ('Electrotecnia I',                                    'ELTEN1'),
    ('Electrónica Analógica I',                            'ELANA1'),
    ('Electrónica Digital I',                              'ELDGT1'),
    ('ADM de la Producción y Comercialización',            'ADMPC'),
    ('Psicología',                                         'PSICO'),
    ('Tecnologías de la Información y Comunicación',       'TIC'),
    ('Electrónica Analógica II',                           'ELANA2'),
    ('Electrónica Digital II',                             'ELDGT2'),
    ('Electrotecnia II',                                   'ELTEN2'),
    ('Informática Electrónica II',                         'INFEL2'),
    ('Sistemas de Información Contable',                   'SINCON'),
    ('Administración de Recursos Humanos',                 'ADMRH'),
    ('Ciudadanía y Política',                              'CIDPO'),
    ('Teatro',                                             'TEATR'),
    ('Gestión de las Organizaciones Sociales',             'GORG'),
    ('Economía Política',                                  'ECOPO'),
    ('Educación Política',                                 'EDPOL'),
    ('Economía y Gestión de Producción Industrial',        'EGPIN'),
    ('Electrónica Industrial I',                           'ELIND1'),
    ('Instalación Industrial',                             'INSIND'),
    ('Electrónica Digital III',                            'ELDGT3'),
    ('Análisis Matemático',                                'ANMAT'),
    ('Telecomunicaciones I',                               'TELCO1'),
    ('Emprendimientos',                                    'EMPRD'),
    ('Electrónica Industrial II',                          'ELIND2'),
    ('Formación en Ambientes de Trabajo',                  'FAMBT'),
    ('Proyecto Integrador',                                'PRYIN'),
    ('Electrónica Digital IV',                             'ELDGT4'),
    ('Marco Jurídico de las Actividades Industriales',     'MJAI'),
    ('Telecomunicaciones II',                              'TELCO2'),
    ('Inglés Técnico',                                     'INGTE'),
    ('Higiene y Seguridad Laboral',                        'HIGSEG');

-- =========================================================
-- Limpieza: eliminar ECs y Horarios 2026 (reconstrucción desde CSV)
-- =========================================================

DELETE FROM public."AsistenciasPorEspacio"
WHERE "IdClaseDictada" IN (
    SELECT cd."IdClaseDictada"
    FROM public."ClasesDictadas" cd
    INNER JOIN public."EspaciosCurriculares" ec ON ec."IdEC" = cd."IdEC"
    INNER JOIN public."Cursos" c ON c."IdCurso" = ec."IdCurso"
    WHERE c."AñoLectivo"::date = '2026-01-01'::date
);

DELETE FROM public."ClasesDictadas"
WHERE "IdEC" IN (
    SELECT ec."IdEC"
    FROM public."EspaciosCurriculares" ec
    INNER JOIN public."Cursos" c ON c."IdCurso" = ec."IdCurso"
    WHERE c."AñoLectivo"::date = '2026-01-01'::date
);

DELETE FROM public."Horarios"
WHERE "IdCurso" IN (
    SELECT c."IdCurso" FROM public."Cursos" c
    WHERE c."AñoLectivo"::date = '2026-01-01'::date
);

DELETE FROM public."EspaciosCurriculares"
WHERE "IdCurso" IN (
    SELECT c."IdCurso" FROM public."Cursos" c
    WHERE c."AñoLectivo"::date = '2026-01-01'::date
);

-- =========================================================
-- 5) ESPACIOS CURRICULARES (basado en horarios reales del CSV)
-- =========================================================

INSERT INTO public."EspaciosCurriculares" ("IdEC", "IdCurso", "IdCurricula", "IdDocente")
SELECT
    public.seed_uuid('ec|' || eu.curso_codigo || '|' || eu.curricula_codigo),
    c."IdCurso",
    public.seed_uuid('curricula|' || eu.curricula_codigo),
    NULL
FROM (
    SELECT DISTINCT h.curso_codigo, mc.codigo AS curricula_codigo
    FROM _horarios_csv h
    INNER JOIN _materia_codigo mc ON mc.materia = h.materia
) eu
INNER JOIN public."Cursos" c ON c."Codigo" = eu.curso_codigo
ON CONFLICT ("IdEC") DO UPDATE
SET
    "IdCurso"     = EXCLUDED."IdCurso",
    "IdCurricula" = EXCLUDED."IdCurricula",
    "IdDocente"   = EXCLUDED."IdDocente";

-- =========================================================
-- 6) HORARIOS (446 filas reales desde CSV)
-- =========================================================

INSERT INTO public."Horarios" ("IdHorario", "DíaSemana", "HorarioEntrada", "HorarioSalida", "IdCurso", "IdEC")
SELECT
    public.seed_uuid('horario|' || h.curso_codigo || '|d' || h.dia_semana::text || '|' || REPLACE(h.entrada::text, ':', '')),
    h.dia_semana,
    h.entrada,
    h.salida,
    c."IdCurso",
    public.seed_uuid('ec|' || h.curso_codigo || '|' || mc.codigo)
FROM _horarios_csv h
INNER JOIN _materia_codigo mc ON mc.materia = h.materia
INNER JOIN public."Cursos" c ON c."Codigo" = h.curso_codigo
ON CONFLICT ("IdHorario") DO UPDATE
SET
    "DíaSemana"      = EXCLUDED."DíaSemana",
    "HorarioEntrada" = EXCLUDED."HorarioEntrada",
    "HorarioSalida"  = EXCLUDED."HorarioSalida",
    "IdCurso"        = EXCLUDED."IdCurso",
    "IdEC"           = EXCLUDED."IdEC";

-- =========================================================
-- 7) ESTUDIANTES (30 por curso = 570)
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
    "Nombre"          = EXCLUDED."Nombre",
    "Apellido"        = EXCLUDED."Apellido",
    "Documento"       = EXCLUDED."Documento",
    "FechaNacimiento" = EXCLUDED."FechaNacimiento",
    "Domicilio"       = EXCLUDED."Domicilio",
    "Sexo"            = EXCLUDED."Sexo",
    "TeaGeneral"      = EXCLUDED."TeaGeneral";

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
    "Nombre"                   = EXCLUDED."Nombre",
    "Apellido"                 = EXCLUDED."Apellido",
    "Documento"                = EXCLUDED."Documento",
    "Telefono"                 = EXCLUDED."Telefono",
    "Correo"                   = EXCLUDED."Correo",
    "RelacionEstudiante"       = EXCLUDED."RelacionEstudiante",
    "FechaNacimiento"          = EXCLUDED."FechaNacimiento",
    "Disponibilidad"           = EXCLUDED."Disponibilidad",
    "Domicilio"                = EXCLUDED."Domicilio",
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

-- =========================================================
-- 10) PERMISOS
-- =========================================================

WITH permisos AS (
    SELECT *
    FROM (VALUES
        ('Sistema',        'Escritura', 'CARGAS_BASE_RW',        'Cargas base',                     'Gestionar datos base del sistema'),
        ('Alumnos',        'Lectura',   'FICHA_ALUMNO_R',         'Ficha del alumno (lectura)',       'Ver la ficha de un alumno'),
        ('Alumnos',        'Escritura', 'FICHA_ALUMNO_RW',        'Ficha del alumno (escritura)',     'Editar la ficha de un alumno'),
        ('Alumnos',        'Lectura',   'DATOS_CONTACTO_R',       'Datos de contacto (lectura)',      'Ver datos de contacto'),
        ('Alumnos',        'Escritura', 'DATOS_CONTACTO_RW',      'Datos de contacto (escritura)',    'Editar datos de contacto'),
        ('Asistencia',     'Lectura',   'ASISTENCIA_MANUAL_R',    'Asistencia manual (lectura)',      'Ver registros de asistencia manual'),
        ('Asistencia',     'Escritura', 'ASISTENCIA_MANUAL_RW',   'Asistencia manual (escritura)',    'Registrar asistencia manual'),
        ('Asistencia',     'Escritura', 'ASISTENCIA_QR_RW',       'Asistencia QR',                   'Registrar asistencia por QR'),
        ('Credenciales',   'Escritura', 'CREDENCIALES_QR_RW',     'Credenciales QR',                 'Generar y enviar credenciales QR'),
        ('Asistencia',     'Escritura', 'BUSQUEDA_RAPIDA_RW',     'Busqueda rapida',                 'Registrar asistencia por busqueda rapida'),
        ('Asistencia',     'Lectura',   'PARTE_DIARIO_R',         'Parte diario (lectura)',           'Ver el parte diario'),
        ('Asistencia',     'Escritura', 'PARTE_DIARIO_RW',        'Parte diario (escritura)',         'Gestionar el parte diario'),
        ('Reportes',       'Escritura', 'REPORTES_ASISTENCIA_RW', 'Reportes de asistencia general',  'Ver y exportar reportes generales de asistencia'),
        ('Reportes',       'Escritura', 'REPORTES_EC_RW',         'Reportes por espacio curricular', 'Ver y exportar reportes de asistencia por EC'),
        ('Administracion', 'Escritura', 'GESTION_USUARIOS_RW',   'Gestion de usuarios',             'Crear, editar y desactivar usuarios'),
        ('Administracion', 'Escritura', 'ASIGNACION_ROLES_RW',   'Gestion de roles',                'Asignar y quitar roles a usuarios')
    ) p(modulo, accion, codigo, nombre, descripcion)
)
INSERT INTO public."Permisos" ("IdPermiso", "Modulo", "Accion", "Codigo", "Nombre", "Descripcion")
SELECT
    public.seed_uuid('permiso|' || p.codigo),
    p.modulo,
    p.accion,
    p.codigo,
    p.nombre,
    p.descripcion
FROM permisos p
ON CONFLICT ("IdPermiso") DO UPDATE
SET
    "Modulo"      = EXCLUDED."Modulo",
    "Accion"      = EXCLUDED."Accion",
    "Codigo"      = EXCLUDED."Codigo",
    "Nombre"      = EXCLUDED."Nombre",
    "Descripcion" = EXCLUDED."Descripcion";

-- =========================================================
-- 11) ROL-PERMISOS
-- =========================================================

DELETE FROM public."RolPermisos"
WHERE "IdRol" IN (
    SELECT "IdRol" FROM public."Roles"
    WHERE "Nombre" IN ('Secretario', 'Docente', 'Preceptor', 'Equipo Directivo')
);

WITH asignaciones AS (
    SELECT *
    FROM (VALUES
        ('Secretario',       'CARGAS_BASE_RW'),
        ('Secretario',       'FICHA_ALUMNO_R'),
        ('Secretario',       'FICHA_ALUMNO_RW'),
        ('Secretario',       'DATOS_CONTACTO_R'),
        ('Secretario',       'DATOS_CONTACTO_RW'),
        ('Secretario',       'REPORTES_ASISTENCIA_RW'),
        ('Secretario',       'REPORTES_EC_RW'),
        ('Secretario',       'GESTION_USUARIOS_RW'),
        ('Secretario',       'ASIGNACION_ROLES_RW'),
        ('Preceptor',        'FICHA_ALUMNO_R'),
        ('Preceptor',        'FICHA_ALUMNO_RW'),
        ('Preceptor',        'DATOS_CONTACTO_R'),
        ('Preceptor',        'DATOS_CONTACTO_RW'),
        ('Preceptor',        'ASISTENCIA_MANUAL_R'),
        ('Preceptor',        'ASISTENCIA_MANUAL_RW'),
        ('Preceptor',        'ASISTENCIA_QR_RW'),
        ('Preceptor',        'CREDENCIALES_QR_RW'),
        ('Preceptor',        'BUSQUEDA_RAPIDA_RW'),
        ('Preceptor',        'PARTE_DIARIO_R'),
        ('Preceptor',        'PARTE_DIARIO_RW'),
        ('Preceptor',        'REPORTES_ASISTENCIA_RW'),
        ('Docente',          'FICHA_ALUMNO_R'),
        ('Docente',          'DATOS_CONTACTO_R'),
        ('Docente',          'PARTE_DIARIO_R'),
        ('Docente',          'REPORTES_EC_RW'),
        ('Equipo Directivo', 'FICHA_ALUMNO_R'),
        ('Equipo Directivo', 'FICHA_ALUMNO_RW'),
        ('Equipo Directivo', 'DATOS_CONTACTO_R'),
        ('Equipo Directivo', 'DATOS_CONTACTO_RW'),
        ('Equipo Directivo', 'ASISTENCIA_MANUAL_R'),
        ('Equipo Directivo', 'PARTE_DIARIO_R')
    ) a(rol, permiso_codigo)
)
INSERT INTO public."RolPermisos" ("IdRolPermiso", "IdRol", "IdPermiso")
SELECT
    public.seed_uuid('rolpermiso|' || a.rol || '|' || a.permiso_codigo),
    r."IdRol",
    public.seed_uuid('permiso|' || a.permiso_codigo)
FROM asignaciones a
INNER JOIN public."Roles" r ON r."Nombre" = a.rol
ON CONFLICT ("IdRolPermiso") DO NOTHING;

-- =========================================================
-- Fix: admin sin contraseña provisoria ni vencimiento
-- =========================================================

UPDATE public."Usuarios"
SET
    "RequiereCambioContrasena"   = false,
    "FechaVencimientoContrasena" = NULL
WHERE "Email" = 'admin@sistema.local';

COMMIT;
