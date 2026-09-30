namespace AppOperador.Aplicacion.Modelos;

public sealed record IncidenciaRechazada(
	string Uuid,
	string ClaveLocal,
	int? TipoId,
	string? Kilometro,
	Guid? SeveridadId,
	string Nota,
	string? UltimoErrorCodigo,
	string? UltimoErrorMensaje);
