namespace AppOperador.Aplicacion.Modelos;

/// <summary>Espacio del almacenamiento del dispositivo, medido en el momento.</summary>
/// <param name="BytesLibres">Libres, o <see langword="null"/> si no se pudo medir.</param>
/// <param name="BytesTotales">Capacidad total, o <see langword="null"/> si no se pudo medir.</param>
public sealed record EspacioDispositivo(long? BytesLibres, long? BytesTotales)
{
	public static readonly EspacioDispositivo Desconocido = new(null, null);

	public bool EsConocido => BytesLibres is not null && BytesTotales is > 0;

	/// <summary>Porcentaje libre, redondeado, o <see langword="null"/> si no se conoce.</summary>
	public int? PorcentajeLibre => EsConocido
		? (int)Math.Round(100.0 * BytesLibres!.Value / BytesTotales!.Value)
		: null;
}
