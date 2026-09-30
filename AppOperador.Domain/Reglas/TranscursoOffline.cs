namespace AppOperador.Domain.Reglas;

// Manda el mayor entre el reloj y el contador monotónico: ni mover la hora ni reiniciar alargan la ventana.
public sealed class TranscursoOffline
{
	private TranscursoOffline(bool esDeterminable, TimeSpan transcurrido, bool relojRetrocedido)
	{
		EsDeterminable = esDeterminable;
		Transcurrido = transcurrido;
		RelojRetrocedido = relojRetrocedido;
	}

	public static TranscursoOffline Medir(
		DateTime validadoUtc,
		DateTime ahoraUtc,
		TimeSpan monotonicoAlValidar,
		TimeSpan monotonicoAhora)
	{
		InstanteUtc.Exigir(validadoUtc, nameof(validadoUtc));
		InstanteUtc.Exigir(ahoraUtc, nameof(ahoraUtc));

		var porReloj = ahoraUtc - validadoUtc;
		var porMonotonico = monotonicoAhora - monotonicoAlValidar;

		// Negativo: el reloj se atrasó o el contador se reinició con el equipo.
		var relojSirve = porReloj >= TimeSpan.Zero;
		var monotonicoSirve = porMonotonico >= TimeSpan.Zero;

		if (!relojSirve && !monotonicoSirve)
		{
			return new TranscursoOffline(false, TimeSpan.Zero, relojRetrocedido: true);
		}

		var transcurrido = (relojSirve, monotonicoSirve) switch
		{
			(true, true) => porReloj > porMonotonico ? porReloj : porMonotonico,
			(true, false) => porReloj,
			_ => porMonotonico,
		};

		return new TranscursoOffline(true, transcurrido, relojRetrocedido: !relojSirve);
	}

	// Falso solo si fallan las dos señales; entonces la sesión no se reanuda.
	public bool EsDeterminable { get; }

	public TimeSpan Transcurrido { get; }

	// No bloquea por sí solo, pero es la señal de que alguien movió la hora.
	public bool RelojRetrocedido { get; }

	public bool CabeEn(TimeSpan ventana) => EsDeterminable && Transcurrido < ventana;

	public TimeSpan RestanteDe(TimeSpan ventana)
	{
		if (!CabeEn(ventana))
		{
			return TimeSpan.Zero;
		}

		return ventana - Transcurrido;
	}
}
