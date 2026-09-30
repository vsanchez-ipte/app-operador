namespace AppOperador.Aplicacion.Modelos;

public static class ReglaEstadoComunicacion
{
	// Comprobar gana a todo; sin enlace, «error de servicio» solo si el servidor contestó.
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
