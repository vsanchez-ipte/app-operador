namespace AppOperador.Aplicacion.Modelos;

/// <summary>
/// Decide qué estado de comunicación se le muestra al operador (JTT-1386 CA 2).
/// </summary>
/// <remarks>
/// Vive en la capa de aplicación y no en el ViewModel para que se pueda probar: la capa de
/// presentación no tiene pruebas automatizadas en este proyecto.
/// </remarks>
public static class ReglaEstadoComunicacion
{
	/// <summary>
	/// Determina el estado a partir de lo que se sabe del enlace.
	/// </summary>
	/// <param name="hayEnlace">Si el último sondeo alcanzó a Jacob CCO.</param>
	/// <param name="comprobando">Si hay una comprobación o revalidación en curso.</param>
	/// <param name="ultimaCausa">Por qué falló el último sondeo, si falló.</param>
	/// <remarks>
	/// <para>
	/// El orden importa. <b>Comprobar gana a todo</b>: mientras la petición está en vuelo no se
	/// sabe el desenlace, y seguir mostrando el estado anterior haría parpadear la insignia entre
	/// dos valores que ya no son ciertos.
	/// </para>
	/// <para>
	/// Después manda el enlace: si se alcanzó a Jacob, está en línea aunque el sondeo anterior
	/// hubiera fallado. La causa vieja no sobrevive a un sondeo bueno.
	/// </para>
	/// <para>
	/// <b>Sin enlace, «error de servicio» solo si el servidor contestó.</b> Es la distinción que
	/// costó una sesión de diagnóstico: un <c>404</c> presentado como falta de conexión manda a
	/// revisar la antena cuando el problema está en el despliegue.
	/// </para>
	/// </remarks>
	public static EstadoComunicacion Determinar(
		bool hayEnlace,
		bool comprobando,
		CausaSinEnlace ultimaCausa)
	{
		if (comprobando)
		{
			return EstadoComunicacion.Revalidando;
		}

		if (hayEnlace)
		{
			return EstadoComunicacion.EnLinea;
		}

		return ultimaCausa == CausaSinEnlace.RespuestaDeError
			? EstadoComunicacion.ErrorDeServicio
			: EstadoComunicacion.SinConexion;
	}
}
