namespace AppOperador.Domain.Enums;

// Cada motivo lleva a un aviso distinto, porque cada uno se resuelve de otra forma.
public enum MotivoSinKilometro
{
	ServicioNoDisponible = 1,
	PermisoDenegado = 2,
	PrecisionInsuficiente = 3,
	FueraDelCorredor = 4,

	// Distinto de FueraDelCorredor: el operador puede estar en el corredor, pero falta la geometría de ese tramo.
	TramoSinGeometria = 5,

	ErrorAlObtener = 6,
}
