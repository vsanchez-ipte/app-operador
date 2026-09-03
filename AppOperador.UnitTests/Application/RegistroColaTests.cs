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

	// ── Por que no salio ESTE registro ────────────────────────────────────────────────

	[Theory]
	[InlineData(EstadoSincronizacion.Pendiente)]
	[InlineData(EstadoSincronizacion.Sincronizado)]
	[InlineData(EstadoSincronizacion.Enviando)]
	[InlineData(EstadoSincronizacion.Borrador)]
	public void SoloLosFallidosExplicanNadaMas(EstadoSincronizacion estado)
	{
		// Un pendiente arrastra el codigo del intento anterior: al recuperar un envio
		// interrumpido vuelve a Pendiente sin borrarlo. Ensenar ahi un rechazo ya superado
		// diria que algo va mal cuando el registro esta en camino.
		var registro = Registro(null, estado) with
		{
			UltimoErrorCodigo = "appincidencias.km.fueradecorredor",
			UltimoErrorMensaje = "El kilometro no pertenece al corredor.",
		};

		Assert.False(registro.HayMotivoFallo);
	}

	[Fact]
	public void UnRechazoFuncionalDiceQueHayQueCorregirlo()
	{
		// Es el unico caso en que el operador tiene que actuar, y el unico en que esperar no
		// sirve de nada: reenviarlo daria el mismo rechazo.
		var registro = Fallida("appincidencias.nota.requerida", "La nota es obligatoria para Otro.");

		Assert.True(registro.HayMotivoFallo);
		Assert.Equal(
			"El CCO la rechazó: La nota es obligatoria para Otro. Corríjala: no saldrá sola.",
			registro.MotivoFallo);
	}

	[Fact]
	public void UnFalloTecnicoNoMandaCorregirNada()
	{
		// El registro esta bien; lo que fallo fue el camino. Decirle que lo corrija lo mandaria
		// a revisar una captura correcta.
		var registro = Fallida(
			CodigosErrorJacob.ErrorTecnico, "No se pudo completar el envío.");

		Assert.Equal("No llegó al CCO: No se pudo completar el envío.", registro.MotivoFallo);
	}

	[Fact]
	public void SinMensaje_seMuestraElCodigo()
	{
		// Feo, pero infinitamente mas util que "fallo": es lo que se dicta por radio al CCO.
		var registro = Fallida("appincidencias.km.fueradecorredor", null);

		Assert.Equal(
			"El CCO la rechazó: código appincidencias.km.fueradecorredor. Corríjala: no saldrá sola.",
			registro.MotivoFallo);
	}

    [Fact]
    public void SinMensajeNiCodigo_seDiceQueNoSeSabe()
    {
        // Pasa con lo guardado antes de que se registraran los intentos. Callar dejaria la
        // tarjeta igual que antes del arreglo.
        var registro = Fallida(null, null);

        Assert.Equal("No llegó al CCO: no se registró el motivo.", registro.MotivoFallo);
    }

	[Fact]
	public void ElMensajeDeJacobSeCierraConPuntoSiNoLoTrae()
	{
		// El motivo se concatena con lo que sigue: sin punto, las dos frases se leen como una.
		var registro = Fallida("appincidencias.nota.requerida", "  La nota es obligatoria  ");

		Assert.Equal(
			"El CCO la rechazó: La nota es obligatoria. Corríjala: no saldrá sola.",
			registro.MotivoFallo);
	}

	// ── Cuando toca el proximo intento ────────────────────────────────────────────────

	[Theory]
	[InlineData(1, 1)]
	[InlineData(2, 2)]
	[InlineData(3, 4)]
	[InlineData(4, 8)]
	[InlineData(5, 16)]
	[InlineData(6, 30)]
	[InlineData(12, 30)]
	public void LaHoraDelReintentoSigueLaEsperaCreciente(int intentos, int minutosEsperados)
	{
		// "Se reintentara en un minuto" seria falso a partir del segundo fallo, y es justo cuando
		// el operador se pregunta si la aplicacion sigue intentando algo.
		var ultimoIntento = new DateTime(2026, 9, 1, 17, 0, 0, DateTimeKind.Utc);
		var registro = Tecnica(intentos, ultimoIntento);

		Assert.Equal(ultimoIntento.AddMinutes(minutosEsperados), registro.ReintentoUtc);
	}

	[Fact]
	public void UnRechazoFuncionalNoAnunciaReintento()
	{
		// No se reintenta hasta que alguien lo corrija: darle una hora seria prometer algo que no
		// va a pasar.
		var registro = Fallida("appincidencias.nota.requerida", "Falta la nota.") with
		{
			Intentos = 1,
			UltimoIntentoUtc = new DateTime(2026, 9, 1, 17, 0, 0, DateTimeKind.Utc),
		};

		Assert.False(registro.HayReintentoProgramado);
		Assert.Null(registro.ReintentoUtc);
	}

	[Theory]
	[InlineData(EstadoSincronizacion.Pendiente)]
	[InlineData(EstadoSincronizacion.Sincronizado)]
	[InlineData(EstadoSincronizacion.Enviando)]
	[InlineData(EstadoSincronizacion.Borrador)]
	public void SoloLosFallidosAnuncianReintento(EstadoSincronizacion estado)
	{
		// Un pendiente no espera nada: sale en el instante en que vuelve el enlace.
		var registro = Registro(null, estado) with
		{
			Intentos = 2,
			UltimoIntentoUtc = new DateTime(2026, 9, 1, 17, 0, 0, DateTimeKind.Utc),
		};

		Assert.False(registro.HayReintentoProgramado);
	}

	[Fact]
	public void SinFechaDeUltimoIntentoNoSeInventaUnaHora()
	{
		// Pasa con lo guardado antes de que se registrara la fecha. Callar es mejor que anunciar
		// una hora calculada sobre un dato que no existe.
		var registro = Fallida(CodigosErrorJacob.ErrorTecnico, "No respondio.") with
		{
			Intentos = 1,
			UltimoIntentoUtc = null,
		};

		Assert.False(registro.HayReintentoProgramado);
		Assert.Null(registro.ReintentoUtc);
	}

	private static RegistroCola Tecnica(int intentos, DateTime ultimoIntentoUtc) =>
		Fallida(CodigosErrorJacob.ErrorTecnico, "No respondio.") with
		{
			Intentos = intentos,
			UltimoIntentoUtc = ultimoIntentoUtc,
		};

	private static RegistroCola Fallida(string? codigo, string? mensaje) =>
		Registro(null, EstadoSincronizacion.Fallido) with
		{
			UltimoErrorCodigo = codigo,
			UltimoErrorMensaje = mensaje,
		};

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
