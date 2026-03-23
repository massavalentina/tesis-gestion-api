using RepoDB.Entities;
using Xunit;

namespace TesisGestorApi.Tests;

/// <summary>
/// Tests unitarios para Asistencia.CalcularAsistencia().
/// Cada test cubre una combinación del CSV de reglas de negocio.
///
/// Convención de parámetros de tiempo cuando aplica retiro:
///   minTotalesM     = 200 (minutos de clases dadas en la mañana)
///   minPerdidaSalida < 20  → RE  (≤ 10%)
///   minPerdidaSalida = 60  → RA  (30%, entre 10% y 50%)
///   minPerdidaSalida = 110 → RAE (55%, más del 50%)
/// </summary>
public class AsistenciaCalcularTests
{
    // ── Helpers ───────────────────────────────────────────────────────────────

    /// Crea un TipoAsistencia mínimo con el código dado.
    private static TipoAsistencia Tipo(string codigo) =>
        new() { IdTipo = Guid.NewGuid(), Codigo = codigo, Descripcion = codigo, ValorBase = 0 };

    /// Construye una Asistencia con los tipos indicados y llama a CalcularAsistencia
    /// con los parámetros de tiempo proporcionados.
    private static decimal Calcular(
        string? codigoManana,
        string? codigoTarde,
        string? codigoLlegadaManana    = null,
        double  minTotalesM            = 0,
        double  minPerdidaIngresoM     = 0,
        double  minPerdidaSalidaM      = 0,
        double  minTotalesT            = 0,
        double  minPerdidaIngresoT     = 0,
        double  minPerdidaSalidaT      = 0)
    {
        var a = new Asistencia { Id = Guid.NewGuid(), EstudianteId = Guid.NewGuid() };

        if (codigoManana != null)
        {
            a.TipoManiana   = Tipo(codigoManana);
            a.TipoManianaId = a.TipoManiana.IdTipo;
        }

        // TipoLlegadaManiana solo se setea cuando viene explícito
        // (simula el escenario LLT registrado primero, luego sobreescrito por retiro).
        if (codigoLlegadaManana != null)
        {
            a.TipoLlegadaManiana   = Tipo(codigoLlegadaManana);
            a.TipoLlegadaManianaId = a.TipoLlegadaManiana.IdTipo;
        }

        if (codigoTarde != null)
        {
            a.TipoTarde   = Tipo(codigoTarde);
            a.TipoTardeId = a.TipoTarde.IdTipo;
        }

        a.CalcularAsistencia(minTotalesM, minPerdidaIngresoM, minPerdidaSalidaM,
                             minTotalesT, minPerdidaIngresoT, minPerdidaSalidaT);
        return a.ValorTotalInasistencia;
    }

    // ── Casos del CSV: Mañana P ───────────────────────────────────────────────

    [Fact]
    public void P_P_Da_0()
        => Assert.Equal(0m, Calcular("P", "P"));

    [Fact]
    public void P_A_Da_05()
        => Assert.Equal(0.5m, Calcular("P", "A"));

    [Fact]
    public void P_LLT_tarde_Da_05()
        => Assert.Equal(0.5m, Calcular("P", "LLT"));

    [Fact]
    public void P_RA_tarde_Da_05()
        // RA en tarde → código distinto de P/RE/ANC → 0,5
        => Assert.Equal(0.5m, Calcular("P", "RA"));

    [Fact]
    public void P_RE_tarde_Da_0()
        => Assert.Equal(0m, Calcular("P", "RE"));

    [Fact]
    public void P_ANC_tarde_Da_0()
        => Assert.Equal(0m, Calcular("P", "ANC"));

    // ── Casos del CSV: Mañana A ───────────────────────────────────────────────

    [Fact]
    public void A_A_Da_1_no_15()
        // Bug previo: daba 1,5. El tope diario es 1.
        => Assert.Equal(1m, Calcular("A", "A"));

    [Fact]
    public void A_P_Da_1()
        => Assert.Equal(1m, Calcular("A", "P"));

    [Fact]
    public void A_LLT_tarde_Da_1()
        // Mañana ya es 1, tope: sigue siendo 1.
        => Assert.Equal(1m, Calcular("A", "LLT"));

    [Fact]
    public void A_RE_tarde_Da_1()
        => Assert.Equal(1m, Calcular("A", "RE"));

    // ── Casos del CSV: Llegadas tarde en mañana con tarde P ──────────────────

    [Fact]
    public void LLT_P_Da_025()
        => Assert.Equal(0.25m, Calcular("LLT", "P"));

    [Fact]
    public void LLTE_P_Da_05()
        => Assert.Equal(0.5m, Calcular("LLTE", "P"));

    [Fact]
    public void LLTC_P_Da_1()
        => Assert.Equal(1m, Calcular("LLTC", "P"));

    [Fact]
    public void RE_manana_P_Da_0()
        // RE en mañana: el alumno se retiró pero perdió ≤ 10% → sin inasistencia.
        // minPerdidaSalidaM = 15 sobre 200 = 7,5% → RE
        => Assert.Equal(0m, Calcular("RE", "P", minTotalesM: 200, minPerdidaSalidaM: 15));

    [Fact]
    public void RA_manana_P_Da_05()
        // RA en mañana: perdió 30% → 0,5 de inasistencia.
        => Assert.Equal(0.5m, Calcular("RA", "P", minTotalesM: 200, minPerdidaSalidaM: 60));

    [Fact]
    public void RAE_manana_P_Da_1()
        => Assert.Equal(1m, Calcular("RAE", "P"));

    // ── Casos del CSV: Llegadas tarde en mañana con tarde A ──────────────────

    [Fact]
    public void LLT_A_Da_075()
        => Assert.Equal(0.75m, Calcular("LLT", "A"));

    [Fact]
    public void LLTE_A_Da_1()
        // 0,5 (LLTE) + 0,5 (A tarde) = 1,0 → tope
        => Assert.Equal(1m, Calcular("LLTE", "A"));

    [Fact]
    public void LLTC_A_Da_1_no_15()
        // Bug previo: daba 1,5 (1,0 + 0,5). El tope diario es 1.
        => Assert.Equal(1m, Calcular("LLTC", "A"));

    [Fact]
    public void RE_manana_A_Da_05()
        => Assert.Equal(0.5m, Calcular("RE", "A", minTotalesM: 200, minPerdidaSalidaM: 15));

    [Fact]
    public void RA_manana_A_Da_1()
        // RA mañana (0,5) + A tarde (0,5) = 1,0
        => Assert.Equal(1m, Calcular("RA", "A", minTotalesM: 200, minPerdidaSalidaM: 60));

    [Fact]
    public void RAE_manana_A_Da_1_no_15()
        // Bug previo: daba 1,5. El tope diario es 1.
        => Assert.Equal(1m, Calcular("RAE", "A"));

    // ── Llegadas tarde en la tarde ────────────────────────────────────────────

    [Fact]
    public void P_LLTE_tarde_Da_05()
        // Bug previo: LLT/LLTE/LLTC en tarde no tenían HoraSalida → daban 0.
        => Assert.Equal(0.5m, Calcular("P", "LLTE"));

    [Fact]
    public void P_LLTC_tarde_Da_05()
        => Assert.Equal(0.5m, Calcular("P", "LLTC"));

    [Fact]
    public void P_RAE_tarde_Da_05()
        => Assert.Equal(0.5m, Calcular("P", "RAE"));

    // ── Tope diario de 1,0 ───────────────────────────────────────────────────

    [Fact]
    public void ValorNuncaSupera1()
    {
        // LLTC (1,0) + A tarde (0,5) = 1,5 sin tope → debe dar 1,0
        var valor = Calcular("LLTC", "A");
        Assert.True(valor <= 1.0m, $"El valor {valor} supera el máximo de 1,0");
    }

    // ── Escenario LLT + Retiro en mañana (TipoLlegadaManiana preservado) ─────

    [Fact]
    public void LLT_mas_RA_en_manana_Da_075()
    {
        // Escenario: el preceptor primero registra LLT (llegada tarde).
        // Luego el alumno se retira anticipadamente → TipoManiana se sobreescribe con RA.
        // TipoLlegadaManiana queda como LLT (preservado).
        // CalcularAsistencia debe sumar: LLT (0,25) + RA (30% → 0,5) = 0,75.
        var valor = Calcular(
            codigoManana:         "RA",
            codigoTarde:          "P",
            codigoLlegadaManana:  "LLT",
            minTotalesM:          200,
            minPerdidaSalidaM:    60);   // 30% → RA

        Assert.Equal(0.75m, valor);
    }

    [Fact]
    public void LLTC_mas_RE_en_manana_Da_1()
    {
        // Llegó con LLTC y se retiró de forma express (≤ 10%).
        // Sin el fix, TipoManiana = RE daba 0. Con el fix, TipoLlegadaManiana = LLTC → 1,0.
        var valor = Calcular(
            codigoManana:         "RE",
            codigoTarde:          "P",
            codigoLlegadaManana:  "LLTC",
            minTotalesM:          200,
            minPerdidaSalidaM:    15);   // 7,5% → RE

        Assert.Equal(1m, valor);
    }

    [Fact]
    public void LLT_mas_RAE_en_manana_Da_1()
    {
        // LLT (0,25) + RAE automático (perdió > 50%) = min(1, 0,25 + 1,0) = 1,0
        var valor = Calcular(
            codigoManana:         "RAE",
            codigoTarde:          "P",
            codigoLlegadaManana:  "LLT",
            minTotalesM:          200,
            minPerdidaSalidaM:    110);  // 55% → RAE auto

        Assert.Equal(1m, valor);
    }

    // ── Sin tipos definidos ───────────────────────────────────────────────────

    [Fact]
    public void SinTipos_Da_0()
        => Assert.Equal(0m, Calcular(null, null));

    [Fact]
    public void SoloManana_P_Da_0()
        => Assert.Equal(0m, Calcular("P", null));

    [Fact]
    public void SoloTarde_A_Da_05()
        => Assert.Equal(0.5m, Calcular(null, "A"));
}
