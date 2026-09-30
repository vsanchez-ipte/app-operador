namespace AppOperador.Aplicacion.Modelos;

// Casi todo anulable: un borrador admite datos incompletos, y el kilómetro va como texto crudo.
public sealed record BorradorIncidencia(
	string Uuid,
	string ClaveLocal,
	int? TipoId,
	string? Kilometro,
	Guid? SeveridadId,
	string Nota);
