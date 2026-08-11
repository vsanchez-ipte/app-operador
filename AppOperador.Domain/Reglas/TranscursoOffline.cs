namespace AppOperador.Domain.Reglas;

/// <summary>
/// Cuánto tiempo ha pasado desde la última validación en línea, sin fiarse del reloj
/// del dispositivo (JTT-1383).
/// </summary>
/// <remarks>
/// <para>
/// El operador puede cambiar la hora del teléfono, y la ventana offline vale ocho horas de
/// trabajo autorizado: atrasar el reloj no puede regalarle más. Pero tampoco basta el
/// contador monotónico del sistema, porque se reinicia al apagar el equipo, y entonces un
/// reinicio devolvería la ventana entera.
/// </para>
/// <para>
/// <b>La regla es tomar el transcurso mayor de los dos.</b> Cada señal falla de una manera
/// distinta y ninguna de las dos falla hacia arriba:
/// </para>
/// <list type="bullet">
///   <item>
///     Si el reloj se atrasa, su transcurso sale corto o negativo, y manda el monotónico.
///   </item>
///   <item>
///     Si el equipo se reinicia, el contador monotónico arranca de cero y su transcurso
///     sale negativo, y manda el reloj.
///   </item>
/// </list>
/// <para>
/// Así, ni mover la hora ni reiniciar el dispositivo alargan la ventana: solo pueden
/// acortarla, que es el lado seguro. Cubre los criterios 5, 13 y 14.
/// </para>
/// <para>
/// <b>Si las dos señales fallan a la vez</b> —reloj atrasado y equipo reiniciado— no hay
/// forma de acotar el tiempo transcurrido, y entonces la medición no es determinable. El
/// criterio 15 impide dar la ventana por buena «por si acaso»: sin saber cuánto pasó, la
/// sesión no se reanuda y hay que autenticarse.
/// </para>
/// <para>
/// El tipo no consulta ningún reloj: todo entra por parámetro, para que la regla se pueda
/// comprobar.
/// </para>
/// </remarks>
public sealed class TranscursoOffline
{
	private TranscursoOffline(bool esDeterminable, TimeSpan transcurrido, bool relojRetrocedido)
	{
		EsDeterminable = esDeterminable;
		Transcurrido = transcurrido;
		RelojRetrocedido = relojRetrocedido;
	}

	/// <summary>
	/// Mide el tiempo pasado desde la validación combinando las dos señales.
	/// </summary>
	/// <param name="validadoUtc">Instante de la última validación en línea, en UTC.</param>
	/// <param name="ahoraUtc">Instante actual según el reloj del dispositivo, en UTC.</param>
	/// <param name="monotonicoAlValidar">
	/// Lectura del contador monotónico en el momento de validar. Cuenta desde el último
	/// arranque del equipo y no lo afecta cambiar la hora.
	/// </param>
	/// <param name="monotonicoAhora">Lectura actual del mismo contador.</param>
	/// <exception cref="ArgumentException">Algún instante no está expresado en UTC.</exception>
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

		// Negativo significa que esa señal ya no sirve para medir: el reloj se movió hacia
		// atrás, o el contador se reinició con el equipo.
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

	/// <summary>
	/// Indica si se pudo acotar el tiempo transcurrido.
	/// </summary>
	/// <remarks>
	/// Falso solo cuando las dos señales fallaron. No es un error técnico: es una sesión que
	/// no se puede reanudar con garantías.
	/// </remarks>
	public bool EsDeterminable { get; }

	/// <summary>
	/// Tiempo transcurrido desde la validación. <see cref="TimeSpan.Zero"/> si no es
	/// determinable.
	/// </summary>
	public TimeSpan Transcurrido { get; }

	/// <summary>
	/// Indica que el reloj del dispositivo quedó por detrás de la validación.
	/// </summary>
	/// <remarks>
	/// No bloquea por sí solo —el monotónico cubre el hueco— pero conviene dejarlo en la
	/// bitácora: es la señal de que alguien movió la hora.
	/// </remarks>
	public bool RelojRetrocedido { get; }

	/// <summary>Indica si lo transcurrido cabe dentro de la ventana indicada.</summary>
	public bool CabeEn(TimeSpan ventana) => EsDeterminable && Transcurrido < ventana;

	/// <summary>Lo que resta de la ventana, o cero si venció o no es determinable.</summary>
	public TimeSpan RestanteDe(TimeSpan ventana)
	{
		if (!CabeEn(ventana))
		{
			return TimeSpan.Zero;
		}

		return ventana - Transcurrido;
	}
}
