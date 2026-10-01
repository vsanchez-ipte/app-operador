using AppOperador.Aplicacion.Modelos;
using AppOperador.Domain.Enums;

namespace AppOperador.UnitTests.Application;

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
		// En cuanto Jacob confirma, la referencia que vale es la suya.
		var registro = Registro("INC-APK-2026-0034", EstadoSincronizacion.Sincronizado);

		Assert.Equal("INC-APK-2026-0034", registro.ReferenciaPrincipal);
		Assert.True(registro.TieneFolio);
	}

	[Fact]
	public void ConFolio_laClaveLocalSigueDisponible()
	{
		// Sustituir no es borrar: la clave local es la única referencia común con la base y la bitácora.
		var registro = Registro("INC-APK-2026-0034", EstadoSincronizacion.Sincronizado);

		Assert.Equal("LOC-000123", registro.ClaveLocal);
	}

	[Theory]
	[InlineData("")]
	[InlineData("   ")]
	public void UnFolioEnBlanco_noCuentaComoFolio(string folio)
	{
		// Un folio vacío dejaría la tarjeta encabezada por un renglón en blanco.
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
		// El formato del folio lo fija Jacob y puede cambiar sin avisar.
		var registro = Registro(folio, EstadoSincronizacion.Sincronizado);

		Assert.Equal(folio, registro.ReferenciaPrincipal);
	}

	// ── La severidad no es la prioridad de sincronización ────────────────────────────

	[Theory]
	[InlineData("Advertencia")]
	[InlineData("Información")]
	[InlineData("Normal")]
	public void CadaSeveridadSeMuestraTalCualEsAunqueSuPrioridadSeaNormal(string severidad)
	{
		// Las tres comparten prioridad Normal y son severidades distintas.
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
		// Vacío se leería como error de carga, y «Normal» afirmaría algo que el operador no eligió.
		var registro = Registro(null, EstadoSincronizacion.Borrador) with { Severidad = severidad };

		Assert.Equal("Sin severidad", registro.SeveridadLegible);
	}

	[Fact]
	public void LaSeveridadSeMuestraSinEspaciosDeSobra()
	{
		var registro = Registro(null, EstadoSincronizacion.Pendiente) with { Severidad = "  Advertencia " };

		Assert.Equal("Advertencia", registro.SeveridadLegible);
	}

	// ── Por qué no salió este registro ────────────────────────────────────────────────

	[Theory]
	[InlineData(EstadoSincronizacion.Pendiente)]
	[InlineData(EstadoSincronizacion.Sincronizado)]
	[InlineData(EstadoSincronizacion.Enviando)]
	[InlineData(EstadoSincronizacion.Borrador)]
	public void SoloLosFallidosExplicanNadaMas(EstadoSincronizacion estado)
	{
		// Un pendiente arrastra el código del intento anterior y no por eso va mal.
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
		// Es el único caso en que esperar no sirve: reenviarlo daría el mismo rechazo.
		var registro = Fallida("appincidencias.nota.requerida", "La nota es obligatoria para Otro.");

		Assert.True(registro.HayMotivoFallo);
		Assert.Equal(
			"El CCO la rechazó: La nota es obligatoria para Otro. Corríjala: no saldrá sola.",
			registro.MotivoFallo);
	}

	[Fact]
	public void UnFalloTecnicoNoMandaCorregirNada()
	{
		// El registro está bien; lo que falló fue el camino.
		var registro = Fallida(
			CodigosErrorJacob.ErrorTecnico, "No se pudo completar el envío.");

		Assert.Equal("No llegó al CCO: No se pudo completar el envío.", registro.MotivoFallo);
	}

	[Fact]
	public void SinMensaje_seMuestraElCodigo()
	{
		// Feo, pero más útil que «falló»: es lo que se dicta por radio al CCO.
		var registro = Fallida("appincidencias.km.fueradecorredor", null);

		Assert.Equal(
			"El CCO la rechazó: código appincidencias.km.fueradecorredor. Corríjala: no saldrá sola.",
			registro.MotivoFallo);
	}

    [Fact]
    public void SinMensajeNiCodigo_seDiceQueNoSeSabe()
    {
        // Lo guardado antes de registrar intentos no trae motivo.
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

	// ── Cuándo toca el próximo intento ────────────────────────────────────────────────

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
		// La espera crece: «en un minuto» sería falso a partir del segundo fallo.
		var ultimoIntento = new DateTime(2026, 9, 1, 17, 0, 0, DateTimeKind.Utc);
		var registro = Tecnica(intentos, ultimoIntento);

		Assert.Equal(ultimoIntento.AddMinutes(minutosEsperados), registro.ReintentoUtc);
	}

	[Fact]
	public void UnRechazoFuncionalNoAnunciaReintento()
	{
		// No se reintenta hasta que alguien lo corrija.
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
		// Sin fecha del último intento, callar es mejor que anunciar una hora inventada.
		var registro = Fallida(CodigosErrorJacob.ErrorTecnico, "No respondio.") with
		{
			Intentos = 1,
			UltimoIntentoUtc = null,
		};

		Assert.False(registro.HayReintentoProgramado);
		Assert.Null(registro.ReintentoUtc);
	}

	[Fact]
	public void UnPendiente_tocaIntentarlo()
	{
		var registro = Registro(folio: null, EstadoSincronizacion.Pendiente);

		Assert.True(registro.TocaIntentarlo(DateTime.UtcNow));
	}

	[Fact]
	public void UnEnvioAMedias_tocaIntentarlo()
	{
		// Enviando solo lo recoge una tanda; si el reloj de la Cola no lo cuenta, se queda ahí.
		var registro = Registro(folio: null, EstadoSincronizacion.Enviando);

		Assert.True(registro.TocaIntentarlo(DateTime.UtcNow));
	}

	[Fact]
	public void UnFallidoTecnicoConLaEsperaVencida_tocaIntentarlo()
	{
		var ahora = new DateTime(2026, 9, 4, 10, 0, 0, DateTimeKind.Utc);
		var registro = Tecnica(intentos: 1, ultimoIntentoUtc: ahora.AddMinutes(-5));

		Assert.True(registro.TocaIntentarlo(ahora));
	}

	[Fact]
	public void UnFallidoTecnicoConLaEsperaCorriendo_noTocaTodavia()
	{
		var ahora = new DateTime(2026, 9, 4, 10, 0, 0, DateTimeKind.Utc);
		var registro = Tecnica(intentos: 1, ultimoIntentoUtc: ahora.AddSeconds(-10));

		Assert.False(registro.TocaIntentarlo(ahora));
	}

	[Fact]
	public void UnRechazoFuncional_noTocaNuncaAunqueLleveHorasAhi()
	{
		// No va a salir hasta que lo corrijan: contarlo haría sondear el enlace para siempre.
		var registro = Fallida("appincidencias.km.fueradecorredor", "Kilometro fuera del corredor.")
			with
			{
				Intentos = 3,
				UltimoIntentoUtc = new DateTime(2026, 9, 4, 6, 0, 0, DateTimeKind.Utc),
			};

		Assert.False(registro.TocaIntentarlo(new DateTime(2026, 9, 4, 10, 0, 0, DateTimeKind.Utc)));
	}

	[Fact]
	public void UnRegistroYaSincronizado_noTocaIntentarlo()
	{
		var registro = Registro("INC-APK-2026-0034", EstadoSincronizacion.Sincronizado);

		Assert.False(registro.TocaIntentarlo(DateTime.UtcNow));
	}

	private static readonly DateTime Las10 = new(2026, 9, 25, 10, 0, 0, DateTimeKind.Utc);

	[Fact]
	public void UnaSincronizadaConEvidenciaAtorada_loDiceEnLaTarjeta()
	{
		var registro = Registro("INC-APK-2026-0015", EstadoSincronizacion.Sincronizado) with
		{
			EvidenciasSinEnviar = 2,
			ReintentoEvidenciaUtc = Las10,
		};

		Assert.True(registro.HayEvidenciaSinEnviar);
		Assert.Equal("2 evidencias pendientes de enviar al CCO.", registro.AvisoEvidencia);
	}

	[Fact]
	public void LaTarjetaSoloDiceQueEstaPendiente_niElMotivoNiUnReintento()
	{
		var registro = Registro("INC-APK-2026-0015", EstadoSincronizacion.Sincronizado) with
		{
			EvidenciasSinEnviar = 1,
			ReintentoEvidenciaUtc = null,
		};

		Assert.Equal("1 evidencia pendiente de enviar al CCO.", registro.AvisoEvidencia);
	}

	[Fact]
	public void SinEvidenciaAtorada_laTarjetaNoDiceNada()
	{
		var registro = Registro("INC-APK-2026-0015", EstadoSincronizacion.Sincronizado);

		Assert.False(registro.HayEvidenciaSinEnviar);
		Assert.Equal(string.Empty, registro.AvisoEvidencia);
	}

	[Fact]
	public void UnaNoSincronizada_noAvisaDeSuEvidencia()
	{
		var registro = Registro(folio: null, EstadoSincronizacion.Pendiente) with
		{
			EvidenciasSinEnviar = 1,
			ReintentoEvidenciaUtc = Las10,
		};

		Assert.False(registro.HayEvidenciaSinEnviar);
	}

	[Fact]
	public void UnaSincronizadaConEvidenciaQueYaToca_despiertaAlRelojDeLaCola()
	{
		var registro = Registro("INC-APK-2026-0015", EstadoSincronizacion.Sincronizado) with
		{
			EvidenciasSinEnviar = 1,
			ReintentoEvidenciaUtc = Las10,
		};

		Assert.True(registro.TocaIntentarlo(Las10));
		Assert.False(registro.TocaIntentarlo(Las10.AddMinutes(-1)));
	}

	[Fact]
	public void UnaSincronizadaConEvidenciaRechazada_noDespiertaAlReloj()
	{
		var registro = Registro("INC-APK-2026-0015", EstadoSincronizacion.Sincronizado) with
		{
			EvidenciasSinEnviar = 1,
			ReintentoEvidenciaUtc = null,
		};

		Assert.False(registro.TocaIntentarlo(Las10.AddDays(1)));
	}

	private static RegistroCola Tecnica(int intentos, DateTime ultimoIntentoUtc) =>
		Fallida(CodigosErrorJacob.ErrorTecnico, "No respondio.") with
		{
			Intentos = intentos,
			UltimoIntentoUtc = ultimoIntentoUtc,
		};

	// ---------- Corregir un rechazo ----------

	[Fact]
	public void UnRechazoFuncionalSePuedeCorregir()
	{
		// Es el que no saldrá solo: la tarjeta dice «Corríjala», y tiene que haber dónde.
		var registro = Fallida("appincidencias.km.fueradecorredor", "Kilometro fuera del corredor.");

		Assert.True(registro.SePuedeCorregir);
	}

	[Fact]
	public void UnFalloTecnicoNoOfreceCorregir()
	{
		// Se reintenta solo: ofrecer «Corregir» contradiría la hora del reintento.
		var registro = Fallida(CodigosErrorJacob.ErrorTecnico, "No respondio.");

		Assert.False(registro.SePuedeCorregir);
	}

	[Theory]
	[InlineData(EstadoSincronizacion.Pendiente)]
	[InlineData(EstadoSincronizacion.Enviando)]
	[InlineData(EstadoSincronizacion.Sincronizado)]
	[InlineData(EstadoSincronizacion.Borrador)]
	public void SoloUnFallidoSePuedeCorregir(EstadoSincronizacion estado)
	{
		// Un pendiente arrastra el código del intento anterior y aun así está en camino.
		var registro = Registro(null, estado) with
		{
			UltimoErrorCodigo = "appincidencias.km.fueradecorredor",
		};

		Assert.False(registro.SePuedeCorregir);
	}

	// ---------- La hora de captura ----------

	[Fact]
	public void LaHoraDeCapturaViajaEnElRegistro()
	{
		var capturada = new DateTime(2026, 9, 14, 15, 30, 0, DateTimeKind.Utc);
		var registro = Registro(null, EstadoSincronizacion.Pendiente) with { CapturadaUtc = capturada };

		Assert.Equal(capturada, registro.CapturadaUtc);
	}

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
