using AppOperador.Aplicacion.Modelos;
using AppOperador.Domain.Enums;

namespace AppOperador.UnitTests.Application;

/// <summary>
/// Cuál de los dos identificadores encabeza un registro de la cola (JTT-1403 CA 1 y 2).
/// </summary>
/// <remarks>
/// La decisión vive en el modelo de aplicación y no en <c>RegistroColaVista</c> justamente para
/// poder escribir estas pruebas: <c>AppOperador.Mobile</c> es multi-destino y el proyecto de
/// pruebas no puede cargarlo.
/// </remarks>
public class RegistroColaTests
{
	[Fact]
	public void SinFolio_laReferenciaPrincipalEsLaClaveLocal()
	{
		var registro = Registro(folio: null, EstadoSincronizacion.Pendiente);

		Assert.Equal("LOC-000123", registro.ReferenciaPrincipal);
		Assert.False(registro.TieneFolio);
	}

	[Fact]
	public void ConFolio_elFolioSustituyeALaClaveLocalComoReferencia()
	{
		// CA 1: en cuanto Jacob confirma, la referencia que vale es la suya.
		var registro = Registro("INC-APK-2026-0034", EstadoSincronizacion.Sincronizado);

		Assert.Equal("INC-APK-2026-0034", registro.ReferenciaPrincipal);
		Assert.True(registro.TieneFolio);
	}

	[Fact]
	public void ConFolio_laClaveLocalSigueDisponible()
	{
		// CA 2: sustituir no es borrar. La clave local es la única referencia común con la base
		// del dispositivo y con la bitácora local; sin ella el registro deja de rastrearse.
		var registro = Registro("INC-APK-2026-0034", EstadoSincronizacion.Sincronizado);

		Assert.Equal("LOC-000123", registro.ClaveLocal);
	}

	[Theory]
	[InlineData("")]
	[InlineData("   ")]
	public void UnFolioEnBlanco_noCuentaComoFolio(string folio)
	{
		// Un folio vacío escrito por un mapeo descuidado dejaría la lista encabezada por un
		// renglón en blanco, que se lee como error de carga y no como registro pendiente.
		var registro = Registro(folio, EstadoSincronizacion.Sincronizado);

		Assert.False(registro.TieneFolio);
		Assert.Equal("LOC-000123", registro.ReferenciaPrincipal);
	}

	[Theory]
	[InlineData("INC-APK-2026-0034")]
	[InlineData("INC-APP-0034")]
	[InlineData("cualquier-cosa-que-mande-el-servidor")]
	public void ElFolioSeMuestraTalCual_seaCualSeaSuFormato(string folio)
	{
		// La app no interpreta el folio: el formato lo fija Jacob y puede cambiar sin avisar.
		// Validarlo aquí dejaría de mostrar folios buenos el día que el servidor los cambie.
		var registro = Registro(folio, EstadoSincronizacion.Sincronizado);

		Assert.Equal(folio, registro.ReferenciaPrincipal);
	}

	// ── La severidad no es la prioridad de sincronizacion (31-ago) ───────────────────

	[Theory]
	[InlineData("Advertencia")]
	[InlineData("Información")]
	[InlineData("Normal")]
	public void CadaSeveridadSeMuestraTalCualEsAunqueSuPrioridadSeaNormal(string severidad)
	{
		// El defecto: la pantalla mostraba la PRIORIDAD rotulada como severidad, y la prioridad
		// solo tiene dos valores. Las tres de aqui comparten prioridad Normal y son distintas.
		var registro = Registro(null, EstadoSincronizacion.Pendiente) with
		{
			Prioridad = SyncPriority.Normal,
			Severidad = severidad,
		};

		Assert.Equal(severidad, registro.SeveridadLegible);
	}

	[Fact]
	public void UnaSeveridadCriticaNoDependeDeLaPrioridadParaMostrarse()
	{
		var registro = Registro(null, EstadoSincronizacion.Pendiente) with
		{
			Prioridad = SyncPriority.Critica,
			Severidad = "Crítica",
		};

		Assert.Equal("Crítica", registro.SeveridadLegible);
	}

	[Theory]
	[InlineData(null)]
	[InlineData("")]
	[InlineData("   ")]
	public void UnBorradorSinSeveridadLoDiceEnVezDeInventarUna(string? severidad)
	{
		// Dejarlo vacio se leeria como error de carga, y poner "Normal" seria afirmar algo que
		// el operador no eligio.
		var registro = Registro(null, EstadoSincronizacion.Borrador) with { Severidad = severidad };

		Assert.Equal("Sin severidad", registro.SeveridadLegible);
	}

	[Fact]
	public void LaSeveridadSeMuestraSinEspaciosDeSobra()
	{
		var registro = Registro(null, EstadoSincronizacion.Pendiente) with { Severidad = "  Advertencia " };

		Assert.Equal("Advertencia", registro.SeveridadLegible);
	}

	private static RegistroCola Registro(string? folio, EstadoSincronizacion estado) => new(
		"LOC-000123",
		ClaseRegistro.Incidencia,
		SyncPriority.Normal,
		"Objeto en camino",
		"130+200",
		estado,
		folio,
		"Normal");
}
