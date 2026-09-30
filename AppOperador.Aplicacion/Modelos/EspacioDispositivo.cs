namespace AppOperador.Aplicacion.Modelos;

public sealed record EspacioDispositivo(long? BytesLibres, long? BytesTotales)
{
	public static readonly EspacioDispositivo Desconocido = new(null, null);

	public bool EsConocido => BytesLibres is not null && BytesTotales is > 0;

	public int? PorcentajeLibre => EsConocido
		? (int)Math.Round(100.0 * BytesLibres!.Value / BytesTotales!.Value)
		: null;
}
