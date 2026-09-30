namespace AppOperador.Domain.Reglas;

public static class ReglaEsperaReintento
{
	public static readonly TimeSpan EsperaInicial = TimeSpan.FromMinutes(1);

	// Sin tope, un turno sin cobertura dejaría la siguiente espera en más de un día.
	public static readonly TimeSpan EsperaMaxima = TimeSpan.FromMinutes(30);

	public static TimeSpan Para(int intentos)
	{
		if (intentos <= 1)
		{
			return EsperaInicial;
		}

		// Desde aquí la duplicación ya supera el tope, y cortar evita desbordar el desplazamiento.
		const int IntentosHastaElTope = 6;
		if (intentos >= IntentosHastaElTope)
		{
			return EsperaMaxima;
		}

		var minutos = EsperaInicial.TotalMinutes * (1 << (intentos - 1));
		var espera = TimeSpan.FromMinutes(minutos);

		return espera > EsperaMaxima ? EsperaMaxima : espera;
	}

	public static bool YaPuedeReintentarse(int intentos, TimeSpan transcurrido) =>
		transcurrido >= Para(intentos);
}
