namespace AppOperador.Domain.Reglas;

/// <summary>
/// Cuánto hay que esperar antes de volver a intentar un envío fallido (JTT-1401 CA 7).
/// </summary>
/// <remarks>
/// <para>
/// Función pura del dominio, como <see cref="ReglaPrioridadSincronizacion"/>: recibe el número
/// de intentos y devuelve la espera. No conoce relojes ni registros.
/// </para>
/// <para>
/// <b>La espera se cuenta desde el último intento y el contador está persistido</b>, así que
/// cerrar la app no la reinicia. Un contador en memoria convertiría «espera creciente» en «espera
/// de un minuto», porque en campo la app se cierra y se reabre todo el tiempo.
/// </para>
/// </remarks>
public static class ReglaEsperaReintento
{
	/// <summary>Espera tras el primer fallo.</summary>
	public static readonly TimeSpan EsperaInicial = TimeSpan.FromMinutes(1);

	/// <summary>
	/// Tope de la espera, por larga que se ponga la racha.
	/// </summary>
	/// <remarks>
	/// Sin tope, doce fallos seguidos —un turno entero sin cobertura, que en carretera es
	/// normal— dejarían la siguiente espera en más de un día: al recuperar la señal, lo
	/// capturado no saldría hasta la jornada siguiente.
	/// </remarks>
	public static readonly TimeSpan EsperaMaxima = TimeSpan.FromMinutes(30);

	/// <summary>
	/// Espera que corresponde a un registro que ya falló <paramref name="intentos"/> veces.
	/// </summary>
	/// <param name="intentos">
	/// Envíos ya intentados sobre ese registro. Cero o uno dan la espera inicial.
	/// </param>
	/// <remarks>
	/// Duplica en cada fallo —1, 2, 4, 8, 16 minutos— y se detiene en
	/// <see cref="EsperaMaxima"/>. Se calcula por desplazamiento y no con potencias para que una
	/// racha larga no desborde antes de llegar al tope.
	/// </remarks>
	public static TimeSpan Para(int intentos)
	{
		if (intentos <= 1)
		{
			return EsperaInicial;
		}

		// A partir del sexto fallo la duplicación ya supera el tope, así que no hace falta
		// seguir calculando: se corta aquí y de paso se evita desbordar el desplazamiento.
		const int IntentosHastaElTope = 6;
		if (intentos >= IntentosHastaElTope)
		{
			return EsperaMaxima;
		}

		var minutos = EsperaInicial.TotalMinutes * (1 << (intentos - 1));
		var espera = TimeSpan.FromMinutes(minutos);

		return espera > EsperaMaxima ? EsperaMaxima : espera;
	}

	/// <summary>
	/// Indica si un registro que falló ya puede volver a intentarse.
	/// </summary>
	/// <param name="intentos">Envíos ya intentados.</param>
	/// <param name="transcurrido">Tiempo desde el último intento.</param>
	public static bool YaPuedeReintentarse(int intentos, TimeSpan transcurrido) =>
		transcurrido >= Para(intentos);
}
