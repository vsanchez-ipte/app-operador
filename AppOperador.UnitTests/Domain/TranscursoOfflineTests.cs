using AppOperador.Domain.Reglas;

namespace AppOperador.UnitTests.Domain;

/// <summary>
/// Medir el tiempo sin fiarse del reloj del dispositivo (JTT-1383 CA 5, 13, 14 y 15).
/// </summary>
public class TranscursoOfflineTests
{
	private static readonly DateTime Validacion = new(2026, 8, 11, 8, 0, 0, DateTimeKind.Utc);
	private static readonly TimeSpan MonotonicoAlValidar = TimeSpan.FromHours(3);
	private static readonly TimeSpan Ventana = TimeSpan.FromHours(8);

	private static TranscursoOffline Medir(
		TimeSpan avanceDelReloj,
		TimeSpan avanceMonotonico) =>
		TranscursoOffline.Medir(
			Validacion,
			Validacion + avanceDelReloj,
			MonotonicoAlValidar,
			MonotonicoAlValidar + avanceMonotonico);

	// ---------- Camino normal ----------

	[Fact]
	public void Con_las_dos_señales_de_acuerdo_mide_lo_que_paso()
	{
		var transcurso = Medir(TimeSpan.FromHours(2), TimeSpan.FromHours(2));

		Assert.True(transcurso.EsDeterminable);
		Assert.Equal(TimeSpan.FromHours(2), transcurso.Transcurrido);
		Assert.False(transcurso.RelojRetrocedido);
	}

	[Fact]
	public void Dentro_de_la_ventana_todavia_cabe()
	{
		Assert.True(Medir(TimeSpan.FromHours(7), TimeSpan.FromHours(7)).CabeEn(Ventana));
	}

	[Fact]
	public void Pasada_la_ventana_ya_no_cabe()
	{
		Assert.False(Medir(TimeSpan.FromHours(9), TimeSpan.FromHours(9)).CabeEn(Ventana));
	}

	[Fact]
	public void Justo_en_el_limite_la_ventana_ya_vencio()
	{
		Assert.False(Medir(Ventana, Ventana).CabeEn(Ventana));
	}

	// ---------- CA 5 y 13: mover el reloj no alarga la ventana ----------

	[Fact]
	public void Atrasar_el_reloj_no_devuelve_tiempo()
	{
		// El operador atrasa el telefono seis horas para seguir trabajando. El contador
		// monotonico no le hace caso: han pasado siete horas y son las que cuentan.
		var transcurso = Medir(TimeSpan.FromHours(-6), TimeSpan.FromHours(7));

		Assert.True(transcurso.EsDeterminable);
		Assert.Equal(TimeSpan.FromHours(7), transcurso.Transcurrido);
		Assert.True(transcurso.RelojRetrocedido);
	}

	[Fact]
	public void Atrasar_el_reloj_no_reabre_una_ventana_vencida()
	{
		var transcurso = Medir(TimeSpan.FromHours(-20), TimeSpan.FromHours(9));

		Assert.False(transcurso.CabeEn(Ventana));
	}

	[Fact]
	public void Entre_las_dos_señales_manda_la_que_marca_mas_tiempo()
	{
		// Nunca la que conviene: siempre la mayor, que es el lado seguro.
		var transcurso = Medir(TimeSpan.FromHours(5), TimeSpan.FromHours(2));

		Assert.Equal(TimeSpan.FromHours(5), transcurso.Transcurrido);
	}

	// ---------- CA 14: reiniciar el dispositivo no reinicia la ventana ----------

	[Fact]
	public void Reiniciar_el_equipo_no_devuelve_la_ventana()
	{
		// Tras el reinicio el contador arranca de cero, asi que su transcurso sale
		// negativo. El reloj sigue sirviendo y es el que manda.
		var transcurso = TranscursoOffline.Medir(
			Validacion,
			Validacion + TimeSpan.FromHours(6),
			MonotonicoAlValidar,
			TimeSpan.FromMinutes(2));

		Assert.True(transcurso.EsDeterminable);
		Assert.Equal(TimeSpan.FromHours(6), transcurso.Transcurrido);
	}

	[Fact]
	public void Reiniciar_el_equipo_no_reabre_una_ventana_vencida()
	{
		var transcurso = TranscursoOffline.Medir(
			Validacion,
			Validacion + TimeSpan.FromHours(10),
			MonotonicoAlValidar,
			TimeSpan.FromMinutes(2));

		Assert.False(transcurso.CabeEn(Ventana));
	}

	// ---------- CA 15: sin poder medir, no se da la ventana por buena ----------

	[Fact]
	public void Si_fallan_las_dos_señales_el_transcurso_no_es_determinable()
	{
		// Reloj atrasado y equipo reiniciado a la vez: no queda forma de acotar el tiempo.
		var transcurso = TranscursoOffline.Medir(
			Validacion,
			Validacion - TimeSpan.FromHours(3),
			MonotonicoAlValidar,
			TimeSpan.FromMinutes(1));

		Assert.False(transcurso.EsDeterminable);
	}

	[Fact]
	public void Un_transcurso_no_determinable_nunca_cabe_en_la_ventana()
	{
		// Es el punto del criterio: ante la duda no se regala la sesion.
		var transcurso = TranscursoOffline.Medir(
			Validacion,
			Validacion - TimeSpan.FromHours(3),
			MonotonicoAlValidar,
			TimeSpan.FromMinutes(1));

		Assert.False(transcurso.CabeEn(Ventana));
		Assert.Equal(TimeSpan.Zero, transcurso.RestanteDe(Ventana));
	}

	// ---------- Restante ----------

	[Fact]
	public void Informa_cuanto_queda_de_ventana()
	{
		var transcurso = Medir(TimeSpan.FromHours(3), TimeSpan.FromHours(3));

		Assert.Equal(TimeSpan.FromHours(5), transcurso.RestanteDe(Ventana));
	}

	[Fact]
	public void Una_ventana_agotada_no_deja_restante()
	{
		Assert.Equal(TimeSpan.Zero, Medir(TimeSpan.FromHours(12), TimeSpan.FromHours(12)).RestanteDe(Ventana));
	}

	// ---------- Contrato ----------

	[Fact]
	public void Exige_instantes_en_UTC()
	{
		var local = new DateTime(2026, 8, 11, 8, 0, 0, DateTimeKind.Local);

		Assert.Throws<ArgumentException>(() =>
			TranscursoOffline.Medir(local, Validacion, TimeSpan.Zero, TimeSpan.Zero));
		Assert.Throws<ArgumentException>(() =>
			TranscursoOffline.Medir(Validacion, local, TimeSpan.Zero, TimeSpan.Zero));
	}
}
