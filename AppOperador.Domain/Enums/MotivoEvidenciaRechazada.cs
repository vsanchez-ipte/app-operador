namespace AppOperador.Domain.Enums;

public enum MotivoEvidenciaRechazada
{
	Ninguno = 0,

	// Aún no se descarga el catálogo; se resuelve solo al conectarse.
	LimitesDesconocidos = 1,

	CupoLleno = 2,
	FormatoNoAdmitido = 3,
	DemasiadoGrande = 4,
	ArchivoVacio = 5,
	SinEspacio = 6,
}
