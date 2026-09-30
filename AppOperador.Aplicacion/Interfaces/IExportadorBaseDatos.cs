using AppOperador.Aplicacion.Modelos;

namespace AppOperador.Aplicacion.Interfaces;

// La copia queda sin cifrar. Solo existe en paquetes compilados con la exportación.
public interface IExportadorBaseDatos
{
	// El resultado se libera siempre: es el único momento en que se borra el temporal.
	Task<ExportacionBaseDatos> ExportarAsync(
		string etiquetaAmbiente,
		CancellationToken cancelacion = default);
}
